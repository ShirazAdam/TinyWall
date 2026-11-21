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

        var result = Verify(path);
        return result is { } signature ? DescribeResult(signature) : "Signature verification unavailable.";
    }

    internal static WinTrust.VerifyResult? Verify(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            return WinTrust.VerifyFileAuthenticode(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            Debug.WriteLine($"Could not verify the application signature: {ex}");
            return null;
        }
    }

    internal static string DescribeResult(WinTrust.VerifyResult result) => result switch
    {
        WinTrust.VerifyResult.SIGNATURE_VALID => "Windows trusts the embedded signature using cached revocation data. This does not guarantee the application is safe.",
        WinTrust.VerifyResult.SIGNATURE_MISSING => "No embedded digital signature was found. Catalogue-only signatures are not checked.",
        _ => "The file's embedded signature could not be verified as trusted."
    };
}
