using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Models;
using System;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class UiPreviewTests
{
    [TestMethod]
    public void PreviewOverridesArePerLaunchAndDoNotAlterDefaults()
    {
        var preview = UiPreviewOptions.Parse(["--preview-theme=light", "--preview-reduced-motion", "--preview-size=1024x768"]);
        Assert.AreEqual(PreviewTheme.Light, preview.Theme);
        Assert.AreEqual(1024, preview.Width);
        Assert.AreEqual(768, preview.Height);
        Assert.IsFalse(preview.AnimationsAllowed(true, false));
        Assert.AreEqual(new UiPreviewOptions(), UiPreviewOptions.Parse([]));
        Assert.AreEqual(PreviewTheme.Dark, UiPreviewOptions.Parse(["--preview-theme=dark"]).Theme);
    }

    [TestMethod]
    public void SystemAccessibilitySettingsAlwaysTakePrecedence()
    {
        var options = UiPreviewOptions.Parse([]);
        Assert.IsTrue(options.AnimationsAllowed(true, false));
        Assert.IsFalse(options.AnimationsAllowed(false, false));
        Assert.IsFalse(options.AnimationsAllowed(true, true));
    }

    [TestMethod]
    [DataRow("--preview-theme=blue")]
    [DataRow("--preview-size=0x768")]
    [DataRow("--preview-size=1024x-1")]
    [DataRow("--preview-size=1024x768x2")]
    [DataRow("--preview-size=99999x768")]
    [DataRow("--preview-unknown")]
    public void InvalidPreviewArgumentsAreRejected(string argument) =>
        Assert.ThrowsExactly<ArgumentException>(() => UiPreviewOptions.Parse([argument]));
}
