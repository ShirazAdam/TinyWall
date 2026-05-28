using ModernTinyWall.Models;
using ModernTinyWall.TinyWall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModernTinyWall.Services;

internal sealed class SettingsService : ISettingsService
{
    private const string AutoUpdateCheckKey = "service.autoUpdateCheck";
    private const string DisplayOffBlockKey = "service.activeProfile.displayOffBlock";
    private const string LockHostsFileKey = "service.lockHostsFile";
    private const string EnableBlocklistsKey = "service.blocklists.enableBlocklists";
    private const string EnableHostsBlocklistKey = "service.blocklists.enableHostsBlocklist";
    private const string EnablePortBlocklistKey = "service.blocklists.enablePortBlocklist";
    private const string AllowLocalSubnetKey = "service.activeProfile.allowLocalSubnet";
    private const string SpecialProfilePrefix = "service.activeProfile.specialExceptions.";
    private const string AskForExceptionDetailsKey = "controller.askForExceptionDetails";
    private const string EnableGlobalHotkeysKey = "controller.globalHotkeys";

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

            var controllerSettings = ControllerSettings.Load();
            return CreateSections(serviceConfig, controllerSettings);
        }, cancellationToken);
    }

    public Task<SettingsApplyResult> ApplySettingsAsync(IReadOnlyList<SettingsSection> sections, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var changeset = Guid.Empty;
            var response = _controller.GetServerConfig(out var serviceConfig, out _, ref changeset);
            if (response != MessageType.GET_SETTINGS || serviceConfig is null)
                return new SettingsApplyResult(false, "Could not load TinyWall settings from the service.");

            var editableItems = sections.SelectMany(section => section.Items).Where(item => item.IsEditable).ToArray();
            ApplyEditableSettings(serviceConfig, editableItems);
            ApplyControllerSettings(editableItems);

            var updateResponse = _controller.SetServerConfig(serviceConfig, changeset);
            return updateResponse.Type switch
            {
                MessageType.PUT_SETTINGS => new SettingsApplyResult(true, "Settings applied."),
                MessageType.RESPONSE_LOCKED => new SettingsApplyResult(false, "TinyWall is locked. Unlock it before changing settings."),
                MessageType.COM_ERROR => new SettingsApplyResult(false, "Could not contact the TinyWall service."),
                MessageType.RESPONSE_ERROR => new SettingsApplyResult(false, "The TinyWall service could not apply settings."),
                _ => new SettingsApplyResult(false, $"Unexpected TinyWall service response: {updateResponse.Type}.")
            };
        }, cancellationToken);
    }

    private static IReadOnlyList<SettingsSection> CreateSections(ServerConfiguration serviceConfig, ControllerSettings controllerSettings)
    {
        return
        [
            new SettingsSection(
                "General",
                "Application-level preferences migrated from the WinForms General tab.",
                [
                    new SettingsItem(AutoUpdateCheckKey, "Automatic update checks", "Check for TinyWall updates automatically.", serviceConfig.AutoUpdateCheck, true),
                    new SettingsItem(AskForExceptionDetailsKey, "Ask for exception details", "Prompt for details when creating new application exceptions.", controllerSettings.AskForExceptionDetails, true),
                    new SettingsItem(EnableGlobalHotkeysKey, "Global hotkeys", "Enable TinyWall global keyboard shortcuts.", controllerSettings.EnableGlobalHotkeys, true),
                    new SettingsItem("controller.language", "Language", "English is the ModernTinyWall baseline language.", true, false)
                ]),
            new SettingsSection(
                "Machine settings",
                "System-wide firewall and blocklist options migrated from the WinForms Machine settings tab.",
                [
                    new SettingsItem(DisplayOffBlockKey, "Block network when display is off", "Block selected traffic while the display is off.", serviceConfig.ActiveProfile.DisplayOffBlock, true),
                    new SettingsItem(LockHostsFileKey, "Lock hosts file", "Protect the hosts file from unwanted changes.", serviceConfig.LockHostsFile, true),
                    new SettingsItem(EnableBlocklistsKey, "Enable blocklists", "Enable configured hosts and port blocklists.", serviceConfig.Blocklists.EnableBlocklists, true),
                    new SettingsItem(EnableHostsBlocklistKey, "Hosts blocklist", "Use the hosts blocklist when blocklists are enabled.", serviceConfig.Blocklists.EnableHostsBlocklist, true),
                    new SettingsItem(EnablePortBlocklistKey, "Malware port blocklist", "Block known unwanted ports when blocklists are enabled.", serviceConfig.Blocklists.EnablePortBlocklist, true),
                    new SettingsItem(AllowLocalSubnetKey, "Allow local subnet", "Allow local subnet traffic for the active profile.", serviceConfig.ActiveProfile.AllowLocalSubnet, true)
                ]),
            new SettingsSection(
                "Application exceptions",
                "Application, service, package and global exceptions migrated from the WinForms Applications tab.",
                [
                    new SettingsItem("service.activeProfile.appExceptions", "Configured exceptions", $"{serviceConfig.ActiveProfile.AppExceptions.Count} exception{(serviceConfig.ActiveProfile.AppExceptions.Count == 1 ? string.Empty : "s")} configured.", serviceConfig.ActiveProfile.AppExceptions.Count > 0, false),
                    ..CreateSpecialProfileItems(serviceConfig)
                ]),
            new SettingsSection(
                "Maintenance and about",
                "Import, export, update and about actions from the WinForms About tab.",
                [
                    new SettingsItem("maintenance.import", "Import settings", "Import TinyWall settings from a file.", false, false),
                    new SettingsItem("maintenance.export", "Export settings", "Export TinyWall settings to a file.", false, false),
                    new SettingsItem("maintenance.update", "Check for updates", "Check whether a newer TinyWall version is available.", serviceConfig.AutoUpdateCheck, false),
                    new SettingsItem("maintenance.licence", "Licence and attributions", "Show licence and third-party attribution information.", false, false)
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
                    new SettingsItem(AutoUpdateCheckKey, "Automatic update checks", "Check for TinyWall updates automatically.", false, false),
                    new SettingsItem(AskForExceptionDetailsKey, "Ask for exception details", "Prompt for details when creating new application exceptions.", false, false),
                    new SettingsItem(EnableGlobalHotkeysKey, "Global hotkeys", "Enable TinyWall global keyboard shortcuts.", false, false),
                    new SettingsItem("controller.language", "Language", "Use English as the default language baseline.", true, false)
                ]),
            new SettingsSection(
                "Machine settings",
                "System-wide firewall and blocklist options migrated from the WinForms Machine settings tab.",
                [
                    new SettingsItem(DisplayOffBlockKey, "Block network when display is off", "Block selected traffic while the display is off.", false, false),
                    new SettingsItem(LockHostsFileKey, "Lock hosts file", "Protect the hosts file from unwanted changes.", false, false),
                    new SettingsItem(EnableBlocklistsKey, "Enable blocklists", "Enable configured hosts and port blocklists.", false, false),
                    new SettingsItem(EnableHostsBlocklistKey, "Hosts blocklist", "Use the hosts blocklist when blocklists are enabled.", false, false),
                    new SettingsItem(EnablePortBlocklistKey, "Malware port blocklist", "Block known unwanted ports when blocklists are enabled.", false, false),
                    new SettingsItem(AllowLocalSubnetKey, "Allow local subnet", "Allow local subnet traffic for the active profile.", false, false)
                ])
        ];
    }

    private static void ApplyEditableSettings(ServerConfiguration serviceConfig, IEnumerable<SettingsItem> items)
    {
        foreach (var item in items)
        {
            switch (item.Key)
            {
                case AutoUpdateCheckKey:
                    serviceConfig.AutoUpdateCheck = item.IsEnabled;
                    break;
                case DisplayOffBlockKey:
                    serviceConfig.ActiveProfile.DisplayOffBlock = item.IsEnabled;
                    break;
                case LockHostsFileKey:
                    serviceConfig.LockHostsFile = item.IsEnabled;
                    break;
                case EnableBlocklistsKey:
                    serviceConfig.Blocklists.EnableBlocklists = item.IsEnabled;
                    break;
                case EnableHostsBlocklistKey:
                    serviceConfig.Blocklists.EnableHostsBlocklist = item.IsEnabled;
                    break;
                case EnablePortBlocklistKey:
                    serviceConfig.Blocklists.EnablePortBlocklist = item.IsEnabled;
                    break;
                case AllowLocalSubnetKey:
                    serviceConfig.ActiveProfile.AllowLocalSubnet = item.IsEnabled;
                    break;
                default:
                    if (item.Key.StartsWith(SpecialProfilePrefix, StringComparison.Ordinal))
                    {
                        var profileName = item.Key[SpecialProfilePrefix.Length..];
                        if (item.IsEnabled && !serviceConfig.ActiveProfile.SpecialExceptions.Contains(profileName, StringComparer.OrdinalIgnoreCase))
                            serviceConfig.ActiveProfile.SpecialExceptions.Add(profileName);
                        else if (!item.IsEnabled)
                            serviceConfig.ActiveProfile.SpecialExceptions.RemoveAll(name => string.Equals(name, profileName, StringComparison.OrdinalIgnoreCase));
                    }

                    break;
            }
        }
    }

    private static SettingsItem[] CreateSpecialProfileItems(ServerConfiguration serviceConfig)
    {
        if (serviceConfig.ActiveProfile.SpecialExceptions.Count == 0)
        {
            return
            [
                new SettingsItem("service.activeProfile.specialExceptions", "Special profiles", "No special profiles are enabled for the active profile.", false, false)
            ];
        }

        return [.. serviceConfig.ActiveProfile.SpecialExceptions
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .Select(profileName => new SettingsItem(
                SpecialProfilePrefix + profileName,
                profileName.Replace('_', ' '),
                "Special profile enabled for the active firewall profile.",
                true,
                true))];
    }

    private static void ApplyControllerSettings(IEnumerable<SettingsItem> items)
    {
        var controllerSettings = ControllerSettings.Load();
        var changed = false;

        foreach (var item in items)
        {
            switch (item.Key)
            {
                case AskForExceptionDetailsKey:
                    controllerSettings.AskForExceptionDetails = item.IsEnabled;
                    changed = true;
                    break;
                case EnableGlobalHotkeysKey:
                    controllerSettings.EnableGlobalHotkeys = item.IsEnabled;
                    changed = true;
                    break;
            }
        }

        if (changed)
            controllerSettings.Save();
    }
}
