using System;

namespace ModernTinyWall.Models;

public sealed record NetworkActivitySample(long ReceivedBytesPerSecond, long SentBytesPerSecond,
    long LocalReceivedBytesPerSecond, long LocalSentBytesPerSecond)
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
    public bool IsClassified { get; init; } = true;
    public long? AdapterReceivedBytesPerSecond { get; init; }
    public long? AdapterSentBytesPerSecond { get; init; }
    public long TotalReceivedBytesPerSecond => AdapterReceivedBytesPerSecond ?? checked(ReceivedBytesPerSecond + LocalReceivedBytesPerSecond);
    public long TotalSentBytesPerSecond => AdapterSentBytesPerSecond ?? checked(SentBytesPerSecond + LocalSentBytesPerSecond);

    internal static NetworkActivitySample FromAdapterTotals(long previousReceived, long previousSent,
        long received, long sent, double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        return new NetworkActivitySample(0, 0, 0, 0)
        {
            IsClassified = false,
            AdapterReceivedBytesPerSecond = (long)(Math.Max(0, received - previousReceived) / elapsedSeconds),
            AdapterSentBytesPerSecond = (long)(Math.Max(0, sent - previousSent) / elapsedSeconds)
        };
    }

    internal static NetworkActivitySample FromTotals(NetworkTotals previous, NetworkTotals current, double elapsedSeconds)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        return new NetworkActivitySample(
            (long)(Math.Max(0, current.ReceivedBytes - previous.ReceivedBytes) / elapsedSeconds),
            (long)(Math.Max(0, current.SentBytes - previous.SentBytes) / elapsedSeconds),
            (long)(Math.Max(0, current.LocalReceivedBytes - previous.LocalReceivedBytes) / elapsedSeconds),
            (long)(Math.Max(0, current.LocalSentBytes - previous.LocalSentBytes) / elapsedSeconds));
    }
}

internal sealed record NetworkTotals(long ReceivedBytes, long SentBytes, long LocalReceivedBytes,
    long LocalSentBytes, DateTimeOffset? TrackingStarted);
