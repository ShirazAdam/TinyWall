using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;


using System.Security;

namespace ModernTinyWall.Windows.WFP
{
    public partial class SublayerCollection : System.Collections.ObjectModel.ReadOnlyCollection<Sublayer>
    {
        [SuppressUnmanagedCodeSecurity]
        internal static partial class NativeMethods
        {
            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmSubLayerCreateEnumHandle0")]

            internal static partial uint FwpmSubLayerCreateEnumHandle0(
                FwpmEngineSafeHandle engineHandle,
                IntPtr enumTemplate,
                out IntPtr enumHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmSubLayerEnum0")]
            internal static partial uint FwpmSubLayerEnum0(
                FwpmEngineSafeHandle engineHandle,
                FwpmSublayerEnumSafeHandle enumHandle,
                uint numEntriesRequested,
                out FwpmMemorySafeHandle entries,
                out uint numEntriesReturned);
        }

        internal SublayerCollection(Engine engine)
            : base(new List<Sublayer>())
        {
            FwpmSublayerEnumSafeHandle? enumSafeHandle = null;


            try
            {
                var err = NativeMethods.FwpmSubLayerCreateEnumHandle0(engine.NativePtr, IntPtr.Zero, out IntPtr outHndl);
                if (0 == err)
                    enumSafeHandle = new FwpmSublayerEnumSafeHandle(outHndl, engine.NativePtr);
                else
                    throw new WfpException(err, "FwpmSessionCreateEnumHandle0");

                while (true)
                {
                    const uint numEntriesRequested = 10;

                    FwpmMemorySafeHandle? entries = null;
                    try
                    {
                        // FwpmSubLayerEnum0() returns a list of pointers in batches
                        err = NativeMethods.FwpmSubLayerEnum0(engine.NativePtr, enumSafeHandle, numEntriesRequested, out entries, out uint numEntriesReturned);
                        if (0 != err)
                            throw new WfpException(err, "FwpmSubLayerEnum0");

                        // Dereference each pointer in the current batch
                        IntPtr[] ptrList = PInvokeHelper.PtrToStructureArray<IntPtr>(entries.DangerousGetHandle(), numEntriesReturned, (uint)IntPtr.Size);
                        for (int i = 0; i < numEntriesReturned; ++i)
                        {
                            Items.Add(new Sublayer(Marshal.PtrToStructure<Interop.FWPM_SUBLAYER0>(ptrList[i])));
                        }

                        // Exit infinite loop if we have exhausted the list
                        if (numEntriesReturned < numEntriesRequested)
                            break;
                    }
                    finally
                    {
                        entries?.Dispose();
                    }
                } // while
            }
            finally
            {
                enumSafeHandle?.Dispose();
            }
        }
    }
}
