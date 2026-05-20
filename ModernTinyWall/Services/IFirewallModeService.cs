using pylorak.TinyWall;
using ModernTinyWall.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IFirewallModeService
{
    IReadOnlyList<FirewallModeOption> GetModeOptions();
    Task<FirewallModeResult> SetModeAsync(ModernFirewallMode mode, CancellationToken cancellationToken = default);
}

internal sealed record FirewallModeResult(bool Success, string Message, ModernFirewallMode Mode);
