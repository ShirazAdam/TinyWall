using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace ModernTinyWall.Windows.Services
{
    public sealed partial class SafeServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool CloseServiceHandle(IntPtr hSCObject);
        }

        public SafeServiceHandle()
            : this(IntPtr.Zero)
        { }

        public SafeServiceHandle(IntPtr handle) : base(true)
        {
            SetHandle(handle);
        }

        override protected bool ReleaseHandle()
        {
            return NativeMethods.CloseServiceHandle(handle);
        }
    }

    public sealed partial class SafeHandlePowerSettingNotification : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("user32", SetLastError = true)]
            public static partial SafeHandlePowerSettingNotification RegisterPowerSettingNotification(
                IntPtr hRecipient,
                ref Guid PowerSettingGuid,
                DeviceNotifFlags Flags);

            [LibraryImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool UnregisterPowerSettingNotification(IntPtr hPowerNotif);
        }

        public static SafeHandlePowerSettingNotification Create(IntPtr service, Guid powerSetting, DeviceNotifFlags flags)
        {
            return NativeMethods.RegisterPowerSettingNotification(service, ref powerSetting, flags);
        }

        public SafeHandlePowerSettingNotification()
            : this(IntPtr.Zero)
        { }

        public SafeHandlePowerSettingNotification(IntPtr ptr)
            : base(true)
        {
            SetHandle(ptr);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.UnregisterPowerSettingNotification(handle);
        }
    }

    public sealed partial class SafeHandleDeviceNotification : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("user32", SetLastError = true)]
            public static partial SafeHandleDeviceNotification RegisterDeviceNotification(
                IntPtr hRecipient,
                IntPtr NotificationFilter,
                DeviceNotifFlags Flags);

            [LibraryImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static partial bool UnregisterDeviceNotification(IntPtr hDeviceNotif);
        }

        public static SafeHandleDeviceNotification Create(IntPtr recipient, Guid devIfaceClsGuid, DeviceNotifFlags flags)
        {
            var filter = new DEV_BROADCAST_DEVICEINTERFACE_Filter();
            filter.Size = Marshal.SizeOf(typeof(DEV_BROADCAST_DEVICEINTERFACE_Filter));
            filter.DeviceType = DeviceBroadcastHdrDevType.DBT_DEVTYP_DEVICEINTERFACE;
            filter.ClassGuid = devIfaceClsGuid;
            filter.Name = 0;
            filter.Reserved = 0;
            using var filter_hndl = SafeHGlobalHandle.FromStruct(filter);

            return NativeMethods.RegisterDeviceNotification(recipient, filter_hndl.DangerousGetHandle(), flags);
        }

        public SafeHandleDeviceNotification()
            : this(IntPtr.Zero)
        { }

        public SafeHandleDeviceNotification(IntPtr ptr)
            : base(true)
        {
            SetHandle(ptr);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.UnregisterDeviceNotification(handle);
        }
    }
}
