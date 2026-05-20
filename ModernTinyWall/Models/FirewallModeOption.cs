namespace ModernTinyWall.Models;

internal enum ModernFirewallMode
{
    Normal,
    AllowOutgoing,
    BlockAll,
    Disabled,
    Learning,
    Unknown
}

internal sealed record FirewallModeOption(ModernFirewallMode Mode, string DisplayName, string Description);
