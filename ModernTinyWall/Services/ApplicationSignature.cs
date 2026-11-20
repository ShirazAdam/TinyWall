using ModernTinyWall.Windows;
using System;
using System.Diagnostics;
using System.IO;

namespace ModernTinyWall.Services;

internal static class ApplicationSignature
{
    internal static string Describe(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return "Signature unavailable (file not found).";

        try
        {
            return WinTrust.VerifyFileAuthenticode(path) == WinTrust.VerifyResult.SIGNATURE_VALID
                ? "Windows trusts the embedded signature using cached revocation data. This does not guarantee the application is safe."
                : "The file's embedded signature could not be verified as trusted.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            Debug.WriteLine($"Could not verify the application signature: {ex}");
            return "Signature verification unavailable.";
        }
    }
}
