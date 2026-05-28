using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class PackagesPageViewModel : INotifyPropertyChanged
{
    private readonly IPackagesService _packagesService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _clearCommand;
    private bool _isRefreshing;
    private string _statusMessage = "Ready";
    private string _searchText = string.Empty;

    public PackagesPageViewModel()
        : this(new PackagesService())
    {
    }

    internal PackagesPageViewModel(IPackagesService packagesService)
    {
        _packagesService = packagesService;
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, CanRunCommand);
        _clearCommand = new AsyncRelayCommand(ClearAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PackageRowViewModel> Packages { get; } = [];

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
        StatusMessage = "Refreshing packages...";

        try
        {
            var rows = await _packagesService.GetPackagesAsync(new PackageQuery(searchText));
            Packages.Clear();
            foreach (var row in rows)
            {
                Packages.Add(PackageRowViewModel.FromModel(row));
            }

            StatusMessage = $"{Packages.Count} package{(Packages.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            Packages.Clear();
            StatusMessage = $"Could not refresh packages: {ex.Message}";
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

internal sealed record PackageRowViewModel(string Name, string Publisher, string PublisherId, string Sid, string Tampered)
{
    public string PublisherDisplay => $"{PublisherId}, {Publisher}";

    public static PackageRowViewModel FromModel(PackageRow row)
    {
        return new PackageRowViewModel(row.Name, row.Publisher, row.PublisherId, row.Sid, row.Tampered);
    }
}
