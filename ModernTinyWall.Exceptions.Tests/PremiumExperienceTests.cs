using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Exceptions;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class PremiumExperienceTests
{
    [TestMethod]
    public void ExceptionHealthFlagsBroadDuplicateAndAgeingRulesAsAdvisory()
    {
        var oldDate = DateTime.Now.AddDays(-200).ToString("yyyy/MM/dd HH:mm");
        var row = new ExceptionRow(Guid.NewGuid(), "Example", "Global", "", "Unrestricted", oldDate);

        var finding = ExceptionHealthAnalyzer.GetFinding(row, duplicateSubject: true);

        StringAssert.Contains(finding, "Multiple rules");
        StringAssert.Contains(finding, "applies globally");
        StringAssert.Contains(finding, "broad network access");
        StringAssert.Contains(finding, "over six months old");
        StringAssert.Contains(ExceptionHealthAnalyzer.CreateSummary(1), "advisory");
    }

    [TestMethod]
    public void ExceptionHealthDoesNotTreatLocalOnlyPolicyAsBroad()
    {
        var row = new ExceptionRow(Guid.NewGuid(), "Example", "Executable", @"C:\Example.exe",
            "Unrestricted, local network only", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));

        var finding = ExceptionHealthAnalyzer.GetFinding(row, duplicateSubject: false);

        Assert.AreEqual(string.Empty, finding);
    }

    [TestMethod]
    public void TemporaryExceptionDescriptorShowsRestartExpiry()
    {
        var exception = new FirewallExceptionV3(new ExecutableSubject(@"C:\Example.exe"), new UnrestrictedPolicy())
        {
            Timer = AppExceptionTimer.UNTIL_REBOOT
        };

        var row = ExceptionDescriptor.CreateRow(exception);

        Assert.AreEqual("Until restart", row.Duration);
    }

    [TestMethod]
    [DataRow("127.0.0.1", "Loopback")]
    [DataRow("::ffff:192.168.1.1", "Private")]
    [DataRow("172.31.0.1", "Private")]
    [DataRow("169.254.1.1", "Link-local")]
    [DataRow("100.64.1.1", "Shared")]
    [DataRow("fc00::1", "Unique-local")]
    [DataRow("ff02::1", "Multicast")]
    [DataRow("not an address", "unavailable")]
    [DataRow("8.8.8.8", "not verified")]
    public void EndpointContextDescribesAddressesWithoutExternalLookup(string address, string expected)
    {
        StringAssert.Contains(EndpointContext.Describe(address), expected);
    }

    [TestMethod]
    public void MissingExecutableHasUnavailableSignature()
    {
        StringAssert.Contains(ApplicationSignature.Describe(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())), "unavailable");
    }

    [TestMethod]
    public void HistoryDigestAndTimelineGroupOnlyObservedRecords()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[]
        {
            new ConnectionHistoryEntry("1", "App.exe", "TCP", "a", "1", "b", "2", "Blocked", "Out", now.AddHours(-2), now.AddHours(-1)),
            new ConnectionHistoryEntry("2", "app.exe", "UDP", "a", "1", "c", "3", "Blocked", "Out", now, now),
            new ConnectionHistoryEntry("3", "App.exe", "TCP", "a", "1", "d", "4", "Established", "Out", now, now),
            new ConnectionHistoryEntry("4", "Other.exe", "TCP", "a", "1", "e", "5", "Blocked", "Out", now, now)
        };
        var digest = ConnectionHistorySummary.BlockedByApplication(entries);
        var timeline = ConnectionHistorySummary.ForApplication(entries, "APP.EXE");

        Assert.HasCount(2, digest);
        Assert.AreEqual(2, digest[0].ObservedConnections);
        Assert.AreEqual(now, digest[0].LastSeen);
        Assert.HasCount(3, timeline);
        Assert.AreEqual(now, timeline[0].LastSeen);
    }

    [TestMethod]
    public async Task ConnectionHistoryCoalescesRepeatedObservationsAndSupportsSearch()
    {
        using var directory = new TemporaryDirectory();
        var row = CreateConnectionRow("Example.exe", "203.0.113.8");
        var service = new ConnectionHistoryService(new FakeConnectionsService(), directory.FilePath);

        await service.RecordSnapshotAsync([row]);
        var first = await service.GetEntriesAsync();
        await service.RecordSnapshotAsync([row]);
        var second = await service.GetEntriesAsync("203.0.113.8");

        Assert.HasCount(1, first);
        Assert.HasCount(1, second);
        Assert.AreEqual(first[0].FirstSeen, second[0].FirstSeen);
        Assert.IsTrue(second[0].LastSeen >= first[0].LastSeen);
        Assert.AreEqual("Example.exe", second[0].Application);
    }

    [TestMethod]
    public async Task ConnectionHistoryPrunesEntriesOutsideRetentionWindow()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.GetDirectoryName(directory.FilePath)!);
        var service = new ConnectionHistoryService(new FakeConnectionsService(), directory.FilePath);
        _ = await service.GetEntriesAsync();
        await using (var connection = new SqliteConnection($"Data Source={directory.FilePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO history VALUES (
                    'old-entry', 'Old.exe', 'TCP', '192.168.1.2', '50000',
                    '203.0.113.9', '443', 'Established', 'Out',
                    $lastSeen, $lastSeen);
                """;
            command.Parameters.AddWithValue("$lastSeen", DateTimeOffset.UtcNow.AddDays(-10).ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
        service.Configure(enabled: false, retentionDays: 7);

        await service.RecordSnapshotAsync([]);

        Assert.IsEmpty(await service.GetEntriesAsync());
    }

    private static ConnectionRow CreateConnectionRow(string application, string remoteAddress) =>
        new(application, @"C:\Example.exe", "TCP", "50000", "192.168.1.2", "443",
            remoteAddress, "Established", "Out");

    private sealed class FakeConnectionsService : IConnectionsService
    {
        public Task<IReadOnlyList<ConnectionRow>> GetConnectionsAsync(ConnectionQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConnectionRow>>([]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string FilePath { get; } = Path.Combine(Path.GetTempPath(), "TinyWallHistory-" + Guid.NewGuid(), "history.db");

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            var directory = Path.GetDirectoryName(FilePath);
            if (directory is not null && Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
