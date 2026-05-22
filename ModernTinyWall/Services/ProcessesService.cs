using pylorak.TinyWall;
using pylorak.Windows.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class ProcessesService : IProcessesService
{
    public Task<IReadOnlyList<ProcessRow>> GetProcessesAsync(ProcessQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ProcessRow>>(() => BuildProcesses(query, cancellationToken), cancellationToken);
    }

    private static List<ProcessRow> BuildProcesses(ProcessQuery query, CancellationToken cancellationToken)
    {
        var rows = new List<ProcessRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var packageList = new UwpPackageList();
        var servicePidMap = BuildServicePidMap();

        foreach (var process in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (process)
            {
                try
                {
                    var processName = process.ProcessName;
                    if (!string.IsNullOrWhiteSpace(query.SearchText)
                        && !processName.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    var path = GetProcessPath(process);
                    if (string.IsNullOrWhiteSpace(path))
                        continue;

                    var pid = unchecked((uint)process.Id);
                    var package = FindPackage(packageList, pid);
                    var displayName = package.HasValue ? package.Value.Name : $"{processName} ({process.Id})";
                    var services = servicePidMap.TryGetValue(pid, out var mappedServices) ? mappedServices : [];
                    var servicesOrPackage = package.HasValue ? "UWP package" : string.Join(", ", services.OrderBy(service => service, StringComparer.CurrentCultureIgnoreCase));
                    var details = package.HasValue ? $"{package.Value.PublisherId}, {package.Value.Publisher}" : path;

                    var key = $"{displayName}|{details}";
                    if (!seen.Add(key))
                        continue;

                    rows.Add(new ProcessRow(displayName, servicesOrPackage, details));
                }
                catch
                {
                    // Ignore processes that exit or deny access during enumeration.
                }
            }
        }

        return [.. rows.OrderBy(row => row.ProcessName, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static UwpPackageList.Package? FindPackage(UwpPackageList packageList, uint pid)
    {
        try
        {
            return packageList.FindPackageForProcess(pid);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<uint, HashSet<string>> BuildServicePidMap()
    {
        var map = new Dictionary<uint, HashSet<string>>();

        try
        {
            using var scm = new ServiceControlManager();
            foreach (var service in ServiceController.GetServices())
            {
                using (service)
                {
                    try
                    {
                        if (service.Status != ServiceControllerStatus.Running)
                            continue;

                        uint pid = scm.GetServicePid(service.ServiceName) ?? 0;
                        if (pid == 0)
                            continue;

                        if (!map.TryGetValue(pid, out var services))
                        {
                            services = [];
                            map.Add(pid, services);
                        }

                        services.Add(service.ServiceName);
                    }
                    catch
                    {
                        // Ignore services that cannot be queried during enumeration.
                    }
                }
            }
        }
        catch
        {
            // Leave the map empty if the service control manager is unavailable.
        }

        return map;
    }

    private static string GetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
