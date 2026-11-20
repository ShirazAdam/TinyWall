using ModernTinyWall.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IOptionsService
{
    Task<ModernTinyWallOptions> LoadAsync();
    Task SaveAsync(ModernTinyWallOptions options);
    Task UpdateAsync(Func<ModernTinyWallOptions, ModernTinyWallOptions> update);
}

internal sealed record ModernTinyWallOptions
{
    public bool MinimiseToTray { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
    public NetworkActivitySeries VisibleNetworkSeries { get; init; } = NetworkActivitySeries.All;
    public bool ShowProtectionCard { get; init; } = true;
    public bool ShowConnectionsCard { get; init; } = true;
    public bool ShowBlockedTrafficCard { get; init; } = true;
    public bool CollectConnectionHistory { get; init; }
    public int ConnectionHistoryRetentionDays { get; init; } = 30;
    public bool OnboardingComplete { get; init; }
    public Dictionary<Guid, string> RuleTags { get; init; } = new();
    public Dictionary<string, TablePreferences> Tables { get; init; } = new();
}
