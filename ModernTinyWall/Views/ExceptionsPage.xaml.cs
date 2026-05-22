using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.ViewModels;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ExceptionsPage : Page
{
    internal ExceptionsPageViewModel ViewModel { get; } = new();

    public ExceptionsPage()
    {
        InitializeComponent();
        Loaded += ExceptionsPage_Loaded;
    }

    private async void ExceptionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        await RefreshAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
    }

    private void ExceptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedException = (sender as ListView)?.SelectedItem as ExceptionRowViewModel;
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RemoveSelectedAsync();
    }

    private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RemoveAllAsync();
    }

    private Task RefreshAsync()
    {
        return ViewModel.RefreshAsync(SearchBox.Text);
    }
}
