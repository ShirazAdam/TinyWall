using ModernTinyWall.TinyWall;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface IProcessesService
{
    Task<IReadOnlyList<ProcessRow>> GetProcessesAsync(ProcessQuery query, CancellationToken cancellationToken = default);
}

internal sealed record ProcessQuery(string SearchText);
