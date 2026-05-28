using Microsoft.Win32;
using ModernTinyWall.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace ModernTinyWall.Windows
{
    public enum PathFormat
    {
        NativeNt,
        Volume,
        Win32
    }

    public sealed partial class PathMapper : Disposable
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("kernel32", EntryPoint = "QueryDosDeviceW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
            public static partial int QueryDosDevice(string lpDeviceName, char[] lpTargetPath, int ucchMax);

            [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool GetVolumePathNamesForVolumeName(string lpszVolumeName, [Out] char[] lpszVolumePathNames, int cchBufferLength, out int lpcchReturnLength);


            [LibraryImport("kernel32", EntryPoint = "GetVolumePathNameW", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static unsafe partial bool GetVolumePathName(char* lpszFileName, char* lpszVolumePathName, int ccBufferLength);

            [LibraryImport("kernel32", EntryPoint = "GetVolumeNameForVolumeMountPointW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static unsafe partial bool GetVolumeNameForVolumeMountPoint(string lpszVolumeMountPoint, char* lpszVolumeName, int cchBufferLength);
        }

        public class DriveCache(string kernelDevice, List<string> volumes, List<string> drives)
        {
            public string Device = kernelDevice;
            public List<string> Volumes = volumes;
            public List<string> Drives = drives;
        }

        private readonly ManualResetEvent _cacheReadyEvent = new(false);
        private readonly string _systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        private readonly Lock _locker = new();

        private bool _cacheRebuilding;
        private DateTime _lastUpdateTime = DateTime.MinValue;

        private static volatile PathMapper? _instance;
        private static readonly Lock SingletonLock = new();
        public static PathMapper Instance
        {
            get
            {
                if (_instance != null) return _instance;

                lock (SingletonLock)
                {
                    _instance ??= new PathMapper();
                }

                return _instance;
            }
        }

        private PathMapper()
        {
            RebuildCache();
        }

        public bool AutoUpdate { get; set; } = true;

        private DriveCache[]? _cache;

        [AllowNull]
        public DriveCache[] Cache
        {
            get
            {
                if (AutoUpdate || (_cache is null))
                {
                    if ((DateTime.Now - _lastUpdateTime).TotalSeconds > 5)
                        RebuildCache();
                }

                _cacheReadyEvent.WaitOne();
                lock (_locker)
                {
                    return _cache ?? throw new InvalidOperationException("There was an error building the drive map cache.");
                }
            }

            private set
            {
                lock (_locker)
                {
                    _cache = value;
                    _lastUpdateTime = DateTime.Now;
                    _cacheReadyEvent.Set();
                    _cacheRebuilding = false;
                }
            }
        }

        private static List<DriveCache> RebuildCacheImpl_1()
        {
            const int MAX_PATH = 260;

            var sb = new StringBuilder(MAX_PATH);
            var buf = new char[MAX_PATH];
            var newCache = new List<DriveCache>();

            foreach (var vol in FindVolumeSafeHandle.EnumerateVolumes())
            {
                if ((vol[0] != '\\')
                    || (vol[1] != '\\')
                    || (vol[2] != '?')
                    || (vol[3] != '\\')
                    || (vol[^1] != '\\'))
                {
                    continue;
                }

                var cacheEntry = new DriveCache(string.Empty, [vol], []);

                string qddInput = vol.Substring(4, vol.Length - 5); // Also remove trailing backslash
                Array.Clear(buf);
                int charCount = NativeMethods.QueryDosDevice(qddInput, buf, buf.Length);
                if (charCount > 0)
                {
                    sb.Clear();
                    var targetLength = Array.IndexOf(buf, '\0');
                    if (targetLength < 0)
                        targetLength = Math.Min(charCount, buf.Length);
                    sb.Append(buf, 0, targetLength);
                    sb.Append('\\');
                    cacheEntry.Device = sb.ToString();
                }

                if (NativeMethods.GetVolumePathNamesForVolumeName(vol, buf, buf.Length, out int expectedChars))
                {
                    int startIdx = 0;
                    int numChars = 0;
                    for (int i = 0; i < expectedChars; ++i)
                    {
                        if ((buf[i] == '\0') && (numChars > 0))
                        {
                            cacheEntry.Drives.Add(new string(buf, startIdx, numChars));
                            startIdx = i + 1;
                            numChars = 0;
                        }
                        else
                            ++numChars;
                    }
                }

                newCache.Add(cacheEntry);
            }

            return newCache;
        }

        private static List<DriveCache> RebuildCacheImpl_2(List<DriveCache> newCache)
        {
            const string SYMBOLIC_LINK_TYPE = "SymbolicLink";

            using var dir = ObjectManager.OpenDirectoryObjectForRead(@"\GLOBAL??");
            using var volumeNamePrefixHandle = new SafeUnicodeStringHandle("Volume{");
            var volumeNamePrefix = volumeNamePrefixHandle.ToStruct();
            var linkTargetBuff = new SafeUnicodeStringHandle(512);
            try
            {
                foreach (var name in ObjectManager.QueryDirectoryForType(dir, SYMBOLIC_LINK_TYPE))
                {
                    // Found a volume GUID?
                    if (name.StartsWith(volumeNamePrefix, true))
                    {
                        var target = ObjectManager.QueryLinkTarget(ref linkTargetBuff, name, dir) + @"\";
                        var volumePath = @"\\?\" + name + @"\";
                        var existingEntryFound = false;
                        foreach (var cacheEntry in newCache)
                        {
                            if (string.Equals(cacheEntry.Device, target, StringComparison.Ordinal))
                            {
                                existingEntryFound = true;
                                if (!cacheEntry.Volumes.Contains(volumePath))
                                    cacheEntry.Volumes.Add(volumePath);
                            }
                        }
                        if (!existingEntryFound)
                            newCache.Add(new DriveCache(target, [volumePath], []));
                    }

                    // Found a drive letter?
                    bool isDriveLetter;
                    unsafe
                    {
                        char* namePtr = (char*)name.buffer.ToPointer();
                        isDriveLetter = (name.length == 4) && char.IsLetter(namePtr[0]) && (namePtr[1] == ':');
                    }

                    if (isDriveLetter)
                    {
                        var target = ObjectManager.QueryLinkTarget(ref linkTargetBuff, name, dir) + @"\";
                        var drivePath = name + @"\";
                        var existingEntryFound = false;
                        foreach (var cacheEntry in newCache)
                        {
                            if (string.Equals(cacheEntry.Device, target, StringComparison.Ordinal))
                            {
                                existingEntryFound = true;
                                if (!cacheEntry.Drives.Contains(drivePath))
                                    cacheEntry.Drives.Add(drivePath);
                            }
                        }
                        if (!existingEntryFound)
                            newCache.Add(new DriveCache(target, new List<string>(), new List<string>() { drivePath }));
                    }
                }

                return newCache;
            }
            finally
            {
                linkTargetBuff.Dispose();
            }
        }

        public void RebuildCache(bool blocking = false)
        {
            bool queueWork = false;
            lock (_locker)
            {
                if (!_cacheRebuilding)
                {
                    _cacheRebuilding = true;
                    _cacheReadyEvent.Reset();
                    queueWork = true;
                }
            }

            if (queueWork)
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        // We have two different methods to discover drives and volumes.
                        // We chain them and execute both because each one has limitations:
                        // RebuildCacheImpl_1 - Cannot discover some types of drives, such as those created by ImDisk
                        // RebuildCacheImpl_2 - Cannot discover devices mounted to mount points
                        var tmpCache = RebuildCacheImpl_1();
                        try { tmpCache = RebuildCacheImpl_2(tmpCache); }
                        catch
                        {
                            // ignored
                        }

                        Cache = tmpCache.ToArray();
                    }
                    catch
                    {
                        Cache = null;
                    }
                }, null);
            }

            if (blocking)
                _cacheReadyEvent.WaitOne();
        }

        private static string GetMountPoint(ReadOnlySpan<char> path)
        {
            // NOTE: This method only works for drive paths, not for UNC paths.

            if (!((path.Length >= 3) && char.IsLetter(path[0]) && (path[1] == ':') && (path[2] == '\\')))
                throw new ArgumentException("Input path must be an absolute path.");

            int requiredBufferSize = path.Length + 1;
            if (requiredBufferSize <= 512)
            {
                unsafe
                {
                    fixed (char* pathPtr = path)
                    {
                        var buffer = stackalloc char[requiredBufferSize];
                        bool success = NativeMethods.GetVolumePathName(pathPtr, buffer, requiredBufferSize);
                        if (success)
                            return new string(buffer);
                    }
                }
            }
            else
            {
                // TODO: Warn or log somehow.
            }

            // Fallback heuristic
            return path.Slice(3).ToString();
        }

        public string ConvertPathIgnoreErrors(string path, PathFormat target)
        {
            if (path is null)
                return string.Empty;

            return ConvertPathIgnoreErrors(path.AsSpan(), target);
        }

        private static readonly string REGISTRY_CONST = "Registry";
        private static readonly string SYSTEM_CONST = "System";

        public string ConvertPathIgnoreErrors(ReadOnlySpan<char> path, PathFormat target)
        {
            if (0 == path.Length)
                return string.Empty;
            if (path.Equals(REGISTRY_CONST, StringComparison.OrdinalIgnoreCase))
                return REGISTRY_CONST;
            if (path.Equals(SYSTEM_CONST, StringComparison.OrdinalIgnoreCase))
                return SYSTEM_CONST;

            try
            {
                return ConvertPath(path, target);
            }
            catch
            {
                return path.ToString();
            }
        }

        public string ConvertPath(ReadOnlySpan<char> path, PathFormat target)
        {
            var ret = path;
            ret = ReplaceLeading(ret, @"\SystemRoot", _systemRoot);
            ret = ReplaceLeading(ret, @"\\?\", string.Empty);
            ret = ReplaceLeading(ret, @"\\.\", string.Empty);
            ret = ReplaceLeading(ret, @"\??\", string.Empty);
            ret = ReplaceLeading(ret, @"UNC\", @"\\");
            ret = ReplaceLeading(ret, @"GLOBALROOT\", string.Empty);
            ret = ReplaceLeading(ret, @"\Device\Mup\", @"\\");

            if (NetworkPath.IsNetworkPath(ret.ToString()))
            {   // UNC path (like \\server\share\directory\file), or mounted network drive

                if (!NetworkPath.IsUncPath(ret))
                {
                    // Convert a mapped drive to a UNC path
                    char driveLetter = char.ToUpperInvariant(ret[0]);
                    using (var networkKey = Registry.CurrentUser.OpenSubKey("Network", false))
                    {
                        var subkeys = networkKey?.GetSubKeyNames() ?? [];
                        foreach (var sk in subkeys)
                        {
                            if ((sk.Length == 1) && (char.ToUpperInvariant(sk[0]) == driveLetter))
                            {
                                using var driveKey = networkKey?.OpenSubKey(sk, false);
                                if (driveKey?.GetValue("RemotePath") is string remotePath)
                                {
                                    ret = SpanUtils.CombinePath(remotePath.AsSpan(), ret.Slice(3)).AsSpan();
                                    break;
                                }
                            }
                        }
                    }

                    // If conversion failed
                    if (!NetworkPath.IsUncPath(ret))
                        throw new DriveNotFoundException();
                }

                return target switch
                {
                    PathFormat.Win32 => ret.ToString(),
                    PathFormat.NativeNt => SpanUtils.Concat(@"\Device\Mup\".AsSpan(), ret.Slice(2)),
                    _ => throw new NotSupportedException(),
                };
            }
            else if ((ret.Length >= 3) && char.IsLetter(ret[0]) && (ret[1] == ':') && (ret[2] == '\\'))
            {   // Win32 drive letter format, like C:\Windows\explorer.exe

                if (target == PathFormat.Win32)
                    return ret.ToString();

                var dc = Cache;
                var mountPoint = GetMountPoint(ret);

                static (bool, int, int) searchCache(DriveCache[] dc, string mountPoint)
                {
                    for (int i = 0; i < dc.Length; ++i)
                    {
                        for (int j = 0; j < dc[i].Drives.Count; ++j)
                        {
                            if (mountPoint.Equals(dc[i].Drives[j], StringComparison.OrdinalIgnoreCase))
                                return (true, i, j);
                        }
                    }

                    return (false, 0, 0);
                }

                // GetMountPoint() might return a "mount point" that is not real or not
                // known to the system. This happens for example with directories on ImDisk
                // drives. In this case we wouldn't be able to map the path.
                // So we check if the returned mount point is in our list of all known mount
                // points, and if not, we only map the drive letter.
                (var mountPointFound, var cacheIdx, var driveIdx) = searchCache(dc, mountPoint);
                if (!mountPointFound)
                {
                    // Repeat search with only the drive letter
                    mountPoint = mountPoint.Substring(0, 3);
                    (mountPointFound, cacheIdx, driveIdx) = searchCache(dc, mountPoint);
                }

                if (!mountPointFound)
                    throw new DriveNotFoundException();

                // And here we do the mapping
                var trailing = ret.Slice(dc[cacheIdx].Drives[driveIdx].Length);
                switch (target)
                {
                    case PathFormat.NativeNt:
                        return SpanUtils.Concat(dc[cacheIdx].Device.AsSpan(), trailing);
                    case PathFormat.Volume:
                        if (dc[cacheIdx].Volumes.Count > 0)
                            return SpanUtils.Concat(dc[cacheIdx].Volumes[0].AsSpan(), trailing);
                        else
                            throw new NotSupportedException();
                    default:
                        throw new NotSupportedException();
                }
            }
            else if (ret.StartsWith("Volume{".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {   // Volume GUID path, like \\?\Volume{26a21bda-a627-11d7-9931-806e6f6e6963}\Windows\explorer.exe

                if (target == PathFormat.Volume)
                    return path.ToString();

                ret = SpanUtils.Concat(@"\\?\".AsSpan(), ret).AsSpan();
                foreach (var cacheEntry in Cache)
                {
                    for (int j = 0; j < cacheEntry.Volumes.Count; ++j)
                    {
                        if (ret.StartsWith(cacheEntry.Volumes[j].AsSpan(), StringComparison.OrdinalIgnoreCase))
                        {
                            var trailing = ret.Slice(cacheEntry.Volumes[j].Length);
                            switch (target)
                            {
                                case PathFormat.NativeNt:
                                    return SpanUtils.Concat(cacheEntry.Device.AsSpan(), trailing);
                                case PathFormat.Win32:
                                    if (cacheEntry.Drives.Count > 0)
                                        return SpanUtils.Concat(cacheEntry.Drives[0].AsSpan(), trailing);
                                    else
                                        throw new NotSupportedException();
                                default:
                                    throw new NotSupportedException();
                            }
                        }
                    }
                }

                throw new DriveNotFoundException();
            }
            else
            {   // Assume native NT device path, like \Device\HarddiskVolume1\Windows\explorer.exe
                if (target == PathFormat.NativeNt)
                    return path.ToString();

                foreach (var cacheEntry in Cache)
                {
                    if ((cacheEntry.Device is not null) && ret.StartsWith(cacheEntry.Device.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        var trailing = ret.Slice(cacheEntry.Device.Length);
                        switch (target)
                        {
                            case PathFormat.Volume:
                                if (cacheEntry.Volumes.Count > 0)
                                    return SpanUtils.Concat(cacheEntry.Volumes[0].AsSpan(), trailing);
                                else
                                    throw new NotSupportedException();
                            case PathFormat.Win32:
                                if (cacheEntry.Drives.Count > 0)
                                    return SpanUtils.Concat(cacheEntry.Drives[0].AsSpan(), trailing);
                                else
                                    throw new NotSupportedException();
                            default:
                                throw new NotSupportedException();
                        }
                    }
                }

                throw new DriveNotFoundException();
            }
        }

        private static unsafe ReadOnlySpan<char> ReplaceLeading(ReadOnlySpan<char> text, string needle, string replacement)
        {
            if (text.StartsWith(needle.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                text = text.Slice(needle.Length);
                if (!string.IsNullOrEmpty(replacement))
                {
                    text = SpanUtils.Concat(replacement.AsSpan(), text).AsSpan();
                }
            }

            return text;
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                _cacheReadyEvent.WaitOne();
                _cacheReadyEvent.Close();
            }

            _instance = null;
            base.Dispose(disposing);
        }

#if DEBUG
        private void TestConversion(string path)
        {
            var win32Result = ConvertPathIgnoreErrors(path, PathFormat.Win32);
            var ntResult = ConvertPathIgnoreErrors(path, PathFormat.NativeNt);
            var volumeResult = ConvertPathIgnoreErrors(path, PathFormat.Volume);

            string output = path + ":" + Environment.NewLine
                + "    Win32:  " + win32Result + Environment.NewLine
                + "    Nt:     " + ntResult + Environment.NewLine
                + "    Volume: " + volumeResult + Environment.NewLine;

            Debug.WriteLine(output);
        }

        // TODO: Automatically compare with expected outcomes
        public void RunTests()
        {
            string NETMOUNT_DRIVE = @"X:\";
            string NONEXISTENT_DRIVE = @"N:\";
            string VOLUME = @"\\?\Volume{56c747c3-83d9-11e4-91b2-806e6f6e6963}\";
            string DIR_MOUNTPOINT = @"d:\c_drive\";

            TestConversion(@"\\server\share\dir\file.txt");
            TestConversion(@"\\.\UNC\server\share\dir\file.txt");
            TestConversion(NETMOUNT_DRIVE + @"tmp");
            TestConversion(NONEXISTENT_DRIVE + @"tmp");

            TestConversion(@"c:\windows\explorer.exe");
            TestConversion(@"\\?\c:\windows\explorer.exe");
            TestConversion(DIR_MOUNTPOINT + @"windows\explorer.exe");
            TestConversion(@"\\?\UNC\c:\windows\explorer.exe");

            TestConversion(VOLUME + @"Windows\explorer.exe");
            TestConversion(@"\Device\HarddiskVolume1\Windows\explorer.exe");
            TestConversion(@"\SystemRoot\explorer.exe");
            TestConversion(@"\Device\Mup\server\share\dir\file.txt");
        }
#endif
    }
}