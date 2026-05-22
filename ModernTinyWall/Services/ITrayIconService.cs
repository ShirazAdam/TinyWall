using System;
using System.Collections.Generic;

namespace ModernTinyWall.Services;

internal interface ITrayIconService : IDisposable
{
    void Initialise(IntPtr windowHandle);
    void SetStatus(string tooltip);
    IReadOnlyList<TrayCommand> GetCommands();
}

internal sealed record TrayCommand(string Id, string DisplayName);
