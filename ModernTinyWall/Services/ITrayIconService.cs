using System;
using System.Collections.Generic;

namespace ModernTinyWall.Services;

internal interface ITrayIconService : IDisposable
{
    event EventHandler<TrayCommand>? CommandInvoked;

    void Initialise(IntPtr windowHandle);
    void SetStatus(string tooltip);
    bool HandleWindowMessage(uint message, IntPtr wParam, IntPtr lParam);
    IReadOnlyList<TrayCommand> GetCommands();
    void InvokeCommand(string commandId);
}

internal sealed record TrayCommand(string Id, string DisplayName);
