using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class FirewallPromptMonitor
{
    private readonly Controller _controller = new("TinyWallController");
    private readonly HashSet<string> _seenEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _seenEventOrder = new();
    private readonly Dictionary<string, DateTime> _lastPromptByDestination = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public Task<IReadOnlyList<FirewallLogEntry>> GetNewBlockedOutboundEventsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<FirewallLogEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = _controller.BeginReadFwLog();
            var entries = Controller.EndReadFwLog(request.Response);
            var newEntries = new List<FirewallLogEntry>();

            foreach (var entry in entries)
            {
                if (!IsPromptCandidate(entry))
                    continue;

                var eventKey = GetEventKey(entry);
                if (!RememberEvent(eventKey))
                    continue;

                if (!_initialized)
                    continue;

                var destinationKey = GetDestinationKey(entry);
                var now = DateTime.UtcNow;
                if (_lastPromptByDestination.TryGetValue(destinationKey, out var lastPrompt)
                    && now - lastPrompt < TimeSpan.FromSeconds(30))
                {
                    continue;
                }

                if (_lastPromptByDestination.Count >= 2048)
                {
                    var oldestPrompt = _lastPromptByDestination.MinBy(pair => pair.Value);
                    _lastPromptByDestination.Remove(oldestPrompt.Key);
                }

                _lastPromptByDestination[destinationKey] = now;
                newEntries.Add(entry);
            }

            _initialized = true;
            return newEntries;
        }, cancellationToken);
    }

    private bool RememberEvent(string eventKey)
    {
        if (!_seenEvents.Add(eventKey))
            return false;

        _seenEventOrder.Enqueue(eventKey);
        while (_seenEventOrder.Count > 2048)
            _seenEvents.Remove(_seenEventOrder.Dequeue());

        return true;
    }

    private static bool IsPromptCandidate(FirewallLogEntry entry)
    {
        return entry.Event == EventLogEvent.BLOCKED
            && entry.Direction == RuleDirection.Out
            && !string.IsNullOrWhiteSpace(entry.AppPath)
            && !string.Equals(entry.AppPath, "System", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entry.RemoteIp);
    }

    private static string GetEventKey(FirewallLogEntry entry)
    {
        return string.Join(
            "|",
            entry.Timestamp.Ticks,
            entry.AppPath,
            entry.Protocol,
            entry.RemoteIp,
            entry.RemotePort);
    }

    private static string GetDestinationKey(FirewallLogEntry entry)
    {
        return string.Join("|", entry.AppPath, entry.Protocol, entry.RemoteIp, entry.RemotePort);
    }
}
