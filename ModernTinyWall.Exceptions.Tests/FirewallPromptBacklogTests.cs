using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using System;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class FirewallPromptBacklogTests
{
    [TestMethod]
    public void SuccessfulApplicationAllowDiscardsOnlyOlderLogEventsForThatApplication()
    {
        var cutoff = new DateTime(2026, 11, 7, 12, 0, 0);
        var backlog = new FirewallPromptBacklog();
        backlog.MarkApplicationAllowed(@"C:\Apps\App.exe", cutoff);

        Assert.IsTrue(backlog.IsObsolete(new FirewallLogEntry { AppPath = @"c:\apps\APP.exe", Timestamp = cutoff.AddSeconds(-1) }));
        Assert.IsFalse(backlog.IsObsolete(new FirewallLogEntry { AppPath = @"C:\Apps\App.exe", Timestamp = cutoff.AddSeconds(1) }));
        Assert.IsFalse(backlog.IsObsolete(new FirewallLogEntry { AppPath = @"C:\Apps\Other.exe", Timestamp = cutoff.AddSeconds(-1) }));
        Assert.IsFalse(backlog.IsObsolete(new FirewallLogEntry { AppPath = @"C:\Apps\App.exe", Timestamp = cutoff.AddSeconds(-1), PendingConnectionId = 1 }));
    }
}
