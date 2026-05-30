using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class PackagesPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly IExceptionsService _exceptionsService = new ExceptionsService();
    internal PackagesPageViewModel ViewModel { get; } = new();

    internal IAsyncRelayCommand<PackageRowViewModel> AllowPackageCommand { get; }

    internal IAsyncRelayCommand<PackageRowViewModel> BlockPackageCommand { get; }

    public PackagesPage()
    {
        AllowPackageCommand = new AsyncRelayCommand<PackageRowViewModel>(package => ApplyPackageExceptionAsync(package, ExceptionEntryPolicy.Allow), static package => package is not null);
        BlockPackageCommand = new AsyncRelayCommand<PackageRowViewModel>(package => ApplyPackageExceptionAsync(package, ExceptionEntryPolicy.Block), static package => package is not null);
        InitializeComponent();
        Loaded += PackagesPage_Loaded;
    }

    private async void PackagesPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await ViewModel.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            await _dialogueService.ShowMessageAsync(XamlRoot, "Packages", ex.Message);
        }
    }

    private async Task ApplyPackageExceptionAsync(PackageRowViewModel? package, ExceptionEntryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(package);

        var request = new ExceptionEntryActionRequest(ExceptionEntryKind.Package, policy, package.Name, package.Sid, package.PublisherId, package.Publisher);
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
