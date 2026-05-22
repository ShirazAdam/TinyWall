using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.ViewModels;

namespace ModernTinyWall.Views;

public sealed partial class SettingsPage : Page
{
    internal SettingsPageViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyAsync();
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }
}
