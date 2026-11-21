using System;
using System.Runtime.InteropServices;

namespace ModernTinyWall.Windows;

public static partial class WinTrust
{
    public enum VerifyResult
    {
        SIGNATURE_MISSING,
        SIGNATURE_VALID,
        SIGNATURE_INVALID
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint StructSize;
        public IntPtr FilePath;
        public IntPtr FileHandle;
        public IntPtr KnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint StructSize;
        public IntPtr PolicyCallbackData;
        public IntPtr SIPClientData;
        public uint UIChoice;
        public uint RevocationChecks;
        public uint UnionChoice;
        public IntPtr FileInfo;
        public uint StateAction;
        public IntPtr StateData;
        public IntPtr URLReference;
        public uint ProviderFlags;
        public uint UIContext;
        public IntPtr SignatureSettings;
    }

    private static readonly Guid GenericVerifyAction = new(0x00aac56b, 0xcd44, 0x11d0, 0x8c, 0xc2, 0x00, 0xc0, 0x4f, 0xc2, 0x95, 0xee);
    private const uint NoTrustUI = 2;
    private const uint CheckWholeChain = 1;
    private const uint FileSubject = 1;
    private const uint VerifyState = 1;
    private const uint CloseState = 2;
    private const uint CacheOnlyUrlRetrieval = 0x00001000;
    private const uint CheckChainRevocation = 0x00000040;
    private const uint DisableMD2AndMD4 = 0x00002000;

    [LibraryImport("wintrust.dll", EntryPoint = "WinVerifyTrust")]
    private static partial int WinVerifyTrust(IntPtr window, in Guid action, ref WinTrustData data);

    public static unsafe VerifyResult VerifyFileAuthenticode(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        fixed (char* path = filePath)
        {
            var file = new WinTrustFileInfo
            {
                StructSize = (uint)sizeof(WinTrustFileInfo),
                FilePath = (IntPtr)path
            };
            var data = new WinTrustData
            {
                StructSize = (uint)sizeof(WinTrustData),
                UIChoice = NoTrustUI,
                RevocationChecks = CheckWholeChain,
                UnionChoice = FileSubject,
                FileInfo = (IntPtr)(&file),
                StateAction = VerifyState,
                ProviderFlags = CacheOnlyUrlRetrieval | CheckChainRevocation | DisableMD2AndMD4
            };

            int status;
            try
            {
                status = WinVerifyTrust(IntPtr.Zero, GenericVerifyAction, ref data);
            }
            finally
            {
                // Close the exact state returned by Windows while all input pointers remain valid.
                data.StateAction = CloseState;
                WinVerifyTrust(IntPtr.Zero, GenericVerifyAction, ref data);
            }

            return unchecked((uint)status) switch
            {
                0 => VerifyResult.SIGNATURE_VALID,
                0x800B0100 or 0x800B0003 or 0x800B0001 => VerifyResult.SIGNATURE_MISSING,
                _ => VerifyResult.SIGNATURE_INVALID
            };
        }
    }
}
