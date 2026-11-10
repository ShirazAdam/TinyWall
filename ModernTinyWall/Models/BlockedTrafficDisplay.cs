using ModernTinyWall.TinyWall;
using System;
using System.Globalization;

namespace ModernTinyWall.Models;

internal sealed record BlockedTrafficDisplay(string Count, string Detail)
{
    internal static BlockedTrafficDisplay FromSnapshot(BlockedTrafficSnapshot snapshot, DateOnly today)
    {
        if (snapshot.Error is not null)
            return new("Unavailable", snapshot.Error);
        if (snapshot.Day != today || snapshot.Count < 0 || snapshot.TrackingStarted is null)
            return new("Unavailable", "The service returned invalid or out-of-date blocked traffic history.");
        var detail = $"Observed WFP drop events for {today:yyyy-MM-dd}, not unique connections. Tracking began {snapshot.TrackingStarted.Value.LocalDateTime:g}.";
        if (snapshot.Warning is not null)
            detail += " " + snapshot.Warning;
        return new(snapshot.Count.ToString("N0", CultureInfo.CurrentCulture), detail);
    }
}
