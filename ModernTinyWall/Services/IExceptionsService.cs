using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IExceptionsService
{
    Task<IReadOnlyList<ExceptionRow>> GetExceptionsAsync(ExceptionQuery query, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> AddExceptionAsync(ExceptionEditRequest request, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> AddExecutableExceptionsAsync(string executablePath, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> AddServiceExceptionAsync(string executablePath, string serviceName, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> AddPackageExceptionAsync(string packageSid, string displayName, string publisherId, string publisher, CancellationToken cancellationToken = default);
    Task<ExceptionActionResult> PrepareEntryActionAsync(ExceptionEntryActionRequest request, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> ApplyEntryActionAsync(ExceptionEntryActionRequest request, bool replaceExisting, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> UpdateExceptionAsync(Guid exceptionId, ExceptionEditRequest request, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> RemoveExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> RemoveAllExceptionsAsync(CancellationToken cancellationToken = default);
}

internal sealed record ExceptionQuery(string SearchText);

internal sealed record ExceptionRow(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created);

internal sealed record ExceptionMutationResult(bool Success, string Message);

internal enum ExceptionEntryKind
{
    Executable,
    Service,
    Package
}

internal enum ExceptionEntryPolicy
{
    Allow,
    Block
}

internal sealed record ExceptionEntryActionRequest(
    ExceptionEntryKind Kind,
    ExceptionEntryPolicy Policy,
    string Name,
    string Details,
    string PublisherId = "",
    string Publisher = "");

internal sealed record ExceptionActionResult(bool Success, bool ExistingExceptionFound, string Message, string ExistingPolicy = "");

internal sealed record ExceptionEditRequest(
    string SubjectType,
    string Name,
    string Details,
    string Policy,
    string RemoteTcpPorts = "",
    string LocalTcpPorts = "",
    string RemoteUdpPorts = "",
    string LocalUdpPorts = "");
