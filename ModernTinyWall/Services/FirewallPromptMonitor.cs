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
    private readonly object _pendingGuard = new();
    private HashSet<ulong> _pendingIds = [];
    private bool _pendingEnabled;

    public bool IsPending(ulong id)
    {
        lock (_pendingGuard)
            return _pendingIds.Contains(id);
    }

    public void ClearPending()
    {
        lock (_pendingGuard)
            _pendingIds.Clear();
    }

    public void ForgetPending(ulong id)
    {
        lock (_pendingGuard)
            _pendingIds.Remove(id);
    }

    public Task<ModernTinyWall.Exceptions.ExceptionMutationResult> ResolvePendingAsync(ulong id, bool allow)
    {
        return Task.Run(() =>
        {
            var response = _controller.ResolvePendingConnection(id, allow);
            return new ModernTinyWall.Exceptions.ExceptionMutationResult(
                response.Type == MessageType.RESOLVE_PENDING_CONNECTION,
                response.Type == MessageType.RESOLVE_PENDING_CONNECTION
                    ? "Connection decision applied."
                    : $"Could not apply the pending connection decision ({response.Type}); the connection may have expired.");
        });
    }

    public Task<IReadOnlyList<FirewallLogEntry>> GetNewBlockedOutboundEventsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<FirewallLogEntry>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pendingResponse = _controller.ReadPendingConnections();
            if (pendingResponse is TwMessagePendingConnections pending)
            {
                lock (_pendingGuard)
                {
                    _pendingEnabled = pending.Enabled;
                    var newPending = pending.Entries.Where(entry => !_pendingIds.Contains(entry.PendingConnectionId)).ToArray();
                    _pendingIds = pending.Entries.Select(entry => entry.PendingConnectionId).ToHashSet();
                    if (pending.Enabled)
                        return newPending;
                }
            }
            else if (_pendingEnabled || (pendingResponse.Type != MessageType.RESPONSE_ERROR && pendingResponse.Type != MessageType.INVALID_COMMAND))
            {
                throw new InvalidOperationException($"Could not read pending connections ({pendingResponse.Type}).");
            }

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
