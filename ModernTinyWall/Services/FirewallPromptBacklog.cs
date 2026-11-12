using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernTinyWall.Services;

internal sealed class FirewallPromptBacklog
{
    private const int MaximumApplications = 128;
    private readonly Dictionary<string, DateTime> _allowedThrough = new(StringComparer.OrdinalIgnoreCase);

    public void MarkApplicationAllowed(string path, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!_allowedThrough.ContainsKey(path) && _allowedThrough.Count >= MaximumApplications)
            _allowedThrough.Remove(_allowedThrough.MinBy(pair => pair.Value).Key);
        _allowedThrough[path] = timestamp;
    }

    public bool IsObsolete(FirewallLogEntry entry)
    {
        return entry.PendingConnectionId == 0 &&
            !string.IsNullOrWhiteSpace(entry.AppPath) &&
            _allowedThrough.TryGetValue(entry.AppPath, out var cutoff) &&
            entry.Timestamp <= cutoff;
    }
}
