using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using ModernTinyWall.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using WinRT.Interop;

namespace ModernTinyWall;

public sealed partial class MainWindow
{
    private readonly ITrayIconService _trayIconService = new TrayIconService();
    private readonly IFirewallModeService _firewallModeService = new FirewallModeService();
    private readonly IControllerCommandService _controllerCommandService = new ControllerCommandService();
    private readonly IOptionsService _optionsService = new OptionsService();
    private readonly FirewallPromptMonitor _firewallPromptMonitor = App.Services.GetRequiredService<FirewallPromptMonitor>();
    private readonly ITinyWallExceptionStore _exceptionStore = App.Services.GetRequiredService<ITinyWallExceptionStore>();
    private readonly GlobalHotkeyService _globalHotkeyService = new();
    private readonly CancellationTokenSource _promptMonitoringCancellation = new();
    private readonly IntPtr _windowHandle;
    private readonly IntPtr _previousWindowProc;
    private readonly WindowProc _windowProc;
    private int _traySnapshotRefreshInProgress;
    private bool _isTerminating;

    public MainWindow()
    {
        InitializeComponent();
        _windowHandle = WindowNative.GetWindowHandle(this);
        _windowProc = WndProc;
        _previousWindowProc = SetWindowLongPtr(_windowHandle, GwlpWndproc, Marshal.GetFunctionPointerForDelegate(_windowProc));
        _trayIconService.CommandInvoked += TrayIconService_CommandInvoked;
        _trayIconService.Initialise(_windowHandle);
        _globalHotkeyService.HotkeyPressed += GlobalHotkeyService_HotkeyPressed;
        _globalHotkeyService.Initialise(_windowHandle);
        _trayIconService.SetStatus("ModernTinyWall migration shell");
        Closed += MainWindow_Closed;
        ContentFrame.Navigate(typeof(OverviewPage));
        _ = MonitorFirewallEventsAsync(_promptMonitoringCancellation.Token);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _promptMonitoringCancellation.Cancel();
        _trayIconService.CommandInvoked -= TrayIconService_CommandInvoked;
        _globalHotkeyService.HotkeyPressed -= GlobalHotkeyService_HotkeyPressed;
        _globalHotkeyService.Dispose();
        _trayIconService.Dispose();
        if (_previousWindowProc != IntPtr.Zero)
            _ = SetWindowLongPtr(_windowHandle, GwlpWndproc, _previousWindowProc);
    }

