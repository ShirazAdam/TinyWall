using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ModernTinyWall.Services;

internal sealed partial class GlobalHotkeyService : IDisposable
{
    private const uint WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private readonly Dictionary<int, string> _commands = [];
    private IntPtr _windowHandle;

    public event EventHandler<string>? HotkeyPressed;

    public void Initialise(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
        Register(1, MOD_CONTROL | MOD_SHIFT, 'E', "exceptions");
        Register(2, MOD_CONTROL | MOD_SHIFT, 'W', "whitelistWindow");
    }

    public bool HandleWindowMessage(uint message, IntPtr wParam)
    {
        if (message != WM_HOTKEY)
            return false;

        var id = wParam.ToInt32();
        if (!_commands.TryGetValue(id, out var command))
            return false;

        HotkeyPressed?.Invoke(this, command);
        return true;
    }

    public void Dispose()
    {
        foreach (var id in _commands.Keys)
            _ = UnregisterHotKey(_windowHandle, id);

        _commands.Clear();
        _windowHandle = IntPtr.Zero;
    }

    private void Register(int id, uint modifiers, int virtualKey, string command)
    {
        if (RegisterHotKey(_windowHandle, id, modifiers, (uint)virtualKey))
            _commands[id] = command;
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
