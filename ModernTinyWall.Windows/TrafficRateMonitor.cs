using System;
using System.Security;
using System.Runtime.InteropServices;
using ModernTinyWall.Utilities;

namespace ModernTinyWall.Windows
{
    public partial class TrafficRateMonitor : Disposable
    {
        private readonly IntPtr hQuery;
        private readonly IntPtr hTxCounter;
        private readonly IntPtr hRxCounter;
        private byte[] buffer = [];

        public TrafficRateMonitor()
        {
            _ = NativeMethods.PdhOpenQuery(null, IntPtr.Zero, out hQuery);
            _ = NativeMethods.PdhAddEnglishCounter(hQuery, "\\Network Interface(*)\\Bytes Sent/Sec", IntPtr.Zero, out hTxCounter);
            _ = NativeMethods.PdhAddEnglishCounter(hQuery, "\\Network Interface(*)\\Bytes Received/Sec", IntPtr.Zero, out hRxCounter);
            _ = NativeMethods.PdhCollectQueryData(hQuery);
        }

        public void Update()
        {
            _ = NativeMethods.PdhCollectQueryData(hQuery);
            BytesSentPerSec = ReadLongCounter(hTxCounter);
            BytesReceivedPerSec = ReadLongCounter(hRxCounter);
        }

        private long ReadLongCounter(IntPtr hCounter)
        {
            const int PDH_CSTATUS_VALID_DATA = 0;
            const int PDH_CSTATUS_NEW_DATA = 1;

            long ret = 0;

            int size = 0;
            int count = 0;
            _ = NativeMethods.PdhGetFormattedCounterArray(hCounter, PDH_FMT.LARGE | PDH_FMT.NOSCALE | PDH_FMT.NOCAP100, ref size, ref count, IntPtr.Zero);

            if (size > buffer.Length)
                buffer = new byte[size];

            unsafe
            {
                fixed (byte* bufferPtr = buffer)
                {
                    _ = NativeMethods.PdhGetFormattedCounterArray(hCounter, PDH_FMT.LARGE | PDH_FMT.NOSCALE | PDH_FMT.NOCAP100, ref size, ref count, (IntPtr)bufferPtr);

                    int stride = (IntPtr.Size == 8) ? 24 : 16;
                    int statusOffset = IntPtr.Size;
                    int largeValueOffset = IntPtr.Size * 2;
                    for (int i = 0; i < count; ++i)
                    {
#if false
                        PDH_FMT_COUNTERVALUE_ITEM item = (PDH_FMT_COUNTERVALUE_ITEM)Marshal.PtrToStructure((IntPtr)(bufferPtr + i * stride), typeof(PDH_FMT_COUNTERVALUE_ITEM));
                        ret += item.FmtValue.largeValue;
#else
                        byte* itemPtr = bufferPtr + i * stride;
                        int CStatus = *(int*)(itemPtr + statusOffset);
                        if ((CStatus == PDH_CSTATUS_NEW_DATA) || (CStatus == PDH_CSTATUS_VALID_DATA))
                            ret += *(long*)(itemPtr + largeValueOffset);
#endif
                    }

                }
            }

            return ret;
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                // Release managed resources
            }

            _ = NativeMethods.PdhCloseQuery(hQuery);
            base.Dispose(disposing);
        }

        public long BytesSentPerSec { get; private set; }
        public long BytesReceivedPerSec { get; private set; }

#if false   // Not used due to inability to compile without platform-dependence
        [StructLayout(LayoutKind.Explicit)]
        private struct PDH_FMT_COUNTERVALUE
        {
            const int PTRSIZE = IntPtr.Size;    // the problematic line

            [FieldOffset(0)]
            public int CStatus;
            [FieldOffset(PTRSIZE)]
            public int longValue;
            [FieldOffset(PTRSIZE)]
            public double doubleValue;
            [FieldOffset(PTRSIZE)]
            public long largeValue;
            [FieldOffset(PTRSIZE)]
            public IntPtr AnsiStringValue;
            [FieldOffset(PTRSIZE)]
            public IntPtr WideStringValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PDH_FMT_COUNTERVALUE_ITEM
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string szName;
            public PDH_FMT_COUNTERVALUE FmtValue;
        }
#endif

        [Flags]
        private enum PDH_FMT
        {
            DOUBLE = 0x00000200,
            LARGE = 0x00000400,
            LONG = 0x00000100,
            NOSCALE = 0x00001000,
            NOCAP100 = 0x00008000,
            Scale1000 = 0x00002000
        }

        [SuppressUnmanagedCodeSecurity]
        private static partial class NativeMethods
        {
            [LibraryImport("pdh", EntryPoint = "PdhOpenQueryW", StringMarshalling = StringMarshalling.Utf16)]
            public static partial int PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);

            [LibraryImport("pdh", EntryPoint = "PdhAddEnglishCounterW", StringMarshalling = StringMarshalling.Utf16)]
            public static partial int PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

            [LibraryImport("pdh", EntryPoint = "PdhCollectQueryData")]
            public static partial int PdhCollectQueryData(IntPtr hQuery);

            [LibraryImport("pdh", EntryPoint = "PdhGetFormattedCounterArrayW")]
            public static partial int PdhGetFormattedCounterArray(IntPtr hCounter, PDH_FMT dwFormat, ref int lpdwBufferSize, ref int lpdwItemCount, IntPtr ItemBuffer);

            [LibraryImport("pdh", EntryPoint = "PdhCloseQuery")]
            public static partial int PdhCloseQuery(IntPtr hQuery);
        }
    }
}
