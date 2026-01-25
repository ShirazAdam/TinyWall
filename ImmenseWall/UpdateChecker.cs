using Microsoft.Samples;
using pylorak.Windows;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace pylorak.TinyWall
{

    internal class Updater
    {
        private enum UpdaterState
        {
            GettingDescriptor,
            DescriptorReady,
            DownloadingUpdate,
            UpdateDownloadReady
        }

        private UpdaterState _state;
        private string _errorMsg = string.Empty;
        private volatile int _downloadProgress;

        internal static void StartUpdate()
        {
            var updater = new Updater();
            var descriptor = new UpdateDescriptor();
            updater._state = UpdaterState.GettingDescriptor;

            var dialogue = new TaskDialog
            {
                CustomMainIcon = Resources.Icons.firewall,
                WindowTitle = Resources.Messages.TinyWall,
                MainInstruction = Resources.Messages.TinyWallUpdater,
                Content = Resources.Messages.PleaseWaitWhileTinyWallChecksForUpdates,
                AllowDialogCancellation = false,
                CommonButtons = TaskDialogCommonButtons.Cancel,
                ShowMarqueeProgressBar = true,
                Callback = updater.DownloadTickCallback,
                CallbackData = updater,
                CallbackTimer = true
            };

            var updateThread = new Thread(() =>
            {
                try
                {
                    descriptor = UpdateChecker.GetDescriptor();
                    updater._state = UpdaterState.DescriptorReady;
                }
                catch
                {
                    updater._errorMsg = Resources.Messages.ErrorCheckingForUpdates;
                }
            });

            updateThread.Start();

            switch (dialogue.Show())
            {
                case (int)DialogResult.Cancel:
                    updateThread.Interrupt();
                    if (!updateThread.Join(500))
                        updateThread.Abort();
                    break;
                case (int)DialogResult.OK:
                    updater.CheckVersion(descriptor);
                    break;
                case (int)DialogResult.Abort:
                    Utils.ShowMessageBox(updater._errorMsg, Resources.Messages.TinyWall, TaskDialogCommonButtons.Ok, TaskDialogIcon.Error);
                    break;
            }
        }

        private void CheckVersion(UpdateDescriptor descriptor)
        {
            var updateModule = UpdateChecker.GetMainAppModule(descriptor)!;
            var oldVersion = new Version(Application.ProductVersion);
            var newVersion = new Version(updateModule.ComponentVersion!);

            var win10V1903 = VersionInfo.Win10OrNewer && (Environment.OSVersion.Version.Build >= 18362);
            var windowsNewAnyTwUpdate = win10V1903 && (newVersion > oldVersion);
            var windowsOldTwMinorFixOnly = (newVersion > oldVersion) && (newVersion.Major == oldVersion.Major) && (newVersion.Minor == oldVersion.Minor);

            if (windowsNewAnyTwUpdate || windowsOldTwMinorFixOnly)
            {
                var prompt = string.Format(CultureInfo.CurrentCulture, Resources.Messages.UpdateAvailable, updateModule.ComponentVersion);
                if (Utils.ShowMessageBox(prompt, Resources.Messages.TinyWallUpdater, TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogIcon.Warning) == DialogResult.Yes)
                    DownloadUpdate(updateModule);
            }
            else
            {
                var prompt = Resources.Messages.NoUpdateAvailable;
                Utils.ShowMessageBox(prompt, Resources.Messages.TinyWallUpdater, TaskDialogCommonButtons.Ok, TaskDialogIcon.Information);
            }
        }

        private void DownloadUpdate(UpdateModule mainModule)
        {
            _errorMsg = string.Empty;
            var dialogue = new TaskDialog
            {
                CustomMainIcon = Resources.Icons.firewall,
                WindowTitle = Resources.Messages.TinyWall,
                MainInstruction = Resources.Messages.TinyWallUpdater,
                Content = Resources.Messages.DownloadingUpdate,
                AllowDialogCancellation = false,
                CommonButtons = TaskDialogCommonButtons.Cancel,
                ShowProgressBar = true,
                Callback = DownloadTickCallback,
                CallbackData = this,
                CallbackTimer = true,
                EnableHyperlinks = true
            };

            _state = UpdaterState.DownloadingUpdate;

            var tmpFile = Path.GetTempFileName() + ".msi";
            var updateUrl = new Uri(mainModule.UpdateUrl!);
            using var httpClient = new WebClient();
            httpClient.DownloadFileCompleted += Updater_DownloadFinished;
            httpClient.DownloadProgressChanged += Updater_DownloadProgressChanged;
            httpClient.DownloadFileAsync(updateUrl, tmpFile, tmpFile);

            switch (dialogue.Show())
            {
                case (int)DialogResult.Cancel:
                    httpClient.CancelAsync();
                    break;
                case (int)DialogResult.OK:
                    InstallUpdate(tmpFile);
                    break;
                case (int)DialogResult.Abort:
                    Utils.ShowMessageBox(_errorMsg, Resources.Messages.TinyWall, TaskDialogCommonButtons.Ok, TaskDialogIcon.Error);
                    break;
            }
        }

        private static void InstallUpdate(string localFilePath)
        {
            Utils.StartProcess(localFilePath, string.Empty, false);
        }

        private void Updater_DownloadFinished(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled || (e.Error != null))
            {
                _errorMsg = Resources.Messages.DownloadInterrupted;
                return;
            }

            _state = UpdaterState.UpdateDownloadReady;
        }

        private void Updater_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            _downloadProgress = e.ProgressPercentage;
        }

        private bool DownloadTickCallback(ActiveTaskDialogue taskDialogue, TaskDialogueNotificationArgs args, object? callbackData)
        {
            switch (args.Notification)
            {
                case TaskDialogNotification.Created:
                    if (_state == UpdaterState.GettingDescriptor)
                        taskDialogue.SetProgressBarMarquee(true, 25);
                    break;
                case TaskDialogNotification.Timer:
                    if (!string.IsNullOrEmpty(_errorMsg))
                        taskDialogue.ClickButton((int)DialogResult.Abort);
                    switch (_state)
                    {
                        case UpdaterState.DescriptorReady:
                        case UpdaterState.UpdateDownloadReady:
                            taskDialogue.ClickButton((int)DialogResult.OK);
                            break;
                        case UpdaterState.DownloadingUpdate:
                            taskDialogue.SetProgressBarPosition(_downloadProgress);
                            break;
                    }
                    break;
            }
            return false;
        }
    }

    internal static class UpdateChecker
    {
        private const int UPDATER_VERSION = 6;
        private const string URL_UPDATE_DESCRIPTOR = @"https://tinywall.pados.hu/updates/UpdVer{0}/update.json";

        internal static UpdateDescriptor GetDescriptor()
        {
            var url = string.Format(CultureInfo.InvariantCulture, URL_UPDATE_DESCRIPTOR, UPDATER_VERSION);
            var tmpFile = Path.GetTempFileName();

            try
            {
                using (var httpClient = new WebClient())
                {
                    httpClient.Headers.Add("TW-Version", Application.ProductVersion);
                    httpClient.DownloadFile(url, tmpFile);
                }

                var descriptor = SerialisationHelper.DeserialiseFromFile(tmpFile, new UpdateDescriptor());

                return descriptor.MagicWord != "TinyWall Update Descriptor" ? throw new ApplicationException("Bad update descriptor file.") : descriptor;
            }
            finally
            {
                File.Delete(tmpFile);
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
