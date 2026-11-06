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
    public string ModeColour => Mode switch
    {
        ModernFirewallMode.Normal => "#FF107C10",
        ModernFirewallMode.AllowOutgoing => "#FF0078D4",
        ModernFirewallMode.BlockAll => "#FFC42B1C",
        ModernFirewallMode.Disabled => "#FF5D6570",
        ModernFirewallMode.Learning => "#FFFFD700",
        _ => "#FF5D6570"
    };

    public string IconForeground => Mode == ModernFirewallMode.Learning ? "#FF1A1A1A" : "#FFFFFFFF";

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
