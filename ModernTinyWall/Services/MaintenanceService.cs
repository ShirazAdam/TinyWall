using ModernTinyWall.TinyWall;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class MaintenanceService : IMaintenanceService
{
    private readonly Controller _controller = new("TinyWallController");
    private readonly IUpdateService _updateService = new UpdateService();

    public Task<MaintenanceResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        return CheckForUpdatesCoreAsync(cancellationToken);
    }

    private async Task<MaintenanceResult> CheckForUpdatesCoreAsync(CancellationToken cancellationToken)
    {
        var result = await _updateService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
        return new MaintenanceResult(result.Success, result.Message);
    }

    public Task<MaintenanceResult> ImportSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MaintenanceResult(false, "The WinUI import workflow is not yet connected. Use the existing TinyWall settings window until this workflow is fully migrated."));
    }

    public Task<MaintenanceResult> ExportSettingsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MaintenanceResult(false, "The WinUI export workflow is not yet connected. Use the existing TinyWall settings window until this workflow is fully migrated."));
    }

    public Task<MaintenanceResult> ChangePasswordAsync(string newPassword, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = _controller.SetPassphrase(newPassword);
                return response switch
                {
                    MessageType.SET_PASSPHRASE => new MaintenanceResult(true, string.IsNullOrEmpty(newPassword) ? "Password removed." : "Password changed."),
                    MessageType.RESPONSE_LOCKED => new MaintenanceResult(false, "TinyWall is locked. Unlock it before changing the password."),
                    MessageType.COM_ERROR => new MaintenanceResult(false, "Could not contact the TinyWall service."),
                    MessageType.RESPONSE_ERROR => new MaintenanceResult(false, "The TinyWall service could not change the password."),
                    _ => new MaintenanceResult(false, $"Unexpected TinyWall service response: {response}.")
                };
            }
            catch (Exception ex)
            {
                return new MaintenanceResult(false, $"Could not change password: {ex.Message}");
            }
        }, cancellationToken);
    }
}
