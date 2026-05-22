using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.Views;
using System;
using WinRT.Interop;

namespace ModernTinyWall;

public sealed partial class MainWindow : Window
{
    private readonly ITrayIconService _trayIconService = new TrayIconService();

    public MainWindow()
    {
        InitializeComponent();
        _trayIconService.CommandInvoked += TrayIconService_CommandInvoked;
        _trayIconService.Initialise(WindowNative.GetWindowHandle(this));
        _trayIconService.SetStatus("ModernTinyWall migration shell");
        Closed += MainWindow_Closed;
        ContentFrame.Navigate(typeof(OverviewPage));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIconService.CommandInvoked -= TrayIconService_CommandInvoked;
        _trayIconService.Dispose();
    }

    private void TrayIconService_CommandInvoked(object? sender, TrayCommand command)
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
            case "normal":
            case "allowOutgoing":
            case "blockAll":
            case "disabled":
            case "learning":
                NavigateTo(typeof(OverviewPage));
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
}