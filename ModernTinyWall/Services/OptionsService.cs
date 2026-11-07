using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class OptionsService : IOptionsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _optionsFilePath;
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public OptionsService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModernTinyWall", "options.json"))
    {
    }

    internal OptionsService(string optionsFilePath)
    {
        _optionsFilePath = optionsFilePath;
    }

    public async Task<ModernTinyWallOptions> LoadAsync()
    {
        await FileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return await ReadAsync().ConfigureAwait(false);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<ModernTinyWallOptions> ReadAsync()
    {
        if (!File.Exists(_optionsFilePath))
            return new ModernTinyWallOptions();

        await using var stream = File.OpenRead(_optionsFilePath);
        return await JsonSerializer.DeserializeAsync<ModernTinyWallOptions>(stream, JsonOptions).ConfigureAwait(false)
            ?? throw new InvalidDataException("The options file contains no settings.");
    }

    public Task SaveAsync(ModernTinyWallOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return UpdateAsync(_ => options);
    }

    public async Task UpdateAsync(Func<ModernTinyWallOptions, ModernTinyWallOptions> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        await FileLock.WaitAsync().ConfigureAwait(false);
        var temporaryPath = _optionsFilePath + ".tmp";
        try
        {
            var options = update(await ReadAsync().ConfigureAwait(false));
            var directoryPath = Path.GetDirectoryName(_optionsFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
                Directory.CreateDirectory(directoryPath);

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, options, JsonOptions).ConfigureAwait(false);
            }
            File.Move(temporaryPath, _optionsFilePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            finally
            {
                FileLock.Release();
            }
        }
    }
}
