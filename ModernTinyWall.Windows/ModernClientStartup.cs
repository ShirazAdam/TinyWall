using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModernTinyWall.Windows;

public static class ModernClientStartup
{
    public const string StartupArgument = "--startup";

    public static string GetExecutablePath(string serviceExecutablePath)
    {
        var directory = Path.GetDirectoryName(serviceExecutablePath);
        if (string.IsNullOrEmpty(directory))
            throw new ArgumentException("The service executable path must have a directory.", nameof(serviceExecutablePath));
        return Path.Combine(directory, "ModernTinyWall.exe");
    }

    public static bool IsStartup(IEnumerable<string> arguments) =>
        arguments.Any(argument => string.Equals(argument, StartupArgument, StringComparison.OrdinalIgnoreCase));
}
