using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ServicesPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly IExceptionsService _exceptionsService = new ExceptionsService();
    internal ServicesPageViewModel ViewModel { get; } = new();

    internal IAsyncRelayCommand<ServiceRowViewModel> AllowServiceCommand { get; }

    internal IAsyncRelayCommand<ServiceRowViewModel> BlockServiceCommand { get; }

    public ServicesPage()
    {
        AllowServiceCommand = new AsyncRelayCommand<ServiceRowViewModel>(service => ApplyServiceExceptionAsync(service, ExceptionEntryPolicy.Allow), static service => service is not null);
        BlockServiceCommand = new AsyncRelayCommand<ServiceRowViewModel>(service => ApplyServiceExceptionAsync(service, ExceptionEntryPolicy.Block), static service => service is not null);
        InitializeComponent();
        Loaded += ServicesPage_Loaded;
    }

    private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async Task ApplyServiceExceptionAsync(ServiceRowViewModel? service, ExceptionEntryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(service);

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