    private async Task MonitorFirewallEventsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var events = await _firewallPromptMonitor.GetNewBlockedOutboundEventsAsync(cancellationToken);
                foreach (var entry in events)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            await ShowFirewallPromptAsync(entry);
                            completion.TrySetResult();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Could not show outbound connection prompt: {ex}");
                            completion.TrySetException(ex);
                        }
                    }))
                    {
                        await completion.Task;
                    }
                    else
                    {
                        throw new InvalidOperationException("Could not dispatch the outbound connection prompt to the UI thread.");
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(750), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not monitor outbound firewall events: {ex}");
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }
    }

    private async Task ShowFirewallPromptAsync(FirewallLogEntry entry)
    {
        _ = ShowWindow(_windowHandle, SwShow);
        Activate();

        var executablePath = entry.AppPath ?? string.Empty;
        var applicationName = GetApplicationDisplayName(executablePath);
        var icon = new Image { Width = 48, Height = 48, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
        try
        {
            await TryLoadApplicationIconAsync(icon, executablePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not load application icon for '{executablePath}': {ex}");
        }

        var endpoint = entry.RemotePort > 0
            ? $"{(entry.RemoteIp?.Contains(':') == true ? $"[{entry.RemoteIp}]" : entry.RemoteIp)}:{entry.RemotePort}"
            : entry.RemoteIp ?? "Unknown destination";
        var applicationDetails = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        applicationDetails.Children.Add(icon);

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock
        {
            Text = $"{applicationName} attempted to connect",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        text.Children.Add(new TextBlock { Text = "TinyWall blocked this connection. The application may need to retry after you allow it." });
        text.Children.Add(new TextBlock { Text = executablePath, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = $"{entry.Protocol} to {endpoint}" });
        applicationDetails.Children.Add(text);

        var decision = FirewallPromptDecision.DenyOnce;
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(applicationDetails);
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Outbound connection blocked",
            Content = content,
            PrimaryButtonText = "Allow destination (5 min)",
            SecondaryButtonText = "Allow always",
            CloseButtonText = "Deny once",
            DefaultButton = ContentDialogButton.Close
        };
        var denyAlwaysButton = new Button
        {
            Content = "Deny always",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        denyAlwaysButton.Click += (_, _) =>
        {
            decision = FirewallPromptDecision.DenyAlways;
            dialog.Hide();
        };
        content.Children.Add(denyAlwaysButton);

        var result = await ShowDialogAsync(dialog);
        decision = result switch
        {
            ContentDialogResult.Primary => FirewallPromptDecision.AllowOnce,
            ContentDialogResult.Secondary => FirewallPromptDecision.AllowAlways,
            _ when decision == FirewallPromptDecision.DenyAlways => decision,
            _ => FirewallPromptDecision.DenyOnce
        };

        var mutationResult = await _exceptionStore.ApplyFirewallPromptDecisionAsync(entry, decision);
        _trayIconService.SetStatus(mutationResult.Message);
        if (!mutationResult.Success)
            await new DialogueService().ShowMessageAsync(Content.XamlRoot, "Connection decision", mutationResult.Message);
    }

    private static Task TryLoadApplicationIconAsync(Image image, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return Task.CompletedTask;

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fileOperation = StorageFile.GetFileFromPathAsync(executablePath);
        fileOperation.Completed = (fileResult, _) =>
        {
            try
            {
                var file = fileResult.GetResults();
                var thumbnailOperation = file.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
                thumbnailOperation.Completed = (thumbnailResult, _) =>
                {
                    try
                    {
                        var thumbnail = thumbnailResult.GetResults();
                        var bitmap = new BitmapImage();
                        var sourceOperation = bitmap.SetSourceAsync(thumbnail);
                        sourceOperation.Completed = (sourceResult, _) =>
                        {
                            try
                            {
                                sourceResult.GetResults();
                                if (!image.DispatcherQueue.TryEnqueue(() => image.Source = bitmap))
                                    Debug.WriteLine($"Could not display application icon for '{executablePath}'.");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Could not load application icon for '{executablePath}': {ex}");
                            }
                            finally
                            {
                                completion.TrySetResult();
                            }
                        };
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Could not load application icon for '{executablePath}': {ex}");
                        completion.TrySetResult();
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not load application icon for '{executablePath}': {ex}");
                completion.TrySetResult();
            }
        };

        return completion.Task;
    }

    private static string GetApplicationDisplayName(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            return "Unknown application";

        try
        {
            var fileDescription = FileVersionInfo.GetVersionInfo(executablePath).FileDescription;
            return string.IsNullOrWhiteSpace(fileDescription)
                ? Path.GetFileNameWithoutExtension(executablePath)
                : fileDescription;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read application details for '{executablePath}': {ex}");
            return Path.GetFileNameWithoutExtension(executablePath);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if ((message == WmClose && !_isTerminating && ShouldCloseToTray()) || (message == WmSyscommand && (wParam.ToInt64() & 0xFFF0) == ScMinimise && ShouldMinimiseToTray()))
        {
            HideWindow();
            return IntPtr.Zero;
        }

        if (_trayIconService.IsTrayCallbackMessage(message, lParam))
        {
            _ = ShowTrayContextMenuAsync();
            return IntPtr.Zero;
        }

        if (_trayIconService.HandleWindowMessage(message, wParam, lParam) || _globalHotkeyService.HandleWindowMessage(message, wParam))
            return IntPtr.Zero;

        return CallWindowProc(_previousWindowProc, hWnd, message, wParam, lParam);
    }

    private void GlobalHotkeyService_HotkeyPressed(object? sender, string commandId)
    {
        TrayIconService_CommandInvoked(sender, new TrayCommand(commandId, commandId));
    }

    private Task ShowTrayContextMenuAsync()
    {
        _trayIconService.ShowContextMenu(_controllerCommandService.GetTrayStateSnapshotAsync);
        return Task.CompletedTask;
    }

    private void RefreshTraySnapshotInBackground()
    {
        if (Interlocked.Exchange(ref _traySnapshotRefreshInProgress, 1) == 1)
            return;

        _ = RefreshTraySnapshotAsync();
    }

    private async Task RefreshTraySnapshotAsync()
    {
        try
        {
            _trayIconService.SetSnapshot(await _controllerCommandService.GetTrayStateSnapshotAsync());
        }
        catch
        {
            // Keep the previous snapshot if the service is unavailable.
        }
        finally
        {
            Interlocked.Exchange(ref _traySnapshotRefreshInProgress, 0);
        }
    }

    private async void TrayIconService_CommandInvoked(object? sender, TrayCommand command)
    {
        try
        {
            switch (command.Id)
            {
                case "overview":
                    ShowPage(typeof(OverviewPage));
                    break;
                case "settings":
                    ShowPage(typeof(SettingsPage));
                    break;
                case "connections":
                    ShowPage(typeof(ConnectionsPage));
                    break;
                case "processes":
                    ShowPage(typeof(ProcessesPage));
                    break;
                case "services":
                    ShowPage(typeof(ServicesPage));
                    break;
                case "packages":
                    ShowPage(typeof(PackagesPage));
                    break;
                case "exceptions":
                    ShowPage(typeof(ExceptionsPage));
                    break;
                case "options":
                    ShowPage(typeof(OptionsPage));
                    break;
                case "normal":
                    await SetFirewallModeAsync(Models.ModernFirewallMode.Normal);
                    break;
                case "allowOutgoing":
                    await SetFirewallModeAsync(Models.ModernFirewallMode.AllowOutgoing);
                    break;
                case "blockAll":
                    await SetFirewallModeAsync(Models.ModernFirewallMode.BlockAll);
                    break;
                case "disabled":
                    await SetFirewallModeAsync(Models.ModernFirewallMode.Disabled);
                    break;
                case "learning":
                    await SetFirewallModeAsync(Models.ModernFirewallMode.Learning);
                    break;
                case "lock":
                    await SetCommandStatusAsync(_controllerCommandService.LockAsync());
                    break;
                case "unlock":
                    await UnlockAsync();
                    break;
                case "elevate":
                    await SetCommandStatusAsync(_controllerCommandService.ElevateAsync());
                    break;
                case "allowLocalSubnet":
                    await SetCommandStatusAsync(_controllerCommandService.ToggleAllowLocalSubnetAsync());
                    break;
                case "hostsBlocklist":
                    await SetCommandStatusAsync(_controllerCommandService.ToggleHostsBlocklistAsync());
                    break;
                case "whitelistWindow":
                    await SetCommandStatusAsync(_controllerCommandService.WhitelistWindowUnderCursorAsync());
                    break;
                case "exit":
                    TerminateApplication();
                    break;
            }
        }
        catch
        {
            // ignored
        }
    }

    private void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        var pageType = tag switch
        {
            "DataUsage" => typeof(DataUsagePage),
            "Settings" => typeof(SettingsPage),
            "Connections" => typeof(ConnectionsPage),
            "Processes" => typeof(ProcessesPage),
            "Services" => typeof(ServicesPage),
            "Exceptions" => typeof(ExceptionsPage),
            "Packages" => typeof(PackagesPage),
            "Options" => typeof(OptionsPage),
            "About" => typeof(AboutPage),
            _ => typeof(OverviewPage)
        };

        if (ContentFrame.CurrentSourcePageType != pageType)
            NavigateTo(pageType);
    }

    private void NavigateTo(Type pageType)
    {
        if (ContentFrame.CurrentSourcePageType != pageType)
            ContentFrame.Navigate(pageType);
    }

    private void ShowPage(Type pageType)
    {
        NavigateTo(pageType);
        Activate();
    }

    public void TerminateApplication()
    {
        _isTerminating = true;
        Close();
    }

    private async Task SetFirewallModeAsync(Models.ModernFirewallMode mode)
    {
        var result = await _firewallModeService.SetModeAsync(mode);
        _trayIconService.SetStatus(result.Message);
        RefreshTraySnapshotInBackground();
        NavigateTo(typeof(OverviewPage));
    }

    private async Task UnlockAsync()
    {
        var passwordBox = new PasswordBox { Header = "Password" };
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Unlock TinyWall",
            Content = passwordBox,
            PrimaryButtonText = "Unlock",
            CloseButtonText = "Cancel"
        };

        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
            await SetCommandStatusAsync(_controllerCommandService.UnlockAsync(passwordBox.Password));
    }

    private async Task SetCommandStatusAsync(Task<CommandResult> commandTask)
    {
        var result = await commandTask;
        _trayIconService.SetStatus(result.Message);
        RefreshTraySnapshotInBackground();
    }

    private async Task<ModernTinyWallOptions> LoadOptionsAsync()
    {
        try
        {
            return await _optionsService.LoadAsync();
        }
        catch
        {
            return new ModernTinyWallOptions();
        }
    }

    private static Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        var completion = new TaskCompletionSource<ContentDialogResult>();
        var operation = dialog.ShowAsync();
        operation.Completed = (asyncInfo, _) =>
        {
            try
            {
                completion.TrySetResult(asyncInfo.GetResults());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        };

        return completion.Task;
    }

    private bool ShouldMinimiseToTray()
    {
        return LoadOptions().MinimiseToTray;
    }

    private bool ShouldCloseToTray()
    {
        return LoadOptions().CloseToTray;
    }

    private ModernTinyWallOptions LoadOptions()
    {
        try
        {
            return LoadOptionsAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return new ModernTinyWallOptions();
        }
    }

    private void HideWindow()
    {
        _ = ShowWindow(_windowHandle, SwHide);
    }

    private const int GwlpWndproc = -4;
    private const uint WmClose = 0x0010;
    private const uint WmSyscommand = 0x0112;
    private const int ScMinimise = 0xF020;
    private const int SwHide = 0;
    private const int SwShow = 5;

    private delegate IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    private static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);
}