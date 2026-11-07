using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

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

public sealed record FirewallModeOption(ModernFirewallMode Mode, string DisplayName, string Description)
{
    public SolidColorBrush ModeBrush { get; } = new(Mode switch
    {
        ModernFirewallMode.Normal => ColorHelper.FromArgb(255, 16, 124, 16),
        ModernFirewallMode.AllowOutgoing => ColorHelper.FromArgb(255, 0, 120, 212),
        ModernFirewallMode.BlockAll => ColorHelper.FromArgb(255, 196, 43, 28),
        ModernFirewallMode.Learning => ColorHelper.FromArgb(255, 255, 215, 0),
        _ => ColorHelper.FromArgb(255, 93, 101, 112)
    });

    public SolidColorBrush IconBrush { get; } = new(Mode == ModernFirewallMode.Learning
        ? ColorHelper.FromArgb(255, 26, 26, 26)
        : ColorHelper.FromArgb(255, 255, 255, 255));

    public string IconGlyph => Mode switch
    {
        ModernFirewallMode.Normal => "\uEA18",
        ModernFirewallMode.AllowOutgoing => "\uE72A",
        ModernFirewallMode.BlockAll => "\uE711",
        ModernFirewallMode.Disabled => "\uE7E8",
        ModernFirewallMode.Learning => "\uE82D",
        _ => "\uE9CE"
    };
}
