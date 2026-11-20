using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Extensions.DependencyInjection;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using ModernTinyWall.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;

namespace ModernTinyWall.Views;

public sealed partial class ConnectionsPage : Page
{
    private readonly IDialogueService _dialogueService = new DialogueService();
    private readonly ITinyWallExceptionStore _exceptionStore = App.Services.GetRequiredService<ITinyWallExceptionStore>();
    internal ConnectionsPageViewModel ViewModel { get; } = new();

    public ConnectionsPage()
    {
        InitializeComponent();
        ConnectionsList.Configure("connections",
            TableColumn.Text<ConnectionRowViewModel>("name", "Application", 300, row => row.Application),
            TableColumn.Text<ConnectionRowViewModel>("protocol", "Protocol", 85, row => row.Protocol),
            TableColumn.Value<ConnectionRowViewModel, int?>("localPort", "Local port", 90, row => TableSortValues.Port(row.LocalPort)),
            TableColumn.Text<ConnectionRowViewModel>("localAddress", "Local address", 170, row => row.LocalAddress),
            TableColumn.Value<ConnectionRowViewModel, int?>("remotePort", "Remote port", 95, row => TableSortValues.Port(row.RemotePort)),
            TableColumn.Text<ConnectionRowViewModel>("remoteAddress", "Remote address", 170, row => row.RemoteAddress),
            TableColumn.Text<ConnectionRowViewModel>("state", "State", 110, row => row.State),
            TableColumn.Text<ConnectionRowViewModel>("direction", "Direction", 100, row => row.Direction));
        Loaded += ConnectionsPage_Loaded;
    }

    private async void ConnectionsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.SearchText = SearchBox.Text;
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await RefreshAsync();
        }
    }

    private Task RefreshAsync()
    {
        if (ViewModel.IsRefreshing)
            return Task.CompletedTask;

        return ViewModel.RefreshAsync(
            ShowActiveCheckBox.IsChecked == true,
            ShowListeningCheckBox.IsChecked == true,
            ShowBlockedCheckBox.IsChecked == true,
            SearchBox.Text);
    }

    private async void AllowConnectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { CommandParameter: ConnectionRowViewModel connection })
            await ApplyConnectionExceptionAsync(connection, ExceptionEntryPolicy.Allow);
    }

    private async void BlockConnectionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { CommandParameter: ConnectionRowViewModel connection })
            await ApplyConnectionExceptionAsync(connection, ExceptionEntryPolicy.Block);
    }

    private async void ConnectionDetailsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { CommandParameter: ConnectionRowViewModel connection })
            return;

        var content = new StackPanel { Spacing = 8 };
        AddDetail(content, "Application", connection.Application);
        AddDetail(content, "Executable", connection.ExecutablePath);
        AddDetail(content, "Protocol", connection.Protocol);
        AddDetail(content, "Direction", connection.Direction);
        AddDetail(content, "State", connection.State);
        AddDetail(content, "Local endpoint", $"{connection.LocalAddress}:{connection.LocalPort}");
        AddDetail(content, "Remote endpoint", $"{connection.RemoteAddress}:{connection.RemotePort}");
        AddDetail(content, "Remote address context", EndpointContext.Describe(connection.RemoteAddress));
        content.Children.Add(new TextBlock
        {
            Text = "Matching application exceptions",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        try
        {
            var matches = string.IsNullOrWhiteSpace(connection.ExecutablePath)
                ? []
                : (await _exceptionStore.GetExceptionsAsync(new ExceptionQuery(connection.ExecutablePath)))
                    .Where(row => string.Equals(row.Details, connection.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            if (matches.Length == 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = "No exception with this exact executable path was found. Other firewall rules may still affect this connection.",
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                foreach (var match in matches)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"{match.Policy} — {match.SubjectType}: {match.Name}. {ExceptionRowViewModel.FromModel(match).PolicyScope}",
                        TextWrapping = TextWrapping.Wrap
                    });
                }
            }
        }
        catch (Exception ex)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Could not read matching exceptions: {ex.Message}",
                TextWrapping = TextWrapping.Wrap
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = "This is a live connection snapshot. A matching exception does not guarantee the effective outcome; other firewall rules may apply.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        });
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Connection details",
            Content = new ScrollViewer { Content = content, MaxHeight = 560 },
            CloseButtonText = "Close"
        };
        _ = dialog.ShowAsync();
    }

    private static void AddDetail(Panel panel, string label, string value)
    {
        panel.Children.Add(new TextBlock
        {
            Text = $"{label}: {value}",
            TextWrapping = TextWrapping.Wrap
        });
    }

    private async Task ApplyConnectionExceptionAsync(ConnectionRowViewModel connection, ExceptionEntryPolicy policy)
    {
        var request = new ExceptionEntryActionRequest(ExceptionEntryKind.Executable, policy, connection.Application, connection.ExecutablePath);
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
