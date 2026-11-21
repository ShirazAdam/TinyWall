using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Services;
using ModernTinyWall.Windows;
using System;
using System.IO;
using System.Linq;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ApplicationFileInformationTests
{
    [TestMethod]
    public void ExistingUnsignedFileShowsMetadataWithoutClaimingTrust()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tinywall-info-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "test");

            var information = ApplicationFileInformation.Read(new("Test application", path));
            var fields = information.Fields.ToDictionary(field => field.Key, field => field.Value);

            Assert.IsNull(information.Error);
            Assert.AreEqual(Path.GetFileName(path), fields["File name"]);
            Assert.AreEqual(path, fields["File path"]);
            Assert.AreEqual("4 bytes", fields["Size"]);
            Assert.AreEqual("Unavailable", fields["Publisher (unverified file metadata)"]);
            Assert.AreNotEqual(ApplicationSignature.DescribeResult(WinTrust.VerifyResult.SIGNATURE_VALID),
                fields["Digitally signed (embedded signature)"]);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fields["Modified"]));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void MissingFileKeepsTheRequestedPathAndReportsAnError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");

        var information = ApplicationFileInformation.Read(new("Missing application", path));

        StringAssert.Contains(information.Error!, "Could not read file information");
        Assert.AreEqual(path, information.Fields.Single(field => field.Key == "File path").Value);
        Assert.IsFalse(information.Fields.Any(field => field.Key.StartsWith("Digitally signed")));
    }

    [TestMethod]
    public void RowWithoutExecutableShowsContextWithoutInferringAFile()
    {
        var information = ApplicationFileInformation.Read(new("Example.exe", "", "History has no executable path."));

        StringAssert.Contains(information.Error!, "does not identify an executable");
        Assert.AreEqual("Unavailable", information.Fields.Single(field => field.Key == "File path").Value);
        Assert.AreEqual("History has no executable path.", information.Fields.Single(field => field.Key == "Row information").Value);
    }

    [TestMethod]
    public void GroupsKeepDifferentPathsAndCoalesceRepeatedFiles()
    {
        var files = ApplicationFileInformation.DistinctFiles(new[]
        {
            new ApplicationFileTarget("Example (1)", @"C:\One\Example.exe"),
            new ApplicationFileTarget("Example (2)", @"c:\one\example.exe"),
            new ApplicationFileTarget("Example (3)", @"C:\Two\Example.exe")
        });

        Assert.HasCount(2, files);
        Assert.AreEqual(@"C:\One\Example.exe", files[0].FilePath);
        Assert.AreEqual(@"C:\Two\Example.exe", files[1].FilePath);
    }

    [TestMethod]
    public void SignatureResultsDistinguishMissingUntrustedAndTrusted()
    {
        StringAssert.Contains(ApplicationSignature.DescribeResult(WinTrust.VerifyResult.SIGNATURE_MISSING), "No embedded");
        StringAssert.Contains(ApplicationSignature.DescribeResult(WinTrust.VerifyResult.SIGNATURE_INVALID), "could not be verified");
        StringAssert.Contains(ApplicationSignature.DescribeResult(WinTrust.VerifyResult.SIGNATURE_VALID), "cached revocation");
    }
}
