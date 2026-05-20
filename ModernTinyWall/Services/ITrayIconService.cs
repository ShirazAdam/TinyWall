using System;

namespace ModernTinyWall.Services;

internal interface ITrayIconService : IDisposable
{
    void Initialise(IntPtr windowHandle);
    void SetStatus(string tooltip);
}
