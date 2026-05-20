using pylorak.TinyWall;
using ModernTinyWall.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal interface ISettingsService
{
    Task<IReadOnlyList<SettingsSection>> GetSettingsSectionsAsync(CancellationToken cancellationToken = default);
}
