using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IMaintenanceService
{
    Task<MaintenanceResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceResult> ImportSettingsAsync(string filePath, CancellationToken cancellationToken = default);
    Task<MaintenanceResult> ExportSettingsAsync(string filePath, CancellationToken cancellationToken = default);
    Task<MaintenanceResult> ChangePasswordAsync(string newPassword, CancellationToken cancellationToken = default);
}

internal sealed record MaintenanceResult(bool Success, string Message);
