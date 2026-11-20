using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using ModernTinyWall.Views;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.ViewManagement;
using Windows.System;
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
    private readonly ConnectionHistoryService _connectionHistoryService = App.Services.GetRequiredService<ConnectionHistoryService>();
    private readonly GlobalHotkeyService _globalHotkeyService = new();
    private readonly FirewallPromptBacklog _firewallPromptBacklog = new();
    private readonly CancellationTokenSource _promptMonitoringCancellation = new();
    private readonly Channel<FirewallLogEntry> _promptQueue = Channel.CreateBounded<FirewallLogEntry>(
        new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
    private readonly ConcurrentDictionary<ulong, byte> _queuedPending = new();
    private readonly IntPtr _windowHandle;
    private readonly IntPtr _previousWindowProc;
    private readonly WindowProc _windowProc;
    private int _traySnapshotRefreshInProgress;
    private bool _isTerminating;
    private bool _isWindowVisible = true;
    private readonly UISettings _uiSettings = new();
    private Type? _pendingPageType;
    private bool _isNavigating;
    private bool _hasCheckedOnboarding;
    private CancellationTokenSource? _navigationFadeCancellation;
    private readonly DispatcherTimer _protectionModeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private FirewallMode? _displayedProtectionMode = FirewallMode.Unknown;
    private int _protectionModeRefreshInProgress;

    internal event Action<bool>? WindowVisibilityChanged;

    internal bool IsWindowVisible => _isWindowVisible;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowDragRegion);
        ShellNavigation.ActualThemeChanged += ShellNavigation_ActualThemeChanged;
        _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;
        ApplySystemTheme();
        _windowHandle = WindowNative.GetWindowHandle(this);
        AppWindow.SetIcon(ProtectionModeIcon.GetPath(FirewallMode.Unknown));
        SetSidebarProtectionIcon(FirewallMode.Unknown);
        CentreOnScreen();
        ShellRoot.Loaded += ShellRoot_Loaded;
        _windowProc = WndProc;
        _previousWindowProc = SetWindowLongPtr(_windowHandle, GwlpWndproc, Marshal.GetFunctionPointerForDelegate(_windowProc));
        _trayIconService.CommandInvoked += TrayIconService_CommandInvoked;
        _trayIconService.Initialise(_windowHandle);
        _connectionHistoryService.SetWindowVisible(_isWindowVisible);
        _ = ConfigureConnectionHistoryAsync();
        var paletteAccelerator = new KeyboardAccelerator
        {
            Key = VirtualKey.K,
            Modifiers = VirtualKeyModifiers.Control
        };
        paletteAccelerator.Invoked += (sender, args) =>
        {
            args.Handled = true;
            _ = ShowCommandPaletteAsync();
        };
        ShellRoot.KeyboardAccelerators.Add(paletteAccelerator);
        _protectionModeTimer.Tick += ProtectionModeTimer_Tick;
        _protectionModeTimer.Start();
        RefreshProtectionModeInBackground();
        _globalHotkeyService.HotkeyPressed += GlobalHotkeyService_HotkeyPressed;
        _globalHotkeyService.Initialise(_windowHandle);
        _trayIconService.SetStatus("Firewall controls ready");
        Closed += MainWindow_Closed;
        ContentFrame.Navigate(typeof(OverviewPage));
        _ = MonitorFirewallEventsAsync(_promptMonitoringCancellation.Token);
        _ = ConsumeFirewallPromptsAsync(_promptMonitoringCancellation.Token);
    }

    private async void ShellRoot_Loaded(object sender, RoutedEventArgs args)
    {
        ShellRoot.Loaded -= ShellRoot_Loaded;
        // Reapply once the first show has established the monitor's DPI.
        CentreOnScreen();
        await ShowOnboardingIfNeededAsync();
        if (App.PreviewOptions.BlockedConnectionDialog)
        {
            _ = ShowFirewallPromptAsync(new FirewallLogEntry
            {
                AppPath = Environment.ProcessPath,
                Protocol = Protocol.TCP,
                RemoteIp = "203.0.113.10",
                RemotePort = 443
            });
        }
    }

    private void CentreOnScreen()
    {
        var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        AppWindow.MoveAndResize(WindowPlacement.Centre(display.WorkArea,
            new global::Windows.Graphics.SizeInt32(App.PreviewOptions.Width ?? 1536, App.PreviewOptions.Height ?? 1024)));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _isTerminating = true;
        _connectionHistoryService.SetWindowVisible(false);
        _protectionModeTimer.Stop();
        _protectionModeTimer.Tick -= ProtectionModeTimer_Tick;
        ShellNavigation.ActualThemeChanged -= ShellNavigation_ActualThemeChanged;
        _uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
        _navigationFadeCancellation?.Cancel();
        _promptMonitoringCancellation.Cancel();
        _trayIconService.CommandInvoked -= TrayIconService_CommandInvoked;
        _globalHotkeyService.HotkeyPressed -= GlobalHotkeyService_HotkeyPressed;
        _globalHotkeyService.Dispose();
        _trayIconService.Dispose();
        ShellRoot.KeyboardAccelerators.Clear();
        if (_previousWindowProc != IntPtr.Zero)
            _ = SetWindowLongPtr(_windowHandle, GwlpWndproc, _previousWindowProc);
    }

    private void ShellNavigation_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateTitleBarTheme();
    }

    private void UiSettings_ColorValuesChanged(UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isTerminating)
                ApplySystemTheme();
        });
    }

    private void ApplySystemTheme()
    {
        var background = _uiSettings.GetColorValue(UIColorType.Background);
        ShellRoot.RequestedTheme = new AccessibilitySettings().HighContrast
            ? ElementTheme.Default
            : App.PreviewOptions.Theme switch
            {
                PreviewTheme.Light => ElementTheme.Light,
                PreviewTheme.Dark => ElementTheme.Dark,
                _ => background.R + background.G + background.B >= 384 ? ElementTheme.Light : ElementTheme.Dark
            };
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
            return;

        var highContrast = new AccessibilitySettings().HighContrast;
        var foreground = highContrast ? _uiSettings.GetColorValue(UIColorType.Foreground)
            : ShellNavigation.ActualTheme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        var titleBar = AppWindow.TitleBar;
        titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
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
                    if (entry.PendingConnectionId != 0 && !_queuedPending.TryAdd(entry.PendingConnectionId, 0))
                        continue;
                    if (!_promptQueue.Writer.TryWrite(entry))
                    {
                        if (entry.PendingConnectionId != 0)
                        {
                            _queuedPending.TryRemove(entry.PendingConnectionId, out _);
                            _firewallPromptMonitor.ForgetPending(entry.PendingConnectionId);
                        }
                        _trayIconService.SetStatus("Connection prompt queue is full. Unanswered connections remain blocked.");
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
                _firewallPromptMonitor.ClearPending();
                _trayIconService.SetStatus("Could not monitor outbound connection prompts. Connections remain blocked.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    private async Task ConsumeFirewallPromptsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var entry in _promptQueue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    if (entry.PendingConnectionId != 0 && !_firewallPromptMonitor.IsPending(entry.PendingConnectionId))
                        continue;
                    if (_firewallPromptBacklog.IsObsolete(entry))
                        continue;
                    if (await ShowFirewallPromptAsync(entry) && entry.PendingConnectionId != 0)
                        _firewallPromptMonitor.ForgetPending(entry.PendingConnectionId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not show outbound connection prompt: {ex}");
                    if (entry.PendingConnectionId != 0)
                    {
                        try
                        {
                            var denied = await _firewallPromptMonitor.ResolvePendingAsync(entry.PendingConnectionId, false);
                            if (!denied.Success)
                                Debug.WriteLine(denied.Message);
                        }
                        catch (Exception resolutionError)
                        {
                            Debug.WriteLine($"Could not release the pending connection: {resolutionError}");
                        }
                    }
                    _trayIconService.SetStatus("Could not complete the outbound connection prompt. The connection status may be unknown.");
                }
                finally
                {
                    if (entry.PendingConnectionId != 0)
                        _queuedPending.TryRemove(entry.PendingConnectionId, out _);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> ShowFirewallPromptAsync(FirewallLogEntry entry)
    {
        _ = ShowWindow(_windowHandle, SwShow);
        Activate();
        SetWindowVisibility(true);

        var executablePath = entry.AppPath ?? string.Empty;
        var isPending = entry.PendingConnectionId != 0;
        ApplicationDetails details;
        try
        {
            details = await Task.Run(() => ApplicationDetails.Read(executablePath))
                .WaitAsync(TimeSpan.FromSeconds(1), _promptMonitoringCancellation.Token);
        }
        catch (TimeoutException)
        {
            Debug.WriteLine($"Timed out reading application details for '{executablePath}'.");
            details = ApplicationDetails.FromMetadata(executablePath, null, null, null);
        }
        var icon = new Image { Width = 48, Height = 48, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
        try
        {
            _ = TryLoadApplicationIconAsync(icon, executablePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not load application icon for '{executablePath}': {ex}");
        }

        var endpoint = entry.RemotePort > 0
            ? $"{(entry.RemoteIp?.Contains(':') == true ? $"[{entry.RemoteIp}]" : entry.RemoteIp)}:{entry.RemotePort}"
            : entry.RemoteIp ?? "Unknown destination";
        var applicationDetails = new Grid { ColumnSpacing = 12 };
        applicationDetails.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        applicationDetails.ColumnDefinitions.Add(new ColumnDefinition());
        applicationDetails.Children.Add(icon);

        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(new TextBlock
        {
            Text = isPending ? $"{details.Name} wants to connect" : $"{details.Name} attempted to connect",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        text.Children.Add(new TextBlock
        {
            Text = isPending
                ? "Modern TinyWall is holding this connection for your decision. No response within two minutes denies it."
                : "Modern TinyWall blocked this connection. The application may need to retry after you allow it.",
            TextWrapping = TextWrapping.Wrap
        });
        text.Children.Add(new TextBlock { Text = $"Application: {details.Name}", TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = $"Publisher (unverified file metadata): {details.Publisher}", TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = $"Description (file metadata): {details.Description}", TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = "File details may not identify an individual hosted service.", TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = executablePath, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = $"{entry.Protocol} to {endpoint}", TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(text, 1);
        applicationDetails.Children.Add(text);

        var decision = FirewallPromptDecision.DenyOnce;
        var temporaryDuration = AppExceptionTimer.FOR_5_MINUTES;
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new ScrollViewer
        {
            Content = applicationDetails,
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = isPending ? "Outbound connection awaiting permission" : "Outbound connection blocked",
            Content = content,
            CloseButtonText = "Deny once",
            DefaultButton = ContentDialogButton.Close
        };
        const string denyOnceTip = "Deny this connection without saving a rule. The application may prompt again.";
        var closeStyle = new Style(typeof(Button));
        closeStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, denyOnceTip));
        closeStyle.Setters.Add(new Setter(AutomationProperties.HelpTextProperty, denyOnceTip));
        if (!new AccessibilitySettings().HighContrast)
        {
            closeStyle.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 164, 38, 44))));
            closeStyle.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Microsoft.UI.Colors.White)));
        }
        dialog.CloseButtonStyle = closeStyle;

        var buttons = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.ColumnDefinitions.Add(new ColumnDefinition());
        buttons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        buttons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        void AddDecisionButton(string label, string tooltip, FirewallPromptDecision choice, int row, int column, SolidColorBrush colour)
        {
            var button = new Button
            {
                Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                MinHeight = 44
            };
            if (!new AccessibilitySettings().HighContrast)
            {
                button.Background = colour;
                button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            }
            ToolTipService.SetToolTip(button, tooltip);
            AutomationProperties.SetHelpText(button, tooltip);
            button.Click += (_, _) => { decision = choice; dialog.Hide(); };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            buttons.Children.Add(button);
        }

        var green = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 11, 106, 57));
        var blue = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0, 90, 158));
        var red = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 164, 38, 44));
        var temporaryDurationPicker = new ComboBox
        {
            Header = "Temporary allowance duration",
            ItemsSource = new[]
            {
                new TemporaryDurationOption("5 minutes", AppExceptionTimer.FOR_5_MINUTES),
                new TemporaryDurationOption("30 minutes", AppExceptionTimer.FOR_30_MINUTES),
                new TemporaryDurationOption("1 hour", AppExceptionTimer.FOR_1_HOUR),
                new TemporaryDurationOption("4 hours", AppExceptionTimer.FOR_4_HOURS),
                new TemporaryDurationOption("9 hours", AppExceptionTimer.FOR_9_HOURS),
                new TemporaryDurationOption("24 hours", AppExceptionTimer.FOR_24_HOURS),
                new TemporaryDurationOption("Until restart", AppExceptionTimer.UNTIL_REBOOT)
            },
            DisplayMemberPath = nameof(TemporaryDurationOption.Label),
            SelectedIndex = 0,
            MinWidth = 240
        };
        AutomationProperties.SetName(temporaryDurationPicker, "Temporary allowance duration");
        content.Children.Add(temporaryDurationPicker);
        AddDecisionButton("Allow all outbound", "Always allow this executable to make outbound connections to any IP address or port. This affects every process using it and replaces its application-wide block. Other block rules may still apply.",
            FirewallPromptDecision.AllowAlways, 0, 0, green);
        AddDecisionButton("Deny all traffic", "Always block inbound and outbound traffic for this executable.",
            FirewallPromptDecision.DenyAlways, 0, 1, red);
        AddDecisionButton("Allow IP and port temporarily", "Allow this executable to connect to the displayed IP address and port using this protocol for the selected duration.",
            FirewallPromptDecision.AllowOnce, 1, 0, blue);
        AddDecisionButton("Allow IP and port always", "Always allow this executable to connect to the displayed IP address and port using this protocol. This replaces its application-wide block; other block rules may still apply.",
            FirewallPromptDecision.AllowDestinationAlways, 1, 1, blue);
        content.Children.Add(buttons);

        var expired = false;
        var expiryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        expiryTimer.Tick += (_, _) =>
        {
            if (_promptMonitoringCancellation.IsCancellationRequested
                || (isPending && !_firewallPromptMonitor.IsPending(entry.PendingConnectionId)))
            {
                expired = true;
                dialog.Hide();
            }
        };
        expiryTimer.Start();
        try
        {
            _ = await ShowDialogAsync(dialog);
        }
        finally
        {
            expiryTimer.Stop();
        }
        if (decision == FirewallPromptDecision.AllowOnce
            && temporaryDurationPicker.SelectedItem is TemporaryDurationOption selectedDuration)
            temporaryDuration = selectedDuration.Duration;
        if (App.PreviewOptions.BlockedConnectionDialog)
            return true;
        if (expired)
        {
            _trayIconService.SetStatus("Pending connection expired or was withdrawn. No rule was saved.");
            return true;
        }
        var allow = decision is FirewallPromptDecision.AllowOnce or FirewallPromptDecision.AllowAlways or FirewallPromptDecision.AllowDestinationAlways;
        if (isPending && allow)
        {
            if (!FirewallPromptValidation.TryGetDestination(entry, out _, out _))
            {
                var invalid = await _exceptionStore.ApplyFirewallPromptDecisionAsync(entry, decision, temporaryDuration);
                var denial = await _firewallPromptMonitor.ResolvePendingAsync(entry.PendingConnectionId, false);
                var message = denial.Success ? invalid.Message : $"{invalid.Message} {denial.Message}";
                _trayIconService.SetStatus(message);
                await new DialogueService().ShowMessageAsync(Content.XamlRoot, "Connection decision", message);
                return false;
            }
            var resolution = await _firewallPromptMonitor.ResolvePendingAsync(entry.PendingConnectionId, true);
            if (!resolution.Success)
            {
                _trayIconService.SetStatus(resolution.Message);
                await new DialogueService().ShowMessageAsync(Content.XamlRoot, "Connection decision", resolution.Message);
                return false;
            }
        }
        var mutationResult = await _exceptionStore.ApplyFirewallPromptDecisionAsync(entry, decision, temporaryDuration);
        if (mutationResult.Success && decision == FirewallPromptDecision.AllowAlways)
            _firewallPromptBacklog.MarkApplicationAllowed(executablePath, DateTime.Now);
        if (isPending && !allow)
        {
            var resolution = await _firewallPromptMonitor.ResolvePendingAsync(entry.PendingConnectionId, false);
            if (mutationResult.Success && !resolution.Success)
                mutationResult = resolution;
        }
        else if (isPending && !mutationResult.Success)
            mutationResult = new ExceptionMutationResult(false, $"The connection decision was submitted, but the rule could not be saved: {mutationResult.Message}");
        _trayIconService.SetStatus(mutationResult.Message);
        if (!mutationResult.Success)
            await new DialogueService().ShowMessageAsync(Content.XamlRoot, "Connection decision", mutationResult.Message);
        return false;
    }

    private static Task TryLoadApplicationIconAsync(Image image, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
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

    private void ProtectionModeTimer_Tick(object? sender, object args)
    {
        RefreshProtectionModeInBackground();
    }

    private void RefreshProtectionModeInBackground()
    {
        if (!_isTerminating && Interlocked.Exchange(ref _protectionModeRefreshInProgress, 1) == 0)
            _ = RefreshProtectionModeAsync();
    }

    private async Task RefreshProtectionModeAsync()
    {
        var queued = false;
        try
        {
            FirewallMode mode;
            try
            {
                mode = await _controllerCommandService.GetProtectionModeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not read the protection mode: {ex}");
                mode = FirewallMode.Unknown;
            }
            queued = DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (!_isTerminating && mode != _displayedProtectionMode)
                    {
                        _displayedProtectionMode = null;
                        _trayIconService.SetProtectionMode(mode);
                        AppWindow.SetIcon(ProtectionModeIcon.GetPath(mode));
                        SetSidebarProtectionIcon(mode);
                        _displayedProtectionMode = mode;
                    }

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not update the protection mode icons: {ex}");
                }
                finally
                {
                    Interlocked.Exchange(ref _protectionModeRefreshInProgress, 0);
                }
            });
            if (!queued)
                Debug.WriteLine("Could not dispatch the protection mode icon update.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not dispatch the protection mode icon update: {ex}");
        }
        finally
        {
            if (!queued)
                Interlocked.Exchange(ref _protectionModeRefreshInProgress, 0);
        }
    }

    private void SetSidebarProtectionIcon(FirewallMode mode)
    {
        SidebarProtectionIcon.Source = new BitmapImage(new Uri(ProtectionModeIcon.GetImagePath(mode), UriKind.Absolute));
        var description = mode switch
        {
            FirewallMode.Normal => "normal",
            FirewallMode.AllowOutgoing => "allow outgoing",
            FirewallMode.BlockAll => "block all",
            FirewallMode.Learning => "learning",
            FirewallMode.Disabled => "disabled",
            _ => "unavailable"
        };
        AutomationProperties.SetName(SidebarProtectionIcon, $"Modern TinyWall, protection mode {description}");
    }

    private async Task RefreshTraySnapshotAsync()
    {
        try
        {
            _trayIconService.SetSnapshot(await _controllerCommandService.GetTrayStateSnapshotAsync());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not refresh tray status: {ex}");
            _trayIconService.SetStatus($"Firewall service unavailable: {ex.Message}");
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
                case "activityHistory":
                    ShowPage(typeof(ConnectionHistoryPage));
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
        catch (Exception ex)
        {
            Debug.WriteLine($"Tray action '{command.Id}' failed: {ex}");
            _trayIconService.SetStatus($"Action failed: {ex.Message}");
        }
    }

    private void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
            return;

        NavigateTo(GetPageType(tag));
    }

    private void CommandPaletteButton_Click(object sender, RoutedEventArgs e) => _ = ShowCommandPaletteAsync();

    private sealed record TemporaryDurationOption(string Label, AppExceptionTimer Duration);

    internal void ShowConnections()
    {
        ShellNavigation.SelectedItem = ShellNavigation.MenuItems.OfType<NavigationViewItem>()
            .First(item => item.Tag is "Connections");
    }

    private static Type GetPageType(string tag)
    {
        return tag switch
        {
            "DataUsage" => typeof(DataUsagePage),
            "Settings" => typeof(SettingsPage),
            "Connections" => typeof(ConnectionsPage),
            "ActivityHistory" => typeof(ConnectionHistoryPage),
            "Processes" => typeof(ProcessesPage),
            "Services" => typeof(ServicesPage),
            "Exceptions" => typeof(ExceptionsPage),
            "Packages" => typeof(PackagesPage),
            "Options" => typeof(OptionsPage),
            "About" => typeof(AboutPage),
            _ => typeof(OverviewPage)
        };
    }

    private async Task ConfigureConnectionHistoryAsync()
    {
        try
        {
            var options = await _optionsService.LoadAsync().ConfigureAwait(false);
            _connectionHistoryService.Configure(options.CollectConnectionHistory, options.ConnectionHistoryRetentionDays);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not configure local connection history: {ex}");
            _trayIconService.SetStatus($"Could not load history settings: {ex.Message}");
        }
    }

    private async Task ShowOnboardingIfNeededAsync()
    {
        if (!_isWindowVisible || _hasCheckedOnboarding)
            return;
        _hasCheckedOnboarding = true;
        try
        {
            if (!(await _optionsService.LoadAsync()).OnboardingComplete)
                await ShowGettingStartedAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not read the first-run guide setting: {ex}");
            _trayIconService.SetStatus($"Getting started guide could not be loaded: {ex.Message}");
        }
    }

    internal async Task ShowGettingStartedAsync()
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Modern TinyWall controls the TinyWall firewall service. Protection is shown as active only when the service confirms its state.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = "Use Overview to check protection, Connections to review observed activity, and Exceptions to inspect or change application rules. Allowing an application can grant network access beyond the connection that prompted you.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = "Connection history is optional, stored locally, and pauses while this window is hidden. Modern TinyWall does not install or start the firewall service for you.",
            TextWrapping = TextWrapping.Wrap
        });
        AutomationProperties.SetName(content, "Modern TinyWall getting started guide");
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Getting started with Modern TinyWall",
            Content = content,
            PrimaryButtonText = "Review application rules",
            SecondaryButtonText = "Open Overview",
            CloseButtonText = "Later",
            DefaultButton = ContentDialogButton.Secondary
        };
        var result = await ShowDialogAsync(dialog);
        await _optionsService.UpdateAsync(options => options with { OnboardingComplete = true });
        if (result == ContentDialogResult.Primary)
            ShowPage(typeof(ExceptionsPage));
        else if (result == ContentDialogResult.Secondary)
            ShowPage(typeof(OverviewPage));
    }

    private async Task ShowCommandPaletteAsync()
    {
        var commands = new[]
        {
            new PaletteCommand("Overview", "Protection status and traffic", typeof(OverviewPage)),
            new PaletteCommand("Data usage", "Search application traffic totals", typeof(DataUsagePage)),
            new PaletteCommand("Connections", "Search active, listening and blocked connections", typeof(ConnectionsPage)),
            new PaletteCommand("Activity history", "Search local connection history", typeof(ConnectionHistoryPage)),
            new PaletteCommand("Processes", "Find running applications", typeof(ProcessesPage)),
            new PaletteCommand("Services", "Find Windows services", typeof(ServicesPage)),
            new PaletteCommand("Packages", "Find packaged applications", typeof(PackagesPage)),
            new PaletteCommand("Exceptions", "Search firewall rules and exceptions", typeof(ExceptionsPage)),
            new PaletteCommand("Settings", "Configure firewall and service options", typeof(SettingsPage)),
            new PaletteCommand("Options", "Configure Modern TinyWall and history", typeof(OptionsPage)),
            new PaletteCommand("Getting started", "Open the first-run guide", null)
        };
        var searchBox = new TextBox { PlaceholderText = "Search pages and actions" };
        AutomationProperties.SetName(searchBox, "Search pages and actions");
        var results = new ListView
        {
            MaxHeight = 360,
            SelectionMode = ListViewSelectionMode.Single,
            DisplayMemberPath = nameof(PaletteCommand.Label),
            ItemsSource = commands
        };
        results.SelectedIndex = 0;
        void FilterCommands()
        {
            var matches = commands.Where(command =>
                command.Label.Contains(searchBox.Text, StringComparison.CurrentCultureIgnoreCase)
                || command.Description.Contains(searchBox.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
            results.ItemsSource = matches;
            results.SelectedIndex = matches.Length > 0 ? 0 : -1;
        }
        searchBox.TextChanged += (_, _) => FilterCommands();
        searchBox.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Down && results.Items.Count > 0)
            {
                results.Focus(FocusState.Programmatic);
                args.Handled = true;
            }
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(searchBox);
        panel.Children.Add(results);
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Command palette",
            Content = panel,
            PrimaryButtonText = "Open",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary
        };
        var result = await ShowDialogAsync(dialog);
        if (result != ContentDialogResult.Primary || results.SelectedItem is not PaletteCommand selected)
            return;
        if (selected.PageType is null)
            await ShowGettingStartedAsync();
        else
            ShowPage(selected.PageType);
    }

    private sealed record PaletteCommand(string Label, string Description, Type? PageType);

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
        if (!_isWindowVisible || !App.PreviewOptions.AnimationsAllowed(_uiSettings.AnimationsEnabled,
                new AccessibilitySettings().HighContrast) || _isTerminating)
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
        RefreshProtectionModeInBackground();
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
        RefreshProtectionModeInBackground();
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

    internal void StartInTray() => HideWindow();

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
        _connectionHistoryService.SetWindowVisible(isVisible);
        if (isVisible && ShellRoot.IsLoaded)
            _ = ShowOnboardingIfNeededAsync();
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
