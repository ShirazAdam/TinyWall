using ModernTinyWall.TinyWall;
using ModernTinyWall.Exceptions.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Exceptions;

public sealed class TinyWallExceptionStore : ITinyWallExceptionStore
{
    private readonly Controller _controller;

    public TinyWallExceptionStore()
        : this(new Controller("TinyWallController"))
    {
    }

    public TinyWallExceptionStore(Controller controller)
    {
        ArgumentNullException.ThrowIfNull(controller);

        _controller = controller;
    }

    public Task<IReadOnlyList<ExceptionRow>> GetExceptionsAsync(ExceptionQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ExceptionRow>>(() => BuildExceptions(query, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddExceptionAsync(ExceptionEditRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config => config.ActiveProfile.AppExceptions.Add(CreateException(request)), ExceptionStoreStrings.ExceptionAdded, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddExecutableExceptionsAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForExecutable(executablePath));
        }, ExceptionStoreStrings.ExecutableExceptionsAdded, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddServiceExceptionAsync(string executablePath, string serviceName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForService(executablePath, serviceName));
        }, ExceptionStoreStrings.ServiceExceptionAdded, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddPackageExceptionAsync(string packageSid, string displayName, string publisherId, string publisher, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForPackage(packageSid, displayName, publisherId, publisher));
        }, ExceptionStoreStrings.PackageExceptionAdded, cancellationToken), cancellationToken);
    }

    public Task<ExceptionActionResult> PrepareEntryActionAsync(ExceptionEntryActionRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => PrepareEntryAction(request, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> ApplyEntryActionAsync(ExceptionEntryActionRequest request, bool replaceExisting, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ApplyEntryAction(request, replaceExisting, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> UpdateExceptionAsync(Guid exceptionId, ExceptionEditRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            var index = config.ActiveProfile.AppExceptions.FindIndex(ex => ex.Id == exceptionId);
            if (index >= 0)
                config.ActiveProfile.AppExceptions[index] = CreateException(request);
        }, ExceptionStoreStrings.ExceptionUpdated, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> RemoveExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config => config.ActiveProfile.AppExceptions.RemoveAll(ex => ex.Id == exceptionId), ExceptionStoreStrings.ExceptionRemoved, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> RemoveAllExceptionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config => config.ActiveProfile.AppExceptions.Clear(), ExceptionStoreStrings.AllExceptionsRemoved, cancellationToken), cancellationToken);
    }

    private List<ExceptionRow> BuildExceptions(ExceptionQuery query, CancellationToken cancellationToken)
    {
        var changeset = Guid.Empty;
        var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
        if (response != MessageType.GET_SETTINGS || serviceConfig is null)
            return [];

        var rows = new List<ExceptionRow>();
        foreach (var exception in serviceConfig.ActiveProfile.AppExceptions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = ExceptionDescriptor.CreateRow(exception);

            if (!string.IsNullOrWhiteSpace(query.SearchText)
                && !row.Name.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase)
                && !row.SubjectType.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase)
                && !row.Details.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase)
                && !row.Policy.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            rows.Add(row);
        }

        return [.. rows.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private ExceptionMutationResult MutateExceptions(Action<ServerConfiguration> mutation, string successMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var changeset = Guid.Empty;
        var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
        if (response != MessageType.GET_SETTINGS || serviceConfig is null)
            return new ExceptionMutationResult(false, ExceptionStoreStrings.CouldNotLoadSettings);

        mutation(serviceConfig);

        var updateResponse = _controller.SetServerConfig(serviceConfig, changeset);
        return updateResponse.Type switch
        {
            MessageType.PUT_SETTINGS => new ExceptionMutationResult(true, successMessage),
            MessageType.RESPONSE_LOCKED => new ExceptionMutationResult(false, ExceptionStoreStrings.TinyWallLocked),
            MessageType.COM_ERROR => new ExceptionMutationResult(false, ExceptionStoreStrings.CouldNotContactService),
            MessageType.RESPONSE_ERROR => new ExceptionMutationResult(false, ExceptionStoreStrings.CouldNotUpdateExceptions),
            _ => new ExceptionMutationResult(false, ExceptionStoreStrings.UnexpectedServiceResponse(updateResponse.Type))
        };
    }

    private ExceptionActionResult PrepareEntryAction(ExceptionEntryActionRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var validationMessage = ValidateEntryAction(request);
        if (validationMessage is not null)
            return new ExceptionActionResult(false, false, validationMessage);

        var changeset = Guid.Empty;
        var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
        if (response != MessageType.GET_SETTINGS || serviceConfig is null)
            return new ExceptionActionResult(false, false, ExceptionStoreStrings.CouldNotLoadSettings);

        var subject = CreateSubject(request);
        var existing = FindExistingException(serviceConfig, subject);
        if (existing is null)
            return new ExceptionActionResult(true, false, string.Empty);

        return new ExceptionActionResult(
            true,
            true,
            ExceptionStoreStrings.ExistingExceptionPrompt(GetEntryKindDisplayName(request.Kind), ExceptionDescriptor.DescribePolicy(existing.Policy).ToLowerInvariant(), GetPolicyDisplayName(request.Policy).ToLowerInvariant()),
            ExceptionDescriptor.DescribePolicy(existing.Policy));
    }

    private ExceptionMutationResult ApplyEntryAction(ExceptionEntryActionRequest request, bool replaceExisting, CancellationToken cancellationToken)
    {
        var validationMessage = ValidateEntryAction(request);
        if (validationMessage is not null)
            return new ExceptionMutationResult(false, validationMessage);

        return MutateExceptions(config =>
        {
            var subject = CreateSubject(request);
            var exception = new FirewallExceptionV3(subject, CreatePolicy(request.Policy));
            var existing = FindExistingException(config, subject);

            if (existing is not null && replaceExisting)
            {
                config.ActiveProfile.AppExceptions.Remove(existing);
            }
            else if (existing is not null)
            {
                return;
            }

            config.ActiveProfile.AppExceptions.Add(exception);
        }, ExceptionStoreStrings.ExceptionApplied(GetPolicyDisplayName(request.Policy)), cancellationToken);
    }

    private static FirewallExceptionV3? FindExistingException(ServerConfiguration config, ExceptionSubject subject)
    {
        return config.ActiveProfile.AppExceptions.FirstOrDefault(exception => exception.Subject.Equals(subject));
    }

    private static ExceptionSubject CreateSubject(ExceptionEntryActionRequest request)
    {
        return request.Kind switch
        {
            ExceptionEntryKind.Service => new ServiceSubject(request.Details, request.Name),
            ExceptionEntryKind.Package => new AppContainerSubject(request.Details, request.Name, request.Publisher, request.PublisherId),
            _ => new ExecutableSubject(request.Details)
        };
    }

    private static string? ValidateEntryAction(ExceptionEntryActionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ExceptionStoreStrings.CannotApplyUnnamedItem(GetPolicyVerb(request.Policy));

        if (string.IsNullOrWhiteSpace(request.Details))
        {
            return request.Kind switch
            {
                ExceptionEntryKind.Service => ExceptionStoreStrings.CannotApplyServiceWithoutPath(GetPolicyVerb(request.Policy)),
                ExceptionEntryKind.Package => ExceptionStoreStrings.CannotApplyPackageWithoutSid(GetPolicyVerb(request.Policy)),
                _ => ExceptionStoreStrings.CannotApplyProcessWithoutPath(GetPolicyVerb(request.Policy))
            };
        }

        return null;
    }

    private static ExceptionPolicy CreatePolicy(ExceptionEntryPolicy policy)
    {
        return policy == ExceptionEntryPolicy.Block ? HardBlockPolicy.Instance : new TcpUdpPolicy(true);
    }

    private static string GetEntryKindDisplayName(ExceptionEntryKind kind)
    {
        return kind switch
        {
            ExceptionEntryKind.Service => "service",
            ExceptionEntryKind.Package => "package",
            _ => "process"
        };
    }

    private static string GetPolicyDisplayName(ExceptionEntryPolicy policy)
    {
        return policy == ExceptionEntryPolicy.Block ? "Blocked" : "Allowed";
    }

    private static string GetPolicyVerb(ExceptionEntryPolicy policy)
    {
        return policy == ExceptionEntryPolicy.Block ? "block" : "allow";
    }

    private static FirewallExceptionV3 CreateException(ExceptionEditRequest request)
    {
        ExceptionSubject subject = request.SubjectType switch
        {
            "Global" => GlobalSubject.Instance,
            "Service" => new ServiceSubject(request.Details, request.Name),
            "UWP app" => new AppContainerSubject(request.Details, request.Name, string.Empty, string.Empty),
            _ => new ExecutableSubject(request.Details)
        };

        ExceptionPolicy policy = request.Policy switch
        {
            "Hard block" => HardBlockPolicy.Instance,
            "TCP/UDP" => new TcpUdpPolicy(true)
            {
                AllowedRemoteTcpConnectPorts = request.RemoteTcpPorts,
                AllowedLocalTcpListenerPorts = request.LocalTcpPorts,
                AllowedRemoteUdpConnectPorts = request.RemoteUdpPorts,
                AllowedLocalUdpListenerPorts = request.LocalUdpPorts
            },
            _ => new UnrestrictedPolicy()
        };

        return new FirewallExceptionV3(subject, policy);
    }
}
