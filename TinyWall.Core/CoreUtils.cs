using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ModernTinyWall.TinyWall;

internal static class Utils
{
    internal static string AppDataPath => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    internal static string ExecutablePath => Environment.ProcessPath ?? AppContext.BaseDirectory;

    internal static string HexEncode(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        Span<byte> randomBytes = stackalloc byte[length];
        RandomNumberGenerator.Fill(randomBytes);

        var builder = new StringBuilder(length);
        foreach (var b in randomBytes)
            builder.Append(chars[b % chars.Length]);

        return builder.ToString();
    }

    internal static void SplitFirstLine(string str, out string firstLine, out string restLines)
    {
        var lines = str.Split([Environment.NewLine], StringSplitOptions.None);

        firstLine = lines[0];
        restLines = string.Empty;

        if (lines.Length <= 1) return;

        restLines = lines[1];
        for (var i = 2; i < lines.Length; ++i)
            restLines += Environment.NewLine + lines[i];
    }
}
