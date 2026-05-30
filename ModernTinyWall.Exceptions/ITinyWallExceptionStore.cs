using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions;

public interface ITinyWallExceptionStore
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
