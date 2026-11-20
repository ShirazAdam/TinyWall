using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ModernTinyWall.Models;

internal sealed record SettingsSection(string Title, string Description, IReadOnlyList<SettingsItem> Items);

internal sealed class SettingsItem : INotifyPropertyChanged
{
    private bool _isEnabled;
    private bool _isEditable;
    private string _description;

    public SettingsItem(string key, string name, string description, bool isEnabled, bool isEditable)
    {
        Key = key;
        Name = name;
        _description = description;
        _isEnabled = isEnabled;
        _isEditable = isEditable;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string Name { get; }
    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
                return;
            _description = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (_isEditable == value)
                return;
            _isEditable = value;
            OnPropertyChanged();
        }
    }

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
