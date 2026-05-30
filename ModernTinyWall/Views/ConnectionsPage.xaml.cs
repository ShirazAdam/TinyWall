using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ConnectionsPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly IExceptionsService _exceptionsService = new ExceptionsService();
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

    private async void AllowConnectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { CommandParameter: ConnectionRowViewModel connection })
            await ApplyConnectionExceptionAsync(connection, ExceptionEntryPolicy.Allow);
    }

    private async void BlockConnectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { CommandParameter: ConnectionRowViewModel connection })
            await ApplyConnectionExceptionAsync(connection, ExceptionEntryPolicy.Block);
    }

    private async Task ApplyConnectionExceptionAsync(ConnectionRowViewModel connection, ExceptionEntryPolicy policy)
    {
        var request = new ExceptionEntryActionRequest(ExceptionEntryKind.Executable, policy, connection.Application, connection.ExecutablePath);
        var prepareResult = await _exceptionsService.PrepareEntryActionAsync(request);
        if (!prepareResult.Success)
        {
            await _dialogueService.ShowMessageAsync(XamlRoot, "Application exception", prepareResult.Message);
            return;
        }

        var replaceExisting = false;
        if (prepareResult.ExistingExceptionFound)
        {
            replaceExisting = await _dialogueService.ConfirmAsync(XamlRoot, "Update application exception", prepareResult.Message, "Yes");
            if (!replaceExisting)
                return;
        }

        var result = await _exceptionsService.ApplyEntryActionAsync(request, replaceExisting);
        await _dialogueService.ShowMessageAsync(XamlRoot, "Application exception", result.Message);
    }
}
