using ModernTinyWall.TinyWall;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class UpdateService : IUpdateService
{
    private const int UpdaterVersion = 6;
    private const string UpdateDescriptorUrl = "https://tinywall.pados.hu/updates/UpdVer{0}/update.json";
    private static readonly HttpClient HttpClient = new();

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var descriptor = await GetDescriptorAsync(cancellationToken).ConfigureAwait(false);
            var mainModule = descriptor.Modules.FirstOrDefault(module => string.Equals(module.Component, "TinyWall", StringComparison.InvariantCultureIgnoreCase));
            if (mainModule?.ComponentVersion is null)
                return new UpdateCheckResult(false, false, "The update descriptor did not include TinyWall version information.");

            var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);
            var availableVersion = new Version(mainModule.ComponentVersion);
            if (availableVersion > currentVersion)
            {
                return new UpdateCheckResult(true, true, $"TinyWall {availableVersion} is available.", availableVersion.ToString(), mainModule.UpdateUrl);
            }

            return new UpdateCheckResult(true, false, "No update is available.", availableVersion.ToString(), mainModule.UpdateUrl);
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, false, $"Could not check for updates: {ex.Message}");
        }
    }

    public async Task<UpdateDownloadResult> DownloadUpdateAsync(string downloadUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var updateUri))
                return new UpdateDownloadResult(false, "The update download URL is invalid.");

            var targetFile = Path.Combine(Path.GetTempPath(), $"TinyWall-update-{Guid.NewGuid():N}.msi");
            await using var downloadStream = await HttpClient.GetStreamAsync(updateUri, cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await downloadStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            return new UpdateDownloadResult(true, $"Update downloaded to {targetFile}.", targetFile);
        }
        catch (Exception ex)
        {
            return new UpdateDownloadResult(false, $"Could not download update: {ex.Message}");
        }
    }

    private static async Task<UpdateDescriptor> GetDescriptorAsync(CancellationToken cancellationToken)
    {
        var url = string.Format(CultureInfo.InvariantCulture, UpdateDescriptorUrl, UpdaterVersion);
        var tmpFile = Path.GetTempFileName();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("TW-Version", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0");
            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destinationStream = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);

            var descriptor = SerialisationHelper.DeserialiseFromFile(tmpFile, new UpdateDescriptor(), readOnlySource: true);
            return descriptor.MagicWord != "TinyWall Update Descriptor"
                ? throw new InvalidDataException("Bad update descriptor file.")
                : descriptor;
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
