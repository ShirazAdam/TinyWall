using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ExceptionsPage : Page
{
    private readonly IDialogService _dialogService = new DialogService();
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

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        await _dialogService.ShowMessageAsync(
            XamlRoot,
            "Add exception",
            "The WinUI add-exception editor is not yet available. Use the existing TinyWall settings window for creating new exceptions until this workflow is fully migrated.");
    }

    private async void ModifyButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedException is null)
        {
            await _dialogService.ShowMessageAsync(XamlRoot, "Modify exception", "Select an exception to modify.");
            return;
        }

        await _dialogService.ShowMessageAsync(
            XamlRoot,
            "Modify exception",
            "The WinUI modify-exception editor is not yet available. Use the existing TinyWall settings window for editing exceptions until this workflow is fully migrated.");
    }

    private async void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedException is null)
        {
            await _dialogService.ShowMessageAsync(XamlRoot, "Remove exception", "Select an exception to remove.");
            return;
        }

        if (await _dialogService.ConfirmAsync(XamlRoot, "Remove exception", "Remove the selected application exception?", "Remove"))
            await ViewModel.RemoveSelectedAsync();
    }

    private async void RemoveAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (await _dialogService.ConfirmAsync(XamlRoot, "Remove all exceptions", "Remove all application exceptions?", "Remove all"))
            await ViewModel.RemoveAllAsync();
    }

    private Task RefreshAsync()
    {
        return ViewModel.RefreshAsync(SearchBox.Text);
    }
}
