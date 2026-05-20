using pylorak.TinyWall;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

                    var key = $"{processName}|{path}";
                    if (!seen.Add(key))
                        continue;

                    rows.Add(new ProcessRow($"{processName} ({process.Id})", string.Empty, path));
                }
                catch
                {
                    // Ignore processes that exit or deny access during enumeration.
                }
            }
        }

        return [.. rows.OrderBy(row => row.ProcessName, StringComparer.CurrentCultureIgnoreCase)];
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
