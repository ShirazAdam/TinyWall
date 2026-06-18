using ModernTinyWall.TinyWall;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IServicesService
{
    Task<IReadOnlyList<ServiceRow>> GetServicesAsync(ServiceQuery query, CancellationToken cancellationToken = default);
}

internal sealed record ServiceQuery(string SearchText);
