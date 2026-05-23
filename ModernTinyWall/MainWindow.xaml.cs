using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.Views;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinRT.Interop;

namespace ModernTinyWall;

public sealed partial class MainWindow : Window
{
    private readonly ITrayIconService _trayIconService = new TrayIconService();
    private readonly IFirewallModeService _firewallModeService = new FirewallModeService();
    private readonly IControllerCommandService _controllerCommandService = new ControllerCommandService();
    private readonly GlobalHotkeyService _globalHotkeyService = new();
    private readonly IntPtr _windowHandle;
    private readonly WindowProc _windowProc;
    private IntPtr _previousWindowProc;

    public MainWindow()
    {
        InitializeComponent();
        _windowHandle = WindowNative.GetWindowHandle(this);
        _windowProc = WndProc;
        _previousWindowProc = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_windowProc));
        _trayIconService.CommandInvoked += TrayIconService_CommandInvoked;
        _trayIconService.Initialise(_windowHandle);
        _globalHotkeyService.HotkeyPressed += GlobalHotkeyService_HotkeyPressed;
        _globalHotkeyService.Initialise(_windowHandle);
        _trayIconService.SetStatus("ModernTinyWall migration shell");
        Closed += MainWindow_Closed;
        ContentFrame.Navigate(typeof(OverviewPage));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIconService.CommandInvoked -= TrayIconService_CommandInvoked;
        _globalHotkeyService.HotkeyPressed -= GlobalHotkeyService_HotkeyPressed;
        _globalHotkeyService.Dispose();
        _trayIconService.Dispose();
        if (_previousWindowProc != IntPtr.Zero)
            _ = SetWindowLongPtr(_windowHandle, GWLP_WNDPROC, _previousWindowProc);
    }

    private IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (_trayIconService.IsTrayCallbackMessage(message, lParam))
        {
            _ = ShowTrayContextMenuAsync();
            return IntPtr.Zero;
        }

        if (_trayIconService.HandleWindowMessage(message, wParam, lParam))
            return IntPtr.Zero;

        if (_globalHotkeyService.HandleWindowMessage(message, wParam))
            return IntPtr.Zero;

        return CallWindowProc(_previousWindowProc, hWnd, message, wParam, lParam);
    }

    private void GlobalHotkeyService_HotkeyPressed(object? sender, string commandId)
    {
        TrayIconService_CommandInvoked(sender, new TrayCommand(commandId, commandId));
    }

    private async Task ShowTrayContextMenuAsync()
    {
        _trayIconService.SetSnapshot(await _controllerCommandService.GetTrayStateSnapshotAsync());
        _trayIconService.ShowContextMenu();
    }

    private async void TrayIconService_CommandInvoked(object? sender, TrayCommand command)
    {
        switch (command.Id)
        {
            case "overview":
                NavigateTo(typeof(OverviewPage));
                break;
            case "settings":
                NavigateTo(typeof(SettingsPage));
                break;
            case "connections":
                NavigateTo(typeof(ConnectionsPage));
                break;
            case "processes":
                NavigateTo(typeof(ProcessesPage));
                break;
            case "services":
                NavigateTo(typeof(ServicesPage));
                break;
            case "packages":
                NavigateTo(typeof(PackagesPage));
                break;
            case "exceptions":
                NavigateTo(typeof(ExceptionsPage));
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
                Close();
                break;
        }
    }

    private void ShellNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        var pageType = tag switch
        {
            "Settings" => typeof(SettingsPage),
            "Connections" => typeof(ConnectionsPage),
            "Processes" => typeof(ProcessesPage),
            "Services" => typeof(ServicesPage),
            "Exceptions" => typeof(ExceptionsPage),
            "Packages" => typeof(PackagesPage),
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

    private async Task SetFirewallModeAsync(Models.ModernFirewallMode mode)
    {
        var result = await _firewallModeService.SetModeAsync(mode);
        _trayIconService.SetStatus(result.Message);
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

    private const int GWLP_WNDPROC = -4;

    private delegate IntPtr WindowProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    private static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}