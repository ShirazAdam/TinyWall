using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModernTinyWall.Services;
using System.IO;

namespace ModernTinyWall.Exceptions.Tests;

[TestClass]
public sealed class ApplicationDetailsTests
{
    [TestMethod]
    public void MetadataUsesProductPublisherAndDescription()
    {
        var details = ApplicationDetails.FromMetadata(@"C:\Apps\example.exe", " Example ", " Publisher ", " Description ");
        Assert.AreEqual("Example", details.Name);
        Assert.AreEqual("Publisher", details.Publisher);
        Assert.AreEqual("Description", details.Description);
    }

    [TestMethod]
    public void MissingMetadataUsesDescriptionOrFileNameAndShowsUnavailableFields()
    {
        var details = ApplicationDetails.FromMetadata(@"C:\Apps\example.exe", null, " ", "Example description");
        Assert.AreEqual("Example description", details.Name);
        Assert.AreEqual("Unavailable", details.Publisher);
        Assert.AreEqual("Example description", details.Description);

        details = ApplicationDetails.Read(Path.Combine(Path.GetTempPath(), "missing-application-details-test", "example.exe"));
        Assert.AreEqual("example", details.Name);
        Assert.AreEqual("Unavailable", details.Publisher);
        Assert.AreEqual("Unavailable", details.Description);

        Assert.AreEqual("Unknown application", ApplicationDetails.FromMetadata("", null, null, null).Name);
    }
}
