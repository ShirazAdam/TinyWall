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
            "TCP/UDP" => new TcpUdpPolicy(true),
            _ => new UnrestrictedPolicy()
        };

        return new FirewallExceptionV3(subject, policy);
    }
}
