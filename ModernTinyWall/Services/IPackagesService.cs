using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IPackagesService
{
    Task<IReadOnlyList<PackageRow>> GetPackagesAsync(PackageQuery query, CancellationToken cancellationToken = default);
}

internal sealed record PackageQuery(string SearchText);

internal sealed record PackageRow(string Name, string Publisher, string PublisherId, string Sid, string Tampered);
