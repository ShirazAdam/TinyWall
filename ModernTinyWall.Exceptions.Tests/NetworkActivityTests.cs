using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Models;
using System;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class NetworkActivityTests
{
    [TestMethod]
    public void SixSeriesUseElapsedTimeAndCombineDirectionsCorrectly()
    {
        var previous = new NetworkTotals(100, 200, 300, 400, null);
        var current = new NetworkTotals(2100, 4200, 6300, 8400, null);
        var sample = NetworkActivitySample.FromTotals(previous, current, 2);
        Assert.AreEqual(1000L, sample.ReceivedBytesPerSecond);
        Assert.AreEqual(2000L, sample.SentBytesPerSecond);
        Assert.AreEqual(3000L, sample.LocalReceivedBytesPerSecond);
        Assert.AreEqual(4000L, sample.LocalSentBytesPerSecond);
        Assert.AreEqual(4000L, sample.TotalReceivedBytesPerSecond);
        Assert.AreEqual(6000L, sample.TotalSentBytesPerSecond);
    }

    [TestMethod]
    public void IdleTrafficProducesZeroInAllSeries()
    {
        var totals = new NetworkTotals(100, 200, 300, 400, null);
        var sample = NetworkActivitySample.FromTotals(totals, totals, 1);
        Assert.AreEqual(0L, sample.TotalReceivedBytesPerSecond);
        Assert.AreEqual(0L, sample.TotalSentBytesPerSecond);
    }

    [TestMethod]
    public void InvalidElapsedTimeIsRejected()
    {
        var totals = new NetworkTotals(0, 0, 0, 0, null);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NetworkActivitySample.FromTotals(totals, totals, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NetworkActivitySample.FromTotals(totals, totals, double.NaN));
    }

    [TestMethod]
    public void AdapterFallbackReportsOnlyUnclassifiedTotals()
    {
        var sample = NetworkActivitySample.FromAdapterTotals(100, 200, 2100, 4200, 2);
        Assert.IsFalse(sample.IsClassified);
        Assert.AreEqual(1000L, sample.TotalReceivedBytesPerSecond);
        Assert.AreEqual(2000L, sample.TotalSentBytesPerSecond);
    }

    [TestMethod]
    public void FallbackAndRecoveryAlwaysTakeFreshBaselines()
    {
        var baseline = new NetworkActivityBaseline();
        var time = System.Diagnostics.Stopwatch.Frequency;
        var totals = new NetworkTotals(100, 200, 300, 400, DateTimeOffset.UtcNow);
        Assert.IsNull(baseline.RecordClassified(totals, time));
        Assert.IsNotNull(baseline.RecordClassified(totals with { ReceivedBytes = 200 }, 2 * time));
        Assert.IsNull(baseline.RecordAdapter(9000, 10000, 3 * time));
        var fallback = baseline.RecordAdapter(10000, 12000, 4 * time);
        Assert.IsNotNull(fallback);
        Assert.IsFalse(fallback.IsClassified);
        Assert.AreEqual(1000L, fallback.TotalReceivedBytesPerSecond);
        Assert.IsNull(baseline.RecordClassified(totals with { ReceivedBytes = 5000 }, 5 * time));
        var recovered = baseline.RecordClassified(totals with { ReceivedBytes = 6000 }, 6 * time);
        Assert.IsNotNull(recovered);
        Assert.IsTrue(recovered.IsClassified);
        Assert.AreEqual(1000L, recovered.ReceivedBytesPerSecond);
    }

    [TestMethod]
    public void RestartsCounterResetsAndResumeNeverProduceSpikes()
    {
        var baseline = new NetworkActivityBaseline();
        var time = System.Diagnostics.Stopwatch.Frequency;
        var totals = new NetworkTotals(1000, 2000, 3000, 4000, DateTimeOffset.UtcNow);
        baseline.RecordClassified(totals, time);
        Assert.IsNull(baseline.RecordClassified(totals with { ReceivedBytes = 1 }, 2 * time));
        Assert.IsNull(baseline.RecordClassified(totals with { TrackingStarted = DateTimeOffset.UtcNow.AddMinutes(1) }, 3 * time));
        baseline.Reset();
        Assert.IsNull(baseline.RecordClassified(totals, 4 * time));
        baseline.RecordAdapter(1000, 2000, 5 * time);
        Assert.IsNull(baseline.RecordAdapter(100, 200, 6 * time));
        Assert.IsNull(baseline.RecordAdapter(100, 200, 6 * time));
        baseline.Reset();
        Assert.IsNull(baseline.RecordAdapter(2000, 3000, 7 * time));
    }
}
