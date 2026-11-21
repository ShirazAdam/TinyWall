using ModernTinyWall.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModernTinyWall.Models;

internal sealed record BlockedApplicationSummary(string Application, int ObservedConnections, DateTimeOffset LastSeen);

internal static class ConnectionHistorySummary
{
    internal static IReadOnlyList<BlockedApplicationSummary> BlockedByApplication(IEnumerable<ConnectionHistoryEntry> entries) =>
        entries.Where(entry => string.Equals(entry.State, "Blocked", StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Application, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BlockedApplicationSummary(group.Key, group.Count(), group.Max(entry => entry.LastSeen)))
            .OrderByDescending(group => group.ObservedConnections)
            .ThenBy(group => group.Application, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    internal static IReadOnlyList<ConnectionHistoryEntry> ForApplication(
        IEnumerable<ConnectionHistoryEntry> entries, string application) =>
        entries.Where(entry => string.Equals(entry.Application, application, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.LastSeen)
            .ToArray();
}
