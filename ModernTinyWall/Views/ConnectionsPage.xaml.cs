using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.ViewModels;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ConnectionsPage : Page
{
    internal ConnectionsPageViewModel ViewModel { get; } = new();

    public ConnectionsPage()
    {
        InitializeComponent();
        Loaded += ConnectionsPage_Loaded;
    }

    private async void ConnectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
    }

    private Task RefreshAsync()
    {
        return ViewModel.RefreshAsync(
            ShowActiveCheckBox.IsChecked == true,
            ShowListeningCheckBox.IsChecked == true,
            ShowBlockedCheckBox.IsChecked == true,
            SearchBox.Text);
    }
}
