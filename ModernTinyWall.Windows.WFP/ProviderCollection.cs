using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using System.Security;

namespace ModernTinyWall.Windows.WFP
{
    public partial class ProviderCollection : System.Collections.ObjectModel.ReadOnlyCollection<Interop.FWPM_PROVIDER0>
    {
        [SuppressUnmanagedCodeSecurity]
        internal static partial class NativeMethods
        {
            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmProviderCreateEnumHandle0")]

            internal static partial uint FwpmProviderCreateEnumHandle0(
                FwpmEngineSafeHandle engineHandle,
                IntPtr enumTemplate,
                out IntPtr enumHandle);

            [LibraryImport("FWPUClnt.dll", EntryPoint = "FwpmProviderEnum0")]
            internal static partial uint FwpmProviderEnum0(
                FwpmEngineSafeHandle engineHandle,
                FwpmProviderEnumSafeHandle enumHandle,
                uint numEntriesRequested,
                out FwpmMemorySafeHandle entries,
                out uint numEntriesReturned);
        }

        internal ProviderCollection(Engine engine)
            : base(new List<Interop.FWPM_PROVIDER0>())
        {
            FwpmProviderEnumSafeHandle? enumSafeHandle = null;

            try
            {
                var err = NativeMethods.FwpmProviderCreateEnumHandle0(engine.NativePtr, IntPtr.Zero, out IntPtr outHndl);
                if (0 == err)
                    enumSafeHandle = new FwpmProviderEnumSafeHandle(outHndl, engine.NativePtr);
                else
                    throw new WfpException(err, "FwpmProviderCreateEnumHandle0");

                while (true)
                {
                    const uint numEntriesRequested = 10;

                    FwpmMemorySafeHandle? entries = null;
                    try
                    {
                        // FwpmProviderEnum0() returns a list of pointers in batches
                        err = NativeMethods.FwpmProviderEnum0(engine.NativePtr, enumSafeHandle, numEntriesRequested, out entries, out uint numEntriesReturned);
                        if (0 != err)
                            throw new WfpException(err, "FwpmProviderEnum0");

                        // Dereference each pointer in the current batch
                        IntPtr[] ptrList = PInvokeHelper.PtrToStructureArray<IntPtr>(entries.DangerousGetHandle(), numEntriesReturned, (uint)IntPtr.Size);
                        for (int i = 0; i < numEntriesReturned; ++i)
                        {
                            Items.Add(Marshal.PtrToStructure<Interop.FWPM_PROVIDER0>(ptrList[i]));
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
