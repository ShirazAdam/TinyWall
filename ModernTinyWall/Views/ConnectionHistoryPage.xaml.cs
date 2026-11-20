using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class ConnectionHistoryPage : Page
{
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    internal ConnectionHistoryPageViewModel ViewModel { get; } =
        new(App.Services.GetRequiredService<ConnectionHistoryService>());

    public ConnectionHistoryPage()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;
        _searchTimer.Tick += SearchTimer_Tick;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();

    private void Page_Unloaded(object sender, RoutedEventArgs e) => _searchTimer.Stop();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private async void SearchTimer_Tick(object? sender, object e)
    {
        _searchTimer.Stop();
        await ViewModel.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshAsync();

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Clear connection history?",
            Content = "This permanently removes the local connection history file.",
            PrimaryButtonText = "Clear history",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await ShowDialogAsync(dialog) == ContentDialogResult.Primary)
            await ViewModel.ClearAsync();
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
