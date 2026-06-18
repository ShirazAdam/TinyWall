using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task<UpdateDownloadResult> DownloadUpdateAsync(string downloadUrl, CancellationToken cancellationToken = default);
}

internal sealed record UpdateCheckResult(bool Success, bool UpdateAvailable, string Message, string? Version = null, string? DownloadUrl = null);

internal sealed record UpdateDownloadResult(bool Success, string Message, string? FilePath = null);
