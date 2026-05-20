using System;

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

    public void Dispose()
    {
        _windowHandle = IntPtr.Zero;
    }
}
