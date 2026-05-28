using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace ModernTinyWall.Windows
{
    public sealed partial class SafeHGlobalHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("kernel32")]
            public static partial IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

            [LibraryImport("kernel32")]
            public static partial IntPtr GlobalFree(IntPtr hMem);
        }

        private Type? MarshalDestroyType;

        private bool NeedsMarshalDestroy => (MarshalDestroyType != null);

        private static IntPtr AllocNativeMem(uint nBytes, bool zeroInit = false)
        {
            const uint GMEM_ZEROINIT = 0x0040;
            return NativeMethods.GlobalAlloc(zeroInit ? GMEM_ZEROINIT : 0, new UIntPtr(nBytes));
        }

        public static SafeHGlobalHandle Alloc(uint nBytes, bool zeroInit = false)
        {
            return new SafeHGlobalHandle(AllocNativeMem(nBytes, zeroInit));
        }

        public static SafeHGlobalHandle Alloc(int nBytes, bool zeroInit = false)
        {
            return Alloc((uint)nBytes, zeroInit);
        }

        public static SafeHGlobalHandle FromString(string? str)
        {
            return (str == null)
                ? new SafeHGlobalHandle()
                : new SafeHGlobalHandle(Marshal.StringToHGlobalUni(str));
        }

        public static SafeHGlobalHandle FromStruct<T>(T obj) where T : unmanaged
        {
            var size = Marshal.SizeOf(typeof(T));
            var ret = Alloc(size, true);
            ret.MarshalFromStruct(obj);
            return ret;
        }

        public static SafeHGlobalHandle FromManagedStruct<T>(T obj) where T : notnull
        {
            var size = Marshal.SizeOf(typeof(T));
            var ret = Alloc(size, true);
            ret.MarshalFromManagedStruct(obj, typeof(T));
            return ret;
        }

        public void MarshalFromStruct<T>(T obj, int offset = 0) where T : unmanaged
        {
            if (MarshalDestroyType is not null)
                Marshal.DestroyStructure(this.handle, MarshalDestroyType);

            int size = Marshal.SizeOf(typeof(T));
            unsafe
            {
                Buffer.MemoryCopy(&obj, (byte*)this.handle.ToPointer() + offset, size, size);
            }
            MarshalDestroyType = null;
        }

#nullable disable
        public void MarshalFromManagedStruct(object obj, Type structureType)
        {
            Marshal.StructureToPtr(obj, this.handle, MarshalDestroyType is not null);
            MarshalDestroyType = structureType;
        }
