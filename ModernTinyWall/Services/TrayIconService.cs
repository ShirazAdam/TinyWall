using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed partial class TrayIconService : ITrayIconService
{
    private static readonly IReadOnlyList<TrayCommand> Commands =
    [
        new TrayCommand("overview", "Overview"),
        new TrayCommand("settings", "Settings"),
        new TrayCommand("connections", "Connections"),
        new TrayCommand("processes", "Processes"),
        new TrayCommand("services", "Services"),
        new TrayCommand("packages", "UWP packages"),
        new TrayCommand("exceptions", "Application exceptions"),
        new TrayCommand("options", "Options"),
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

    private static readonly Dictionary<string, TrayCommand> CommandsById = Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

    private const int NimAdd = 0x00000000;
    private const int NimModify = 0x00000001;
    private const int NimDelete = 0x00000002;
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;
    private const int NifShowtip = 0x00000080;
    private const int WmApp = 0x8000;
    private const int WmRbuttonup = 0x0205;
    private const int WmLbuttondblclk = 0x0203;
    private const int WmTimer = 0x0113;
    private const int WmNull = 0x0000;
    private const int WmMenuTrafficReady = WmApp + 101;
    private const int TrayCallbackMessage = WmApp + 100;
    private const nuint MenuTrafficTimerId = 1;
    private const uint TpmRightbutton = 0x0002;
    private const uint TpmReturncmd = 0x0100;
    private const int ImageIcon = 1;
    private const int LrLoadfromfile = 0x00000010;
    private const int LrDefaultsize = 0x00000040;

    private IntPtr _windowHandle;
    private IntPtr _iconHandle;
    private bool _ownsIconHandle;
    private bool _isVisible;
    private const string TooltipText = "Modern TinyWall";
    private readonly Dictionary<uint, string> _menuCommandMap = [];
    private TrayStateSnapshot _snapshot = new("Traffic rate unavailable", "unknown", false, false, false);
    private IntPtr _activeMenu;
    private Func<CancellationToken, Task<TrayStateSnapshot>>? _activeSnapshotProvider;
    private int _activeMenuRefreshInProgress;
    private int _activeMenuRefreshGeneration;
    private TrayStateSnapshot? _pendingActiveMenuSnapshot;

    public event EventHandler<TrayCommand>? CommandInvoked;

    public void Initialise(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        (_iconHandle, _ownsIconHandle) = LoadFirewallIcon();
        UpdateIcon(NimAdd);
        _isVisible = true;
    }

    public void SetSnapshot(TrayStateSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public bool HandleWindowMessage(uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message != TrayCallbackMessage)
        {
            if (message == WmTimer && wParam == new IntPtr((nint)MenuTrafficTimerId) && _activeMenu != IntPtr.Zero)
            {
                try
                {
                    RefreshActiveMenuTrafficInBackground();
                }
                catch (ObjectDisposedException)
                {
                    return true;
                }

                return true;
            }

            if (message == WmMenuTrafficReady)
            {
                ApplyPendingActiveMenuSnapshot(lParam.ToInt32());
                return true;
            }

            return false;
        }

        var mouseMessage = lParam.ToInt32();
        if (mouseMessage == WmRbuttonup)
        {
            return false;
        }

        if (mouseMessage == WmLbuttondblclk)
        {
            InvokeCommand("overview");
            return true;
        }

        return false;
    }

    private static (IntPtr Handle, bool OwnsHandle) LoadFirewallIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "firewall.ico");
        if (File.Exists(iconPath))
        {
            var handle = LoadImage(IntPtr.Zero, iconPath, ImageIcon, 0, 0, LrLoadfromfile | LrDefaultsize);
            if (handle != IntPtr.Zero)
                return (handle, true);
        }

        return (LoadIcon(IntPtr.Zero, new IntPtr(32512)), false);
    }

    public bool IsTrayCallbackMessage(uint message, IntPtr lParam)
    {
        return message == TrayCallbackMessage && lParam.ToInt32() == WmRbuttonup;
    }

    public void ShowContextMenu(Func<CancellationToken, Task<TrayStateSnapshot>> snapshotProvider)
    {
        ArgumentNullException.ThrowIfNull(snapshotProvider);

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
            AppendMenuItem(menu, 300, "Overview", "overview");
            AppendMenuItem(menu, 301, "Manage settings", "settings");
            AppendMenuItem(menu, 302, "Show connections", "connections");
            AppendMenuItem(menu, 303, "Show processes", "processes");
            AppendMenuItem(menu, 304, "Show services", "services");
            AppendMenuItem(menu, 305, "Show UWP packages", "packages");
            AppendMenuItem(menu, 306, "Application exceptions", "exceptions");
            AppendMenuItem(menu, 307, "Options", "options");
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 400, "Lock", "lock", enabled: !_snapshot.IsLocked);
            AppendMenuItem(menu, 401, "Unlock", "unlock", enabled: _snapshot.IsLocked);
            AppendMenuItem(menu, 402, "Run elevated", "elevate");
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 500, "Allow local subnet", "allowLocalSubnet", isChecked: _snapshot.AllowLocalSubnet);
            AppendMenuItem(menu, 501, "Enable hosts blocklist", "hostsBlocklist", isChecked: _snapshot.HostsBlocklistEnabled);
            AppendMenuSeparator(menu);
            AppendMenuItem(menu, 900, "Exit", "exit");

            _activeMenu = menu;
            _activeSnapshotProvider = snapshotProvider;
            _activeMenuRefreshInProgress = 0;
            _activeMenuRefreshGeneration++;
            _pendingActiveMenuSnapshot = null;
            RefreshActiveMenuTrafficInBackground();

            _ = SetForegroundWindow(_windowHandle);
            _ = SetTimer(_windowHandle, MenuTrafficTimerId, 1000, IntPtr.Zero);
            _ = GetCursorPos(out var point);
            var commandId = TrackPopupMenu(menu, TpmRightbutton | TpmReturncmd, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
            _ = KillTimer(_windowHandle, MenuTrafficTimerId);
            _ = PostMessage(_windowHandle, WmNull, IntPtr.Zero, IntPtr.Zero);

            if (commandId != 0 && _menuCommandMap.TryGetValue(commandId, out var command))
                InvokeCommand(command);
        }
        finally
        {
            _ = KillTimer(_windowHandle, MenuTrafficTimerId);
            _activeSnapshotProvider = null;
            _activeMenu = IntPtr.Zero;
            _activeMenuRefreshInProgress = 0;
            _activeMenuRefreshGeneration++;
            _pendingActiveMenuSnapshot = null;
            _ = DestroyMenu(menu);
        }
    }

    private void RefreshActiveMenuTrafficInBackground()
    {
        if (_activeSnapshotProvider is null || _activeMenu == IntPtr.Zero)
            return;

        if (Interlocked.Exchange(ref _activeMenuRefreshInProgress, 1) == 1)
            return;

        _ = RefreshActiveMenuTrafficAsync(_activeSnapshotProvider, _activeMenuRefreshGeneration);
    }

    private async Task RefreshActiveMenuTrafficAsync(Func<CancellationToken, Task<TrayStateSnapshot>> snapshotProvider, int refreshGeneration)
    {
        var menu = _activeMenu;

        try
        {
            var snapshot = await snapshotProvider(CancellationToken.None);
            if (refreshGeneration == _activeMenuRefreshGeneration && menu != IntPtr.Zero && menu == _activeMenu)
            {
                _pendingActiveMenuSnapshot = snapshot;
                _ = PostMessage(_windowHandle, WmMenuTrafficReady, IntPtr.Zero, new IntPtr(refreshGeneration));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException ex)
        {
            Debug.WriteLine($"Tray traffic refresh skipped because the tray service was disposed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Tray traffic refresh failed: {ex}");
        }
        finally
        {
            Interlocked.Exchange(ref _activeMenuRefreshInProgress, 0);
        }
    }

    private void ApplyPendingActiveMenuSnapshot(int refreshGeneration)
    {
        if (refreshGeneration != _activeMenuRefreshGeneration || _activeMenu == IntPtr.Zero || _pendingActiveMenuSnapshot is not { } snapshot)
            return;

        SetSnapshot(snapshot);
        UpdateMenuTrafficText(_activeMenu, snapshot.TrafficText);
        DrawMenuBar(_windowHandle);
    }

    private static void UpdateMenuTrafficText(IntPtr menu, string text)
    {
        const uint MF_BYCOMMAND = 0x00000000;
        const uint MF_STRING = 0x00000000;
        const uint MF_GRAYED = 0x00000001;
        _ = ModifyMenu(menu, 100, MF_BYCOMMAND | MF_STRING | MF_GRAYED, 100, text);
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
        if (_isVisible)
            UpdateIcon(NimModify);
    }

    public IReadOnlyList<TrayCommand> GetCommands()
    {
        return Commands;
    }

    public void InvokeCommand(string commandId)
    {
        if (CommandsById.TryGetValue(commandId, out var command))
            CommandInvoked?.Invoke(this, command);
    }

    public void Dispose()
    {
        if (_isVisible)
        {
            UpdateIcon(NimDelete);
            _isVisible = false;
        }

        if (_ownsIconHandle && _iconHandle != IntPtr.Zero)
        {
            _ = DestroyIcon(_iconHandle);
            _ownsIconHandle = false;
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
            uFlags = NifMessage | NifIcon | NifTip | NifShowtip,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _iconHandle
        };

        unsafe
        {
            CopyString(data.szTip, 128, TooltipText);
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

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool Shell_NotifyIcon(int dwMessage, NotifyIconData* lpData);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW", SetLastError = true)]
    private static partial IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);

    [LibraryImport("user32.dll", EntryPoint = "CreatePopupMenu", SetLastError = true)]
    private static partial IntPtr CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIdNewItem, string lpNewItem);

    [LibraryImport("user32.dll", EntryPoint = "ModifyMenuW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ModifyMenu(IntPtr hMnu, uint uPosition, uint uFlags, uint uIdNewItem, string lpNewItem);

    [LibraryImport("user32.dll", EntryPoint = "DestroyMenu", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(IntPtr hMenu);

    [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenu", SetLastError = true)]
    private static partial uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [LibraryImport("user32.dll", EntryPoint = "SetForegroundWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "GetCursorPos", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "DrawMenuBar", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DrawMenuBar(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "SetTimer", SetLastError = true)]
    private static partial nuint SetTimer(IntPtr hWnd, nuint nIdEvent, uint uElapse, IntPtr lpTimerFunc);

    [LibraryImport("user32.dll", EntryPoint = "KillTimer", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool KillTimer(IntPtr hWnd, nuint uIdEvent);

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
