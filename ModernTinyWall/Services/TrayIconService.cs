using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace ModernTinyWall.Services;

internal sealed partial class TrayIconService : ITrayIconService
{
    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;
    private const int NIF_SHOWTIP = 0x00000080;
    private const int WM_APP = 0x8000;
    private const int TrayCallbackMessage = WM_APP + 100;

    private IntPtr _windowHandle;
    private IntPtr _iconHandle;
    private bool _isVisible;
    private string _tooltip = "ModernTinyWall";

    public event EventHandler<TrayCommand>? CommandInvoked;

    public void Initialise(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _iconHandle = LoadIcon(IntPtr.Zero, new IntPtr(32512));
        UpdateIcon(NIM_ADD);
        _isVisible = true;
    }

    public void SetStatus(string tooltip)
    {
        _tooltip = tooltip;
        if (_isVisible)
            UpdateIcon(NIM_MODIFY);
    }

    public IReadOnlyList<TrayCommand> GetCommands()
    {
        return
        [
            new TrayCommand("overview", "Overview"),
            new TrayCommand("settings", "Settings"),
            new TrayCommand("connections", "Connections"),
            new TrayCommand("normal", "Normal protection"),
            new TrayCommand("allowOutgoing", "Allow outgoing"),
            new TrayCommand("blockAll", "Block all"),
            new TrayCommand("disabled", "Disabled"),
            new TrayCommand("learning", "Learning"),
            new TrayCommand("exit", "Exit")
        ];
    }

    public void InvokeCommand(string commandId)
    {
        var command = GetCommands().FirstOrDefault(command => command.Id == commandId);
        if (command is not null)
            CommandInvoked?.Invoke(this, command);
    }

    public void Dispose()
    {
        if (_isVisible)
        {
            UpdateIcon(NIM_DELETE);
            _isVisible = false;
        }

        _windowHandle = IntPtr.Zero;
        _iconHandle = IntPtr.Zero;
    }

    private void UpdateIcon(int message)
    {
        var data = new NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
            hWnd = _windowHandle,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _iconHandle
        };

        unsafe
        {
            CopyString(data.szTip, 128, _tooltip);
            _ = Shell_NotifyIcon(message, &data);
        }
    }

    private static unsafe void CopyString(char* destination, int destinationLength, string value)
    {
        var length = Math.Min(value.Length, destinationLength - 1);
        for (var i = 0; i < length; i++)
            destination[i] = value[i];

        destination[length] = '\0';
    }

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool Shell_NotifyIcon(int dwMessage, NotifyIconData* lpData);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        public fixed char szTip[128];
        public uint dwState;
        public uint dwStateMask;
        public fixed char szInfo[256];
        public uint uTimeoutOrVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }
}
