using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Windows;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ModernClientStartupTests
{
    [TestMethod]
    public void StartupArgumentIsRecognisedWithoutAffectingNormalLaunches()
    {
        Assert.IsTrue(ModernClientStartup.IsStartup(["--STARTUP"]));
        Assert.IsFalse(ModernClientStartup.IsStartup([]));
        Assert.IsFalse(ModernClientStartup.IsStartup(["--preview-theme=dark"]));
        Assert.IsFalse(ModernClientStartup.IsStartup(["--startup-extra"]));
    }

    [TestMethod]
    public void ControllerPathIsAlongsideServiceExecutable()
    {
        Assert.AreEqual(@"C:\Program Files\TinyWall\ModernTinyWall.exe",
            ModernClientStartup.GetExecutablePath(@"C:\Program Files\TinyWall\TinyWall.exe"));
        Assert.ThrowsExactly<System.ArgumentException>(() => ModernClientStartup.GetExecutablePath("TinyWall.exe"));
    }
}
