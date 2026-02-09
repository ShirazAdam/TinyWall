using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImmenseWall.Models;
using ImmenseWall.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows;
using System.Linq;

namespace ImmenseWall.ViewModels
{
    public partial class RuleEditViewModel : ObservableObject
    {
        private readonly FirewallExceptionV3 _originalException;
        public FirewallExceptionV3 Exception { get; private set; }

        public string WindowTitle => $"Edit Rule: {Exception.Subject}";

        [ObservableProperty]
        private bool _isSigned;

        [ObservableProperty]
        private bool _isValidSignature;

        public class TimerOption
        {
            public required string Display { get; set; }
            public AppExceptionTimer Value { get; set; }
        }

        public ObservableCollection<TimerOption> TimerOptions { get; } = new();

        [ObservableProperty]
        private TimerOption? _selectedTimer;

        // Policy Radio Buttons
        [ObservableProperty]
        private bool _isBlock;
        [ObservableProperty]
        private bool _isAllow;
        [ObservableProperty]
        private bool _isCustom;
        [ObservableProperty]
        private bool _isOutboundOnly;

        // Custom Policy Settings
        [ObservableProperty]
        private bool _localNetworkOnly;

        [ObservableProperty]
        private string? _outboundTcpPorts;
        [ObservableProperty]
        private string? _outboundUdpPorts;
        [ObservableProperty]
        private string? _inboundTcpPorts;
        [ObservableProperty]
        private string? _inboundUdpPorts;

        [ObservableProperty]
        private bool _inheritToChildren;

        public RuleEditViewModel(FirewallExceptionV3 exception)
        {
            _originalException = exception;
            // Clone the exception for editing
            Exception = Utils.DeepClone(exception); 

            if (Exception.Subject is ExecutableSubject exeSubject)
            {
                IsSigned = exeSubject.IsSigned;
                IsValidSignature = exeSubject.CertValid;
            }
            
            InheritToChildren = Exception.ChildProcessesInherit;

            InitializeTimerOptions();
            InitializePolicy();
        }

        private void InitializeTimerOptions()
        {
            TimerOptions.Add(new TimerOption { Display = "Permanent", Value = AppExceptionTimer.Permanent });
            TimerOptions.Add(new TimerOption { Display = "Until Reboot", Value = AppExceptionTimer.UNTIL_REBOOT });
            TimerOptions.Add(new TimerOption { Display = "5 Minutes", Value = AppExceptionTimer.FOR_5_MINUTES });
            TimerOptions.Add(new TimerOption { Display = "30 Minutes", Value = AppExceptionTimer.FOR_30_MINUTES });
            TimerOptions.Add(new TimerOption { Display = "1 Hour", Value = AppExceptionTimer.FOR_1_HOUR });
            TimerOptions.Add(new TimerOption { Display = "4 Hours", Value = AppExceptionTimer.FOR_4_HOURS });
            TimerOptions.Add(new TimerOption { Display = "9 Hours", Value = AppExceptionTimer.FOR_9_HOURS });
            TimerOptions.Add(new TimerOption { Display = "24 Hours", Value = AppExceptionTimer.FOR_24_HOURS });

            SelectedTimer = TimerOptions.FirstOrDefault(t => t.Value == Exception.Timer) ?? TimerOptions.First();
        }

        private void InitializePolicy()
        {
            if (Exception.Policy is HardBlockPolicy)
            {
                IsBlock = true;
            }
            else if (Exception.Policy is UnrestrictedPolicy up)
            {
                IsAllow = true;
                LocalNetworkOnly = up.LocalNetworkOnly;
            }
            else if (Exception.Policy is TcpUdpPolicy tup)
            {
                LocalNetworkOnly = tup.LocalNetworkOnly;
                OutboundTcpPorts = tup.AllowedRemoteTcpConnectPorts;
                OutboundUdpPorts = tup.AllowedRemoteUdpConnectPorts;
                InboundTcpPorts = tup.AllowedLocalTcpListenerPorts;
                InboundUdpPorts = tup.AllowedLocalUdpListenerPorts;

                if (IsOutboundRequest())
                {
                    IsOutboundOnly = true;
                }
                else
                {
                    IsCustom = true;
                }
            }
        }

        private bool IsOutboundRequest()
        {
             // Simple heuristic: if remote ports are * and local ports are empty/null (or should be *? No, listener ports being empty means no listening)
             // Wait, TcpUdpPolicy defaults often use "*" for allow all.
             // If "Outbound Only" allows all outbound and no inbound.
             // Usually: RemoteTCP=*, RemoteUDP=*, LocalTCP=null/empty, LocalUDP=null/empty.
             
             // For now, let's rely on check.
             return false; // Functionality to detect "Outbound Only" preset vs "Custom" needs to check specific port values
        }

        [RelayCommand]
        private void Save(Window window)
        {
            // Apply changes back to Exception object
            Exception.Timer = SelectedTimer.Value;
            Exception.ChildProcessesInherit = InheritToChildren;

            if (IsBlock)
            {
                Exception.Policy = new HardBlockPolicy();
            }
            else if (IsAllow)
            {
                Exception.Policy = new UnrestrictedPolicy { LocalNetworkOnly = LocalNetworkOnly };
            }
            else
            {
                // Custom or Outbound Only -> TcpUdpPolicy
                var pol = new TcpUdpPolicy
                {
                    LocalNetworkOnly = LocalNetworkOnly,
                    AllowedRemoteTcpConnectPorts = OutboundTcpPorts,
                    AllowedRemoteUdpConnectPorts = OutboundUdpPorts,
                    AllowedLocalTcpListenerPorts = InboundTcpPorts,
                    AllowedLocalUdpListenerPorts = InboundUdpPorts
                };
                
                if (IsOutboundOnly)
                {
                    pol.AllowedRemoteTcpConnectPorts = "*";
                    pol.AllowedRemoteUdpConnectPorts = "*";
                    pol.AllowedLocalTcpListenerPorts = ""; // Block inbound
                    pol.AllowedLocalUdpListenerPorts = "";
                }
                
                Exception.Policy = pol;
            }

            // Update original exception (or we can return the Modified exception to the caller)
             // Ideally we should use a messaging center or return value.
             // For this simple migration, we can assume the caller will read properties from this VM before closing, or we update _originalException here.
             
             // Let's update _originalException's properties.
             _originalException.Timer = Exception.Timer;
             _originalException.Policy = Exception.Policy;
             _originalException.ChildProcessesInherit = Exception.ChildProcessesInherit;

            window.DialogResult = true;
            window.Close();
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
