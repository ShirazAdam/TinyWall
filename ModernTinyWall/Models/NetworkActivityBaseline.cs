using System;
using System.Diagnostics;

namespace ModernTinyWall.Models;

internal sealed class NetworkActivityBaseline
{
    private NetworkTotals? _classified;
    private (long Received, long Sent)? _adapter;
    private long _timestamp;

    internal NetworkActivitySample? RecordClassified(NetworkTotals current, long timestamp)
    {
        NetworkActivitySample? sample = null;
        if (_classified is { } previous && timestamp > _timestamp &&
            previous.TrackingStarted == current.TrackingStarted &&
            current.ReceivedBytes >= previous.ReceivedBytes && current.SentBytes >= previous.SentBytes &&
            current.LocalReceivedBytes >= previous.LocalReceivedBytes && current.LocalSentBytes >= previous.LocalSentBytes)
            sample = NetworkActivitySample.FromTotals(previous, current, Stopwatch.GetElapsedTime(_timestamp, timestamp).TotalSeconds);
        _adapter = null;
        _classified = current;
        _timestamp = timestamp;
        return sample;
    }

    internal NetworkActivitySample? RecordAdapter(long received, long sent, long timestamp)
    {
        NetworkActivitySample? sample = null;
        if (_adapter is { } previous && timestamp > _timestamp && received >= previous.Received && sent >= previous.Sent)
            sample = NetworkActivitySample.FromAdapterTotals(previous.Received, previous.Sent,
                received, sent, Stopwatch.GetElapsedTime(_timestamp, timestamp).TotalSeconds);
        _classified = null;
        _adapter = (received, sent);
        _timestamp = timestamp;
        return sample;
    }

    internal void Reset()
    {
        _classified = null;
        _adapter = null;
    }
}
