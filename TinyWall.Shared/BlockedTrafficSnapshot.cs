using System;

namespace ModernTinyWall.TinyWall;

public sealed record BlockedTrafficSnapshot(
    DateOnly Day,
    long Count,
    DateTimeOffset? TrackingStarted,
    string? Error,
    string? Warning);
