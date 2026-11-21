using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class PackagesPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly ITinyWallExceptionStore _exceptionStore = App.Services.GetRequiredService<ITinyWallExceptionStore>();
    internal PackagesPageViewModel ViewModel { get; } = new();

    internal IAsyncRelayCommand<PackageRowViewModel> AllowPackageCommand { get; }

    internal IAsyncRelayCommand<PackageRowViewModel> BlockPackageCommand { get; }

    public PackagesPage()
    {
        AllowPackageCommand = new AsyncRelayCommand<PackageRowViewModel>(package => ApplyPackageExceptionAsync(package, ExceptionEntryPolicy.Allow), static package => package is not null);
        BlockPackageCommand = new AsyncRelayCommand<PackageRowViewModel>(package => ApplyPackageExceptionAsync(package, ExceptionEntryPolicy.Block), static package => package is not null);
        InitializeComponent();
        PackagesTable.Configure("packages",
            TableColumn.Text<PackageRowViewModel>("name", "Name", 300, row => row.Name),
            TableColumn.Text<PackageRowViewModel>("publisher", "Publisher", 360, row => row.PublisherDisplay),
            TableColumn.Text<PackageRowViewModel>("sid", "SID", 300, row => row.Sid),
            TableColumn.Text<PackageRowViewModel>("tampered", "Tampered", 110, row => row.Tampered));
        PackagesTable.EnableFileInformation(item =>
        {
            var row = (PackageRowViewModel)item;
            return new ApplicationFileTarget(row.Name, "", $"Package publisher: {row.PublisherDisplay}\nPackage family: {row.PackageFamilyName}\nSID: {row.Sid}\nTampered: {row.Tampered}\nThis package row does not identify a single executable.");
        });
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
        var prepareResult = await _exceptionStore.PrepareEntryActionAsync(request);
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

        var result = await _exceptionStore.ApplyEntryActionAsync(request, replaceExisting);
        await _dialogueService.ShowMessageAsync(XamlRoot, "Application exception", result.Message);
    }
}
