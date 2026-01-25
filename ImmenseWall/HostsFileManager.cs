using pylorak.Utilities;
using System;
using System.IO;

namespace pylorak.TinyWall
{
    internal class HostsFileManager : Disposable
    {
        // Active system hosts file
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        // Local copy of active hosts file
        private static readonly string HostsBackup = Path.Combine(Utils.AppDataPath, "hosts.bck");
        // User's original hosts file
        private static readonly string HostsOriginal = Path.Combine(Utils.AppDataPath, "hosts.orig");

        public readonly FileLocker FileLocker = new();

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                FileLocker.Dispose();
            }

            base.Dispose(disposing);
        }


        private bool _enableProtection;
        public bool EnableProtection
        {
            get => _enableProtection;
            set
            {
                _enableProtection = value;
                if (File.Exists(HostsPath))
                {
                    if (_enableProtection)
                        FileLocker.Lock(HostsPath, FileAccess.Read, FileShare.Read);
                    else
                        FileLocker.Unlock(HostsPath);
                }

                if (File.Exists(HostsBackup))
                    FileLocker.Lock(HostsBackup, FileAccess.Read, FileShare.Read);

                if (File.Exists(HostsOriginal))
                    FileLocker.Lock(HostsOriginal, FileAccess.Read, FileShare.Read);
            }
        }

        private void CreateOriginalBackup()
        {
            FileLocker.Unlock(HostsOriginal);
            File.Copy(HostsPath, HostsOriginal, true);
            FileLocker.Lock(HostsOriginal, FileAccess.Read, FileShare.Read);
        }

        public void UpdateHostsFile(string path)
        {
            // We keep a copy of the hosts file for ourself, so that
            // we can re-install it any time without a net connection.
            FileLocker.Unlock(HostsBackup);
            using (var afu = new AtomicFileUpdater(HostsBackup))
            {
                File.Copy(path, afu.TemporaryFilePath, true);
                afu.Commit();
            }
            FileLocker.Lock(HostsBackup, FileAccess.Read, FileShare.Read);
        }

        public static string GetHostsHash()
        {
            return File.Exists(HostsBackup) ? Hasher.HashFile(HostsBackup) : string.Empty;
        }

        public bool EnableHostsFile()
        {
            // If we have no backup of the user's original hosts file,
            // we make a copy of it.
            if (!File.Exists(HostsOriginal))
                CreateOriginalBackup();

            try
            {
                InstallHostsFile(HostsBackup);
                FlushDnsCache();
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool DisableHostsFile()
        {
            try
            {
                InstallHostsFile(HostsOriginal);

                // Delete backup of original so that it can be
                // recreated next time we install a custom hosts.
                if (File.Exists(HostsOriginal))
                {
                    FileLocker.Unlock(HostsOriginal);
                    File.Delete(HostsOriginal);
                }

                FlushDnsCache();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void FlushDnsCache()
        {
            try
            {
                // Flush DNS cache
                Utils.FlushDnsCache();
            }
            catch
            {
                // We just want to block exceptions.
            }
        }

        private void InstallHostsFile(string sourcePath)
        {
            try
            {
                if (!File.Exists(sourcePath)) return;
                FileLocker.Unlock(HostsPath);
                File.Copy(sourcePath, HostsPath, true);
            }
            finally
            {
                if (_enableProtection)
                    FileLocker.Lock(HostsPath, FileAccess.Read, FileShare.Read);
                else
                    FileLocker.Unlock(HostsPath);
            }
        }

    }
}
