using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ControllerClientIdentityTests
{
    [TestMethod]
    [DataRow(@"C:\Program Files\TinyWall\TinyWall.exe", true)]
    [DataRow(@"C:\Program Files\TinyWall\ModernTinyWall.exe", true)]
    [DataRow(@"c:\program files\tinywall\moderntinywall.exe", true)]
    [DataRow(@"C:\Other\ModernTinyWall.exe", false)]
    [DataRow(@"C:\Program Files\TinyWall\Other.exe", false)]
    [DataRow(@"C:\Program Files\TinyWall\Subfolder\ModernTinyWall.exe", false)]
    [DataRow(@"C:\Program Files\TinyWall2\ModernTinyWall.exe", false)]
    public void OnlyColocatedClientsAreTrusted(string client, bool expected)
    {
        Assert.AreEqual(expected, ControllerClientIdentity.IsTrusted(@"C:\Program Files\TinyWall\TinyWall.exe", client));
    }
}
