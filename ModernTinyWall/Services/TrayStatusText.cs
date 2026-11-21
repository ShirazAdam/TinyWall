namespace ModernTinyWall.Services;

internal static class TrayStatusText
{
    internal static string Compact(string text)
    {
        const int maximumLength = 48;
        var status = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (status.Length <= maximumLength)
            return status;

        var length = maximumLength - 3;
        if (char.IsHighSurrogate(status[length - 1]))
            length--;
        return status[..length].TrimEnd() + "...";
    }
}
