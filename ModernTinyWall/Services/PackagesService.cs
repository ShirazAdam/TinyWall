using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class PackagesService : IPackagesService
{
    public Task<IReadOnlyList<PackageRow>> GetPackagesAsync(PackageQuery query, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<PackageRow>>(() => BuildPackages(query, cancellationToken), cancellationToken);
    }

    private static List<PackageRow> BuildPackages(PackageQuery query, CancellationToken cancellationToken)
    {
        var packageList = new UwpPackageList();
        var rows = new List<PackageRow>();

        foreach (var package in packageList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(query.SearchText)
                && !package.Name.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase)
                && !package.Publisher.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase)
                && !package.PublisherId.Contains(query.SearchText, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            rows.Add(new PackageRow(
                package.Name,
                package.Publisher,
                package.PublisherId,
                package.Sid,
                package.Tampered.ToString()));
        }

        return [.. rows.OrderBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
