using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace ModernTinyWall.Services;

internal static class ApplicationIconLoader
{
    private const int CacheLimit = 256;
    private static readonly Dictionary<string, Task<BitmapImage?>> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Queue<string> CacheOrder = new();
    private static readonly SemaphoreSlim LoadLimit = new(8);
    private static readonly Lazy<Task<Dictionary<string, string>>> PackageLogos = new(() => Task.Run(FindPackageLogos));

    public static async Task<BitmapImage?> GetAsync(string? path, string? packageFamilyName)
    {
        if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(packageFamilyName))
            (await PackageLogos.Value).TryGetValue(packageFamilyName, out path);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!Cache.TryGetValue(path, out var task))
        {
            if (Cache.Count >= CacheLimit)
                Cache.Remove(CacheOrder.Dequeue());
            task = LoadAsync(path);
            Cache.Add(path, task);
            CacheOrder.Enqueue(path);
        }
        return await task;
    }

    private static async Task<BitmapImage?> LoadAsync(string path)
    {
        await LoadLimit.WaitAsync();
        try
        {
            if (!await Task.Run(() => File.Exists(path)))
                return null;
            var fileCompletion = new TaskCompletionSource<StorageFile>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fileOperation = StorageFile.GetFileFromPathAsync(path);
            fileOperation.Completed = (result, _) =>
            {
                try { fileCompletion.TrySetResult(result.GetResults()); }
                catch (Exception ex) { fileCompletion.TrySetException(ex); }
            };
            var file = await fileCompletion.Task;
            var thumbnailCompletion = new TaskCompletionSource<StorageItemThumbnail?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thumbnailOperation = file.GetThumbnailAsync(ThumbnailMode.SingleItem, 48);
            thumbnailOperation.Completed = (result, _) =>
            {
                try { thumbnailCompletion.TrySetResult(result.GetResults()); }
                catch (Exception ex) { thumbnailCompletion.TrySetException(ex); }
            };
            using var thumbnail = await thumbnailCompletion.Task;
            if (thumbnail is null)
                return null;
            var bitmap = new BitmapImage();
            var sourceCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var sourceOperation = bitmap.SetSourceAsync(thumbnail);
            sourceOperation.Completed = (result, _) =>
            {
                try { result.GetResults(); sourceCompletion.TrySetResult(); }
                catch (Exception ex) { sourceCompletion.TrySetException(ex); }
            };
            await sourceCompletion.Task;
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not load icon for '{path}': {ex}");
            return null;
        }
        finally
        {
            LoadLimit.Release();
        }
    }

    private static Dictionary<string, string> FindPackageLogos()
    {
        var logos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var package in new PackageManager().FindPackagesForUser(string.Empty))
            {
                try
                {
                    var uri = package.Logo;
                    if (uri is null || (!uri.IsFile && uri.Scheme != "ms-appx"))
                        continue;
                    var path = uri.IsFile ? uri.LocalPath : Path.Combine(package.InstalledLocation.Path,
                        Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/').Replace('/', '\\'));
                    if (!File.Exists(path))
                    {
                        var directory = Path.GetDirectoryName(path);
                        if (directory is not null && Directory.Exists(directory))
                            path = Directory.EnumerateFiles(directory,
                                $"{Path.GetFileNameWithoutExtension(path)}.*{Path.GetExtension(path)}").FirstOrDefault() ?? path;
                    }
                    if (File.Exists(path))
                        logos.TryAdd(package.Id.FamilyName, path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not find a package logo: {ex}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not enumerate package logos: {ex}");
        }
        return logos;
    }
}
