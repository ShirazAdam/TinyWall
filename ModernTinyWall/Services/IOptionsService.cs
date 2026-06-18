using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IOptionsService
{
    Task<ModernTinyWallOptions> LoadAsync();
    Task SaveAsync(ModernTinyWallOptions options);
}

internal sealed record ModernTinyWallOptions
{
    public bool MinimiseToTray { get; init; } = true;
    public bool CloseToTray { get; init; } = true;
}
