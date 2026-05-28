using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.TinyWall;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ProcessesPageViewModel : INotifyPropertyChanged
{
    private readonly IProcessesService _processesService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _clearCommand;
    private bool _isRefreshing;
    private string _statusMessage = "Ready";
    private string _searchText = string.Empty;

    public ProcessesPageViewModel()
        : this(new ProcessesService())
    {
    }

    internal ProcessesPageViewModel(IProcessesService processesService)
    {
        _processesService = processesService;
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
        _clearCommand = new AsyncRelayCommand(ClearAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProcessRowViewModel> Processes { get; } = [];

    public IAsyncRelayCommand RefreshCommand => _refreshCommand;

    public IAsyncRelayCommand ClearCommand => _clearCommand;

    public string SearchText
    {
        get => _searchText;
        set => SetField(ref _searchText, value);
    }

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

    public async Task RefreshAsync(string searchText)
    {
        SearchText = searchText;
        IsRefreshing = true;
        StatusMessage = "Refreshing processes...";

        try
        {
            var rows = await _processesService.GetProcessesAsync(new ProcessQuery(searchText));
            Processes.Clear();
            foreach (var row in rows)
            {
                Processes.Add(ProcessRowViewModel.FromModel(row));
            }

            StatusMessage = $"{Processes.Count} process{(Processes.Count == 1 ? string.Empty : "es")}";
        }
        catch (Exception ex)
        {
            Processes.Clear();
            StatusMessage = $"Could not refresh processes: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private Task RefreshAsync()
    {
        return RefreshAsync(SearchText);
    }

    private Task ClearAsync()
    {
        SearchText = string.Empty;
        return RefreshAsync(SearchText);
    }

    private bool CanRunCommand()
    {
        return !IsRefreshing;
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
        if (propertyName == nameof(IsRefreshing))
        {
            _refreshCommand.NotifyCanExecuteChanged();
            _clearCommand.NotifyCanExecuteChanged();
        }
    }
}

internal sealed record ProcessRowViewModel(string ProcessName, string Services, string Path)
{
    public static ProcessRowViewModel FromModel(ProcessRow row)
    {
        return new ProcessRowViewModel(row.ProcessName, row.Services, row.Path);
    }
}
