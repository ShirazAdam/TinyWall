using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;

namespace ModernTinyWall.Services;

internal sealed record ApplicationFileTarget(string Name, string FilePath, string Context = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(FilePath) ? Name : FilePath;
}

internal sealed record ApplicationFileInformation(IReadOnlyList<KeyValuePair<string, string>> Fields, string? Error)
{
    internal static IReadOnlyList<ApplicationFileTarget> DistinctFiles(IEnumerable<ApplicationFileTarget> targets) =>
        targets.DistinctBy(target => string.IsNullOrWhiteSpace(target.FilePath)
            ? $"context:{target.Name}|{target.Context}" : $"file:{target.FilePath}", StringComparer.OrdinalIgnoreCase).ToArray();

    internal static ApplicationFileInformation Read(ApplicationFileTarget target)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("Application", target.Name),
            new("File path", string.IsNullOrWhiteSpace(target.FilePath) ? "Unavailable" : target.FilePath)
        };
        if (!string.IsNullOrWhiteSpace(target.Context))
            fields.Add(new("Row information", target.Context));
        if (string.IsNullOrWhiteSpace(target.FilePath))
            return new(fields, "This row does not identify an executable file. File metadata and signature status are unavailable.");

        try
        {
            var file = new FileInfo(target.FilePath);
            fields.Add(new("File name", file.Name));
            var version = FileVersionInfo.GetVersionInfo(file.FullName);
            fields.Add(new("Publisher (unverified file metadata)", Available(version.CompanyName)));
            fields.Add(new("Digitally signed (embedded signature)", ApplicationSignature.Describe(file.FullName)));
            fields.Add(new("Size", $"{file.Length.ToString("N0", CultureInfo.CurrentCulture)} bytes"));
            fields.Add(new("Created", file.CreationTime.ToString("G")));
            fields.Add(new("Modified", file.LastWriteTime.ToString("G")));
            fields.Add(new("Attributes", file.Attributes.ToString()));
            fields.Add(new("Description", Available(version.FileDescription)));
            fields.Add(new("Product name", Available(version.ProductName)));
            fields.Add(new("File version", Available(version.FileVersion)));
            fields.Add(new("Product version", Available(version.ProductVersion)));
            fields.Add(new("Original file name", Available(version.OriginalFilename)));
            fields.Add(new("Copyright", Available(version.LegalCopyright)));
            return new(fields, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            Debug.WriteLine($"Could not read additional file information for '{target.FilePath}': {ex}");
            return new(fields, $"Could not read file information: {ex.Message}");
        }
    }

    private static string Available(string? value) => string.IsNullOrWhiteSpace(value) ? "Unavailable" : value.Trim();
}
