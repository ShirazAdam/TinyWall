using System;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Utilities;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class IpAddrMaskTests
{
    [TestMethod]
    [DataRow("192.0.2.1", "192.0.2.1", 32)]
    [DataRow("192.0.2.1/24", "192.0.2.1", 24)]
    [DataRow("2001:db8::1", "2001:db8::1", 128)]
    [DataRow("2001:db8::1/64", "2001:db8::1", 64)]
    public void ParseStringReturnsAddressAndPrefix(string input, string address, int prefix)
    {
        var result = IpAddrMask.Parse(input);
        Assert.AreEqual(IPAddress.Parse(address), result.Address);
        Assert.AreEqual(prefix, result.PrefixLen);
    }

    [TestMethod]
    public void ParseStringRejectsInvalidAddress()
    {
        Assert.ThrowsExactly<FormatException>(() => IpAddrMask.Parse("not-an-address"));
    }
}
