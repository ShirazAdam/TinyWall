using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace ModernTinyWall.Windows.WFP
{
    public sealed partial class FwpmEngineSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        internal static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmEngineClose0")]

            //internal static extern uint FwpmEngineClose0(
            //    [In] IntPtr engineHandle);

            [LibraryImport("FWPUClnt.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]

            internal static partial uint FwpmEngineClose0(IntPtr engineHandle);
        }

        public FwpmEngineSafeHandle()
            : base(true)
        {
        }



        protected override bool ReleaseHandle()
        {
            return (0 == NativeMethods.FwpmEngineClose0(handle));
        }
    }

    public sealed partial class FwpmMemorySafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        internal static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmFreeMemory0")]

            //internal static extern void FwpmFreeMemory0(
            //    [In] ref IntPtr p);

            [LibraryImport("FWPUClnt.dll", StringMarshalling = StringMarshalling.Utf16, EntryPoint = "FwpmFreeMemory0")]

            internal static partial void FwpmFreeMemory0(ref IntPtr p);

        }

        public FwpmMemorySafeHandle()
            : base(true)
        {
        }



        protected override bool ReleaseHandle()
        {
            NativeMethods.FwpmFreeMemory0(ref handle);
            return true;
        }
    }

    public sealed partial class FwpmFilterSubscriptionSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmFilterUnsubscribeChanges0")]

            //internal static extern uint FwpmFilterUnsubscribeChanges0([In] FwpmEngineSafeHandle engineHandle, [In] IntPtr changeHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmFilterUnsubscribeChanges0", StringMarshalling = StringMarshalling.Utf16)]

            internal static partial uint FwpmFilterUnsubscribeChanges0(FwpmEngineSafeHandle engineHandle, IntPtr changeHandle);

        }

        private readonly FwpmEngineSafeHandle _safeEngineHandle;

        public FwpmFilterSubscriptionSafeHandle(IntPtr wrappedHndl, FwpmEngineSafeHandle engineHndl) : base(true)
        {
            bool success = false;

            engineHndl.DangerousAddRef(ref success);

            if (!success)
            {
                _ = NativeMethods.FwpmFilterUnsubscribeChanges0(engineHndl, wrappedHndl);
                throw new Exception("Failed to add reference.");
            }

            _safeEngineHandle = engineHndl;
            SetHandle(wrappedHndl);
        }



        protected override bool ReleaseHandle()
        {
            if (0 == NativeMethods.FwpmFilterUnsubscribeChanges0(_safeEngineHandle, handle))
            {
                _safeEngineHandle.DangerousRelease();
                return true;
            }
            return false;
        }
    }

    public sealed partial class FwpmNetEventSubscriptionSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmNetEventUnsubscribe0")]

            //internal static extern uint FwpmNetEventUnsubscribe0(
            //    [In] FwpmEngineSafeHandle engineHandle,
            //    [In] IntPtr changeHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmNetEventUnsubscribe0", StringMarshalling = StringMarshalling.Utf16)]

            internal static partial uint FwpmNetEventUnsubscribe0(FwpmEngineSafeHandle engineHandle, IntPtr changeHandle);

        }

        private readonly FwpmEngineSafeHandle _safeEngineHandle;

        public FwpmNetEventSubscriptionSafeHandle(IntPtr wrappedHndl, FwpmEngineSafeHandle engineHndl) : base(true)
        {
            bool success = false;

            engineHndl.DangerousAddRef(ref success);

            if (!success)
            {
                _ = NativeMethods.FwpmNetEventUnsubscribe0(engineHndl, wrappedHndl);
                throw new Exception("Failed to add reference.");
            }

            _safeEngineHandle = engineHndl;
            SetHandle(wrappedHndl);
        }



        protected override bool ReleaseHandle()
        {
            if (0 == NativeMethods.FwpmNetEventUnsubscribe0(_safeEngineHandle, handle))
            {
                _safeEngineHandle.DangerousRelease();
                return true;
            }
            return false;
        }
    }

    public sealed partial class FwpmFilterEnumSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmFilterDestroyEnumHandle0")]

            //internal static extern uint FwpmFilterDestroyEnumHandle0(
            //    [In] FwpmEngineSafeHandle engineHandle,
            //    [In] IntPtr enumHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmFilterDestroyEnumHandle0", StringMarshalling = StringMarshalling.Utf16)]

            internal static partial uint FwpmFilterDestroyEnumHandle0(FwpmEngineSafeHandle engineHandle, IntPtr enumHandle);

        }

        private readonly FwpmEngineSafeHandle _safeEngineHandle;

        public FwpmFilterEnumSafeHandle(IntPtr wrappedHndl, FwpmEngineSafeHandle engineHndl) : base(true)
        {
            bool success = false;

            engineHndl.DangerousAddRef(ref success);

            if (!success)
            {
                _ = NativeMethods.FwpmFilterDestroyEnumHandle0(engineHndl, wrappedHndl);
                throw new Exception("Failed to add reference.");
            }

            _safeEngineHandle = engineHndl;
            SetHandle(wrappedHndl);
        }



        protected override bool ReleaseHandle()
        {
            if (0 == NativeMethods.FwpmFilterDestroyEnumHandle0(_safeEngineHandle, handle))
            {
                _safeEngineHandle.DangerousRelease();
                return true;
            }
            return false;
        }
    }

    public sealed partial class FwpmProviderEnumSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmProviderDestroyEnumHandle0")]

            //internal static extern uint FwpmProviderDestroyEnumHandle0(
            //    [In] FwpmEngineSafeHandle engineHandle,
            //    [In] IntPtr enumHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmProviderDestroyEnumHandle0", StringMarshalling = StringMarshalling.Utf16)]

            internal static partial uint FwpmProviderDestroyEnumHandle0(FwpmEngineSafeHandle engineHandle, IntPtr enumHandle);

        }

        private readonly FwpmEngineSafeHandle _safeEngineHandle;

        public FwpmProviderEnumSafeHandle(IntPtr wrappedHndl, FwpmEngineSafeHandle engineHndl) : base(true)
        {
            bool success = false;

            engineHndl.DangerousAddRef(ref success);

            if (!success)
            {
                _ = NativeMethods.FwpmProviderDestroyEnumHandle0(engineHndl, wrappedHndl);
                throw new Exception("Failed to add reference.");
            }

            _safeEngineHandle = engineHndl;
            SetHandle(wrappedHndl);
        }

        protected override bool ReleaseHandle()
        {
            if (0 == NativeMethods.FwpmProviderDestroyEnumHandle0(_safeEngineHandle, handle))
            {
                _safeEngineHandle.DangerousRelease();
                return true;
            }
            return false;
        }
    }

    public sealed partial class FwpmSessionEnumSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmSessionDestroyEnumHandle0")]

            //internal static extern uint FwpmSessionDestroyEnumHandle0(
            //    [In] FwpmEngineSafeHandle engineHandle,
            //    [In] IntPtr enumHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmSessionDestroyEnumHandle0", StringMarshalling = StringMarshalling.Utf16)]

            internal static partial uint FwpmSessionDestroyEnumHandle0(FwpmEngineSafeHandle engineHandle, IntPtr enumHandle);

        }

        private readonly FwpmEngineSafeHandle _safeEngineHandle;

        public FwpmSessionEnumSafeHandle(IntPtr wrappedHndl, FwpmEngineSafeHandle engineHndl) : base(true)
        {
            bool success = false;

            engineHndl.DangerousAddRef(ref success);

            if (!success)
            {
                _ = NativeMethods.FwpmSessionDestroyEnumHandle0(engineHndl, wrappedHndl);
                throw new Exception("Failed to add reference.");
            }

            _safeEngineHandle = engineHndl;
            SetHandle(wrappedHndl);
        }



        protected override bool ReleaseHandle()
        {
            if (0 == NativeMethods.FwpmSessionDestroyEnumHandle0(_safeEngineHandle, handle))
            {
                _safeEngineHandle.DangerousRelease();
                return true;
            }
            return false;
        }
    }

    public sealed partial class FwpmSublayerEnumSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("FWPUClnt.dll", EntryPoint = "FwpmSubLayerDestroyEnumHandle0")]

            //internal static extern uint FwpmSubLayerDestroyEnumHandle0(
            //    [In] FwpmEngineSafeHandle engineHandle,
            //    [In] IntPtr enumHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmSubLayerDestroyEnumHandle0", StringMarshalling = StringMarshalling.Utf16)]

            internal static partial uint FwpmSubLayerDestroyEnumHandle0(FwpmEngineSafeHandle engineHandle, IntPtr enumHandle);

        }

        private readonly FwpmEngineSafeHandle _safeEngineHandle;

        public FwpmSublayerEnumSafeHandle(IntPtr wrappedHndl, FwpmEngineSafeHandle engineHndl) : base(true)
        {
            bool success = false;

            engineHndl.DangerousAddRef(ref success);

            if (!success)
            {
                _ = NativeMethods.FwpmSubLayerDestroyEnumHandle0(engineHndl, wrappedHndl);
                throw new Exception("Failed to add reference.");
            }

            _safeEngineHandle = engineHndl;
            SetHandle(wrappedHndl);
        }

        protected override bool ReleaseHandle()
        {
            if (0 == NativeMethods.FwpmSubLayerDestroyEnumHandle0(_safeEngineHandle, handle))
            {
                _safeEngineHandle.DangerousRelease();
                return true;
            }
            return false;
        }
    }

    public sealed partial class SafeHGlobalHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("kernel32")]
            //public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

            //[DllImport("kernel32")]
            //public static extern IntPtr GlobalFree(IntPtr hMem);

            [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16)]
            public static partial IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

            [LibraryImport("kernel32", StringMarshalling = StringMarshalling.Utf16)]
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

        public static SafeHGlobalHandle FromString(string str)
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

        public static SafeHGlobalHandle FromManagedStruct<T>(T obj)
        {
            var size = Marshal.SizeOf(typeof(T));
            var ret = Alloc(size, true);
            ret.MarshalFromManagedStruct(obj);
            return ret;
        }

        public void MarshalFromStruct<T>(T obj, int offset = 0) where T : unmanaged
        {
            if (NeedsMarshalDestroy)
                Marshal.DestroyStructure(this.handle, MarshalDestroyType);

            int size = Marshal.SizeOf(typeof(T));
            unsafe
            {
                System.Diagnostics.Debug.Assert(sizeof(T) == size);
                Buffer.MemoryCopy(&obj, (byte*)this.handle.ToPointer() + offset, size, size);
            }
            MarshalDestroyType = null;
        }

        public void MarshalFromManagedStruct<T>(T obj)
        {
            Marshal.StructureToPtr(obj, this.handle, NeedsMarshalDestroy);
            MarshalDestroyType = typeof(T);
        }

        public T ToStruct<T>() where T : unmanaged
        {
            T ret = default;
            var size = Marshal.SizeOf(typeof(T));
            unsafe
            {
                System.Diagnostics.Debug.Assert(sizeof(T) == size);
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
                if (NeedsMarshalDestroy)
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
            if (NeedsMarshalDestroy)
            {
                Marshal.DestroyStructure(this.handle, MarshalDestroyType);
                MarshalDestroyType = null;
            }
            bool ret = (IntPtr.Zero == NativeMethods.GlobalFree(handle));
            SetHandle(IntPtr.Zero);
            return ret;
        }
    }

    public sealed partial class AllocHLocalSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("kernel32.dll")]
            //internal static extern IntPtr LocalAlloc(uint uFlags, UIntPtr dwBytes);

            //[DllImport("kernel32.dll")]

            //internal static extern IntPtr LocalFree(IntPtr hMem);

            [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
            internal static partial IntPtr LocalAlloc(uint uFlags, UIntPtr dwBytes);

            [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
            internal static partial IntPtr LocalFree(IntPtr hMem);

        }

        public AllocHLocalSafeHandle(int nBytes)
            : base(true)
        {
            handle = NativeMethods.LocalAlloc(0, new UIntPtr((uint)nBytes));
        }

        public AllocHLocalSafeHandle(IntPtr ptr, bool ownsHandle)
            : base(ownsHandle)
        {
            handle = ptr;
        }

        public AllocHLocalSafeHandle()
            : base(true)
        {
            handle = IntPtr.Zero;
        }



        protected override bool ReleaseHandle()
        {
            return (IntPtr.Zero == NativeMethods.LocalFree(handle));
        }
    }

    public sealed partial class SidSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            //[DllImport("advapi32.dll", SetLastError = false)]
            //internal static extern IntPtr FreeSid(IntPtr sid);

            [LibraryImport("advapi32.dll", SetLastError = false, StringMarshalling = StringMarshalling.Utf16)]
            internal static partial IntPtr FreeSid(IntPtr sid);

        }

        public SidSafeHandle(IntPtr ptr, bool ownsHandle)
            : base(ownsHandle)
        {
            handle = ptr;
        }

        public SidSafeHandle()
            : base(true)
        {
            handle = IntPtr.Zero;
        }



        protected override bool ReleaseHandle()
        {
            return (IntPtr.Zero == NativeMethods.FreeSid(handle));
        }
    }
}
