using Microsoft.Win32;
using pylorak.TinyWall;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class ServicesService : IServicesService
{
    public Task<IReadOnlyList<ServiceRow>> GetServicesAsync(ServiceQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<ServiceRow>>(() => BuildServices(query, cancellationToken), cancellationToken);
    }

    private static List<ServiceRow> BuildServices(ServiceQuery query, CancellationToken cancellationToken)
    {
        var rows = new List<ServiceRow>();

        foreach (var service in ServiceController.GetServices())
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (service)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(query.SearchText)
                        && !service.ServiceName.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase)
                        && !service.DisplayName.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase))
                        continue;

                    rows.Add(new ServiceRow(service.DisplayName, service.ServiceName, GetServiceExecutable(service.ServiceName)));
                }
                catch
                {
                    // Ignore services that cannot be read during enumeration.
                }
            }
        }

        return [.. rows.OrderBy(row => row.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    private static string GetServiceExecutable(string serviceName)
    {
        try
        {
            using RegistryKey keyHklm = Registry.LocalMachine;
            using RegistryKey key = keyHklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName) ?? throw new InvalidOperationException();
            var imagePath = (key.GetValue("ImagePath") as string ?? string.Empty).Replace("\"", string.Empty);

            while (true)
            {
                if (File.Exists(imagePath))
                    return imagePath;

                int i = imagePath.LastIndexOf(' ');
                if (i == -1)
                    break;

                imagePath = imagePath[..i];
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }
}
