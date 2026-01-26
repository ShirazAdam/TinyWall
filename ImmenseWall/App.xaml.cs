using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ImmenseWall.ViewModels;
using ImmenseWall.Views;

namespace ImmenseWall;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : global::System.Windows.Application
{
    public App()
    {
        Services = ConfigureServices();
        this.InitializeComponent();
    }

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use
    /// </summary>
    public new static App Current => (App)Application.Current;

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Configures the services for the application.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Services
        
        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainView>();

        return services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mainView = Services.GetRequiredService<MainView>();
        mainView.Show();
    }
}