#nullable restore

        public T ToStruct<T>() where T : unmanaged
        {
            T ret = default;
            var size = Marshal.SizeOf(typeof(T));
            unsafe
            {
                Buffer.MemoryCopy(handle.ToPointer(), &ret, size, size);
            }
            return ret;
        }

        public void ForgetAndResize(uint newSize, bool zeroInit = false)
        {
            if (this.IsClosed)
                throw new InvalidOperationException("The SafeHandle is already closed.");

            var newHndl = AllocNativeMem(newSize, zeroInit);
            if (newHndl == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception();

            if (!this.IsInvalid)
            {
                if (MarshalDestroyType is not null)
                {
                    Marshal.DestroyStructure(this.handle, MarshalDestroyType);
                    MarshalDestroyType = null;
                }
                NativeMethods.GlobalFree(this.handle);
            }

            SetHandle(newHndl);
        }

        public SafeHGlobalHandle()
            : this(IntPtr.Zero)
        { }

        public SafeHGlobalHandle(IntPtr ptr)
            : base(true)
        {
            SetHandle(ptr);
        }

        protected override bool ReleaseHandle()
        {
            if (MarshalDestroyType is not null)
            {
                Marshal.DestroyStructure(this.handle, MarshalDestroyType);
                MarshalDestroyType = null;
            }
            bool ret = (IntPtr.Zero == NativeMethods.GlobalFree(handle));
            SetHandle(IntPtr.Zero);
            return ret;
        }
    }

    public sealed partial class SafeObjectHandle : SafeHandleZeroOrMinusOneIsInvalid   // OpenProcess returns 0 on failure
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool CloseHandle(IntPtr hHandle);
        }

        public SafeObjectHandle() : base(true) { }

        internal SafeObjectHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }


        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    public sealed partial class HeapSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("kernel32")]
            internal static partial IntPtr HeapAlloc(IntPtr heap, uint uFlags, UIntPtr dwBytes);

            [LibraryImport("kernel32", SetLastError = true)]
            internal static partial IntPtr GetProcessHeap();

            [LibraryImport("kernel32", SetLastError = true)]

            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool HeapFree(IntPtr heap, uint flags, IntPtr mem);
        }

        private static IntPtr ProcessHeap { get; } = NativeMethods.GetProcessHeap();

        public HeapSafeHandle(int nBytes, bool zeroBytes = false)
            : base(true)
        {
            uint flags = zeroBytes ? 0x00000008u : 0u;
            this.handle = NativeMethods.HeapAlloc(ProcessHeap, flags, (UIntPtr)(uint)nBytes);
        }

        public HeapSafeHandle(IntPtr ptr, bool ownsHandle)
            : base(ownsHandle)
        {
            this.handle = ptr;
        }

        public HeapSafeHandle()
            : base(true)
        {
            this.handle = IntPtr.Zero;
        }



        protected override bool ReleaseHandle()
        {
            return NativeMethods.HeapFree(ProcessHeap, 0, handle);
        }
    }

    public enum RegistryBaseKey
    {
        HKEY_CLASSES_ROOT = -2147483648,
        HKEY_CURRENT_USER = -2147483647,
        HKEY_LOCAL_MACHINE = -2147483646,
        HKEY_USERS = -2147483645,
        HKEY_PERFORMANCE_DATA = -2147483644,
        HKEY_CURRENT_CONFIG = -2147483643,
        HKEY_DYN_DATA = -2147483642
    }

    public sealed partial class SafeRegistryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [Flags]
        public enum RegistryRights : uint
        {
            KEY_READ = 0x20019u,
            KEY_WRITE = 0x20006u,
            KEY_ALL_ACCESS = 0xF003Fu,
            KEY_WOW64_32KEY = 0x0200u,
            KEY_WOW64_64KEY = 0x0100u,
        }

        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("advapi32")]
            public static partial int RegCloseKey(IntPtr hKey);

            [LibraryImport("advapi32", StringMarshalling = StringMarshalling.Utf16)]
            public static partial int RegOpenKeyEx(IntPtr hKey, string subKey, uint ulOptions, uint samDesired, out SafeRegistryHandle hkResult);
        }

        public SafeRegistryHandle() : base(true) { }

        internal SafeRegistryHandle(IntPtr hndl, bool ownsHandle) : base(ownsHandle)
        {
            SetHandle(hndl);
        }

        public static SafeRegistryHandle Open(RegistryBaseKey baseKey, string subKey, RegistryRights access)
        {
            int err = NativeMethods.RegOpenKeyEx(new IntPtr((int)baseKey), subKey, 0, (uint)access, out SafeRegistryHandle safeHandle);
            if (0 == err)
                return safeHandle;
            else
                throw new Win32Exception(err, "RegOpenKeyEx");
        }

        private static bool RemoveFromStart(ref string val, string prefix)
        {
            if (val.StartsWith(prefix))
            {
                val = val.Remove(0, prefix.Length);
                return true;
            }

            return false;
        }

        public static SafeRegistryHandle Open(string key, RegistryRights access)
        {
            key = key.Replace('/', '\\');
            if (RemoveFromStart(ref key, @"HKEY_CLASSES_ROOT\"))
                return Open(RegistryBaseKey.HKEY_CLASSES_ROOT, key, access);
            else if (RemoveFromStart(ref key, @"HKEY_CURRENT_USER\"))
                return Open(RegistryBaseKey.HKEY_CURRENT_USER, key, access);
            else if (RemoveFromStart(ref key, @"HKEY_LOCAL_MACHINE\"))
                return Open(RegistryBaseKey.HKEY_LOCAL_MACHINE, key, access);
            else if (RemoveFromStart(ref key, @"HKEY_USERS\"))
                return Open(RegistryBaseKey.HKEY_USERS, key, access);
            else if (RemoveFromStart(ref key, @"HKEY_PERFORMANCE_DATA\"))
                return Open(RegistryBaseKey.HKEY_PERFORMANCE_DATA, key, access);
            else if (RemoveFromStart(ref key, @"HKEY_CURRENT_CONFIG\"))
                return Open(RegistryBaseKey.HKEY_CURRENT_CONFIG, key, access);
            else if (RemoveFromStart(ref key, @"HKEY_DYN_DATA\"))
                return Open(RegistryBaseKey.HKEY_DYN_DATA, key, access);
            else
                throw new ArgumentException("Unrecognized registry base key.");
        }

        override protected bool ReleaseHandle()
        {
            return (0 == NativeMethods.RegCloseKey(handle));
        }
    }

    public sealed partial class AllocHLocalSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("kernel32.dll")]
            internal static partial IntPtr LocalAlloc(uint uFlags, UIntPtr dwBytes);

            [LibraryImport("kernel32.dll")]

            internal static partial IntPtr LocalFree(IntPtr hMem);
        }

        public AllocHLocalSafeHandle(int nBytes)
            : base(true)
        {
            this.handle = NativeMethods.LocalAlloc(0, new UIntPtr((uint)nBytes));
        }

        public AllocHLocalSafeHandle(IntPtr ptr, bool ownsHandle)
            : base(ownsHandle)
        {
            this.handle = ptr;
        }

        public AllocHLocalSafeHandle()
            : base(true)
        {
            this.handle = IntPtr.Zero;
        }



        protected override bool ReleaseHandle()
        {
            return (IntPtr.Zero == NativeMethods.LocalFree(handle));
        }
    }

    public sealed partial class FindVolumeSafeHandle : SafeHandleMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("kernel32", EntryPoint = "FindFirstVolumeW", SetLastError = true)]
            internal static unsafe partial IntPtr FindFirstVolume(char* lpszVolumeName, int cchBufferLength);

            [LibraryImport("kernel32", EntryPoint = "FindNextVolumeW", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static unsafe partial bool FindNextVolume(IntPtr hFindVolume, char* lpszVolumeName, int cchBufferLength);

            [LibraryImport("kernel32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static partial bool FindVolumeClose(IntPtr hFindVolume);
        }

        public FindVolumeSafeHandle()
            : base(true)
        {
            SetHandleAsInvalid();
        }

        private FindVolumeSafeHandle(IntPtr handle)
            : base(true)
        {
            this.handle = handle;
        }

        public static IEnumerable<string> EnumerateVolumes()
        {
            const int ERROR_NO_MORE_FILES = 18;
            const int VolumeNameBufferLength = 64;

            var volumes = new List<string>();
            unsafe
            {
                var buffer = stackalloc char[VolumeNameBufferLength];
                using var safeHandle = FindFirstVolume(buffer, VolumeNameBufferLength);
                if (safeHandle.IsInvalid)
                    throw new Win32Exception();

                volumes.Add(new string(buffer));

                while (safeHandle.FindNextVolume(buffer, VolumeNameBufferLength))
                {
                    volumes.Add(new string(buffer));
                }
            }

            foreach (var volume in volumes)
            {
                yield return volume;
            }

            int errno = Marshal.GetLastWin32Error();
            if (errno == ERROR_NO_MORE_FILES)
                yield break;
            else
                throw new Win32Exception(errno);
        }

        private static unsafe FindVolumeSafeHandle FindFirstVolume(char* dst, int length)
        {
            return new FindVolumeSafeHandle(NativeMethods.FindFirstVolume(dst, length));
        }

        private unsafe bool FindNextVolume(char* dst, int length)
        {
            return NativeMethods.FindNextVolume(handle, dst, length);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.FindVolumeClose(handle);
        }
    }

    public sealed partial class SafeSidHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("advapi32")]
            public static partial IntPtr FreeSid(IntPtr pSid);

            [LibraryImport("advapi32", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool ConvertSidToStringSid(IntPtr Sid, out AllocHLocalSafeHandle StringSid);
        }

        public SafeSidHandle(IntPtr ptr, bool ownsHandle)
            : base(ownsHandle)
        {
            this.handle = ptr;
        }

        public SafeSidHandle()
            : base(true)
        {
            this.handle = IntPtr.Zero;
        }



        protected override bool ReleaseHandle()
        {
            return (IntPtr.Zero == NativeMethods.FreeSid(handle));
        }

        public static string? ToStringSid(IntPtr pSid)
        {
            AllocHLocalSafeHandle? ptrStrSid = null;
            try
            {
                if (!NativeMethods.ConvertSidToStringSid(pSid, out ptrStrSid))
                    return null;

                return Marshal.PtrToStringUni(ptrStrSid.DangerousGetHandle());
            }
            finally
            {
                ptrStrSid?.Dispose();
            }
        }

        public string? GetStringSid()
        {
            return ToStringSid(handle);
        }
    }

}
