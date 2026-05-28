using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Threading.Tasks;
using WinRT.Interop;

namespace ModernTinyWall.Views;

public sealed partial class SettingsPage : Page
{
    internal SettingsPageViewModel ViewModel { get; } = new();

    internal IAsyncRelayCommand ChangePasswordCommand { get; }

    internal IAsyncRelayCommand ImportCommand { get; }

    internal IAsyncRelayCommand ExportCommand { get; }

    public SettingsPage()
    {
        ChangePasswordCommand = new AsyncRelayCommand(ChangePasswordAsync);
        ImportCommand = new AsyncRelayCommand(ImportAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private async Task ChangePasswordAsync()
    {
        var passwordBox = new PasswordBox { Header = "New password" };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Change password",
            Content = passwordBox,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Remove password",
            CloseButtonText = "Cancel"
        };

        var result = await ShowDialogAsync(dialog);
        if (result == ContentDialogResult.Primary)
            await ViewModel.ChangePasswordAsync(passwordBox.Password);
        else if (result == ContentDialogResult.Secondary)
            await ViewModel.ChangePasswordAsync(string.Empty);
    }

    private async Task ImportAsync()
    {
        var filePath = FilePickerService.PickOpenFile(WindowNative.GetWindowHandle(App.MainWindow), "Import TinyWall settings");
        if (filePath is not null)
            await ViewModel.ImportSettingsAsync(filePath);
    }

    private async Task ExportAsync()
    {
        var filePath = FilePickerService.PickSaveFile(WindowNative.GetWindowHandle(App.MainWindow), "Export TinyWall settings", "TinyWall-settings.tws");
        if (filePath is not null)
            await ViewModel.ExportSettingsAsync(filePath);
    }

    private static Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        var completion = new TaskCompletionSource<ContentDialogResult>();
        var operation = dialog.ShowAsync();
        operation.Completed = (asyncInfo, _) =>
        {
            try
            {
                completion.TrySetResult(asyncInfo.GetResults());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        };

        return completion.Task;
    }
}
