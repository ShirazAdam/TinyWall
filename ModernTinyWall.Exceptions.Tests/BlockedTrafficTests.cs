using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Data.Sqlite;
using ModernTinyWall.Models;
using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class BlockedTrafficTests
{
    private string _directory = null!;
    private string DatabasePath => Path.Combine(_directory, "blocked.db");
    private static readonly DateOnly Today = new(2026, 11, 10);
    private static readonly DateTimeOffset Started = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private readonly List<Exception> _errors = [];

    [TestInitialize]
    public void Initialise()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"TinyWall.Blocked.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, true);

    private BlockedTrafficHistory Start()
    {
        var history = new BlockedTrafficHistory(DatabasePath, _errors.Add);
        history.Start(Started, automaticFlush: false);
        return history;
    }

    private static FirewallLogEntry Entry(DateOnly day, EventLogEvent kind = EventLogEvent.BLOCKED) =>
        new() { Timestamp = day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Local), Event = kind };

    [TestMethod]
    public void CountsOnlyObservedDropsAndSeparatesCalendarDays()
    {
        using var history = Start();
        history.Record(Entry(Today));
        history.Record(Entry(Today));
        history.Record(Entry(Today, EventLogEvent.ALLOWED));
        history.Record(Entry(Today.AddDays(-1)));
        history.Record(Entry(Today.AddDays(1)));
        var snapshot = history.Query(Today);
        Assert.AreEqual(2L, snapshot.Count);
        Assert.AreEqual(1L, history.Query(Today.AddDays(-1)).Count);
        Assert.AreEqual(0L, history.Query(Today.AddDays(-2)).Count);
        Assert.AreEqual(Started, snapshot.TrackingStarted);
        StringAssert.Contains(snapshot.Warning!, "not unique connections");
        Assert.IsNull(snapshot.Error);
        Assert.IsEmpty(_errors);
    }

    [TestMethod]
    public void FlushesOnShutdownAndDoesNotDoubleCountRepeatedReadsOrRestarts()
    {
        using (var history = Start())
        {
            history.Record(Entry(Today));
            Assert.AreEqual(1L, history.Query(Today).Count);
            Assert.AreEqual(1L, history.Query(Today).Count);
            history.Record(Entry(Today));
        }
        using var reopened = Start();
        reopened.Record(Entry(Today));
        var snapshot = reopened.Query(Today);
        Assert.AreEqual(3L, snapshot.Count);
        Assert.AreEqual(Started, snapshot.TrackingStarted);
        Assert.IsFalse(snapshot.Warning!.Contains("interrupted", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ConcurrentCallbackAndQueriesDoNotLoseOrDuplicateEvents()
    {
        using var history = Start();
        Parallel.For(0, 80, index =>
        {
            history.Record(Entry(Today));
            if (index % 10 == 0)
                history.Query(Today);
        });
        Assert.AreEqual(80L, history.Query(Today).Count);
        Assert.IsEmpty(_errors);
    }

    [TestMethod]
    public void UncleanShutdownIsReportedAfterReopening()
    {
        using var first = Start();
        first.Record(Entry(Today));
        first.Query(Today);
        using var second = Start();
        StringAssert.Contains(second.Query(Today).Warning!, "interrupted");
    }

    [TestMethod]
    public void FailedBatchIsRolledBackAndRetriedWithoutLosingOrDuplicatingCounts()
    {
        using var history = Start();
        using var connection = new SqliteConnection($"Data Source={DatabasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TRIGGER reject_test_day BEFORE INSERT ON blocked_days
            WHEN NEW.day = '{Today.AddDays(1):yyyy-MM-dd}'
            BEGIN SELECT RAISE(ABORT, 'Synthetic storage failure'); END;
            """;
        command.ExecuteNonQuery();
        history.Record(Entry(Today));
        history.Record(Entry(Today.AddDays(1)));
        Assert.IsNotNull(history.Query(Today).Error);
        command.CommandText = "SELECT COUNT(*) FROM blocked_days;";
        Assert.AreEqual(0L, (long)command.ExecuteScalar()!);
        command.CommandText = "DROP TRIGGER reject_test_day;";
        command.ExecuteNonQuery();
        var recovered = history.Query(Today);
        Assert.IsNull(recovered.Error);
        Assert.AreEqual(1L, recovered.Count);
        Assert.AreEqual(1L, history.Query(Today.AddDays(1)).Count);
        Assert.AreEqual(1L, history.Query(Today).Count);
        Assert.HasCount(1, _errors);
    }

    [TestMethod]
    public void UtcNotificationsAreAssignedToTheLocalCalendarDay()
    {
        using var history = Start();
        var timestamp = Today.ToDateTime(new TimeOnly(0, 1), DateTimeKind.Local).ToUniversalTime();
        history.Record(new FirewallLogEntry { Timestamp = timestamp, Event = EventLogEvent.BLOCKED });
        Assert.AreEqual(1L, history.Query(Today).Count);
        Assert.AreEqual(0L, history.Query(Today.AddDays(-1)).Count);
    }

    [TestMethod]
    public void StorageFailureIsLoggedAndNeverDisplayedAsZero()
    {
        using var history = new BlockedTrafficHistory(_directory, _errors.Add);
        history.Start(Started, automaticFlush: false);
        var snapshot = history.Query(Today);
        Assert.IsNotNull(snapshot.Error);
        Assert.IsNotEmpty(_errors);
        Assert.AreEqual("Unavailable", BlockedTrafficDisplay.FromSnapshot(snapshot, Today).Count);
    }

    [TestMethod]
    public void ProtocolRoundTripsCountCoverageAndFailure()
    {
        var snapshot = new BlockedTrafficSnapshot(Today, 42, Started, null, "Observed periods only.");
        TwMessage response = new TwMessageBlockedTraffic(snapshot);
        var decoded = (TwMessageBlockedTraffic)SerialisationHelper.Deserialise(
            SerialisationHelper.Serialise(response), TwMessageComError.Instance as TwMessage);
        Assert.AreEqual(snapshot, decoded.Snapshot);
        var request = (TwMessageBlockedTraffic)SerialisationHelper.Deserialise(
            SerialisationHelper.Serialise<TwMessage>(new TwMessageBlockedTraffic(null)), TwMessageComError.Instance as TwMessage);
        Assert.IsNull(request.Snapshot);
        Assert.AreEqual(MessageType.READ_BLOCKED_TRAFFIC, request.Type);
        var failure = snapshot with { Error = "Storage is unavailable." };
        var decodedFailure = (TwMessageBlockedTraffic)SerialisationHelper.Deserialise(
            SerialisationHelper.Serialise<TwMessage>(new TwMessageBlockedTraffic(failure)), TwMessageComError.Instance as TwMessage);
        Assert.AreEqual(failure, decodedFailure.Snapshot);
    }

    [TestMethod]
    public void StaleInvalidAndFailedSnapshotsCannotMasqueradeAsDailyCounts()
    {
        var valid = new BlockedTrafficSnapshot(Today, 42, Started, null, "Partial coverage.");
        Assert.AreEqual("42", BlockedTrafficDisplay.FromSnapshot(valid, Today).Count);
        StringAssert.Contains(BlockedTrafficDisplay.FromSnapshot(valid, Today).Detail, "Partial coverage");
        Assert.AreEqual("Unavailable", BlockedTrafficDisplay.FromSnapshot(valid with { Day = Today.AddDays(-1) }, Today).Count);
        Assert.AreEqual("Unavailable", BlockedTrafficDisplay.FromSnapshot(valid with { Count = -1 }, Today).Count);
        Assert.AreEqual("Unavailable", BlockedTrafficDisplay.FromSnapshot(valid with { TrackingStarted = null }, Today).Count);
        Assert.AreEqual("Unavailable", BlockedTrafficDisplay.FromSnapshot(valid with { Error = "Service unavailable." }, Today).Count);
    }
}
