using ModernTinyWall.Windows;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModernTinyWall.TinyWall
{

    internal class Updater
    {
        internal static async void StartUpdate()
        {
            var updater = new Updater();
            using var updateCancellation = new CancellationTokenSource();
            try
            {
                UpdateDescriptor? descriptor = null;
                var completed = await updater.ShowProgressDialogAsync(
                    Resources.Messages.PleaseWaitWhileTinyWallChecksForUpdates,
                    TaskDialogProgressBarState.Marquee,
                    updateCancellation,
                    async (_, cancellationToken) =>
                    {
                        descriptor = await UpdateChecker.GetDescriptorAsync(cancellationToken);
                    });

                if (!completed)
                    return;
                if (descriptor is null)
                {
                    Utils.ShowMessageBox(
                        Resources.Messages.ErrorCheckingForUpdates,
                        Resources.Messages.TinyWall,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                await updater.CheckVersion(descriptor);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex, Utils.LOG_ID_GUI);
                Utils.ShowMessageBox(
                    Resources.Messages.ErrorCheckingForUpdates,
                    Resources.Messages.TinyWall,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private async Task CheckVersion(UpdateDescriptor descriptor)
        {
            var updateModule = UpdateChecker.GetMainAppModule(descriptor);
            if (updateModule?.ComponentVersion is null)
            {
                Utils.ShowMessageBox(
                    Resources.Messages.ErrorCheckingForUpdates,
                    Resources.Messages.TinyWall,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            var oldVersion = new Version(Application.ProductVersion);
            var newVersion = new Version(updateModule.ComponentVersion);

            var win10V1903 = VersionInfo.Win10OrNewer && (Environment.OSVersion.Version.Build >= 18362);
            var windowsNewAnyTwUpdate = win10V1903 && (newVersion > oldVersion);
            var windowsOldTwMinorFixOnly = (newVersion > oldVersion) && (newVersion.Major == oldVersion.Major) && (newVersion.Minor == oldVersion.Minor);

            if (windowsNewAnyTwUpdate || windowsOldTwMinorFixOnly)
            {
                var prompt = string.Format(CultureInfo.CurrentCulture, Resources.Messages.UpdateAvailable, updateModule.ComponentVersion);
                if (Utils.ShowMessageBox(prompt, Resources.Messages.TinyWallUpdater, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    await DownloadUpdate(updateModule);
            }
            else
            {
                Utils.ShowMessageBox(Resources.Messages.NoUpdateAvailable, Resources.Messages.TinyWallUpdater, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task DownloadUpdate(UpdateModule mainModule)
        {
            var tmpFile = Path.Combine(Path.GetTempPath(), $"TinyWall-{Guid.NewGuid():N}.msi");
            var keepDownloadedFile = false;
            try
            {
                using var downloadCancellation = new CancellationTokenSource();
                var updateUrl = new Uri(mainModule.UpdateUrl!);
                var completed = await ShowProgressDialogAsync(
                    Resources.Messages.DownloadingUpdate,
                    TaskDialogProgressBarState.Marquee,
                    downloadCancellation,
                    (progress, cancellationToken) => UpdateChecker.DownloadUpdateAsync(updateUrl, tmpFile, progress, cancellationToken));

                if (completed)
                {
                    InstallUpdate(tmpFile);
                    keepDownloadedFile = true;
                }
            }
            catch (Exception ex)
            {
                Utils.LogException(ex, Utils.LOG_ID_GUI);
                Utils.ShowMessageBox(
                    Resources.Messages.ErrorCheckingForUpdates,
                    Resources.Messages.TinyWall,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                if (!keepDownloadedFile && File.Exists(tmpFile))
                {
                    try
                    {
                        File.Delete(tmpFile);
                    }
                    catch (Exception ex)
                    {
                        Utils.LogException(ex, Utils.LOG_ID_GUI);
                    }
                }
            }
        }

        private async Task<bool> ShowProgressDialogAsync(
            string content,
            TaskDialogProgressBarState progressState,
            CancellationTokenSource cancellation,
            Func<IProgress<int>, CancellationToken, Task> operation)
        {
            using var dialogIcon = new TaskDialogIcon(Resources.Icons.firewall);
            var progressBar = new TaskDialogProgressBar(progressState);
            var page = new TaskDialogPage
            {
                Caption = Resources.Messages.TinyWall,
                Heading = Resources.Messages.TinyWallUpdater,
                Text = content,
                Icon = dialogIcon,
                ProgressBar = progressBar,
                AllowCancel = false
            };
            var cancelButton = new TaskDialogButton("Cancel");
            var waitingButton = new TaskDialogButton("Close", enabled: false, allowCloseDialog: false)
            {
                Visible = false
            };
            page.Buttons.Add(cancelButton);
            page.Buttons.Add(waitingButton);

            var dialogTask = TaskDialog.ShowDialogAsync(page, TaskDialogStartupLocation.CenterScreen);
            var progress = new Progress<int>(value =>
            {
                if (page.BoundDialog is null)
                    return;
                if (progressBar.State == TaskDialogProgressBarState.Marquee)
                    progressBar.State = TaskDialogProgressBarState.Normal;
                progressBar.Value = Math.Clamp(value, progressBar.Minimum, progressBar.Maximum);
            });
            Task operationTask;
            try
            {
                operationTask = operation(progress, cancellation.Token);
            }
            catch (Exception ex)
            {
                operationTask = Task.FromException(ex);
            }

            if (await Task.WhenAny(dialogTask, operationTask) == dialogTask)
            {
                cancellation.Cancel();
                try
                {
                    await operationTask;
                }
                catch (Exception) when (cancellation.IsCancellationRequested)
                {
                }
                return false;
            }

            Exception? operationError = null;
            try
            {
                await operationTask;
            }
            catch (Exception ex)
            {
                operationError = ex;
            }

            waitingButton.Enabled = true;
            waitingButton.Visible = true;
            waitingButton.PerformClick();
            await dialogTask;

            if (operationError is not null)
                ExceptionDispatchInfo.Capture(operationError).Throw();

            return true;
        }

        private static void InstallUpdate(string localFilePath)
        {
            Utils.StartProcess(localFilePath, string.Empty, false);
        }
    }

    internal static class UpdateChecker
    {
        private static readonly HttpClient HttpClient = new();
        private const int UPDATER_VERSION = 6;
        private const string URL_UPDATE_DESCRIPTOR = @"https://tinywall.pados.hu/updates/UpdVer{0}/update.json";

        internal static async Task<UpdateDescriptor> GetDescriptorAsync(CancellationToken cancellationToken = default)
        {
            var url = string.Format(CultureInfo.InvariantCulture, URL_UPDATE_DESCRIPTOR, UPDATER_VERSION);
            var tmpFile = Path.GetTempFileName();

            try
            {
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("TW-Version", Application.ProductVersion);
                    using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    await using var destinationStream = new FileStream(tmpFile, FileMode.Create, FileAccess.Write, FileShare.None);
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
                }

                var descriptor = SerialisationHelper.DeserialiseFromFile(tmpFile, new UpdateDescriptor());

                return descriptor.MagicWord != "TinyWall Update Descriptor" ? throw new ApplicationException("Bad update descriptor file.") : descriptor;
            }
            finally
            {
                File.Delete(tmpFile);
            }
        }

        internal static async Task DownloadUpdateAsync(Uri updateUrl, string targetFile, IProgress<int> progress, CancellationToken cancellationToken = default)
        {
            using var response = await HttpClient.GetAsync(updateUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None);

            var totalBytes = response.Content.Headers.ContentLength;
            var buffer = new byte[81920];
            long bytesReadTotal = 0;
            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                bytesReadTotal += bytesRead;
                if (totalBytes is > 0)
                    progress.Report((int)(bytesReadTotal * 100 / totalBytes.Value));
            }
        }

        internal static UpdateModule? GetUpdateModule(UpdateDescriptor descriptor, string moduleName)
        {
            return descriptor.Modules.FirstOrDefault(t => t.Component!.Equals(moduleName, StringComparison.InvariantCultureIgnoreCase));
        }

        internal static UpdateModule? GetMainAppModule(UpdateDescriptor descriptor)
        {
            return GetUpdateModule(descriptor, "TinyWall");
        }
        internal static UpdateModule? GetHostsFileModule(UpdateDescriptor descriptor)
        {
            return GetUpdateModule(descriptor, "HostsFile");
        }
        internal static UpdateModule? GetDatabaseFileModule(UpdateDescriptor descriptor)
        {
            return GetUpdateModule(descriptor, "Database");
        }
    }
}
