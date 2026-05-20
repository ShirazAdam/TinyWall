using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.ViewModels;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ProcessesPage : Page
{
    internal ProcessesPageViewModel ViewModel { get; } = new();

    public ProcessesPage()
    {
        InitializeComponent();
        Loaded += ProcessesPage_Loaded;
    }

    private async void ProcessesPage_Loaded(object sender, RoutedEventArgs e)
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

    private Task RefreshAsync()
    {
        return ViewModel.RefreshAsync(SearchBox.Text);
    }
}
