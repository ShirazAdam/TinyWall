using ModernTinyWall.TinyWall;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ConnectionsPageViewModel : INotifyPropertyChanged
{
    private readonly IConnectionsService _connectionsService;
    private bool _isRefreshing;
    private string _statusMessage = "Ready";

    public ConnectionsPageViewModel()
        : this(new ConnectionsService())
    {
    }

    internal ConnectionsPageViewModel(IConnectionsService connectionsService)
    {
        _connectionsService = connectionsService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ConnectionRowViewModel> Connections { get; } = [];
    public string SearchText { get; set; } = string.Empty;

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetField(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public async Task RefreshAsync(bool showActive, bool showListening, bool showBlocked, string searchText)
    {
        SearchText = searchText;
        IsRefreshing = true;
        StatusMessage = "Refreshing connections...";

        try
        {
            var query = new ConnectionQuery(showActive, showListening, showBlocked, searchText);
            var rows = await _connectionsService.GetConnectionsAsync(query);

            Connections.Clear();
            foreach (var row in rows)
            {
                Connections.Add(ConnectionRowViewModel.FromModel(row));
            }

            StatusMessage = $"{Connections.Count} connection{(Connections.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            Connections.Clear();
            StatusMessage = $"Could not refresh connections: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }
}

internal sealed record ConnectionRowViewModel(string Application, string Protocol, string LocalPort, string LocalAddress, string RemotePort, string RemoteAddress, string State, string Direction)
{
    public static ConnectionRowViewModel FromModel(ConnectionRow row)
    {
        return new ConnectionRowViewModel(row.Application, row.Protocol, row.LocalPort, row.LocalAddress, row.RemotePort, row.RemoteAddress, row.State, row.Direction);
    }
}
