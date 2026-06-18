using ModernTinyWall.TinyWall;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IConnectionsService
{
    Task<IReadOnlyList<ConnectionRow>> GetConnectionsAsync(ConnectionQuery query, CancellationToken cancellationToken = default);
}

internal sealed record ConnectionQuery(bool ShowActive, bool ShowListening, bool ShowBlocked, string SearchText);
