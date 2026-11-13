namespace ModernTinyWall.TinyWall;

internal readonly record struct ServiceRecoveryStart(int Restarts, bool RestartOnFailure);

internal static class ServiceRecoveryPolicy
{
    internal const int AutomaticRestarts = 2;

    // At the limit all native recovery actions are disabled, so the next start begins a new cycle.
    internal static ServiceRecoveryStart OnStart(bool previousRunEndedUnexpectedly, int restarts)
    {
        if (!previousRunEndedUnexpectedly || restarts < 0 || restarts >= AutomaticRestarts)
            return new ServiceRecoveryStart(0, true);
        var next = restarts + 1;
        return new ServiceRecoveryStart(next, next < AutomaticRestarts);
    }

    internal static bool LimitReached(bool previousRunEndedUnexpectedly, int restarts) =>
        previousRunEndedUnexpectedly && restarts >= AutomaticRestarts;
}
