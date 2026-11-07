using ModernTinyWall.Models;
using System;
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
}
