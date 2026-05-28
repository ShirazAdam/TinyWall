using System;
using System.Runtime.InteropServices;

namespace ModernTinyWall.Windows.Services
{
    internal static partial class NativeMethods
    {
        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr RegisterServiceCtrlHandlerEx(string serviceName, ServiceCtrlHandlerExDelegate callback, IntPtr userData);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetServiceStatus(IntPtr hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool StartServiceCtrlDispatcher(IntPtr entry);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial SafeServiceHandle OpenSCManager(
            string? machineName,
            string? databaseName,
            ServiceControlAccessRights desiredAccess);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial SafeServiceHandle OpenService(
            SafeServiceHandle hSCManager,
            string serviceName,
            ServiceAccessRights desiredAccess);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryServiceConfig(
            SafeServiceHandle hService,
            IntPtr intPtrQueryConfig,
            uint cbBufSize,
            out uint pcbBytesNeeded);

        [LibraryImport("advapi32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ChangeServiceConfig(
            SafeServiceHandle hService,
            uint nServiceType,
            uint nStartType,
            uint nErrorControl,
            string? lpBinaryPathName,
            string? lpLoadOrderGroup,
            IntPtr lpdwTagId,
            IntPtr lpDependencies,
            string? lpServiceStartName,
            string? lpPassword,
            string? lpDisplayName);

        [LibraryImport("advapi32", SetLastError = true)]
        public static partial int ChangeServiceConfig2(
            SafeServiceHandle hService,
            ServiceConfig2InfoLevel dwInfoLevel,
            IntPtr lpInfo);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryServiceStatus(SafeServiceHandle hServiceStatus, ref SERVICE_STATUS lpServiceStatus);

        [LibraryImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryServiceStatusEx(
            SafeServiceHandle hService,
            ServiceInfoLevel InfoLevel,
            IntPtr lpBuffer,
            uint cbBufSize,
            out uint pcbBytesNeeded);
    }
}
