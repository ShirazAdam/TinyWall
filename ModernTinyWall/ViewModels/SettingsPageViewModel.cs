using pylorak.TinyWall;
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
    private bool _isLoading;
    private string _statusMessage = "Settings migration layout ready.";

    public SettingsPageViewModel()
        : this(new SettingsService())
    {
    }

    internal SettingsPageViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SettingsSection> Sections { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged();
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

            StatusMessage = "Settings sections loaded. Live persistence will follow shared core extraction.";
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
