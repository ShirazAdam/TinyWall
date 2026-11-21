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

public sealed partial class ProcessesPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly ITinyWallExceptionStore _exceptionStore = App.Services.GetRequiredService<ITinyWallExceptionStore>();
    internal ProcessesPageViewModel ViewModel { get; } = new();

    internal IAsyncRelayCommand<ProcessRowViewModel> AllowProcessCommand { get; }

    internal IAsyncRelayCommand<ProcessRowViewModel> BlockProcessCommand { get; }

    public ProcessesPage()
    {
        AllowProcessCommand = new AsyncRelayCommand<ProcessRowViewModel>(process => ApplyProcessExceptionAsync(process, ExceptionEntryPolicy.Allow), static process => process is not null);
        BlockProcessCommand = new AsyncRelayCommand<ProcessRowViewModel>(process => ApplyProcessExceptionAsync(process, ExceptionEntryPolicy.Block), static process => process is not null);
        InitializeComponent();
        ProcessesTable.Configure("processes",
            TableColumn.Text<ProcessRowViewModel>("name", "Process", 300, row => row.ProcessName),
            TableColumn.Text<ProcessRowViewModel>("services", "Services", 240, row => row.Services),
            TableColumn.Text<ProcessRowViewModel>("path", "Path", 440, row => row.Path));
        ProcessesTable.EnableGrouping(item => (item as ProcessRowViewModel)?.Path, TableGrouping.WithoutProcessId);
        ProcessesTable.EnableFileInformation(item =>
        {
            var row = (ProcessRowViewModel)item;
            return row.Services == "UWP package"
                ? new ApplicationFileTarget(row.ProcessName, "", $"Package metadata: {row.Path}\nThis package row does not identify a single executable.")
                : new ApplicationFileTarget(row.ProcessName, row.Path);
        });
        Loaded += ProcessesPage_Loaded;
    }

    private async void ProcessesPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
    }

    private async Task ApplyProcessExceptionAsync(ProcessRowViewModel? process, ExceptionEntryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(process);

        var request = new ExceptionEntryActionRequest(ExceptionEntryKind.Executable, policy, process.ProcessName, process.Path);
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
