using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.TinyWall;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ModernTinyWall.ViewModels;

internal sealed class SettingsPageViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly IMaintenanceService _maintenanceService;
    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _applyCommand;
    private readonly AsyncRelayCommand _checkForUpdatesCommand;
    private bool _isLoading;
    private string _statusMessage = "Settings migration layout ready.";

    public SettingsPageViewModel()
        : this(new SettingsService(), new MaintenanceService())
    {
    }

    internal SettingsPageViewModel(ISettingsService settingsService, IMaintenanceService maintenanceService)
    {
        _settingsService = settingsService;
        _maintenanceService = maintenanceService;
        _loadCommand = new AsyncRelayCommand(LoadAsync, CanRunCommand);
        _applyCommand = new AsyncRelayCommand(ApplyAsync, CanRunCommand);
        _checkForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, CanRunCommand);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SettingsSection> Sections { get; } = [];

    public IAsyncRelayCommand LoadCommand => _loadCommand;

    public IAsyncRelayCommand ApplyCommand => _applyCommand;

    public IAsyncRelayCommand CheckForUpdatesCommand => _checkForUpdatesCommand;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged();
            NotifyCommandStatesChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
                return;

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading settings sections...";

        try
        {
            var sections = await _settingsService.GetSettingsSectionsAsync();
            Sections.Clear();
            foreach (var section in sections)
            {
                Sections.Add(section);
            }

            StatusMessage = "Settings sections loaded.";
        }
        catch (Exception ex)
        {
            Sections.Clear();
            StatusMessage = $"Could not load settings sections: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ApplyAsync()
    {
        IsLoading = true;
        StatusMessage = "Applying settings...";

        try
        {
            var result = await _settingsService.ApplySettingsAsync(Sections);
            StatusMessage = result.Message;
            if (result.Success)
                await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not apply settings: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ChangePasswordAsync(string newPassword)
    {
        IsLoading = true;
        StatusMessage = "Changing password...";

        try
        {
            var result = await _maintenanceService.ChangePasswordAsync(newPassword);
            StatusMessage = result.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task CheckForUpdatesAsync()
    {
        var result = await _maintenanceService.CheckForUpdatesAsync();
        StatusMessage = result.Message;
    }

    public async Task ImportSettingsAsync(string filePath)
    {
        var result = await _maintenanceService.ImportSettingsAsync(filePath);
        StatusMessage = result.Message;
        if (result.Success)
            await LoadAsync();
    }

    public async Task ExportSettingsAsync(string filePath)
    {
        var result = await _maintenanceService.ExportSettingsAsync(filePath);
        StatusMessage = result.Message;
    }

    private bool CanRunCommand()
    {
        return !IsLoading;
    }

    private void NotifyCommandStatesChanged()
    {
        _loadCommand.NotifyCanExecuteChanged();
        _applyCommand.NotifyCanExecuteChanged();
        _checkForUpdatesCommand.NotifyCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
