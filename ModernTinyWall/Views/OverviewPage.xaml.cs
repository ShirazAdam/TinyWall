using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Models;
using ModernTinyWall.ViewModels;

namespace ModernTinyWall.Views;

public sealed partial class OverviewPage : Page
{
    internal OverviewPageViewModel ViewModel { get; } = new();

    public OverviewPage()
    {
        InitializeComponent();
    }

    private async void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: FirewallModeOption option })
        {
            await ViewModel.ApplyModeAsync(option);
        }
    }
}
