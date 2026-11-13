using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;
using System;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ServiceRecoveryPolicyTests
{
    [TestMethod]
    public void RecoveryStopsAfterThreeFailuresAndManualStartResetsTheCycle()
    {
        var failures = 0;
        for (var cycle = 0; cycle < 2; cycle++)
        {
            if (!ServiceRecoveryPolicy.IsAutomaticStart([]))
                failures = 0;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                failures = ServiceRecoveryPolicy.NextFailure(failures);
                Assert.AreEqual(attempt < 3, ServiceRecoveryPolicy.ShouldRestart(failures));
                if (attempt < 3)
                    Assert.IsTrue(ServiceRecoveryPolicy.IsAutomaticStart([ServiceRecoveryPolicy.AutomaticStartArgument]));
            }
        }
    }

    [TestMethod]
    public void InvalidCounterDoesNotPermitARecovery()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => ServiceRecoveryPolicy.NextFailure(-1));
        Assert.ThrowsExactly<InvalidOperationException>(() => ServiceRecoveryPolicy.NextFailure(4));
        Assert.AreEqual(3, ServiceRecoveryPolicy.NextFailure(3));
    }
}
