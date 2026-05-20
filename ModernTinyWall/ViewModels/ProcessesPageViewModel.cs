using pylorak.TinyWall;
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
    private bool _isRefreshing;
    private string _statusMessage = "Ready";

    public ProcessesPageViewModel()
        : this(new ProcessesService())
    {
    }

    internal ProcessesPageViewModel(IProcessesService processesService)
    {
        _processesService = processesService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProcessRowViewModel> Processes { get; } = [];
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

internal sealed record ProcessRowViewModel(string ProcessName, string Services, string Path)
{
    public static ProcessRowViewModel FromModel(ProcessRow row)
    {
        return new ProcessRowViewModel(row.ProcessName, row.Services, row.Path);
    }
}
