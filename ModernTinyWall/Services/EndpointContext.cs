using System;
using System.Net;
using System.Net.Sockets;

namespace ModernTinyWall.Services;

internal static class EndpointContext
{
    internal static string Describe(string? address)
    {
        if (!IPAddress.TryParse(address, out var ip))
            return "Address classification unavailable.";
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip))
            return "Loopback address (this device).";
        if (ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
            return "Unspecified address.";

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            if (bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
                || bytes[0] == 192 && bytes[1] == 168)
                return "Private IPv4 address.";
            if (bytes[0] == 169 && bytes[1] == 254)
                return "Link-local IPv4 address.";
            if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                return "Shared IPv4 address (carrier-grade NAT).";
            if (bytes[0] is >= 224 and <= 239)
                return "Multicast IPv4 address.";
        }
        else
        {
            if (ip.IsIPv6LinkLocal)
                return "Link-local IPv6 address.";
            if (ip.IsIPv6Multicast)
                return "Multicast IPv6 address.";
            if ((bytes[0] & 0xfe) == 0xfc)
                return "Unique-local IPv6 address.";
        }

        return "Other IP address; location and ownership are not verified.";
    }
}
