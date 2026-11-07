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
}
