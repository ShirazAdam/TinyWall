using System;
using System.Diagnostics;
using System.IO;
using System.Security;

namespace ModernTinyWall.Services;

internal sealed record ApplicationDetails(string Name, string Publisher, string Description)
{
    internal static ApplicationDetails Read(string executablePath)
    {
        try
        {
            var version = FileVersionInfo.GetVersionInfo(executablePath);
            return FromMetadata(executablePath, version.ProductName, version.CompanyName, version.FileDescription);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            Debug.WriteLine($"Could not read application details for '{executablePath}': {ex}");
            return FromMetadata(executablePath, null, null, null);
        }
    }

    internal static ApplicationDetails FromMetadata(string executablePath, string? productName, string? companyName, string? description)
    {
        var fallback = string.IsNullOrWhiteSpace(executablePath)
            ? "Unknown application"
            : Path.GetFileNameWithoutExtension(executablePath);
        return new ApplicationDetails(
            FirstAvailable(productName, description, fallback, "Unknown application"),
            FirstAvailable(companyName, "Unavailable"),
            FirstAvailable(description, "Unavailable"));
    }

    private static string FirstAvailable(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "Unavailable";
    }
}
