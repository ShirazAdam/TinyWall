using CommunityToolkit.Mvvm.Input;
using ModernTinyWall.TinyWall;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
    private readonly Dictionary<string, SettingsItem> _fixedItems;
    private bool _isLoading;
    private bool _hasLoaded;
    private Task? _loadTask;
    private string _statusMessage = "Settings ready.";

    internal SettingsPageViewModel(ISettingsService settingsService, IMaintenanceService maintenanceService,
        IReadOnlyList<SettingsSection> initialSections)
    {
        _settingsService = settingsService;
        _maintenanceService = maintenanceService;
        _fixedItems = initialSections.SelectMany(section => section.Items).ToDictionary(item => item.Key);
        _loadCommand = new AsyncRelayCommand(LoadAsync, CanRunCommand);
        _applyCommand = new AsyncRelayCommand(ApplyAsync, () => CanRunCommand() && _fixedItems.Values.Any(item => item.IsEditable));
        _checkForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync, CanRunCommand);
        Sections = initialSections.Append(new SettingsSection("Special profiles", string.Empty, SpecialProfiles)).ToArray();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SettingsSection> Sections { get; }
    public ObservableCollection<SettingsItem> SpecialProfiles { get; } = [];
    public SettingsItem AutomaticUpdateChecks => _fixedItems[SettingsService.AutoUpdateCheckKey];
    public SettingsItem AskForExceptionDetails => _fixedItems[SettingsService.AskForExceptionDetailsKey];
    public SettingsItem GlobalHotkeys => _fixedItems[SettingsService.EnableGlobalHotkeysKey];
    public SettingsItem DisplayOffBlock => _fixedItems[SettingsService.DisplayOffBlockKey];
    public SettingsItem LockHostsFile => _fixedItems[SettingsService.LockHostsFileKey];
    public SettingsItem PendingConnectionPrompts => _fixedItems[SettingsService.EnablePendingPromptsKey];
    public SettingsItem EnableBlocklists => _fixedItems[SettingsService.EnableBlocklistsKey];
    public SettingsItem HostsBlocklist => _fixedItems[SettingsService.EnableHostsBlocklistKey];
    public SettingsItem PortBlocklist => _fixedItems[SettingsService.EnablePortBlocklistKey];
    public SettingsItem AllowLocalSubnet => _fixedItems[SettingsService.AllowLocalSubnetKey];
    public SettingsItem ConfiguredExceptions => _fixedItems[SettingsService.ConfiguredExceptionsKey];

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

    public Task EnsureLoadedAsync()
    {
        return _hasLoaded ? Task.CompletedTask : LoadAsync();
    }

    public Task LoadAsync()
    {
        if (_loadTask is { IsCompleted: false })
            return _loadTask;

        _loadTask = LoadCoreAsync();
        return _loadTask;
    }

    private async Task LoadCoreAsync()
    {
        IsLoading = true;
        StatusMessage = "Reading settings from the TinyWall service...";

        try
        {
            var sections = await _settingsService.GetSettingsSectionsAsync();
            var items = sections.SelectMany(section => section.Items).ToDictionary(item => item.Key);
            foreach (var item in _fixedItems.Values)
            {
                if (items.TryGetValue(item.Key, out var loaded))
                {
                    item.IsEnabled = loaded.IsEnabled;
                    item.IsEditable = loaded.IsEditable;
                    item.Description = loaded.Description;
                }
                else
                {
                    item.IsEditable = false;
                }
            }
            SpecialProfiles.Clear();
            foreach (var item in items.Values.Where(item => item.Key.StartsWith(SettingsService.SpecialProfilePrefix, StringComparison.Ordinal)))
                SpecialProfiles.Add(item);

            StatusMessage = _fixedItems.Values.Any(item => item.IsEditable)
                ? "Settings ready."
                : "TinyWall service settings are unavailable. Select Cancel to retry.";
        }
        catch (Exception ex)
        {
            foreach (var item in _fixedItems.Values)
                item.IsEditable = false;
            ConfiguredExceptions.Description = "Service settings are unavailable.";
            SpecialProfiles.Clear();
            StatusMessage = $"Could not load settings sections: {ex.Message}";
        }
        finally
        {
            _hasLoaded = true;
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
