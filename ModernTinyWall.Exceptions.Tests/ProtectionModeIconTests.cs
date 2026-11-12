using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Services;
using ModernTinyWall.TinyWall;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ProtectionModeIconTests
{
    [TestMethod]
    public void ProtectionModesHaveDistinctMultiResolutionIcons()
    {
        var modes = new[] { FirewallMode.Normal, FirewallMode.AllowOutgoing, FirewallMode.BlockAll, FirewallMode.Learning, FirewallMode.Disabled };
        var hashes = modes.Select(mode =>
        {
            var bytes = File.ReadAllBytes(ProtectionModeIcon.GetPath(mode));
            Assert.AreEqual((ushort)1, BitConverter.ToUInt16(bytes, 2), $"Invalid icon for {mode}.");
            var count = BitConverter.ToUInt16(bytes, 4);
            var sizes = Enumerable.Range(0, count).Select(i => bytes[6 + i * 16] == 0 ? 256 : bytes[6 + i * 16]).ToArray();
            CollectionAssert.IsSubsetOf(new[] { 16, 32, 48, 256 }, sizes);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }).ToArray();

        Assert.AreEqual(modes.Length, hashes.Distinct().Count());
    }

    [TestMethod]
    public void UnknownOrUnrecognisedModeUsesDisabledIcon()
    {
        Assert.AreEqual(ProtectionModeIcon.GetPath(FirewallMode.Disabled), ProtectionModeIcon.GetPath(FirewallMode.Unknown));
        Assert.AreEqual(ProtectionModeIcon.GetPath(FirewallMode.Disabled), ProtectionModeIcon.GetPath((FirewallMode)999));
        Assert.AreNotEqual(ProtectionModeIcon.GetPath(FirewallMode.Normal), ProtectionModeIcon.GetPath(FirewallMode.Unknown));
    }
}
