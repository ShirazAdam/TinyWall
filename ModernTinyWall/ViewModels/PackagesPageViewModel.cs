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
    private bool _isRefreshing;
    private string _statusMessage = "Ready";

    public PackagesPageViewModel()
        : this(new PackagesService())
    {
    }

    internal PackagesPageViewModel(IPackagesService packagesService)
    {
        _packagesService = packagesService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<PackageRowViewModel> Packages { get; } = [];
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

internal sealed record PackageRowViewModel(string Name, string Publisher, string PublisherId, string Sid, string Tampered)
{
    public string PublisherDisplay => $"{PublisherId}, {Publisher}";

    public static PackageRowViewModel FromModel(PackageRow row)
    {
        return new PackageRowViewModel(row.Name, row.Publisher, row.PublisherId, row.Sid, row.Tampered);
    }
}
