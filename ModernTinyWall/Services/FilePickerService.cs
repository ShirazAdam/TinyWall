using System;
using System.Runtime.InteropServices;

namespace ModernTinyWall.Services;

internal static partial class FilePickerService
{
    private const int MaxPath = 260;
    private const string FileFilter = "TinyWall settings (*.tws)\0*.tws\0All files (*.*)\0*.*\0";
    private const string DefaultExtension = "tws";
    private const uint OFN_EXPLORER = 0x00080000;
    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_HIDEREADONLY = 0x00000004;
    private const uint OFN_OVERWRITEPROMPT = 0x00000002;

    public static string? PickOpenFile(IntPtr owner, string title)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));
        var filterBuffer = Marshal.StringToHGlobalUni(FileFilter);
        var titleBuffer = Marshal.StringToHGlobalUni(title);

        try
        {
            ClearFileBuffer(fileBuffer, MaxPath);

            var ofn = CreateOpenFileName(owner, fileBuffer, MaxPath, titleBuffer, filterBuffer);
            ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_HIDEREADONLY;

            return GetOpenFileName(ref ofn) ? Marshal.PtrToStringUni(fileBuffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(titleBuffer);
            Marshal.FreeHGlobal(filterBuffer);
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    public static string? PickSaveFile(IntPtr owner, string title, string defaultFileName)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPath * sizeof(char));
        var filterBuffer = Marshal.StringToHGlobalUni(FileFilter);
        var titleBuffer = Marshal.StringToHGlobalUni(title);
        var defaultExtensionBuffer = Marshal.StringToHGlobalUni(DefaultExtension);

        try
        {
            ClearFileBuffer(fileBuffer, MaxPath);
            WriteFileBuffer(fileBuffer, MaxPath, defaultFileName);

            var ofn = CreateOpenFileName(owner, fileBuffer, MaxPath, titleBuffer, filterBuffer);
            ofn.Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_HIDEREADONLY;
            ofn.lpstrDefExt = defaultExtensionBuffer;

            return GetSaveFileName(ref ofn) ? Marshal.PtrToStringUni(fileBuffer) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(defaultExtensionBuffer);
            Marshal.FreeHGlobal(titleBuffer);
            Marshal.FreeHGlobal(filterBuffer);
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    private static OpenFileName CreateOpenFileName(IntPtr owner, IntPtr fileBuffer, int fileBufferLength, IntPtr titleBuffer, IntPtr filterBuffer)
    {
        return new OpenFileName
        {
            lStructSize = Marshal.SizeOf<OpenFileName>(),
            hwndOwner = owner,
            lpstrFilter = filterBuffer,
            lpstrFile = fileBuffer,
            nMaxFile = fileBufferLength,
            lpstrTitle = titleBuffer
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

    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetOpenFileName(ref OpenFileName openFileName);

    [LibraryImport("comdlg32.dll", EntryPoint = "GetSaveFileNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSaveFileName(ref OpenFileName openFileName);

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public uint Flags;
        public short nFileOffset;
        public short nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }
}
