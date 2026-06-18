using System;

namespace ModernTinyWall.Exceptions;

public sealed record ExceptionQuery(string SearchText);

public sealed record ExceptionRow(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created);

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
