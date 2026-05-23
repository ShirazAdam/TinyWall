using Microsoft.UI.Xaml;

namespace ModernTinyWall;

public partial class App : Application
{
    private Window? _window;

    internal static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }
}
