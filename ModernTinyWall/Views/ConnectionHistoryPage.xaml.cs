using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ModernTinyWall.Services;
using ModernTinyWall.Models;
using ModernTinyWall.ViewModels;
using System;
using System.Linq;
using System.Text;
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
        HistoryList.Configure("history",
            TableColumn.Text<ConnectionHistoryEntry>("name", "Application", 300, row => row.Application),
            TableColumn.Text<ConnectionHistoryEntry>("protocol", "Protocol", 90, row => row.Protocol),
            TableColumn.Text<ConnectionHistoryEntry>("local", "Local endpoint", 220, row => row.LocalEndpoint),
            TableColumn.Text<ConnectionHistoryEntry>("remote", "Remote endpoint", 220, row => row.RemoteEndpoint),
            TableColumn.Text<ConnectionHistoryEntry>("state", "State", 120, row => row.State),
            TableColumn.Value<ConnectionHistoryEntry, DateTimeOffset>("firstSeen", "First seen", 160, row => row.FirstSeen),
            TableColumn.Value<ConnectionHistoryEntry, DateTimeOffset>("lastSeen", "Last seen", 160, row => row.LastSeen));
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

    private async void Timeline_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is ConnectionHistoryEntry entry)
            await ViewModel.ShowApplicationAsync(entry.Application);
        else
            await ShowDialogAsync(new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Application timeline",
                Content = "Select a connection first.",
                CloseButtonText = "Close"
            });
    }

    private async void AllApplications_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.ShowApplicationAsync(null);

    private async void BlockedDigest_Click(object sender, RoutedEventArgs e)
    {
        string text;
        try
        {
            var history = await App.Services.GetRequiredService<ConnectionHistoryService>().GetEntriesAsync();
            var groups = ConnectionHistorySummary.BlockedByApplication(history);
            var summary = new StringBuilder("Sampled blocked connection records, grouped by application. These are not counts of firewall events or unique attempts; history is opt-in and incomplete.\n\n");
            if (groups.Count == 0)
                summary.Append("No blocked connection records are available.");
            else
                foreach (var group in groups.Take(10))
                    summary.AppendLine($"{group.Application}: {group.ObservedConnections:N0} records; last seen {group.LastSeen.ToLocalTime():g}");
            if (groups.Count > 10)
                summary.AppendLine($"\nShowing 10 of {groups.Count:N0} applications.");
            text = summary.ToString();
        }
        catch (Exception ex)
        {
            text = $"Could not read the blocked activity digest: {ex.Message}";
        }

        await ShowDialogAsync(new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Blocked activity digest",
            Content = new ScrollViewer
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
                MaxHeight = 480
            },
            CloseButtonText = "Close"
        });
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Clear connection history?",
            Content = "This permanently removes the stored connection history records.",
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
