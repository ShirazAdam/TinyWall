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
        var fileBuffer = new char[MaxPath];
        var ofn = CreateOpenFileName(owner, fileBuffer, title);
        ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY;

        return GetOpenFileName(ref ofn) ? new string(fileBuffer, 0, Array.IndexOf(fileBuffer, '\0')) : null;
    }

    public static string? PickSaveFile(IntPtr owner, string title, string defaultFileName)
    {
        var fileBuffer = new char[MaxPath];
        defaultFileName.CopyTo(0, fileBuffer, 0, Math.Min(defaultFileName.Length, fileBuffer.Length - 1));
        var ofn = CreateOpenFileName(owner, fileBuffer, title);
        ofn.Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY;
        ofn.lpstrDefExt = "tws";

        return GetSaveFileName(ref ofn) ? new string(fileBuffer, 0, Array.IndexOf(fileBuffer, '\0')) : null;
    }

    private static OpenFileName CreateOpenFileName(IntPtr owner, char[] fileBuffer, string title)
    {
        return new OpenFileName
        {
            lStructSize = Marshal.SizeOf<OpenFileName>(),
            hwndOwner = owner,
            lpstrFilter = "TinyWall settings (*.tws)\0*.tws\0All files (*.*)\0*.*\0",
            lpstrFile = fileBuffer,
            nMaxFile = fileBuffer.Length,
            lpstrTitle = title
        };
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
        [MarshalAs(UnmanagedType.LPWStr)]
        public char[] lpstrFile;
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
