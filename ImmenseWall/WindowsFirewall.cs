using NetFwTypeLib;
using pylorak.Utilities;
using System;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;

namespace pylorak.TinyWall
{
    class WindowsFirewall : Disposable
    {
        private readonly EventLogWatcher? _wfEventWatcher;

        // This is a list of apps that are allowed to change firewall rules
        private static readonly string[] WhitelistedApps = {
#if DEBUG
            Path.Combine(Path.GetDirectoryName(Utils.ExecutablePath)!, "TinyWall.vshost.exe"),
#endif
            Utils.ExecutablePath,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dllhost.exe")
        };

        public WindowsFirewall()
        {
            DisableMpsSvc();

            try
            {
                _wfEventWatcher = new EventLogWatcher("Microsoft-Windows-Windows Firewall With Advanced Security/Firewall");
                _wfEventWatcher.EventRecordWritten += WFEventWatcher_EventRecordWritten;
                _wfEventWatcher.Enabled = true;
            }
            catch (Exception e)
            {
                Utils.Log("Cannot monitor Windows Firewall. Is the 'eventlog' service running? For details see next log entry.", Utils.LOG_ID_SERVICE);
                Utils.LogException(e, Utils.LOG_ID_SERVICE);
            }
        }

        private static void WFEventWatcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
        {
            try
            {
                int propidx;
                switch (e.EventRecord.Id)
                {
                    case 2003:     // firewall setting changed
                        {
                            propidx = 7;
                            break;
                        }
                    case 2005:     // rule changed
                        {
                            propidx = 22;
                            break;
                        }
                    case 2006:     // rule deleted
                        {
                            propidx = 3;
                            break;
                        }
                    case 2032:     // firewall has been reset
                        {
                            propidx = 1;
                            break;
                        }
                    default:
                        // Nothing to do
                        return;
                }

                // If the rules were changed by us, do nothing
                var eVpath = (string)e.EventRecord.Properties[propidx].Value;

                if (WhitelistedApps.Any(t => string.Compare(t, eVpath, StringComparison.OrdinalIgnoreCase) == 0))
                {
                    return;
                }
            }
            catch
            {
                // ignored
            }

            DisableMpsSvc();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsDisposed)
                return;

            if (disposing)
            {
                _wfEventWatcher?.Dispose();
            }

            RestoreMpsSvc();
            base.Dispose(disposing);
        }

        private static INetFwPolicy2 GetFwPolicy2()
        {
            Type tNetFwPolicy2 = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            return (INetFwPolicy2)Activator.CreateInstance(tNetFwPolicy2);
        }

        private static INetFwRule CreateFwRule(string name, NET_FW_ACTION_ action, NET_FW_RULE_DIRECTION_ dir)
        {
            Type tNetFwRule = Type.GetTypeFromProgID("HNetCfg.FwRule");
            INetFwRule rule = (INetFwRule)Activator.CreateInstance(tNetFwRule);

            rule.Name = name;
            rule.Action = action;
            rule.Direction = dir;
            rule.Grouping = "TinyWall";
            rule.Profiles = (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE | (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC | (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN;
            rule.Enabled = true;
            if ((NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN == dir) && (NET_FW_ACTION_.NET_FW_ACTION_ALLOW == action))
                rule.EdgeTraversal = true;

            return rule;
        }

        private static void MpsNotificationsDisable(INetFwPolicy2 pol, bool disable)
        {
            if (pol.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] != disable)
                pol.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PRIVATE] = disable;
            if (pol.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] != disable)
                pol.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_PUBLIC] = disable;
            if (pol.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] != disable)
                pol.NotificationsDisabled[NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_DOMAIN] = disable;
        }

        private static void DisableMpsSvc()
        {
            try
            {
                var fwPolicy2 = GetFwPolicy2();

                // Disable Windows Firewall notifications
                MpsNotificationsDisable(fwPolicy2, true);

                // Add new rules
                var newRuleId = $"TinyWall Compat [{Utils.RandomString(6)}]";
                fwPolicy2.Rules.Add(CreateFwRule(newRuleId, NET_FW_ACTION_.NET_FW_ACTION_ALLOW, NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN));
                fwPolicy2.Rules.Add(CreateFwRule(newRuleId, NET_FW_ACTION_.NET_FW_ACTION_ALLOW, NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_OUT));

                // Remove earlier rules
                var rules = fwPolicy2.Rules;

                foreach (var rule in from INetFwRule rule in rules let ruleName = rule.Name where !string.IsNullOrEmpty(ruleName) && ruleName.Contains("TinyWall") && (ruleName != newRuleId) select rule)
                {
                    rules.Remove(rule.Name);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static void RestoreMpsSvc()
        {
            try
            {
                var fwPolicy2 = GetFwPolicy2();

                // Enable Windows Firewall notifications
                MpsNotificationsDisable(fwPolicy2, false);

                // Remove earlier rules
                var rules = fwPolicy2.Rules;

                foreach (INetFwRule rule in rules)
                {
                    if (rule.Grouping is "TinyWall")
                        rules.Remove(rule.Name);
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
