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
