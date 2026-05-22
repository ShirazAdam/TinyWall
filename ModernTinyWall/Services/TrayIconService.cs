using System;
using System.Collections.Generic;

namespace ModernTinyWall.Services;

internal sealed class TrayIconService : ITrayIconService
{
    private IntPtr _windowHandle;
    private string _tooltip = "ModernTinyWall";

    public void Initialise(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public void SetStatus(string tooltip)
    {
        _tooltip = tooltip;
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

    public void Dispose()
    {
        _windowHandle = IntPtr.Zero;
    }
}
