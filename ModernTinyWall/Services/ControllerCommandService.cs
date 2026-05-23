using ModernTinyWall.TinyWall;
using ModernTinyWall.Windows;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IControllerCommandService
{
    Task<CommandResult> LockAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> UnlockAsync(string password, CancellationToken cancellationToken = default);
    Task<CommandResult> ElevateAsync(CancellationToken cancellationToken = default);
    Task<TrayStateSnapshot> GetTrayStateSnapshotAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> ToggleAllowLocalSubnetAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> ToggleHostsBlocklistAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> WhitelistWindowUnderCursorAsync(CancellationToken cancellationToken = default);
}

internal sealed partial class ControllerCommandService : IControllerCommandService
{
    private readonly Controller _controller = new("TinyWallController");
    private readonly TrafficRateMonitor _trafficRateMonitor = new();

    public Task<TrayStateSnapshot> GetTrayStateSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var changeset = Guid.Empty;
                _ = _controller.GetServerConfig(out var serviceConfig, out var serverState, ref changeset);
                _trafficRateMonitor.Update();

                return new TrayStateSnapshot(
                    FormatTraffic(_trafficRateMonitor.BytesReceivedPerSec, _trafficRateMonitor.BytesSentPerSec),
                    ToModeId(serverState?.Mode ?? FirewallMode.Unknown),
                    serverState?.Locked ?? _controller.IsServerLocked,
                    serviceConfig?.ActiveProfile.AllowLocalSubnet ?? false,
                    serviceConfig?.Blocklists.EnableHostsBlocklist == true && serviceConfig.Blocklists.EnableBlocklists);
            }
            catch
            {
                return new TrayStateSnapshot("Traffic rate unavailable", "unknown", false, false, false);
            }
        }, cancellationToken);
    }

    public Task<CommandResult> LockAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _controller.LockServer();
            return response switch
            {
                MessageType.LOCK => new CommandResult(true, "TinyWall locked."),
                MessageType.COM_ERROR => new CommandResult(false, "Could not contact the TinyWall service."),
                _ => new CommandResult(false, $"Unexpected TinyWall service response: {response}.")
            };
        }, cancellationToken);
    }

    public Task<CommandResult> UnlockAsync(string password, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _controller.TryUnlockServer(password);
            return response switch
            {
                MessageType.UNLOCK => new CommandResult(true, "TinyWall unlocked."),
                MessageType.RESPONSE_LOCKED => new CommandResult(false, "The password was not accepted."),
                MessageType.COM_ERROR => new CommandResult(false, "Could not contact the TinyWall service."),
                _ => new CommandResult(false, $"Unexpected TinyWall service response: {response}.")
            };
        }, cancellationToken);
    }

    public Task<CommandResult> ElevateAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? AppContext.BaseDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                });

                return new CommandResult(true, "Elevated ModernTinyWall instance requested.");
            }
            catch (Exception ex)
            {
                return new CommandResult(false, $"Could not request elevation: {ex.Message}");
            }
        }, cancellationToken);
    }

    public Task<CommandResult> ToggleAllowLocalSubnetAsync(CancellationToken cancellationToken = default)
    {
        return MutateServerConfigAsync(config =>
        {
            config.ActiveProfile.AllowLocalSubnet = !config.ActiveProfile.AllowLocalSubnet;
            return config.ActiveProfile.AllowLocalSubnet ? "Local subnet allowed." : "Local subnet blocked.";
        }, cancellationToken);
    }

    public Task<CommandResult> ToggleHostsBlocklistAsync(CancellationToken cancellationToken = default)
    {
        return MutateServerConfigAsync(config =>
        {
            config.Blocklists.EnableBlocklists = true;
            config.Blocklists.EnableHostsBlocklist = !config.Blocklists.EnableHostsBlocklist;
            return config.Blocklists.EnableHostsBlocklist ? "Hosts blocklist enabled." : "Hosts blocklist disabled.";
        }, cancellationToken);
    }

    public Task<CommandResult> WhitelistWindowUnderCursorAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GetCursorPos(out var point))
                return new CommandResult(false, "Could not get the cursor position.");

            var window = WindowFromPoint(point);
            if (window == IntPtr.Zero)
                return new CommandResult(false, "No window was found under the cursor.");

            _ = GetWindowThreadProcessId(window, out var processId);
            if (processId == 0)
                return new CommandResult(false, "Could not identify the selected window process.");

            return MutateServerConfig(
                config => config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForProcessId(processId)),
                "Window application exceptions added.",
                cancellationToken);
        }, cancellationToken);
    }

    private Task<CommandResult> MutateServerConfigAsync(Func<ServerConfiguration, string> mutation, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changeset = Guid.Empty;
            var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
            if (response != MessageType.GET_SETTINGS || serviceConfig is null)
                return new CommandResult(false, "Could not load TinyWall settings from the service.");

            var successMessage = mutation(serviceConfig);
            var updateResponse = _controller.SetServerConfig(serviceConfig, changeset);
            return updateResponse.Type switch
            {
                MessageType.PUT_SETTINGS => new CommandResult(true, successMessage),
                MessageType.RESPONSE_LOCKED => new CommandResult(false, "TinyWall is locked. Unlock it before changing settings."),
                MessageType.COM_ERROR => new CommandResult(false, "Could not contact the TinyWall service."),
                MessageType.RESPONSE_ERROR => new CommandResult(false, "The TinyWall service could not apply the change."),
                _ => new CommandResult(false, $"Unexpected TinyWall service response: {updateResponse.Type}.")
            };
        }, cancellationToken);
    }

    private CommandResult MutateServerConfig(Action<ServerConfiguration> mutation, string successMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var changeset = Guid.Empty;
        var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
        if (response != MessageType.GET_SETTINGS || serviceConfig is null)
            return new CommandResult(false, "Could not load TinyWall settings from the service.");

        mutation(serviceConfig);
        var updateResponse = _controller.SetServerConfig(serviceConfig, changeset);
        return updateResponse.Type switch
        {
            MessageType.PUT_SETTINGS => new CommandResult(true, successMessage),
            MessageType.RESPONSE_LOCKED => new CommandResult(false, "TinyWall is locked. Unlock it before changing settings."),
            MessageType.COM_ERROR => new CommandResult(false, "Could not contact the TinyWall service."),
            MessageType.RESPONSE_ERROR => new CommandResult(false, "The TinyWall service could not apply the change."),
            _ => new CommandResult(false, $"Unexpected TinyWall service response: {updateResponse.Type}.")
        };
    }

    private static string ToModeId(FirewallMode mode)
    {
        return mode switch
        {
            FirewallMode.Normal => "normal",
            FirewallMode.BlockAll => "blockAll",
            FirewallMode.AllowOutgoing => "allowOutgoing",
            FirewallMode.Disabled => "disabled",
            FirewallMode.Learning => "learning",
            _ => "unknown"
        };
    }

    private static string FormatTraffic(long receivedBytesPerSecond, long sentBytesPerSecond)
    {
        return string.Format(CultureInfo.CurrentCulture, "Traffic: ↓ {0}/s  ↑ {1}/s", FormatBytes(receivedBytesPerSecond), FormatBytes(sentBytesPerSecond));
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = Math.Max(0, bytes);
        var unitIndex = 0;
        var displayValue = (double)value;
        while (displayValue >= 1024 && unitIndex < units.Length - 1)
        {
            displayValue /= 1024;
            unitIndex++;
        }

        return string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", displayValue, units[unitIndex]);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial IntPtr WindowFromPoint(Point point);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

internal sealed record CommandResult(bool Success, string Message);
