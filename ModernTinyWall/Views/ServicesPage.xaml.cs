using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ServicesPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly IExceptionsService _exceptionsService = new ExceptionsService();
    internal ServicesPageViewModel ViewModel { get; } = new();

    public ServicesPage()
    {
        InitializeComponent();
        Loaded += ServicesPage_Loaded;
    }

    private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
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

    private async void AllowServiceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { CommandParameter: ServiceRowViewModel service })
            await ApplyServiceExceptionAsync(service, ExceptionEntryPolicy.Allow);
    }

    private async void BlockServiceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { CommandParameter: ServiceRowViewModel service })
            await ApplyServiceExceptionAsync(service, ExceptionEntryPolicy.Block);
    }

    private async Task ApplyServiceExceptionAsync(ServiceRowViewModel service, ExceptionEntryPolicy policy)
    {
        var request = new ExceptionEntryActionRequest(ExceptionEntryKind.Service, policy, service.ServiceName, service.ExecutablePath);
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
