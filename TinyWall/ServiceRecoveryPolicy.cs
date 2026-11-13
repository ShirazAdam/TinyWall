using System;

namespace ModernTinyWall.TinyWall;

internal static class ServiceRecoveryPolicy
{
    internal const string AutomaticStartArgument = "/automatic-recovery";

    internal static bool IsAutomaticStart(string[] args) =>
        args.Length == 1 && args[0] == AutomaticStartArgument;

    internal static int NextFailure(int failures)
    {
        if (failures < 0 || failures > 3)
            throw new InvalidOperationException("The service recovery counter is invalid.");
        return Math.Min(failures + 1, 3);
    }

    internal static bool ShouldRestart(int failures) => failures < 3;
}
