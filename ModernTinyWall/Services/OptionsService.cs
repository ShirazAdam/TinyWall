using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class OptionsService : IOptionsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _optionsFilePath;
    private ModernTinyWallOptions? _cachedOptions;

    public OptionsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _optionsFilePath = Path.Combine(appDataPath, "ModernTinyWall", "options.json");
    }

    public async Task<ModernTinyWallOptions> LoadAsync()
    {
        if (_cachedOptions is not null)
            return _cachedOptions;

        if (!File.Exists(_optionsFilePath))
            return _cachedOptions = new ModernTinyWallOptions();

        await using var stream = File.OpenRead(_optionsFilePath);
        return _cachedOptions = await JsonSerializer.DeserializeAsync<ModernTinyWallOptions>(stream, JsonOptions) ?? new ModernTinyWallOptions();
    }

    public async Task SaveAsync(ModernTinyWallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var directoryPath = Path.GetDirectoryName(_optionsFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
            Directory.CreateDirectory(directoryPath);

        await using var stream = File.Create(_optionsFilePath);
        await JsonSerializer.SerializeAsync(stream, options, JsonOptions);
        _cachedOptions = options;
    }
}
