using System;
using System.IO;

namespace ModernTinyWall.TinyWall;

internal static class ControllerClientIdentity
{
    internal static bool IsTrusted(string serviceExecutable, string clientExecutable)
    {
        var servicePath = Path.GetFullPath(serviceExecutable);
        var clientPath = Path.GetFullPath(clientExecutable);
        var directory = Path.GetDirectoryName(servicePath);
        return directory is not null &&
            (string.Equals(clientPath, servicePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(clientPath, Path.Combine(directory, "ModernTinyWall.exe"), StringComparison.OrdinalIgnoreCase));
    }
}
