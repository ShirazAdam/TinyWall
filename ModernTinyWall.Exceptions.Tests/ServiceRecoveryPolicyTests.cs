using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ServiceRecoveryPolicyTests
{
    [TestMethod]
    public void TwoAutomaticRestartsDisableRecoveryUntilManualStartBeginsANewCycle()
    {
        var running = false;
        var restarts = 0;
        for (var cycle = 0; cycle < 3; cycle++)
        {
            // Manual start, followed by Windows' two automatic restarts.
            var start = ServiceRecoveryPolicy.OnStart(running, restarts);
            Assert.AreEqual(0, start.Restarts);
            Assert.IsTrue(start.RestartOnFailure);
            (running, restarts) = (true, start.Restarts);

            for (var restart = 1; restart <= 2; restart++)
            {
                Assert.IsFalse(ServiceRecoveryPolicy.LimitReached(running, restarts));
                start = ServiceRecoveryPolicy.OnStart(running, restarts);
                Assert.AreEqual(restart < 2, start.RestartOnFailure);
                Assert.AreEqual(restart, start.Restarts);
                restarts = start.Restarts;
            }

            // The third failure leaves the service stopped.
            Assert.IsTrue(ServiceRecoveryPolicy.LimitReached(running, restarts));
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void StartAfterACleanStopReenablesRecovery(int restarts)
    {
        var start = ServiceRecoveryPolicy.OnStart(previousRunEndedUnexpectedly: false, restarts);
        Assert.IsTrue(start.RestartOnFailure);
        Assert.AreEqual(0, start.Restarts);
        Assert.IsFalse(ServiceRecoveryPolicy.LimitReached(false, 2));
    }

    [TestMethod]
    public void InvalidRestartCountBeginsANewCycle()
    {
        var start = ServiceRecoveryPolicy.OnStart(true, -1);
        Assert.IsTrue(start.RestartOnFailure);
        Assert.AreEqual(0, start.Restarts);
    }
}
