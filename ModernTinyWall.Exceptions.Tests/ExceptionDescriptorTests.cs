using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.TinyWall;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ExceptionDescriptorTests
{
    [TestMethod]
    public void DescribeSubjectWhenExecutableThenReturnsExecutableDetails()
    {
        var subject = new ExecutableSubject(@"C:\Program Files\App\app.exe");

        var result = ExceptionDescriptor.DescribeSubject(subject);

        Assert.AreEqual("app.exe", result.Name);
        Assert.AreEqual("Executable", result.SubjectType);
        Assert.AreEqual(@"C:\Program Files\App\app.exe", result.Details);
    }

    [TestMethod]
    public void DescribeSubjectWhenPackageThenReturnsPackageDetails()
    {
        var subject = new AppContainerSubject("S-1-15-2-1", "PackageName", "Publisher", "PublisherId");

        var result = ExceptionDescriptor.DescribeSubject(subject);

        Assert.AreEqual("PackageName", result.Name);
        Assert.AreEqual("UWP app", result.SubjectType);
        Assert.AreEqual("PublisherId, Publisher", result.Details);
    }

    [TestMethod]
    public void DescribePolicyWhenHardBlockThenReturnsHardBlock()
    {
        var result = ExceptionDescriptor.DescribePolicy(HardBlockPolicy.Instance);

        Assert.AreEqual("Hard block", result);
    }

    [TestMethod]
    public void DescribePolicyWhenTcpAndUdpPortsThenReturnsTcpUdp()
    {
        var policy = new TcpUdpPolicy(true)
        {
            AllowedRemoteTcpConnectPorts = "443",
            AllowedRemoteUdpConnectPorts = "53"
        };

        var result = ExceptionDescriptor.DescribePolicy(policy);

        Assert.AreEqual("TCP/UDP", result);
    }
}
