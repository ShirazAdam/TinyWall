using CommunityToolkit.Mvvm.ComponentModel;
using ImmenseWall.Models;
using ImmenseWall.Services;
using System.IO;
using System.Windows.Media;

namespace ImmenseWall.ViewModels
{
    public partial class RuleViewModel : ObservableObject
    {
        public FirewallExceptionV3 Exception { get; }

        [ObservableProperty]
        private string _name;

        [ObservableProperty]
        private ImageSource? _icon;

        [ObservableProperty]
        private string _policySummary;

        [ObservableProperty]
        private bool _isModified;

        public RuleViewModel(FirewallExceptionV3 exception)
        {
            Exception = exception;
            _name = GetName(exception);
            _icon = GetIcon(exception);
            _policySummary = GetPolicySummary(exception);
        }

        private static string GetName(FirewallExceptionV3 ex)
        {
            if (ex.Subject is ExecutableSubject exeSub)
            {
                return exeSub.ExecutableName;
            }
            if (ex.Subject is ServiceSubject svcSub)
            {
                return $"{svcSub.ServiceName} ({svcSub.ExecutableName})";
            }
            if (ex.Subject is AppContainerSubject uwpSub)
            {
                return uwpSub.DisplayName;
            }
            return ex.Subject.ToString() ?? "Unknown";
        }

        private static ImageSource? GetIcon(FirewallExceptionV3 ex)
        {
            try
            {
                string? path = null;
                if (ex.Subject is ExecutableSubject exeSub)
                {
                    path = exeSub.ExecutablePath;
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using var icon = IconTools.GetIconForFile(path, IconTools.ShellIconSize.SmallIcon);
                    return IconHelper.ToImageSource(icon);
                }
                
                // Fallback or generic icon could be added here
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetPolicySummary(FirewallExceptionV3 ex)
        {
            return ex.Policy switch
            {
                UnrestrictedPolicy p => p.LocalNetworkOnly ? "Allow LAN (All Ports)" : "Allow All",
                HardBlockPolicy => "Block All",
                TcpUdpPolicy p => $"Allow {(p.LocalNetworkOnly ? "LAN" : "All")} (Custom Ports)",
                RuleListPolicy => "Custom Rules",
                _ => "Unknown"
            };
        }
    }
}
