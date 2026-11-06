using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;
using ModernTinyWall.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class DataUsageTests
{
    private string _directory = null!;
    private string HistoryPath => Path.Combine(_directory, "usage.db");
    private static readonly DateOnly Today = new(2026, 11, 6);

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"TinyWall.DataUsage.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_directory, true);
    }

    [TestMethod]
    [DataRow("10.0.0.1", true)]
    [DataRow("172.16.0.1", true)]
    [DataRow("172.31.255.255", true)]
    [DataRow("172.15.255.255", false)]
    [DataRow("172.32.0.0", false)]
    [DataRow("192.168.1.1", true)]
    [DataRow("169.254.1.1", true)]
    [DataRow("127.0.0.2", true)]
    [DataRow("100.64.0.0", true)]
    [DataRow("100.127.255.255", true)]
    [DataRow("100.63.255.255", false)]
    [DataRow("100.128.0.0", false)]
    [DataRow("224.0.0.251", true)]
    [DataRow("255.255.255.255", true)]
    [DataRow("8.8.8.8", false)]
    [DataRow("::1", true)]
    [DataRow("fe80::1234", true)]
    [DataRow("fc00::1", true)]
    [DataRow("fdff::1", true)]
    [DataRow("ff02::1", true)]
    [DataRow("::ffff:192.168.1.1", true)]
    [DataRow("::ffff:8.8.8.8", false)]
    [DataRow("2001:4860:4860::8888", false)]
    public void ClassifiesRemoteAddresses(string address, bool expected)
    {
        Assert.AreEqual(expected, DataUsageAddressClassifier.IsLocal(IPAddress.Parse(address)));
    }

    [TestMethod]
    public void PersistsAllFourCountersAndCombinesExecutableInstances()
    {
        var history = new DataUsageHistory(HistoryPath);
        var started = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        history.MarkTrackingStarted(started);
        history.Append([
            new(Today, @"C:\Apps\App.exe", 10, 20, 30, 40),
            new(Today, @"c:\apps\app.EXE", 1, 2, 3, 4),
            new(Today, @"C:\Other\App.exe", 5, 0, 0, 0)
        ]);
        history.MarkTrackingStopped();

        var reopened = new DataUsageHistory(HistoryPath);
        reopened.MarkTrackingStarted(started.AddDays(1));
        var snapshot = reopened.Query(DataUsagePeriod.AllTime, Today);
        Assert.AreEqual(started, snapshot.TrackingStarted);
        Assert.IsNull(snapshot.Warning);
        Assert.HasCount(2, snapshot.Applications);
        var application = snapshot.Applications[0];
        Assert.AreEqual(11L, application.InternetReceived);
        Assert.AreEqual(22L, application.InternetSent);
        Assert.AreEqual(33L, application.LocalReceived);
        Assert.AreEqual(44L, application.LocalSent);
        Assert.AreEqual(110L, application.Total);
    }

    [TestMethod]
    public void PeriodsIncludeTodayAndExcludeFutureAndOutsideBoundary()
    {
        var history = new DataUsageHistory(HistoryPath);
        history.Append([
            new(Today.AddDays(1), "app", 1000, 0, 0, 0),
            new(Today, "app", 1, 0, 0, 0),
            new(Today.AddDays(-6), "app", 2, 0, 0, 0),
            new(Today.AddDays(-7), "app", 4, 0, 0, 0),
            new(Today.AddDays(-29), "app", 8, 0, 0, 0),
            new(Today.AddDays(-30), "app", 16, 0, 0, 0)
        ]);
        Assert.AreEqual(1L, history.Query(DataUsagePeriod.Today, Today).Applications.Single().Total);
        Assert.AreEqual(3L, history.Query(DataUsagePeriod.Last7Days, Today).Applications.Single().Total);
        Assert.AreEqual(15L, history.Query(DataUsagePeriod.Last30Days, Today).Applications.Single().Total);
        Assert.AreEqual(31L, history.Query(DataUsagePeriod.AllTime, Today).Applications.Single().Total);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Query((DataUsagePeriod)999, Today));
    }

    [TestMethod]
    public void UnicodeExecutablePathsAreGroupedCaseInsensitivelyAcrossDays()
    {
        var history = new DataUsageHistory(HistoryPath);
        history.Append([
            new(Today, "C:\\Apps\\\u00c5pp.exe", 10, 0, 0, 0),
            new(Today.AddDays(-1), "c:\\apps\\\u00e5PP.EXE", 20, 0, 0, 0)
        ]);
        var snapshot = history.Query(DataUsagePeriod.AllTime, Today);
        Assert.HasCount(1, snapshot.Applications);
        Assert.AreEqual(30L, snapshot.Applications.Single().Total);
    }

    [TestMethod]
    public void RejectsInvalidBatchWithoutPartialWrites()
    {
        var history = new DataUsageHistory(HistoryPath);
        Assert.Throws<ArgumentOutOfRangeException>(() => history.Append([
            new(Today, "valid", 10, 0, 0, 0),
            new(Today, "invalid", -1, 0, 0, 0)
        ]));
        Assert.IsEmpty(history.Query(DataUsagePeriod.AllTime, Today).Applications);
    }

    [TestMethod]
    public void AccumulatesEachDirectionIntoExactlyOneCounter()
    {
        var delta = new DataUsageDelta(Today, "App.exe", 0, 0, 0, 0)
            .Add(100, false, false)
            .Add(200, true, false)
            .Add(300, false, true)
            .Add(400, true, true)
            .Add(0, true, true);
        Assert.AreEqual(100L, delta.InternetReceived);
        Assert.AreEqual(200L, delta.InternetSent);
        Assert.AreEqual(300L, delta.LocalReceived);
        Assert.AreEqual(400L, delta.LocalSent);
        Assert.Throws<ArgumentOutOfRangeException>(() => delta.Add(-1, false, false));
        Assert.Throws<OverflowException>(() => delta.Add(long.MaxValue, false, false));
    }

    [TestMethod]
    public void PersistedCounterOverflowFailsWithoutChangingPriorHistory()
    {
        var history = new DataUsageHistory(HistoryPath);
        history.Append([new(Today, "App.exe", long.MaxValue, 0, 0, 0)]);
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() =>
            history.Append([new(Today, "App.exe", 1, 0, 0, 0)]));
        Assert.AreEqual(long.MaxValue, history.Query(DataUsagePeriod.Today, Today).Applications.Single().Total);
    }

    [TestMethod]
    public void WarningsPersistAcrossRestartAndDetectInterruptedCollection()
    {
        var history = new DataUsageHistory(HistoryPath);
        history.MarkTrackingStarted(DateTimeOffset.Now);
        var reopened = new DataUsageHistory(HistoryPath);
        reopened.MarkTrackingStarted(DateTimeOffset.Now);
        StringAssert.Contains(reopened.Query(DataUsagePeriod.Today, Today).Warning!, "clean shutdown");
        reopened.MarkIncomplete("ETW events lost");
        reopened.MarkTrackingStopped();
        reopened = new DataUsageHistory(HistoryPath);
        reopened.MarkTrackingStarted(DateTimeOffset.Now);
        Assert.AreEqual("ETW events lost", reopened.Query(DataUsagePeriod.Today, Today).Warning);
    }

    [TestMethod]
    public void UsageMessagesRoundTripThroughSharedIpcSerializer()
    {
        TwMessage message = new TwMessageDataUsage(DataUsagePeriod.Last7Days,
            new([new(@"C:\Apps\App.exe", 10, 20, 30, 40)], DateTimeOffset.Now, "Collector stopped", "Partial history"));
        var bytes = SerialisationHelper.Serialise(message);
        var result = SerialisationHelper.Deserialise(bytes, message);
        Assert.IsInstanceOfType<TwMessageDataUsage>(result);
        var usage = (TwMessageDataUsage)result;
        Assert.AreEqual(DataUsagePeriod.Last7Days, usage.Period);
        Assert.AreEqual(100L, usage.Snapshot!.Applications.Single().Total);
        Assert.AreEqual("Collector stopped", usage.Snapshot.Error);
        Assert.AreEqual("Partial history", usage.Snapshot.Warning);

        message = new TwMessageDataUsage(DataUsagePeriod.Today, null);
        result = SerialisationHelper.Deserialise(SerialisationHelper.Serialise(message), message);
        Assert.IsNull(((TwMessageDataUsage)result).Snapshot);
    }

    [TestMethod]
    public async Task ViewFiltersSortsAndShowsAllCounters()
    {
        var snapshot = new DataUsageSnapshot([
            new(@"C:\Apps\Small.exe", 1, 2, 3, 4),
            new(@"C:\Apps\Large.exe", 100, 200, 300, 400)
        ], DateTimeOffset.Now, null, null);
        var viewModel = new DataUsagePageViewModel(_ => Task.FromResult(snapshot));
        await viewModel.RefreshAsync();
        Assert.AreEqual("Large.exe", viewModel.Applications[0].Application);
        Assert.AreEqual("100 B", viewModel.Applications[0].InternetReceived);
        Assert.AreEqual("200 B", viewModel.Applications[0].InternetSent);
        Assert.AreEqual("300 B", viewModel.Applications[0].LocalReceived);
        Assert.AreEqual("400 B", viewModel.Applications[0].LocalSent);
        viewModel.SearchText = "small";
        Assert.HasCount(1, viewModel.Applications);
        StringAssert.Contains(viewModel.Summary, "Total 10 B");
        viewModel.SearchText = @"C:\Apps";
        viewModel.SortIndex = 1;
        Assert.AreEqual("Large.exe", viewModel.Applications[0].Application);
        viewModel.SortIndex = 5;
        Assert.AreEqual("400 B", viewModel.Applications[0].LocalSent);
    }

    [TestMethod]
    public async Task ChangedPeriodDoesNotDisplayStaleResponse()
    {
        var firstResponse = new TaskCompletionSource<DataUsageSnapshot>();
        var calls = new List<DataUsagePeriod>();
        var viewModel = new DataUsagePageViewModel(period =>
        {
            calls.Add(period);
            return calls.Count == 1 ? firstResponse.Task :
                Task.FromResult(new DataUsageSnapshot([new("New.exe", 20, 0, 0, 0)], null, null, null));
        });
        var refresh = viewModel.RefreshAsync();
        viewModel.Period = DataUsagePeriod.AllTime;
        await viewModel.RefreshAsync();
        firstResponse.SetResult(new([new("Old.exe", 10, 0, 0, 0)], null, null, null));
        await refresh;
        CollectionAssert.AreEqual(new[] { DataUsagePeriod.Today, DataUsagePeriod.AllTime }, calls);
        Assert.AreEqual("New.exe", viewModel.Applications.Single().Application);
        Assert.IsFalse(viewModel.IsRefreshing);
    }

    [TestMethod]
    public async Task FailedReadShowsErrorInsteadOfStaleSuccess()
    {
        var fail = false;
        var viewModel = new DataUsagePageViewModel(_ => fail
            ? Task.FromException<DataUsageSnapshot>(new IOException("Service unavailable"))
            : Task.FromResult(new DataUsageSnapshot([new("App.exe", 10, 0, 0, 0)], null, null, null)));
        await viewModel.RefreshAsync();
        fail = true;
        await viewModel.RefreshAsync();
        Assert.IsEmpty(viewModel.Applications);
        Assert.AreEqual("Service unavailable", viewModel.StatusMessage);
        Assert.IsFalse(viewModel.IsRefreshing);
    }

    [TestMethod]
    public async Task CollectorErrorStillDisplaysPersistedHistoryAsIncomplete()
    {
        var viewModel = new DataUsagePageViewModel(_ => Task.FromResult(
            new DataUsageSnapshot([new("App.exe", 10, 0, 0, 0)], null, "Access denied", "ETW events lost")));
        await viewModel.RefreshAsync();
        Assert.HasCount(1, viewModel.Applications);
        StringAssert.Contains(viewModel.StatusMessage, "Access denied");
        StringAssert.Contains(viewModel.StatusMessage, "incomplete");
        StringAssert.Contains(viewModel.StatusMessage, "ETW events lost");
    }

    [TestMethod]
    public void FormatsBinaryUnitsWithExplicitUnits()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.AreEqual("0 B", DataUsageRow.FormatBytes(0));
            Assert.AreEqual("1.00 KiB", DataUsageRow.FormatBytes(1024));
            Assert.AreEqual("1.00 GiB", DataUsageRow.FormatBytes(1024L * 1024 * 1024));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [TestMethod]
    [TestCategory("LiveETW")]
    public async Task ElevatedCollectorRecordsIpv4AndIpv6LoopbackAndPersistsOnStop()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            Assert.Inconclusive("Live ETW requires an elevated Windows test process.");

        var errors = new List<Exception>();
        using (var collector = new DataUsageCollector(HistoryPath,
            ex => { lock (errors) errors.Add(ex); }, $"TinyWall.DataUsage.Test.{Guid.NewGuid():N}"))
        {
            var ready = false;
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (collector.Query(DataUsagePeriod.Today).Error is null)
                {
                    ready = true;
                    break;
                }
                lock (errors)
                    if (errors.Count > 0)
                        Assert.Fail(errors[0].ToString());
                await Task.Delay(100);
            }
            Assert.IsTrue(ready, "Collector did not become ready.");
            foreach (var address in new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
            {
                using var receiver = new UdpClient(new IPEndPoint(address, 0));
                using var sender = new UdpClient(address.AddressFamily);
                var endpoint = (IPEndPoint)receiver.Client.LocalEndPoint!;
                for (var i = 0; i < 10; i++)
                {
                    await sender.SendAsync(new byte[4096], endpoint);
                    var packet = await receiver.ReceiveAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.HasCount(4096, packet.Buffer);
                }
            }
            var timeout = Stopwatch.StartNew();
            DataUsageSnapshot snapshot;
            ApplicationDataUsage? application;
            do
            {
                snapshot = collector.Query(DataUsagePeriod.Today);
                Assert.IsNull(snapshot.Error, snapshot.Error);
                application = snapshot.Applications.SingleOrDefault(row =>
                    string.Equals(row.ApplicationPath, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase));
                if (application is not null && application.LocalSent >= 81920 && application.LocalReceived >= 81920)
                    break;
                await Task.Delay(250);
            }
            while (timeout.Elapsed < TimeSpan.FromSeconds(30));

            var observed = string.Join(", ", snapshot.Applications.Select(row =>
                $"{row.ApplicationPath}: sent={row.LocalSent}, received={row.LocalReceived}"));
            Assert.IsNotNull(application,
                $"No usage recorded for {Environment.ProcessPath}. Observed applications: {observed}");
            Assert.IsGreaterThanOrEqualTo(81920L, application.LocalSent, $"Observed applications: {observed}");
            Assert.IsGreaterThanOrEqualTo(81920L, application.LocalReceived, $"Observed applications: {observed}");
        }
        var saved = new DataUsageHistory(HistoryPath).Query(DataUsagePeriod.Today, DateOnly.FromDateTime(DateTime.Now));
        Assert.IsTrue(saved.Applications.Any(row => row.Total >= 163840L));
        lock (errors)
            Assert.IsEmpty(errors);
    }
}
