using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.Views;
using WinRT.Interop;

namespace ModernTinyWall;

public sealed partial class MainWindow : Window
{
    private readonly ITrayIconService _trayIconService = new TrayIconService();

    public MainWindow()
    {
        InitializeComponent();
        _trayIconService.Initialise(WindowNative.GetWindowHandle(this));
        _trayIconService.SetStatus("ModernTinyWall migration shell");
        Closed += MainWindow_Closed;
        ContentFrame.Navigate(typeof(OverviewPage));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIconService.Dispose();
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
            ContentFrame.Navigate(pageType);
    }
}