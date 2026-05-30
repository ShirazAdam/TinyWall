using ModernTinyWall.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class ExceptionsService : IExceptionsService
{
    private readonly ITinyWallExceptionStore _exceptionStore;

    public ExceptionsService()
        : this(new TinyWallExceptionStore())
    {
    }

    internal ExceptionsService(ITinyWallExceptionStore exceptionStore)
    {
        ArgumentNullException.ThrowIfNull(exceptionStore);

        _exceptionStore = exceptionStore;
    }

    public Task<IReadOnlyList<ExceptionRow>> GetExceptionsAsync(ExceptionQuery query, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.GetExceptionsAsync(query, cancellationToken);
    }

    public Task<ExceptionMutationResult> AddExceptionAsync(ExceptionEditRequest request, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.AddExceptionAsync(request, cancellationToken);
    }

    public Task<ExceptionMutationResult> AddExecutableExceptionsAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.AddExecutableExceptionsAsync(executablePath, cancellationToken);
    }

    public Task<ExceptionMutationResult> AddServiceExceptionAsync(string executablePath, string serviceName, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.AddServiceExceptionAsync(executablePath, serviceName, cancellationToken);
    }

    public Task<ExceptionMutationResult> AddPackageExceptionAsync(string packageSid, string displayName, string publisherId, string publisher, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.AddPackageExceptionAsync(packageSid, displayName, publisherId, publisher, cancellationToken);
    }

    public Task<ExceptionActionResult> PrepareEntryActionAsync(ExceptionEntryActionRequest request, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.PrepareEntryActionAsync(request, cancellationToken);
    }

    public Task<ExceptionMutationResult> ApplyEntryActionAsync(ExceptionEntryActionRequest request, bool replaceExisting, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.ApplyEntryActionAsync(request, replaceExisting, cancellationToken);
    }

    public Task<ExceptionMutationResult> UpdateExceptionAsync(Guid exceptionId, ExceptionEditRequest request, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.UpdateExceptionAsync(exceptionId, request, cancellationToken);
    }

    public Task<ExceptionMutationResult> RemoveExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        return _exceptionStore.RemoveExceptionAsync(exceptionId, cancellationToken);
    }

    public Task<ExceptionMutationResult> RemoveAllExceptionsAsync(CancellationToken cancellationToken = default)
    {
        return _exceptionStore.RemoveAllExceptionsAsync(cancellationToken);
    }
}
