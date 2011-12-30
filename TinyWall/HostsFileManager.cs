﻿using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PKSoft
{
    internal static class HostsFileManager
    {
        // Active system hosts file
        private static string HOSTS_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        // Local copy of active hosts file
        private static string HOSTS_BACKUP = Path.Combine(SettingsManager.AppDataPath, "hosts.bck");
        // User's original hosts file
        private static string HOSTS_ORIGINAL = Path.Combine(SettingsManager.AppDataPath, "hosts.orig");
        // Should we lock the system's hosts file
        private static bool _ProtectHostsFile = false;

        internal static void EnableProtection(bool protectHosts)
        {
            _ProtectHostsFile = protectHosts;
            if (File.Exists(HOSTS_PATH))
            {
                if (_ProtectHostsFile)
                    FileLocker.LockFile(HOSTS_PATH, FileAccess.Read, FileShare.Read);
                else
                    FileLocker.UnlockFile(HOSTS_PATH);
            }

            if (File.Exists(HOSTS_BACKUP))
                FileLocker.LockFile(HOSTS_BACKUP, FileAccess.Read, FileShare.Read);

            if (File.Exists(HOSTS_ORIGINAL))
                FileLocker.LockFile(HOSTS_ORIGINAL, FileAccess.Read, FileShare.Read);
        }

        internal static void CreateOriginalBackup()
        {
            FileLocker.UnlockFile(HOSTS_ORIGINAL);
            File.Copy(HOSTS_PATH, HOSTS_ORIGINAL);
            FileLocker.LockFile(HOSTS_ORIGINAL, FileAccess.Read, FileShare.Read);
        }

        internal static void UpdateHostsFile(string path, bool enable)
        {
            // If we have no backup of the user's original hosts file,
            // we make a copy of it.
            if (!File.Exists(HOSTS_ORIGINAL))
                CreateOriginalBackup();

            // We keep a copy of the hosts file for ourself, so that
            // we can re-install it any time without a net connection.
            FileLocker.UnlockFile(HOSTS_BACKUP);
            File.Copy(path, HOSTS_BACKUP);
            FileLocker.LockFile(HOSTS_BACKUP, FileAccess.Read, FileShare.Read);

            if (enable)
                EnableHostsFile();
        }

        internal static string HostsMD5()
        {
            byte[] hash;

            using (FileStream fs = new FileStream(HOSTS_PATH, FileMode.Open, FileAccess.Read))
            {
                MD5 md5 = new MD5CryptoServiceProvider();
                hash = md5.ComputeHash(fs);
                fs.Close();
            }

            StringBuilder sb = new StringBuilder();
            foreach (byte hex in hash)
                sb.Append(hex.ToString("x2"));

            return sb.ToString().ToUpperInvariant();
        }

        internal static void EnableHostsFile()
        {
            InstallHostsFile(HOSTS_BACKUP);
        }

        internal static void DisableHostsFile()
        {
            InstallHostsFile(HOSTS_ORIGINAL);
        }

        private static void InstallHostsFile(string sourcePath)
        {
            try
            {
                if (File.Exists(sourcePath))
                {
                    FileLocker.UnlockFile(HOSTS_PATH);
                    File.Copy(sourcePath, HOSTS_PATH);
                }
            }
            finally
            {
                if (_ProtectHostsFile)
                    FileLocker.LockFile(HOSTS_PATH, FileAccess.Read, FileShare.Read);
                else
                    FileLocker.UnlockFile(HOSTS_PATH);
            }
        }

    }
}
