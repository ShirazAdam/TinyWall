using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class ExceptionsService : IExceptionsService
{
    private readonly Controller _controller = new("TinyWallController");

    public Task<IReadOnlyList<ExceptionRow>> GetExceptionsAsync(ExceptionQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ExceptionRow>>(() => BuildExceptions(query, cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddExceptionAsync(ExceptionEditRequest request, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config => config.ActiveProfile.AppExceptions.Add(CreateException(request)), "Exception added.", cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddExecutableExceptionsAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForExecutable(executablePath));
        }, "Executable exceptions added.", cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddServiceExceptionAsync(string executablePath, string serviceName, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForService(executablePath, serviceName));
        }, "Service exception added.", cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> AddPackageExceptionAsync(string packageSid, string displayName, string publisherId, string publisher, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config =>
        {
            config.ActiveProfile.AddExceptions(AppExceptionFactory.CreateForPackage(packageSid, displayName, publisherId, publisher));
        }, "Package exception added.", cancellationToken), cancellationToken);
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
        }, "Exception updated.", cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> RemoveExceptionAsync(Guid exceptionId, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config => config.ActiveProfile.AppExceptions.RemoveAll(ex => ex.Id == exceptionId), "Exception removed.", cancellationToken), cancellationToken);
    }

    public Task<ExceptionMutationResult> RemoveAllExceptionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => MutateExceptions(config => config.ActiveProfile.AppExceptions.Clear(), "All exceptions removed.", cancellationToken), cancellationToken);
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
            var row = CreateRow(exception);

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

    private static ExceptionRow CreateRow(FirewallExceptionV3 exception)
    {
        var (name, subjectType, details) = DescribeSubject(exception.Subject);
        return new ExceptionRow(
            exception.Id,
            name,
            subjectType,
            details,
            DescribePolicy(exception.Policy),
            exception.CreationDate.ToString("yyyy/MM/dd HH:mm"));
    }

    private static (string Name, string SubjectType, string Details) DescribeSubject(ExceptionSubject subject)
    {
        return subject switch
        {
            ExecutableSubject executable => (executable.ExecutableName, "Executable", executable.ExecutablePath),
            AppContainerSubject appContainer => (appContainer.DisplayName, "UWP app", $"{appContainer.PublisherId}, {appContainer.Publisher}"),
            GlobalSubject => ("All applications", "Global", string.Empty),
            _ => (subject.ToString() ?? string.Empty, subject.SubjectType.ToString(), string.Empty)
        };
    }

    private static string DescribePolicy(ExceptionPolicy policy)
    {
        return policy switch
        {
            HardBlockPolicy => "Hard block",
            UnrestrictedPolicy unrestricted when unrestricted.LocalNetworkOnly => "Unrestricted, local network only",
            UnrestrictedPolicy => "Unrestricted",
            TcpUdpPolicy tcpUdp => DescribeTcpUdpPolicy(tcpUdp),
            RuleListPolicy ruleList => $"Rule list ({ruleList.Rules.Count})",
            _ => policy.PolicyType.ToString()
        };
    }

    private static string DescribeTcpUdpPolicy(TcpUdpPolicy policy)
    {
        var allowedParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(policy.AllowedRemoteTcpConnectPorts) || !string.IsNullOrWhiteSpace(policy.AllowedLocalTcpListenerPorts))
            allowedParts.Add("TCP");

        if (!string.IsNullOrWhiteSpace(policy.AllowedRemoteUdpConnectPorts) || !string.IsNullOrWhiteSpace(policy.AllowedLocalUdpListenerPorts))
            allowedParts.Add("UDP");

        if (allowedParts.Count == 0)
            return "TCP/UDP";

        return string.Join("/", allowedParts);
    }

    private ExceptionMutationResult MutateExceptions(Action<ServerConfiguration> mutation, string successMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var changeset = Guid.Empty;
        var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
        if (response != MessageType.GET_SETTINGS || serviceConfig is null)
            return new ExceptionMutationResult(false, "Could not load TinyWall settings from the service.");

        mutation(serviceConfig);

        var updateResponse = _controller.SetServerConfig(serviceConfig, changeset);
        return updateResponse.Type switch
        {
            MessageType.PUT_SETTINGS => new ExceptionMutationResult(true, successMessage),
            MessageType.RESPONSE_LOCKED => new ExceptionMutationResult(false, "TinyWall is locked. Unlock it before changing application exceptions."),
            MessageType.COM_ERROR => new ExceptionMutationResult(false, "Could not contact the TinyWall service."),
            MessageType.RESPONSE_ERROR => new ExceptionMutationResult(false, "The TinyWall service could not update application exceptions."),
            _ => new ExceptionMutationResult(false, $"Unexpected TinyWall service response: {updateResponse.Type}.")
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
            return new ExceptionActionResult(false, false, "Could not load TinyWall settings from the service.");

        var subject = CreateSubject(request);
        var existing = FindExistingException(serviceConfig, subject);
        if (existing is null)
            return new ExceptionActionResult(true, false, string.Empty);

        return new ExceptionActionResult(
            true,
            true,
            $"This {GetEntryKindDisplayName(request.Kind)} was added before as {DescribePolicy(existing.Policy).ToLowerInvariant()}. Apply the new {GetPolicyDisplayName(request.Policy).ToLowerInvariant()} requirement?",
            DescribePolicy(existing.Policy));
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
        }, $"{GetPolicyDisplayName(request.Policy)} exception applied.", cancellationToken);
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
            return $"Cannot {GetPolicyVerb(request.Policy)} this item because its name is unavailable.";

        if (string.IsNullOrWhiteSpace(request.Details))
        {
            return request.Kind switch
            {
                ExceptionEntryKind.Service => $"Cannot {GetPolicyVerb(request.Policy)} this service because its executable path is unavailable.",
                ExceptionEntryKind.Package => $"Cannot {GetPolicyVerb(request.Policy)} this package because its package SID is unavailable.",
                _ => $"Cannot {GetPolicyVerb(request.Policy)} this process because its executable path is unavailable."
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
