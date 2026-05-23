using ModernTinyWall.TinyWall.DatabaseClasses;
using System.Collections.Generic;

namespace ModernTinyWall.TinyWall;

public static class AppExceptionFactory
{
    public static List<FirewallExceptionV3> CreateForExecutable(string executablePath)
    {
        var subject = new ExecutableSubject(executablePath);

        try
        {
            return AppDatabase.Load().GetExceptionsForApp(subject, true, out _);
        }
        catch
        {
            return [new FirewallExceptionV3(subject, new TcpUdpPolicy(true))];
        }
    }

    public static List<FirewallExceptionV3> CreateForProcessId(uint processId)
    {
        var path = ModernTinyWall.Windows.ProcessManager.GetProcessPath(processId);
        return string.IsNullOrWhiteSpace(path)
            ? []
            : CreateForExecutable(path);
    }

    public static List<FirewallExceptionV3> CreateForService(string executablePath, string serviceName)
    {
        var subject = new ServiceSubject(executablePath, serviceName);
        return [new FirewallExceptionV3(subject, new TcpUdpPolicy(true))];
    }

    public static List<FirewallExceptionV3> CreateForPackage(string packageSid, string displayName, string publisherId, string publisher)
    {
        var subject = new AppContainerSubject(packageSid, displayName, publisherId, publisher);
        return [new FirewallExceptionV3(subject, new TcpUdpPolicy(true))];
    }
}
