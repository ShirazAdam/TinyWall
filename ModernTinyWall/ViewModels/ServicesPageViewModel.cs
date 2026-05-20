using pylorak.TinyWall;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class ServicesPageViewModel : INotifyPropertyChanged
{
    private readonly IServicesService _servicesService;
    private bool _isRefreshing;
    private string _statusMessage = "Ready";

    public ServicesPageViewModel()
        : this(new ServicesService())
    {
    }

    internal ServicesPageViewModel(IServicesService servicesService)
    {
        _servicesService = servicesService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ServiceRowViewModel> Services { get; } = [];
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
        StatusMessage = "Refreshing services...";

        try
        {
            var rows = await _servicesService.GetServicesAsync(new ServiceQuery(searchText));
            Services.Clear();
            foreach (var row in rows)
            {
                Services.Add(ServiceRowViewModel.FromModel(row));
            }

            StatusMessage = $"{Services.Count} service{(Services.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            Services.Clear();
            StatusMessage = $"Could not refresh services: {ex.Message}";
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

internal sealed record ServiceRowViewModel(string DisplayName, string ServiceName, string ExecutablePath)
{
    public static ServiceRowViewModel FromModel(ServiceRow row)
    {
        return new ServiceRowViewModel(row.DisplayName, row.ServiceName, row.ExecutablePath);
    }
}
