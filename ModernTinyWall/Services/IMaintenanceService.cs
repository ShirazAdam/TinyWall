using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IMaintenanceService
{
    Task<MaintenanceResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceResult> ImportSettingsAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceResult> ExportSettingsAsync(CancellationToken cancellationToken = default);
    Task<MaintenanceResult> ChangePasswordAsync(string newPassword, CancellationToken cancellationToken = default);
}

internal sealed record MaintenanceResult(bool Success, string Message);
