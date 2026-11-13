using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using ModernTinyWall.TinyWall;

namespace ModernTinyWall.Exceptions;

public sealed record ExceptionQuery(string SearchText);

public sealed record ExceptionRow(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created,
    string IconPath = "", string PackageFamilyName = "");

public sealed record ExceptionMutationResult(bool Success, string Message);

public enum ExceptionEntryKind
{
    Executable,
    Service,
    Package
}

public enum ExceptionEntryPolicy
{
    Allow,
    Block
}

public enum FirewallPromptDecision
{
    AllowOnce,
    AllowAlways,
    DenyOnce,
    DenyAlways,
    AllowDestinationAlways
}

public static class FirewallPromptValidation
{
    public static bool TryGetDestination(FirewallLogEntry entry, [NotNullWhen(true)] out string? path, [NotNullWhen(true)] out IPAddress? address)
    {
        path = entry.AppPath;
        address = null;
        return !string.IsNullOrWhiteSpace(path)
            && !string.Equals(path, "System", StringComparison.OrdinalIgnoreCase)
            && IPAddress.TryParse(entry.RemoteIp, out address);
    }
}

public sealed record ExceptionEntryActionRequest(
    ExceptionEntryKind Kind,
    ExceptionEntryPolicy Policy,
    string Name,
    string Details,
    string PublisherId = "",
    string Publisher = "");

public sealed record ExceptionActionResult(bool Success, bool ExistingExceptionFound, string Message, string ExistingPolicy = "");

public sealed record ExceptionEditRequest(
    string SubjectType,
    string Name,
    string Details,
    string Policy,
    string RemoteTcpPorts = "",
    string LocalTcpPorts = "",
    string RemoteUdpPorts = "",
    string LocalUdpPorts = "");
