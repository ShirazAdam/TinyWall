using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using ModernTinyWall.Views;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.ViewManagement;
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
    private bool _isWindowVisible = true;
    private readonly UISettings _uiSettings = new();
    private Type? _pendingPageType;
    private bool _isNavigating;
    private CancellationTokenSource? _navigationFadeCancellation;

    internal event Action<bool>? WindowVisibilityChanged;

    internal bool IsWindowVisible => _isWindowVisible;

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
        _trayIconService.SetStatus("Modern TinyWall firewall controls");
        Closed += MainWindow_Closed;
        ContentFrame.Navigate(typeof(OverviewPage));
        _ = MonitorFirewallEventsAsync(_promptMonitoringCancellation.Token);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _isTerminating = true;
        _navigationFadeCancellation?.Cancel();
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
        SetWindowVisibility(true);

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
        text.Children.Add(new TextBlock { Text = "Modern TinyWall blocked this connection. The application may need to retry after you allow it." });
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
        if (message == WmShowWindow)
            SetWindowVisibility(wParam != IntPtr.Zero);
        else if (message == WmSize)
            SetWindowVisibility(wParam.ToInt32() != SizeMinimized);

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
                case "dataUsage":
                    ShowPage(typeof(DataUsagePage));
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
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
            return;

        NavigateTo(GetPageType(tag));
    }

    private static Type GetPageType(string tag)
    {
        return tag switch
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
    }

    private async void NavigateTo(Type pageType)
    {
        if (_isTerminating || (!_isNavigating && ContentFrame.CurrentSourcePageType == pageType))
            return;

        _pendingPageType = pageType;
        if (_isNavigating)
            return;

        _isNavigating = true;
        try
        {
            while (_pendingPageType is not null && !_isTerminating)
            {
                SynchroniseNavigationSelection(_pendingPageType);
                if (ContentFrame.CurrentSourcePageType == _pendingPageType)
                {
                    _pendingPageType = null;
                    continue;
                }

                if (ContentFrame.Content is not null)
                    await FadeContentAsync(0, TimeSpan.FromMilliseconds(120));
                if (_isTerminating)
                    return;

                // Use the latest selection if another item was clicked during the fade.
                if (_pendingPageType is not { } nextPageType)
                    return;
                _pendingPageType = null;
                if (ContentFrame.CurrentSourcePageType != nextPageType
                    && !ContentFrame.Navigate(nextPageType, null, new SuppressNavigationTransitionInfo()))
                {
                    throw new InvalidOperationException($"Could not navigate to {nextPageType.Name}.");
                }

                await FadeContentAsync(1, TimeSpan.FromMilliseconds(180));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not change the current view: {ex}");
            _trayIconService.SetStatus("Could not change the current view.");
            if (ContentFrame.CurrentSourcePageType is { } currentPageType)
                SynchroniseNavigationSelection(currentPageType);
        }
        finally
        {
            ContentFrame.Opacity = 1;
            _pendingPageType = null;
            _isNavigating = false;
        }
    }

    private void SynchroniseNavigationSelection(Type pageType)
    {
        foreach (var entry in ShellNavigation.MenuItems.Concat(ShellNavigation.FooterMenuItems))
        {
            if (entry is NavigationViewItem { Tag: string tag } item && GetPageType(tag) == pageType)
            {
                ShellNavigation.SelectedItem = item;
                return;
            }
        }
    }

    private async Task FadeContentAsync(double opacity, TimeSpan duration)
    {
        if (!_isWindowVisible || !_uiSettings.AnimationsEnabled || _isTerminating)
        {
            ContentFrame.Opacity = opacity;
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _navigationFadeCancellation = cancellation;
        var animation = new DoubleAnimation
        {
            From = ContentFrame.Opacity,
            To = opacity,
            Duration = new Duration(duration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(animation, ContentFrame);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnCompleted(object? sender, object args) => completion.TrySetResult();
        storyboard.Completed += OnCompleted;
        using var registration = cancellation.Token.Register(() => completion.TrySetCanceled(cancellation.Token));
        try
        {
            storyboard.Begin();
            await completion.Task;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Hiding or closing the window completes navigation without further animation.
        }
        finally
        {
            storyboard.Completed -= OnCompleted;
            storyboard.Stop();
            ContentFrame.Opacity = opacity;
            _navigationFadeCancellation = null;
        }
    }

    private void ShowPage(Type pageType)
    {
        NavigateTo(pageType);
        Activate();
        SetWindowVisibility(true);
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
            Title = "Unlock Modern TinyWall",
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
            return await _optionsService.LoadAsync().ConfigureAwait(false);
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
        SetWindowVisibility(false);
    }

    private void SetWindowVisibility(bool isVisible)
    {
        if (_isWindowVisible == isVisible)
            return;

        _isWindowVisible = isVisible;
        if (!isVisible)
            _navigationFadeCancellation?.Cancel();
        WindowVisibilityChanged?.Invoke(isVisible);
    }

    private const int GwlpWndproc = -4;
    private const uint WmClose = 0x0010;
    private const uint WmSize = 0x0005;
    private const uint WmShowWindow = 0x0018;
    private const uint WmSyscommand = 0x0112;
    private const int ScMinimise = 0xF020;
    private const int SizeMinimized = 1;
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
