using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModernTinyWall.Models;

internal sealed record SettingsSection(string Title, string Description, IReadOnlyList<SettingsItem> Items);

internal sealed class SettingsItem : INotifyPropertyChanged
{
    private bool _isEnabled;

    public SettingsItem(string key, string name, string description, bool isEnabled, bool isEditable)
    {
        Key = key;
        Name = name;
        Description = description;
        _isEnabled = isEnabled;
        IsEditable = isEditable;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Name { get; }
    public string Description { get; }
    public bool IsEditable { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
