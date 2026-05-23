using ModernTinyWall.TinyWall;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IControllerCommandService
{
    Task<CommandResult> LockAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> UnlockAsync(string password, CancellationToken cancellationToken = default);
    Task<CommandResult> ElevateAsync(CancellationToken cancellationToken = default);
    Task<CommandResult> SetAllowLocalSubnetAsync(bool isAllowed, CancellationToken cancellationToken = default);
    Task<CommandResult> SetHostsBlocklistAsync(bool isEnabled, CancellationToken cancellationToken = default);
}

internal sealed class ControllerCommandService : IControllerCommandService
{
    private readonly Controller _controller = new("TinyWallController");

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

    public Task<CommandResult> SetAllowLocalSubnetAsync(bool isAllowed, CancellationToken cancellationToken = default)
    {
        return MutateServerConfigAsync(config => config.ActiveProfile.AllowLocalSubnet = isAllowed, isAllowed ? "Local subnet allowed." : "Local subnet blocked.", cancellationToken);
    }

    public Task<CommandResult> SetHostsBlocklistAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        return MutateServerConfigAsync(config =>
        {
            config.Blocklists.EnableBlocklists = true;
            config.Blocklists.EnableHostsBlocklist = isEnabled;
        }, isEnabled ? "Hosts blocklist enabled." : "Hosts blocklist disabled.", cancellationToken);
    }

    private Task<CommandResult> MutateServerConfigAsync(Action<ServerConfiguration> mutation, string successMessage, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
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
        }, cancellationToken);
    }
}

internal sealed record CommandResult(bool Success, string Message);
