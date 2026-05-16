using pylorak.Windows;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;

namespace pylorak.TinyWall;

internal sealed record PathMetadata(string? ImageKey, bool IsNetworkPath, bool CanUsePathIcon, byte[]? IconPng);

internal static class PathMetadataCache
{
    private const int MaxEntries = 256;
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<CacheKey, LinkedListNode<CacheEntry>> Cache = new();
    private static readonly LinkedList<CacheEntry> RecentEntries = new();

    internal static PathMetadata Get(string path, bool hasPackage, int iconWidth, int iconHeight)
    {
        var key = new CacheKey(path, hasPackage, iconWidth, iconHeight);

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(key, out var cachedNode))
            {
                RecentEntries.Remove(cachedNode);
                RecentEntries.AddFirst(cachedNode);
                return cachedNode.Value.Metadata;
            }
        }

        var metadata = Create(path, hasPackage, iconWidth, iconHeight);

        lock (SyncRoot)
        {
            if (Cache.TryGetValue(key, out var existingNode))
            {
                RecentEntries.Remove(existingNode);
                RecentEntries.AddFirst(existingNode);
                return existingNode.Value.Metadata;
            }

            var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, metadata));
            RecentEntries.AddFirst(node);
            Cache.Add(key, node);

            while (Cache.Count > MaxEntries)
            {
                var last = RecentEntries.Last;
                if (last is null)
                    break;

                RecentEntries.RemoveLast();
                Cache.Remove(last.Value.Key);
            }
        }

        return metadata;
    }

    private static PathMetadata Create(string path, bool hasPackage, int iconWidth, int iconHeight)
    {
        if (hasPackage)
            return new PathMetadata(@"store", false, false, null);

        if (path == "System")
            return new PathMetadata(@"system", false, false, null);

        var isNetworkPath = NetworkPath.IsNetworkPath(path);
        if (isNetworkPath)
            return new PathMetadata(@"network-drive", true, false, null);

        if (!Path.IsPathRooted(path) || !File.Exists(path))
            return new PathMetadata(null, false, false, null);

        try
        {
            using var bitmap = Utils.GetIconContained(path, iconWidth, iconHeight);
            using var iconStream = new MemoryStream();
            bitmap.Save(iconStream, ImageFormat.Png);
            return new PathMetadata(path, false, true, iconStream.ToArray());
        }
        catch
        {
            return new PathMetadata(null, false, false, null);
        }
    }

    private sealed record CacheKey(string Path, bool HasPackage, int IconWidth, int IconHeight);
    private sealed record CacheEntry(CacheKey Key, PathMetadata Metadata);
}
