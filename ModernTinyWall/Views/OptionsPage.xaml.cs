using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Views;

public sealed partial class OptionsPage : Page
{
    private readonly IOptionsService _optionsService = new OptionsService();
    private bool _isLoading;

    public OptionsPage()
    {
        InitializeComponent();
        Loaded += OptionsPage_Loaded;
    }

    private async void OptionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        try
        {
            var options = await _optionsService.LoadAsync();
            MinimiseToTraySwitch.IsOn = options.MinimiseToTray;
            CloseToTraySwitch.IsOn = options.CloseToTray;
            ProtectionCardSwitch.IsOn = options.ShowProtectionCard;
            ConnectionsCardSwitch.IsOn = options.ShowConnectionsCard;
            BlockedTrafficCardSwitch.IsOn = options.ShowBlockedTrafficCard;
            CollectHistorySwitch.IsOn = options.CollectConnectionHistory;
            HistoryRetentionBox.SelectedIndex = options.ConnectionHistoryRetentionDays switch
            {
                <= 7 => 0,
                <= 30 => 1,
                _ => 2
            };
            App.Services.GetRequiredService<ConnectionHistoryService>()
                .Configure(options.CollectConnectionHistory, options.ConnectionHistoryRetentionDays);
        }
        catch (Exception ex)
        {
            OptionsStatus.IsOpen = true;
            OptionsStatus.Severity = InfoBarSeverity.Error;
            OptionsStatus.Title = "Could not load options";
            OptionsStatus.Message = ex.Message;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void OptionsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        await SaveOptionsAsync();
    }

    private void TerminateButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mainWindow)
            mainWindow.TerminateApplication();
    }

    private async Task SaveOptionsAsync()
    {
        var minimiseToTray = MinimiseToTraySwitch.IsOn;
        var closeToTray = CloseToTraySwitch.IsOn;
        var showProtectionCard = ProtectionCardSwitch.IsOn;
        var showConnectionsCard = ConnectionsCardSwitch.IsOn;
        var showBlockedTrafficCard = BlockedTrafficCardSwitch.IsOn;
        var collectConnectionHistory = CollectHistorySwitch.IsOn;
        var retentionDays = (HistoryRetentionBox.SelectedItem as ComboBoxItem)?.Tag is string days
            && int.TryParse(days, out var parsedDays) ? parsedDays : 30;
        try
        {
            await SaveOptionsAndConfigureHistoryAsync(options => options with
            {
                MinimiseToTray = minimiseToTray,
                CloseToTray = closeToTray,
                ShowProtectionCard = showProtectionCard,
                ShowConnectionsCard = showConnectionsCard,
                ShowBlockedTrafficCard = showBlockedTrafficCard,
                CollectConnectionHistory = collectConnectionHistory,
                ConnectionHistoryRetentionDays = retentionDays
            });
            OptionsStatus.IsOpen = false;
        }
        catch (Exception ex)
        {
            OptionsStatus.IsOpen = true;
            OptionsStatus.Severity = InfoBarSeverity.Error;
            OptionsStatus.Title = "Could not save options";
            OptionsStatus.Message = ex.Message;
        }
    }

    private async Task SaveOptionsAndConfigureHistoryAsync(Func<ModernTinyWallOptions, ModernTinyWallOptions> update)
    {
        await _optionsService.UpdateAsync(update);
        var options = await _optionsService.LoadAsync();
        App.Services.GetRequiredService<ConnectionHistoryService>()
            .Configure(options.CollectConnectionHistory, options.ConnectionHistoryRetentionDays);
    }

    private async void HistoryRetention_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading)
            await SaveOptionsAsync();
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
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
        if (await ShowDialogAsync(dialog) != ContentDialogResult.Primary)
            return;

        try
        {
            await App.Services.GetRequiredService<ConnectionHistoryService>().ClearAsync();
            OptionsStatus.IsOpen = false;
        }
        catch (Exception ex)
        {
            OptionsStatus.IsOpen = true;
            OptionsStatus.Severity = InfoBarSeverity.Error;
            OptionsStatus.Title = "Could not clear history";
            OptionsStatus.Message = ex.Message;
        }
    }

    private void GettingStarted_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mainWindow)
            _ = mainWindow.ShowGettingStartedAsync();
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
