using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.ComponentModel;

namespace ModernTinyWall.Models;

public enum ModernFirewallMode
{
    Normal,
    AllowOutgoing,
    BlockAll,
    Disabled,
    Learning,
    Unknown
}

public sealed record FirewallModeOption(ModernFirewallMode Mode, string DisplayName, string Description) : INotifyPropertyChanged
{
    private bool _isSelected;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectionVisibility)));
        }
    }

    public Visibility SelectionVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush ModeBrush { get; } = new(Mode switch
    {
        ModernFirewallMode.Normal => ColorHelper.FromArgb(255, 56, 155, 255),
        ModernFirewallMode.AllowOutgoing => ColorHelper.FromArgb(255, 0, 190, 163),
        ModernFirewallMode.BlockAll => ColorHelper.FromArgb(255, 255, 83, 104),
        ModernFirewallMode.Learning => ColorHelper.FromArgb(255, 255, 215, 0),
        _ => ColorHelper.FromArgb(255, 132, 155, 190)
    });

    public string IconGlyph => Mode switch
    {
        ModernFirewallMode.Normal => "\uEA18",
        ModernFirewallMode.AllowOutgoing => "\uE72A",
        ModernFirewallMode.BlockAll => "\uE72E",
        ModernFirewallMode.Disabled => "\uE769",
        ModernFirewallMode.Learning => "\uE7BE",
        _ => "\uE9CE"
    };
}
