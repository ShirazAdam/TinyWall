using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Models;
using global::Windows.Graphics;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class WindowPlacementTests
{
    [TestMethod]
    [DataRow(0, 0, 1920, 1040)]
    [DataRow(-1920, 40, 1920, 1040)]
    [DataRow(2560, -1440, 2560, 1400)]
    [DataRow(0, 0, 800, 600)]
    [DataRow(10, 20, 20, 25)]
    public void StartsCentredAndEntirelyInsideTheMonitorWorkArea(int x, int y, int width, int height)
    {
        var bounds = WindowPlacement.Centre(new RectInt32(x, y, width, height), new SizeInt32(1536, 1024));
        Assert.IsGreaterThanOrEqualTo(x, bounds.X);
        Assert.IsGreaterThanOrEqualTo(y, bounds.Y);
        Assert.IsLessThanOrEqualTo(x + width, bounds.X + bounds.Width);
        Assert.IsLessThanOrEqualTo(y + height, bounds.Y + bounds.Height);
        Assert.IsLessThanOrEqualTo(1, System.Math.Abs((bounds.X - x) - (x + width - bounds.X - bounds.Width)));
        Assert.IsLessThanOrEqualTo(1, System.Math.Abs((bounds.Y - y) - (y + height - bounds.Y - bounds.Height)));
        Assert.AreEqual(System.Math.Min(1536, System.Math.Max(1, width - 32)), bounds.Width);
        Assert.AreEqual(System.Math.Min(1024, System.Math.Max(1, height - 32)), bounds.Height);
    }

    [TestMethod]
    public void PreviewSizeUsesPhysicalPixelsAndDoesNotExceedTheWorkArea()
    {
        var bounds = WindowPlacement.Centre(new RectInt32(1920, 40, 2560, 1400), new SizeInt32(1024, 768));
        Assert.AreEqual(2688, bounds.X);
        Assert.AreEqual(356, bounds.Y);
        Assert.AreEqual(1024, bounds.Width);
        Assert.AreEqual(768, bounds.Height);
    }
}
