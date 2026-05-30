using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Exceptions;
using System;

namespace ModernTinyWall;

public partial class App : Application
{
    private Window? _window;

    internal static Window? MainWindow { get; private set; }

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
        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
    }
}
