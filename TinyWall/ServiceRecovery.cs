using Microsoft.Win32;
using ModernTinyWall.Windows.Services;
using System;
using System.Threading;

namespace ModernTinyWall.TinyWall;

internal static class ServiceRecovery
{
    // Used by 4.6.4 recovery settings; ignored so that a stale configuration cannot start the GUI as SYSTEM.
    internal const string LegacyCommandArgument = "/recover-service";
    internal static readonly TimeSpan RestartDelay = TimeSpan.FromMinutes(1);
    private static readonly string MutexName = $@"Global\{TinyWallService.SERVICE_NAME}ServiceRecovery";
    private static readonly string KeyPath = $@"SOFTWARE\{TinyWallService.SERVICE_NAME}\ServiceRecovery";
    private const string RunningValue = "Running";
    private const string RestartsValue = "Restarts";

    internal static bool RecoveryLimitReached()
    {
        using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
        return key is not null && ServiceRecoveryPolicy.LimitReached(ReadRunning(key), ReadRestarts(key));
    }

    private static void ConfigureFailureActions(bool restartOnFailure)
    {
        using var scm = new ServiceControlManager();
        scm.SetFailureActions(TinyWallService.SERVICE_NAME, RestartDelay, restartOnFailure);
    }

    internal static void PrepareStart()
    {
        using var recoveryLock = AcquireLock();
        using var key = OpenKey();
        var start = ServiceRecoveryPolicy.OnStart(ReadRunning(key), ReadRestarts(key));
        // Windows retains its failure count across manual starts; enforce our budget independently.
        ConfigureFailureActions(start.RestartOnFailure);
        key.SetValue(RestartsValue, start.Restarts, RegistryValueKind.DWord);
        key.SetValue(RunningValue, 1, RegistryValueKind.DWord);
    }

    internal static void MarkStopped()
    {
        using var recoveryLock = AcquireLock();
        using var key = OpenKey();
        key.SetValue(RunningValue, 0, RegistryValueKind.DWord);
    }

    private static bool ReadRunning(RegistryKey key) => key.GetValue(RunningValue, 0) is 1;

    private static int ReadRestarts(RegistryKey key) => key.GetValue(RestartsValue, 0) is int restarts ? restarts : 0;

    private static RegistryKey OpenKey()
    {
        // Only the recovery subkey is volatile; a volatile parent would prevent persistent sibling keys.
        using var parent = Registry.LocalMachine.CreateSubKey($@"SOFTWARE\{TinyWallService.SERVICE_NAME}", writable: true);
        return parent.CreateSubKey("ServiceRecovery", RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile)
            ?? throw new InvalidOperationException("Could not open the service recovery state.");
    }

    private static IDisposable AcquireLock()
    {
        var mutex = new Mutex(false, MutexName);
        try
        {
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(10)))
                    throw new System.TimeoutException("Timed out waiting for the service recovery lock.");
            }
            catch (AbandonedMutexException)
            {
                // The previous owner exited while holding the lock.
            }
            return new RecoveryLock(mutex);
        }
        catch
        {
            mutex.Dispose();
            throw;
        }
    }

    private sealed class RecoveryLock(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
