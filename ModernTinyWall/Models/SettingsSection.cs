using System.Collections.Generic;

namespace ModernTinyWall.Models;

internal sealed record SettingsSection(string Title, string Description, IReadOnlyList<SettingsItem> Items);

internal sealed record SettingsItem(string Name, string Description, bool IsEnabled);
