using System;
using System.Collections.Generic;
using System.IO;
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
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_NULL = 0x0000;
    private const int TrayCallbackMessage = WM_APP + 100;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;
    private const int IMAGE_ICON = 1;
    private const int LR_LOADFROMFILE = 0x00000010;
    private const int LR_DEFAULTSIZE = 0x00000040;

    private IntPtr _windowHandle;
    private IntPtr _iconHandle;
    private bool _isVisible;
    private string _tooltip = "ModernTinyWall";
    private readonly Dictionary<uint, string> _menuCommandMap = [];
    private TrayStateSnapshot _snapshot = new("Traffic rate unavailable", "unknown", false, false, false);

    public event EventHandler<TrayCommand>? CommandInvoked;

    public void Initialise(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        _iconHandle = LoadFirewallIcon();
        UpdateIcon(NIM_ADD);
        _isVisible = true;
    }

    public void SetSnapshot(TrayStateSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public bool HandleWindowMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != TrayCallbackMessage)
            return false;

        var mouseMessage = lParam.ToInt32();
        if (mouseMessage == WM_RBUTTONUP)
        {
            ShowContextMenu();
            return true;
        }

        if (mouseMessage == WM_LBUTTONDBLCLK)
        {
            InvokeCommand("overview");
            return true;
        }

        return false;
    }

    private static IntPtr LoadFirewallIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "firewall.ico");
        if (File.Exists(iconPath))
        {
            var handle = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
            if (handle != IntPtr.Zero)
                return handle;
        }

        return LoadIcon(IntPtr.Zero, new IntPtr(32512));
    }

    public bool IsTrayCallbackMessage(uint message, IntPtr lParam)
    {
        return message == TrayCallbackMessage && lParam.ToInt32() == WM_RBUTTONUP;
    }

    public void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
            return;

        try
        {
            _menuCommandMap.Clear();
            AppendMenuItem(menu, 100, _snapshot.TrafficText, enabled: false);
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 200, "Mode: Normal protection", "normal", isChecked: _snapshot.ModeId == "normal");
            AppendMenuItem(menu, 201, "Mode: Block all", "blockAll", isChecked: _snapshot.ModeId == "blockAll");
            AppendMenuItem(menu, 202, "Mode: Allow outgoing", "allowOutgoing", isChecked: _snapshot.ModeId == "allowOutgoing");
            AppendMenuItem(menu, 203, "Mode: Disabled", "disabled", isChecked: _snapshot.ModeId == "disabled");
            AppendMenuItem(menu, 204, "Mode: Learning", "learning", isChecked: _snapshot.ModeId == "learning");
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 300, "Manage settings", "settings");
            AppendMenuItem(menu, 301, "Show connections", "connections");
            AppendMenuItem(menu, 302, "Show processes", "processes");
            AppendMenuItem(menu, 303, "Show services", "services");
            AppendMenuItem(menu, 304, "Show UWP packages", "packages");
            AppendMenuItem(menu, 305, "Application exceptions", "exceptions");
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 400, "Lock", "lock", enabled: !_snapshot.IsLocked);
            AppendMenuItem(menu, 401, "Unlock", "unlock", enabled: _snapshot.IsLocked);
            AppendMenuItem(menu, 402, "Run elevated", "elevate");
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 500, "Allow local subnet", "allowLocalSubnet", isChecked: _snapshot.AllowLocalSubnet);
            AppendMenuItem(menu, 501, "Enable hosts blocklist", "hostsBlocklist", isChecked: _snapshot.HostsBlocklistEnabled);
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 900, "Exit", "exit");

            _ = SetForegroundWindow(_windowHandle);
            _ = GetCursorPos(out var point);
            var commandId = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
            _ = PostMessage(_windowHandle, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (commandId != 0 && _menuCommandMap.TryGetValue(commandId, out var command))
                InvokeCommand(command);
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    private void AppendMenuItem(IntPtr menu, uint id, string text, string? commandId = null, bool isChecked = false, bool enabled = true)
    {
        const uint MF_STRING = 0x00000000;
        const uint MF_CHECKED = 0x00000008;
        const uint MF_GRAYED = 0x00000001;
        var flags = MF_STRING | (isChecked ? MF_CHECKED : 0) | (enabled ? 0 : MF_GRAYED);
        _ = AppendMenu(menu, flags, id, text);
        if (commandId is not null)
            _menuCommandMap[id] = commandId;
    }

    private static void AppendMenuSeparator(IntPtr menu)
    {
        const uint MF_SEPARATOR = 0x00000800;
        _ = AppendMenu(menu, MF_SEPARATOR, 0, string.Empty);
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
            new TrayCommand("processes", "Processes"),
            new TrayCommand("services", "Services"),
            new TrayCommand("packages", "UWP packages"),
            new TrayCommand("exceptions", "Application exceptions"),
            new TrayCommand("normal", "Normal protection"),
            new TrayCommand("allowOutgoing", "Allow outgoing"),
            new TrayCommand("blockAll", "Block all"),
            new TrayCommand("disabled", "Disabled"),
            new TrayCommand("learning", "Learning"),
            new TrayCommand("lock", "Lock"),
            new TrayCommand("unlock", "Unlock"),
            new TrayCommand("elevate", "Run elevated"),
            new TrayCommand("allowLocalSubnet", "Allow local subnet"),
            new TrayCommand("hostsBlocklist", "Enable hosts blocklist"),
            new TrayCommand("whitelistWindow", "Whitelist by window"),
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

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW", SetLastError = true)]
    private static partial IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(IntPtr hMenu);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

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
