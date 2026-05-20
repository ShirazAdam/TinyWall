using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.ViewModels;

namespace ModernTinyWall.Views;

public sealed partial class ConnectionsPage : Page
{
    internal ConnectionsPageViewModel ViewModel { get; } = new();

    public ConnectionsPage()
    {
        InitializeComponent();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync(ShowActiveCheckBox.IsChecked == true, ShowListeningCheckBox.IsChecked == true, ShowBlockedCheckBox.IsChecked == true, SearchBox.Text);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
    }
}
