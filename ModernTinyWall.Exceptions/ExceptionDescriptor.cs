using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;

namespace ModernTinyWall.Exceptions;

public static class ExceptionDescriptor
{
    public static ExceptionRow CreateRow(FirewallExceptionV3 exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var (name, subjectType, details) = DescribeSubject(exception.Subject);
        return new ExceptionRow(
            exception.Id,
            name,
            subjectType,
            details,
            DescribePolicy(exception.Policy),
            exception.CreationDate.ToString("yyyy/MM/dd HH:mm"));
    }

    public static (string Name, string SubjectType, string Details) DescribeSubject(ExceptionSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        return subject switch
        {
            ExecutableSubject executable => (executable.ExecutableName, "Executable", executable.ExecutablePath),
            AppContainerSubject appContainer => (appContainer.DisplayName, "UWP app", $"{appContainer.PublisherId}, {appContainer.Publisher}"),
            GlobalSubject => ("All applications", "Global", string.Empty),
            _ => (subject.ToString() ?? string.Empty, subject.SubjectType.ToString(), string.Empty)
        };
    }

    public static string DescribePolicy(ExceptionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

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
}
