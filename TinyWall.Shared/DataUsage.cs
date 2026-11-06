using System;
using System.Net;
using System.Net.Sockets;

namespace ModernTinyWall.TinyWall;

public enum DataUsagePeriod
{
    Today,
    Last7Days,
    Last30Days,
    AllTime
}

public sealed record ApplicationDataUsage(
    string ApplicationPath,
    long InternetReceived,
    long InternetSent,
    long LocalReceived,
    long LocalSent)
{
    public const string SystemPath = "[System]";
    public const string UnattributedPath = "[Unattributed]";
    public long Total => checked(InternetReceived + InternetSent + LocalReceived + LocalSent);
}

public sealed record DataUsageSnapshot(
    ApplicationDataUsage[] Applications,
    DateTimeOffset? TrackingStarted,
    string? Error,
    string? Warning);

public static class DataUsageAddressClassifier
{
    public static bool IsLocal(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                bytes[0] == 10 ||
                (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                bytes[0] is >= 224 and <= 239 ||
                address.Equals(IPAddress.Broadcast);
        }

        return address.Equals(IPAddress.IPv6Any) ||
            address.IsIPv6LinkLocal ||
            address.IsIPv6SiteLocal ||
            address.IsIPv6Multicast ||
            (bytes[0] & 0xfe) == 0xfc;
    }
}
