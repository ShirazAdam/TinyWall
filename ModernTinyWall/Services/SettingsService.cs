using ModernTinyWall.Models;
using pylorak.TinyWall;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class SettingsService : ISettingsService
{
    private readonly Controller _controller = new("TinyWallController");

    public Task<IReadOnlyList<SettingsSection>> GetSettingsSectionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<SettingsSection>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changeset = Guid.Empty;
            var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
            if (response != MessageType.GET_SETTINGS || serviceConfig is null)
            {
                return CreateFallbackSections();
            }

            return CreateSections(serviceConfig);
        }, cancellationToken);
    }

    private static IReadOnlyList<SettingsSection> CreateSections(ServerConfiguration serviceConfig)
    {
        return
        [
            new SettingsSection(
                "General",
                "Application-level preferences migrated from the WinForms General tab.",
                [
                    new SettingsItem("Automatic update checks", "Check for TinyWall updates automatically.", serviceConfig.AutoUpdateCheck),
                    new SettingsItem("Language", "British English is the ModernTinyWall baseline language.", true)
                ]),
            new SettingsSection(
                "Machine settings",
                "System-wide firewall and blocklist options migrated from the WinForms Machine settings tab.",
                [
                    new SettingsItem("Block network when display is off", "Block selected traffic while the display is off.", serviceConfig.ActiveProfile.DisplayOffBlock),
                    new SettingsItem("Lock hosts file", "Protect the hosts file from unwanted changes.", serviceConfig.LockHostsFile),
                    new SettingsItem("Enable blocklists", "Enable configured hosts and port blocklists.", serviceConfig.Blocklists.EnableBlocklists),
                    new SettingsItem("Hosts blocklist", "Use the hosts blocklist when blocklists are enabled.", serviceConfig.Blocklists.EnableHostsBlocklist),
                    new SettingsItem("Malware port blocklist", "Block known unwanted ports when blocklists are enabled.", serviceConfig.Blocklists.EnablePortBlocklist)
                ]),
            new SettingsSection(
                "Application exceptions",
                "Application, service, package and global exceptions migrated from the WinForms Applications tab.",
                [
                    new SettingsItem("Configured exceptions", $"{serviceConfig.ActiveProfile.AppExceptions.Count} exception{(serviceConfig.ActiveProfile.AppExceptions.Count == 1 ? string.Empty : "s")} configured.", serviceConfig.ActiveProfile.AppExceptions.Count > 0),
                    new SettingsItem("Special profiles", $"{serviceConfig.ActiveProfile.SpecialExceptions.Count} special profile{(serviceConfig.ActiveProfile.SpecialExceptions.Count == 1 ? string.Empty : "s")} enabled.", serviceConfig.ActiveProfile.SpecialExceptions.Count > 0)
                ]),
            new SettingsSection(
                "Maintenance and about",
                "Import, export, update and about actions from the WinForms About tab.",
                [
                    new SettingsItem("Import settings", "Import TinyWall settings from a file.", false),
                    new SettingsItem("Export settings", "Export TinyWall settings to a file.", false),
                    new SettingsItem("Check for updates", "Check whether a newer TinyWall version is available.", serviceConfig.AutoUpdateCheck),
                    new SettingsItem("Licence and attributions", "Show licence and third-party attribution information.", false)
                ])
        ];
    }

    private static IReadOnlyList<SettingsSection> CreateFallbackSections()
    {
        return
        [
            new SettingsSection(
                "General",
                "TinyWall service settings could not be loaded. Showing migration placeholders.",
                [
                    new SettingsItem("Automatic update checks", "Check for TinyWall updates automatically.", false),
                    new SettingsItem("Ask for exception details", "Prompt for details when creating new application exceptions.", false),
                    new SettingsItem("Global hotkeys", "Enable TinyWall global keyboard shortcuts.", false),
                    new SettingsItem("Language", "Use British English as the default language baseline.", true)
                ]),
            new SettingsSection(
                "Machine settings",
                "System-wide firewall and blocklist options migrated from the WinForms Machine settings tab.",
                [
                    new SettingsItem("Block network when display is off", "Block selected traffic while the display is off.", false),
                    new SettingsItem("Lock hosts file", "Protect the hosts file from unwanted changes.", false),
                    new SettingsItem("Enable blocklists", "Enable configured hosts and port blocklists.", false),
                    new SettingsItem("Hosts blocklist", "Use the hosts blocklist when blocklists are enabled.", false),
                    new SettingsItem("Malware port blocklist", "Block known unwanted ports when blocklists are enabled.", false)
                ])
        ];
    }
}
