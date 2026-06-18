using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace ModernTinyWall.Windows.Services
{
    public class ServiceControlManager : IDisposable
    {
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

        private bool _disposed;
        private readonly SafeServiceHandle _scManager;

        private SafeServiceHandle OpenService(string serviceName, ServiceAccessRights desiredAccess)
        {
            // Open the service
            var service = NativeMethods.OpenService(
                _scManager,
                serviceName,
                desiredAccess);

            // Verify if the service is opened
            if (service.IsInvalid)
                throw new Win32Exception();

            return service;
        }

        public ServiceControlManager()
        {
            // Open the service control manager
            _scManager = NativeMethods.OpenSCManager(
                null,
                null,
                ServiceControlAccessRights.SC_MANAGER_CONNECT);

            // Verify if the SC is opened
            if (_scManager.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Sets the nominated service to restart on failure.
        /// </summary>
        public void SetRestartOnFailure(string serviceName, bool restartOnFailure)
        {
            const uint delay = 1000;
            const int MAX_ACTIONS = 2;
            int SC_ACTION_SIZE = Marshal.SizeOf(typeof(SC_ACTION));

            // Open the service
            using var service = OpenService(
                serviceName,
                ServiceAccessRights.SERVICE_CHANGE_CONFIG |
                ServiceAccessRights.SERVICE_START);

            using var actionPtr = SafeHGlobalHandle.Alloc(SC_ACTION_SIZE * MAX_ACTIONS);
            int actionCount;
            if (restartOnFailure)
            {
                actionCount = 2;

                // Set up the restart action
                var action1 = new SC_ACTION
                {
                    Type = SC_ACTION_TYPE.SC_ACTION_RESTART,
                    Delay = delay
                };
                actionPtr.MarshalFromStruct(action1, 0);

                // Set up the "do nothing" action
                var action2 = new SC_ACTION
                {
                    Type = SC_ACTION_TYPE.SC_ACTION_NONE,
                    Delay = delay
                };
                actionPtr.MarshalFromStruct(action2, SC_ACTION_SIZE);
            }
            else
            {
                actionCount = 1;

                // Set up the "do nothing" action
                var action1 = new SC_ACTION
                {
                    Type = SC_ACTION_TYPE.SC_ACTION_NONE,
                    Delay = delay
                };
                actionPtr.MarshalFromStruct(action1);
            }

            // Set up the failure actions
            var failureActions = new SERVICE_FAILURE_ACTIONS();
            failureActions.dwResetPeriod = 0;
            failureActions.cActions = (uint)actionCount;
            failureActions.lpsaActions = actionPtr.DangerousGetHandle();
            failureActions.lpRebootMsg = null;
            failureActions.lpCommand = null;
            using var failureActionsPtr = SafeHGlobalHandle.FromManagedStruct(failureActions);

            // Make the change
            int changeResult = NativeMethods.ChangeServiceConfig2(
                service,
                ServiceConfig2InfoLevel.SERVICE_CONFIG_FAILURE_ACTIONS,
                failureActionsPtr.DangerousGetHandle());
            if (changeResult == 0)
                throw new Win32Exception();
        }

        public void SetStartupMode(string serviceName, ServiceStartMode mode)
        {
            using var service = OpenService(
                serviceName,
                ServiceAccessRights.SERVICE_CHANGE_CONFIG |
                ServiceAccessRights.SERVICE_QUERY_CONFIG
            );
            var result = NativeMethods.ChangeServiceConfig(
                service,
                SERVICE_NO_CHANGE,
                (uint)mode,
                SERVICE_NO_CHANGE,
                null,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                null,
                null,
                null);

            if (result == false)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void SetLoadOrderGroup(string serviceName, string group)
        {
            using var service = OpenService(
                serviceName,
                ServiceAccessRights.SERVICE_CHANGE_CONFIG |
                ServiceAccessRights.SERVICE_QUERY_CONFIG
            );
            var result = NativeMethods.ChangeServiceConfig(
                service,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                null,
                group,
                IntPtr.Zero,
                IntPtr.Zero,
                null,
                null,
                null);

            if (result == false)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public uint GetStartupMode(string serviceName)
        {
            using var service = OpenService(serviceName, ServiceAccessRights.SERVICE_QUERY_CONFIG);

            var result = NativeMethods.QueryServiceConfig(service, IntPtr.Zero, 0, out uint structSize);
            using var buff = SafeHGlobalHandle.Alloc(structSize);

            result = NativeMethods.QueryServiceConfig(service, buff.DangerousGetHandle(), structSize, out structSize);
            if (result == false)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            QUERY_SERVICE_CONFIG query_srv_config = Marshal.PtrToStructure<QUERY_SERVICE_CONFIG>(buff.DangerousGetHandle());
            return query_srv_config.dwStartType;
        }

        public uint? GetServicePid(string serviceName)
        {
            using var service = OpenService(serviceName, ServiceAccessRights.SERVICE_QUERY_STATUS);

            var result = NativeMethods.QueryServiceStatusEx(service, ServiceInfoLevel.SC_STATUS_PROCESS_INFO, IntPtr.Zero, 0, out uint structSize);
            using var buff = SafeHGlobalHandle.Alloc(structSize);

            result = NativeMethods.QueryServiceStatusEx(service, ServiceInfoLevel.SC_STATUS_PROCESS_INFO, buff.DangerousGetHandle(), structSize, out structSize);
            if (result == false)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            SERVICE_STATUS_PROCESS? query_srv_status = Marshal.PtrToStructure<SERVICE_STATUS_PROCESS>(buff.DangerousGetHandle());

            return query_srv_status?.dwCurrentState switch
            {
                ServiceState.Running or
                ServiceState.PausePending or
                ServiceState.Paused or
                ServiceState.ContinuePending => query_srv_status.dwProcessId,
                _ => null,
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Release managed resources

                _scManager.Dispose();
            }

            // Release unmanaged resources.
            // Set large fields to null.
            // Call Dispose on your base class.

            _disposed = true;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
