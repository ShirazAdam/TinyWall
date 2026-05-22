using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IExceptionsService
{
    Task<IReadOnlyList<ExceptionRow>> GetExceptionsAsync(ExceptionQuery query, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> AddExceptionAsync(ExceptionEditRequest request, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> UpdateExceptionAsync(Guid exceptionId, ExceptionEditRequest request, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> RemoveExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default);
    Task<ExceptionMutationResult> RemoveAllExceptionsAsync(CancellationToken cancellationToken = default);
}

internal sealed record ExceptionQuery(string SearchText);

internal sealed record ExceptionRow(Guid Id, string Name, string SubjectType, string Details, string Policy, string Created);

internal sealed record ExceptionMutationResult(bool Success, string Message);

internal sealed record ExceptionEditRequest(string SubjectType, string Name, string Details, string Policy);
