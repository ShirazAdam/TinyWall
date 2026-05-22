using ModernTinyWall.Models;
using ModernTinyWall.TinyWall;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class FirewallModeService : IFirewallModeService
{
    private readonly Controller _controller = new("TinyWallController");
    private ModernFirewallMode _currentMode = ModernFirewallMode.Unknown;

    public IReadOnlyList<FirewallModeOption> GetModeOptions()
    {
        return
        [
            new FirewallModeOption(ModernFirewallMode.Normal, "Normal protection", "Recommended TinyWall mode. Blocks unsolicited inbound traffic and allows trusted configured traffic."),
            new FirewallModeOption(ModernFirewallMode.AllowOutgoing, "Allow outgoing", "Allows outgoing connections while retaining inbound protection."),
            new FirewallModeOption(ModernFirewallMode.BlockAll, "Block all", "Blocks inbound and outbound traffic."),
            new FirewallModeOption(ModernFirewallMode.Disabled, "Disabled", "Turns TinyWall filtering off."),
            new FirewallModeOption(ModernFirewallMode.Learning, "Learning", "Temporarily learns allowed traffic. Use with care.")
        ];
    }

    public Task<FirewallModeResult> SetModeAsync(ModernFirewallMode mode, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = _controller.SwitchFirewallMode(ToCoreMode(mode));
            if (response == MessageType.MODE_SWITCH)
            {
                _currentMode = mode;
                return new FirewallModeResult(true, SuccessMessage(mode), _currentMode);
            }

            return new FirewallModeResult(false, FailureMessage(response), _currentMode);
        }, cancellationToken);
    }

    private static FirewallMode ToCoreMode(ModernFirewallMode mode)
    {
        return mode switch
        {
            ModernFirewallMode.Normal => FirewallMode.Normal,
            ModernFirewallMode.AllowOutgoing => FirewallMode.AllowOutgoing,
            ModernFirewallMode.BlockAll => FirewallMode.BlockAll,
            ModernFirewallMode.Disabled => FirewallMode.Disabled,
            ModernFirewallMode.Learning => FirewallMode.Learning,
            _ => FirewallMode.Unknown
        };
    }

    private static string SuccessMessage(ModernFirewallMode mode)
    {
        return mode switch
        {
            ModernFirewallMode.Normal => "Firewall mode set to normal protection.",
            ModernFirewallMode.AllowOutgoing => "Firewall mode set to allow outgoing.",
            ModernFirewallMode.BlockAll => "Firewall mode set to block all.",
            ModernFirewallMode.Disabled => "Firewall mode set to disabled.",
            ModernFirewallMode.Learning => "Firewall mode set to learning.",
            _ => "Firewall mode state is unknown."
        };
    }

    private static string FailureMessage(MessageType response)
    {
        return response switch
        {
            MessageType.COM_ERROR => "Could not contact the TinyWall service.",
            MessageType.RESPONSE_LOCKED => "TinyWall is locked. Unlock it before changing firewall mode.",
            MessageType.RESPONSE_ERROR => "The TinyWall service could not change firewall mode.",
            MessageType.INVALID_COMMAND => "The TinyWall service rejected the firewall mode command.",
            _ => $"Unexpected TinyWall service response: {response}."
        };
    }
}
