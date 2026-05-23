using System;
using System.Runtime.InteropServices;

namespace ModernTinyWall.Services;

internal static partial class FilePickerService
{
    private const int MaxPath = 260;
    private const uint OFN_EXPLORER = 0x00080000;
    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_HIDEREADONLY = 0x00000004;
    private const uint OFN_OVERWRITEPROMPT = 0x00000002;

    public static string? PickOpenFile(IntPtr owner, string title)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));

        try
        {
            ClearFileBuffer(fileBuffer, MaxPath);

            var ofn = CreateOpenFileName(owner, fileBuffer, MaxPath, title);
            ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY;

            return GetOpenFileName(ref ofn) ? Marshal.PtrToStringUni(fileBuffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    public static string? PickSaveFile(IntPtr owner, string title, string defaultFileName)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));

        try
        {
            ClearFileBuffer(fileBuffer, MaxPath);
            WriteFileBuffer(fileBuffer, MaxPath, defaultFileName);

            var ofn = CreateOpenFileName(owner, fileBuffer, MaxPath, title);
            ofn.Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY;
            ofn.lpstrDefExt = "tws";

            return GetSaveFileName(ref ofn) ? Marshal.PtrToStringUni(fileBuffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    private static OpenFileName CreateOpenFileName(IntPtr owner, IntPtr fileBuffer, int fileBufferLength, string title)
    {
        return new OpenFileName
        {
            lStructSize = Marshal.SizeOf<OpenFileName>(),
            hwndOwner = owner,
            lpstrFilter = "TinyWall settings (*.tws)\0*.tws\0All files (*.*)\0*.*\0",
            lpstrFile = fileBuffer,
            nMaxFile = fileBufferLength,
            lpstrTitle = title
        };
    }

    private static void ClearFileBuffer(IntPtr fileBuffer, int fileBufferLength)
    {
        for (var i = 0; i < fileBufferLength; i++)
            Marshal.WriteInt16(fileBuffer, i * sizeof(char), 0);
    }

    private static void WriteFileBuffer(IntPtr fileBuffer, int fileBufferLength, string value)
    {
        var length = Math.Min(value.Length, fileBufferLength - 1);

        for (var i = 0; i < length; i++)
            Marshal.WriteInt16(fileBuffer, i * sizeof(char), value[i]);
    }

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileName(ref OpenFileName openFileName);

    [DllImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSaveFileName(ref OpenFileName openFileName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string? lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public string? lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string lpstrTitle;
        public uint Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string? lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
