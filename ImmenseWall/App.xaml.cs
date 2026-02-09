using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ImmenseWall.ViewModels;
using ImmenseWall.Views;
using ImmenseWall.Models.Database;
using ImmenseWall.Models;
using ImmenseWall.Services;
using System.IO;

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
    public new static App Current => (App)System.Windows.Application.Current;

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
        services.AddSingleton(s => AppDatabase.Load());
        services.AddSingleton(s => 
        {
             var path = Path.Combine(Utils.AppDataPath, "config.json");
             // If config doesn't exist, CreateDefault? 
             // Logic from TinyWallService.LoadServerConfig:
             try 
             {
                 return ServerConfiguration.Load(path);
             }
             catch
             {
                 var config = new ServerConfiguration { ActiveProfileName = "Default" };
                 // Populate recommended exceptions if AppDatabase is available
                 var db = s.GetService<AppDatabase>();
                 if (db != null)
                 {
                     foreach (var app in db.KnownApplications.Where(a => a.HasFlag("TWUI:Special") && a.HasFlag("TWUI:Recommended")))
                     {
                         config.ActiveProfile.SpecialExceptions.Add(app.Name);
                     }
                 }
                 return config;
             }
        });
        
        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<StatusViewModel>();
        services.AddTransient<RulesViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ConnectionsViewModel>();

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

