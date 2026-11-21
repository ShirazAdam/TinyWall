using ModernTinyWall.Services;
using ModernTinyWall.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ConnectionHistoryPageViewModel : INotifyPropertyChanged
{
    private readonly ConnectionHistoryService _historyService;
    private string _searchText = string.Empty;
    private string _statusMessage = "Ready";
    private bool _isRefreshing;
    private string? _timelineApplication;
    private int _refreshVersion;

    public ConnectionHistoryPageViewModel(ConnectionHistoryService historyService)
    {
        _historyService = historyService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ConnectionHistoryEntry> Entries { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetField(ref _isRefreshing, value);
    }

    public string TimelineLabel => _timelineApplication is null
        ? "Showing all applications"
        : $"Application timeline: {_timelineApplication}";

    public async Task ShowApplicationAsync(string? application)
    {
        _timelineApplication = application;
        SearchText = string.Empty;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimelineLabel)));
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        IsRefreshing = true;
        try
        {
            var entries = await _historyService.GetEntriesAsync(SearchText);
            if (version != _refreshVersion)
                return;
            if (_timelineApplication is not null)
                entries = ConnectionHistorySummary.ForApplication(entries, _timelineApplication);
            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);

            StatusMessage = _historyService.LastError is { Length: > 0 } error
                ? error
                : $"{Entries.Count:N0} recorded connection{(Entries.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            if (version == _refreshVersion)
                StatusMessage = $"Could not read local connection history: {ex.Message}";
        }
        finally
        {
            if (version == _refreshVersion)
                IsRefreshing = false;
        }
    }

    public async Task ClearAsync()
    {
        try
        {
            await _historyService.ClearAsync();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not clear local connection history: {ex.Message}";
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
