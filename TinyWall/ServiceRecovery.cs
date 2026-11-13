using Microsoft.Win32;
using System;
using System.ServiceProcess;
using System.Threading;

namespace ModernTinyWall.TinyWall;

internal static class ServiceRecovery
{
    internal const string CommandArgument = "/recover-service";
    private static readonly string MutexName = $@"Global\{TinyWallService.SERVICE_NAME}ServiceRecovery";
    private static readonly string RegistryPath = $@"SOFTWARE\{TinyWallService.SERVICE_NAME}";
    private const string CounterKey = "ServiceRecovery";
    private const string CounterValue = "Failures";

    internal static bool RecoveryLimitReached()
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"{RegistryPath}\{CounterKey}");
        var value = key?.GetValue(CounterValue, 0) ?? 0;
        if (value is not int failures || failures < 0 || failures > 3)
            throw new InvalidOperationException("The service recovery counter is invalid.");
        return failures == 3;
    }

    internal static void PrepareStart(string[] args)
    {
        if (ServiceRecoveryPolicy.IsAutomaticStart(args))
            return;

        using var recoveryLock = AcquireLock();
        using var key = OpenCounter();
        key.SetValue(CounterValue, 0, RegistryValueKind.DWord);
    }

    internal static int Recover()
    {
        try
        {
            using var recoveryLock = AcquireLock();
            using var service = new ServiceController(TinyWallService.SERVICE_NAME);
            if (service.Status != ServiceControllerStatus.Stopped)
                return 0;

            using var key = OpenCounter();
            var value = key.GetValue(CounterValue, 0);
            if (value is not int failures)
                throw new InvalidOperationException("The service recovery counter is invalid.");

            failures = ServiceRecoveryPolicy.NextFailure(failures);
            key.SetValue(CounterValue, failures, RegistryValueKind.DWord);
            if (!ServiceRecoveryPolicy.ShouldRestart(failures))
            {
                Utils.Log("TinyWall service stopped after three unexpected failures; a manual start is required.", Utils.LOG_ID_SERVICE);
                return 0;
            }

            service.Start([ServiceRecoveryPolicy.AutomaticStartArgument]);
            return 0;
        }
        catch (Exception ex)
        {
            Utils.LogException(ex, Utils.LOG_ID_SERVICE);
            return -1;
        }
    }

    private static RegistryKey OpenCounter()
    {
        using var parent = Registry.LocalMachine.CreateSubKey(RegistryPath, writable: true);
        return parent.CreateSubKey(CounterKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryOptions.Volatile)
            ?? throw new InvalidOperationException("Could not open the service recovery counter.");
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
                // The previous recovery process exited while holding the lock.
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
