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

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyAsync();
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
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

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = FilePickerService.PickOpenFile(WindowNative.GetWindowHandle(App.MainWindow), "Import TinyWall settings");
        if (filePath is not null)
            await ViewModel.ImportSettingsAsync(filePath);
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = FilePickerService.PickSaveFile(WindowNative.GetWindowHandle(App.MainWindow), "Export TinyWall settings", "TinyWall-settings.tws");
        if (filePath is not null)
            await ViewModel.ExportSettingsAsync(filePath);
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckForUpdatesAsync();
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
