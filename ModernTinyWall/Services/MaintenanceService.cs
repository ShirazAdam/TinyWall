using ModernTinyWall.TinyWall;
using System;
using System.Diagnostics;
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
        if (result is { Success: true, UpdateAvailable: true, DownloadUrl: not null })
        {
            var download = await _updateService.DownloadUpdateAsync(result.DownloadUrl, cancellationToken).ConfigureAwait(false);
            if (!download.Success || download.FilePath is null)
                return new MaintenanceResult(false, download.Message);

            try
            {
                Process.Start(new ProcessStartInfo(download.FilePath) { UseShellExecute = true });
                return new MaintenanceResult(true, $"{result.Message} Installer launched from {download.FilePath}.");
            }
            catch (Exception ex)
            {
                return new MaintenanceResult(false, $"Update downloaded, but the installer could not be launched: {ex.Message}");
            }
        }

        return new MaintenanceResult(result.Success, result.Message);
    }

    public Task<MaintenanceResult> ImportSettingsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var imported = SerialisationHelper.DeserialiseFromFile(filePath, new ConfigContainer(), true);
                var changeset = Guid.Empty;
                var response = _controller.GetServerConfig(out _, out _, ref changeset);
                if (response != MessageType.GET_SETTINGS)
                    return new MaintenanceResult(false, "Could not load current TinyWall settings before import.");

                var updateResponse = _controller.SetServerConfig(imported.Service, changeset);
                if (updateResponse.Type != MessageType.PUT_SETTINGS)
                    return new MaintenanceResult(false, updateResponse.Type switch
                    {
                        MessageType.RESPONSE_LOCKED => "TinyWall is locked. Unlock it before importing settings.",
                        MessageType.COM_ERROR => "Could not contact the TinyWall service.",
                        MessageType.RESPONSE_ERROR => "The TinyWall service could not import settings.",
                        _ => $"Unexpected TinyWall service response: {updateResponse.Type}."
                    });

                imported.Controller.Save();
                return new MaintenanceResult(true, "Settings imported.");
            }
            catch (Exception ex)
            {
                return new MaintenanceResult(false, $"Could not import settings: {ex.Message}");
            }
        }, cancellationToken);
    }

    public Task<MaintenanceResult> ExportSettingsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var changeset = Guid.Empty;
                var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
                if (response != MessageType.GET_SETTINGS || serviceConfig is null)
                    return new MaintenanceResult(false, "Could not load TinyWall settings from the service.");

                var controllerSettings = ControllerSettings.Load();
                SerialisationHelper.SerialiseToFile(new ConfigContainer(serviceConfig, controllerSettings), filePath);
                return new MaintenanceResult(true, "Settings exported.");
            }
            catch (Exception ex)
            {
                return new MaintenanceResult(false, $"Could not export settings: {ex.Message}");
            }
        }, cancellationToken);
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
