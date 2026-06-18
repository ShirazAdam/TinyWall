using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class OptionsPage : Page
{
    private readonly IOptionsService _optionsService = new OptionsService();
    private bool _isLoading;

    public OptionsPage()
    {
        InitializeComponent();
        Loaded += OptionsPage_Loaded;
    }

    private async void OptionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        try
        {
            var options = await _optionsService.LoadAsync();
            MinimiseToTraySwitch.IsOn = options.MinimiseToTray;
            CloseToTraySwitch.IsOn = options.CloseToTray;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void OptionsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        await SaveOptionsAsync();
    }

    private void TerminateButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mainWindow)
            mainWindow.TerminateApplication();
    }

    private Task SaveOptionsAsync()
    {
        return _optionsService.SaveAsync(new ModernTinyWallOptions
        {
            MinimiseToTray = MinimiseToTraySwitch.IsOn,
            CloseToTray = CloseToTraySwitch.IsOn
        });
    }
}
