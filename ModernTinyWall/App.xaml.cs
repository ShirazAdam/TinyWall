using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Services;
using ModernTinyWall.Models;
using ModernTinyWall.Windows;
using System;

namespace ModernTinyWall;

public partial class App : Application
{
    private Window? _window;

    internal static Window? MainWindow { get; private set; }
    internal static UiPreviewOptions PreviewOptions { get; private set; } = new();

    internal static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        Services = ConfigureServices();
        InitializeComponent();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITinyWallExceptionStore, TinyWallExceptionStore>();
        services.AddSingleton<FirewallPromptMonitor>();
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        PreviewOptions = UiPreviewOptions.Parse(Environment.GetCommandLineArgs()[1..]);
        var window = new MainWindow();
        _window = window;
        MainWindow = window;
        if (ModernClientStartup.IsStartup(Environment.GetCommandLineArgs()[1..]))
            window.StartInTray();
        else
            window.Activate();
    }
}
