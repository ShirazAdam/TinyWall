using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Models;
using ModernTinyWall.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class OptionsTests
{
    private string _directory = null!;
    private string _path = null!;

    [TestInitialize]
    public void Initialise()
    {
        _directory = Path.Combine(Path.GetTempPath(), "TinyWallOptionsTests-" + Guid.NewGuid());
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "options.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (File.Exists(_path)) File.Delete(_path);
        if (File.Exists(_path + ".tmp")) File.Delete(_path + ".tmp");
        Directory.Delete(_directory);
    }

    [TestMethod]
    public async Task OldSettingsDefaultToAllSeries()
    {
        await File.WriteAllTextAsync(_path, "{\"MinimiseToTray\":false,\"CloseToTray\":true}");
        var options = await new OptionsService(_path).LoadAsync();
        Assert.AreEqual(NetworkActivitySeries.All, options.VisibleNetworkSeries);
        Assert.IsFalse(options.MinimiseToTray);
        Assert.IsTrue(options.ShowProtectionCard);
        Assert.IsTrue(options.ShowConnectionsCard);
        Assert.IsTrue(options.ShowBlockedTrafficCard);
    }

    [TestMethod]
    public async Task MissingSettingsDefaultToAllSeries()
    {
        var options = await new OptionsService(_path).LoadAsync();
        Assert.AreEqual(NetworkActivitySeries.All, options.VisibleNetworkSeries);
        Assert.IsTrue(options.ShowProtectionCard);
        Assert.IsTrue(options.ShowConnectionsCard);
        Assert.IsTrue(options.ShowBlockedTrafficCard);
    }

    [TestMethod]
    [DataRow(NetworkActivitySeries.None)]
    [DataRow(NetworkActivitySeries.All)]
    [DataRow(NetworkActivitySeries.LocalUpload | NetworkActivitySeries.TotalDownload)]
    public async Task SelectionsSurviveNewServiceInstance(NetworkActivitySeries selection)
    {
        await new OptionsService(_path).UpdateAsync(options => options with { VisibleNetworkSeries = selection });
        var restored = await new OptionsService(_path).LoadAsync();
        Assert.AreEqual(selection, restored.VisibleNetworkSeries);
        Assert.IsFalse(File.Exists(_path + ".tmp"));
    }

    [TestMethod]
    public async Task ConcurrentPartialUpdatesPreserveUnrelatedSettings()
    {
        var chart = new OptionsService(_path);
        var tray = new OptionsService(_path);
        await Task.WhenAll(
            chart.UpdateAsync(options => options with { VisibleNetworkSeries = NetworkActivitySeries.None }),
            tray.UpdateAsync(options => options with { MinimiseToTray = false, CloseToTray = false }));
        var restored = await chart.LoadAsync();
        Assert.AreEqual(NetworkActivitySeries.None, restored.VisibleNetworkSeries);
        Assert.IsFalse(restored.MinimiseToTray);
        Assert.IsFalse(restored.CloseToTray);
    }

    [TestMethod]
    public async Task DashboardCardSelectionsSurviveReload()
    {
        await new OptionsService(_path).UpdateAsync(options => options with
        {
            ShowProtectionCard = false,
            ShowConnectionsCard = true,
            ShowBlockedTrafficCard = false
        });

        var restored = await new OptionsService(_path).LoadAsync();

        Assert.IsFalse(restored.ShowProtectionCard);
        Assert.IsTrue(restored.ShowConnectionsCard);
        Assert.IsFalse(restored.ShowBlockedTrafficCard);
    }

    [TestMethod]
    public async Task ConnectionHistoryIsOptInAndHasBoundedRetentionDefaults()
    {
        var options = await new OptionsService(_path).LoadAsync();

        Assert.IsFalse(options.CollectConnectionHistory);
        Assert.AreEqual(30, options.ConnectionHistoryRetentionDays);
        Assert.IsFalse(options.OnboardingComplete);
    }

    [TestMethod]
    public async Task ConnectionHistoryAndOnboardingSettingsSurviveReload()
    {
        await new OptionsService(_path).UpdateAsync(options => options with
        {
            CollectConnectionHistory = true,
            ConnectionHistoryRetentionDays = 90,
            OnboardingComplete = true
        });

        var restored = await new OptionsService(_path).LoadAsync();

        Assert.IsTrue(restored.CollectConnectionHistory);
        Assert.AreEqual(90, restored.ConnectionHistoryRetentionDays);
        Assert.IsTrue(restored.OnboardingComplete);
    }
}
