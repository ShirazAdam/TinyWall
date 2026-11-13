using ModernTinyWall.TinyWall;
using System;
using System.IO;

namespace ModernTinyWall.Services;

internal static class ProtectionModeIcon
{
    internal static string GetPath(FirewallMode mode)
    {
        var fileName = mode switch
        {
            FirewallMode.Normal => "ModernTinyWall.ico",
            FirewallMode.AllowOutgoing => "ModernTinyWall-allow-outgoing.ico",
            FirewallMode.BlockAll => "ModernTinyWall-block-all.ico",
            FirewallMode.Learning => "ModernTinyWall-learning.ico",
            _ => "ModernTinyWall-disabled.ico"
        };
        return Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
    }

    internal static string GetImagePath(FirewallMode mode) => Path.ChangeExtension(GetPath(mode), ".png");
}
