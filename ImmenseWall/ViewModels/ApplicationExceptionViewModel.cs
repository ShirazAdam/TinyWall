using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ImmenseWall.Properties;
using pylorak.TinyWall.ViewModels;

namespace ImmenseWall.ViewModels
{
    public class ExceptionSubjectItem : ViewModelBase
    {
        public ExceptionSubject Subject { get; }
        public string DisplayText { get; }
        public string SubjectType { get; }

        public ExceptionSubjectItem(FirewallExceptionV3 exception)
        {
            Subject = exception.Subject;

            switch (exception.Subject.SubjectType)
            {
                case SubjectType.Executable:
                    var exeSubj = (ExecutableSubject)exception.Subject;
                    DisplayText = exeSubj.ExecutablePath;
                    SubjectType = Resources.Messages.SubjectTypeExecutable;
                    break;
                case SubjectType.Service:
                    var srvSubj = (ServiceSubject)exception.Subject;
                    DisplayText = $"{srvSubj.ServiceName} ({srvSubj.ExecutablePath})";
                    SubjectType = Resources.Messages.SubjectTypeService;
                    break;
                case SubjectType.AppContainer:
                    var uwpSubj = (AppContainerSubject)exception.Subject;
                    DisplayText = uwpSubj.DisplayName;
                    SubjectType = Resources.Messages.SubjectTypeUwpApp;
                    break;
                case SubjectType.Global:
                    DisplayText = Resources.Messages.SubjectTypeGlobal;
                    SubjectType = Resources.Messages.SubjectTypeGlobal;
                    break;
            }
        }
    }

    public class ApplicationExceptionViewModel : ViewModelBase
    {
        private AppExceptionTimer _selectedTimer;
        private bool _inheritToChildren;
        private bool _restrictToLocalNetwork;
        private string _outboundPortTcp;
        private string _outboundPortUdp;
        private string _listenPortTcp;
        private string _listenPortUdp;
        private RestrictionType _selectedRestriction;
        private ExceptionSubjectItem _selectedSubject;
        private string _bannerText;
        private string _bannerColor;

        public ObservableCollection<ExceptionSubjectItem> Subjects { get; } = new();
        public ObservableCollection<KeyValuePair<string, AppExceptionTimer>> TimerOptions { get; } = new();
        public ObservableCollection<FirewallExceptionV3> ExceptionSettings { get; } = new();

        public ExceptionSubjectItem SelectedSubject
        {
            get => _selectedSubject;
            set => SetField(ref _selectedSubject, value);
        }

        public AppExceptionTimer SelectedTimer
        {
            get => _selectedTimer;
            set
            {
                if (SetField(ref _selectedTimer, value) && ExceptionSettings.Count > 0)
                {
                    ExceptionSettings[0].Timer = value;
                }
            }
        }

        public bool InheritToChildren
        {
            get => _inheritToChildren;
            set
            {
                if (SetField(ref _inheritToChildren, value))
                {
                    Parallel.For(0, ExceptionSettings.Count - 1, (i, _) =>
                    {
                        ExceptionSettings[i].ChildProcessesInherit = value;
                    });
                }
            }
        }

        public bool RestrictToLocalNetwork
        {
            get => _restrictToLocalNetwork;
            set => SetField(ref _restrictToLocalNetwork, value);
        }

        public string OutboundPortTcp
        {
            get => _outboundPortTcp;
            set => SetField(ref _outboundPortTcp, value);
        }

        public string OutboundPortUdp
        {
            get => _outboundPortUdp;
            set => SetField(ref _outboundPortUdp, value);
        }

        public string ListenPortTcp
        {
            get => _listenPortTcp;
            set => SetField(ref _listenPortTcp, value);
        }

        public string ListenPortUdp
        {
            get => _listenPortUdp;
            set => SetField(ref _listenPortUdp, value);
        }

        public RestrictionType SelectedRestriction
        {
            get => _selectedRestriction;
            set
            {
                if (SetField(ref _selectedRestriction, value))
                {
                    UpdateRestrictionUI();
                }
            }
        }

        public string BannerText
        {
            get => _bannerText;
            private set => SetField(ref _bannerText, value);
        }

        public string BannerColor
        {
            get => _bannerColor;
            private set => SetField(ref _bannerColor, value);
        }

        public ICommand BrowseCommand { get; }
        public ICommand ProcessCommand { get; }
        public ICommand ServiceCommand { get; }
        public ICommand UwpAppCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public ApplicationExceptionViewModel(FirewallExceptionV3 exception = null)
        {
            InitializeTimerOptions();

            if (exception != null)
            {
                ExceptionSettings.Add(exception);
                UpdateUI();
            }

            BrowseCommand = new RelayCommand(ExecuteBrowse);
            ProcessCommand = new RelayCommand(ExecuteProcess);
            ServiceCommand = new RelayCommand(ExecuteService);
            UwpAppCommand = new RelayCommand(ExecuteUwpApp);
            RemoveCommand = new RelayCommand(ExecuteRemove);
            OkCommand = new RelayCommand(ExecuteOk);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void InitializeTimerOptions()
        {
            TimerOptions.Add(new KeyValuePair<string, AppExceptionTimer>(Resources.Messages.Permanent, AppExceptionTimer.Permanent));
            TimerOptions.Add(new KeyValuePair<string, AppExceptionTimer>(Resources.Messages.UntilReboot, AppExceptionTimer.UNTIL_REBOOT));
            TimerOptions.Add(new KeyValuePair<string, AppExceptionTimer>(
                string.Format(CultureInfo.CurrentCulture, Resources.Messages.XMinutes, 5), AppExceptionTimer.FOR_5_MINUTES));
            // Add other timer options...
        }

        private void UpdateUI()
        {
            if (ExceptionSettings.Count == 0) return;

            var index = ExceptionSettings.Count - 1;
            var exception = ExceptionSettings[index];

            // Update timer
            SelectedTimer = exception.Timer;

            // Update banner
            UpdateBanner(exception);

            // Add subject to list
            Subjects.Add(new ExceptionSubjectItem(exception));
            SelectedSubject = Subjects.Last();

            // Update restriction settings
            UpdateRestrictionFromException(exception);
        }

        private void UpdateBanner(FirewallExceptionV3 exception)
        {
            // Similar logic to WinForms version
            // Determine banner color and text based on signature status
        }

        private void UpdateRestrictionFromException(FirewallExceptionV3 exception)
        {
            switch (exception.Policy.PolicyType)
            {
                case PolicyType.HardBlock:
                    SelectedRestriction = RestrictionType.Block;
                    break;
                case PolicyType.TcpUdpOnly:
                    var pol = (TcpUdpPolicy)exception.Policy;
                    // Determine restriction type based on port settings
                    break;
                case PolicyType.Unrestricted:
                    SelectedRestriction = RestrictionType.Unrestricted;
                    break;
            }
        }

        private void UpdateRestrictionUI()
        {
            switch (SelectedRestriction)
            {
                case RestrictionType.Block:
                    OutboundPortTcp = string.Empty;
                    OutboundPortUdp = string.Empty;
                    ListenPortTcp = string.Empty;
                    ListenPortUdp = string.Empty;
                    break;
                case RestrictionType.OnlySpecifiedPorts:
                    // Enable port input fields
                    break;
                case RestrictionType.TcpUdpOut:
                    OutboundPortTcp = "*";
                    OutboundPortUdp = "*";
                    ListenPortTcp = string.Empty;
                    ListenPortUdp = string.Empty;
                    break;
                case RestrictionType.TcpUdpUnrestricted:
                    OutboundPortTcp = "*";
                    OutboundPortUdp = "*";
                    ListenPortTcp = "*";
                    ListenPortUdp = "*";
                    break;
                case RestrictionType.Unrestricted:
                    OutboundPortTcp = "*";
                    OutboundPortUdp = "*";
                    ListenPortTcp = "*";
                    ListenPortUdp = "*";
                    break;
            }
        }

        private void ExecuteBrowse()
        {
            // Implement file dialog
        }

        private void ExecuteProcess()
        {
            // Show process selection dialog
        }

        private void ExecuteService()
        {
            // Show service selection
        }

        private void ExecuteUwpApp()
        {
            // Show UWP app selection
        }

        private void ExecuteRemove()
        {
            if (SelectedSubject != null)
            {
                var exception = ExceptionSettings.FirstOrDefault(e => e.Subject.Equals(SelectedSubject.Subject));
                if (exception != null)
                {
                    ExceptionSettings.Remove(exception);
                    Subjects.Remove(SelectedSubject);
                }
            }
        }

        private void ExecuteOk()
        {
            // Validate and save settings
            if (!ValidatePorts()) return;

            // Update policies based on selected restriction
            UpdatePolicies();

            // Set creation date
            var now = DateTime.Now;
            Parallel.For(0, ExceptionSettings.Count - 1, (i, _) =>
            {
                ExceptionSettings[i].CreationDate = now;
            });

            // Close with OK result
        }

        private bool ValidatePorts()
        {
            try
            {
                // Validate port lists
                return true;
            }
            catch
            {
                // Show error message
                return false;
            }
        }

        private void UpdatePolicies()
        {
            switch (SelectedRestriction)
            {
                case RestrictionType.Block:
                    SetPolicyForAll(new HardBlockPolicy());
                    break;
                case RestrictionType.OnlySpecifiedPorts:
                case RestrictionType.TcpUdpOut:
                case RestrictionType.TcpUdpUnrestricted:
                    var tcpUdpPolicy = new TcpUdpPolicy
                    {
                        LocalNetworkOnly = RestrictToLocalNetwork,
                        AllowedRemoteTcpConnectPorts = CleanupPortsList(OutboundPortTcp),
                        AllowedRemoteUdpConnectPorts = CleanupPortsList(OutboundPortUdp),
                        AllowedLocalTcpListenerPorts = CleanupPortsList(ListenPortTcp),
                        AllowedLocalUdpListenerPorts = CleanupPortsList(ListenPortUdp)
                    };
                    SetPolicyForAll(tcpUdpPolicy);
                    break;
                case RestrictionType.Unrestricted:
                    var unrestrictedPolicy = new UnrestrictedPolicy
                    {
                        LocalNetworkOnly = RestrictToLocalNetwork
                    };
                    SetPolicyForAll(unrestrictedPolicy);
                    break;
            }
        }

        private void SetPolicyForAll(Policy policy)
        {
            Parallel.For(0, ExceptionSettings.Count - 1, (i, _) =>
            {
                ExceptionSettings[i].Policy = policy;
            });
        }

        private string CleanupPortsList(string str)
        {
            // Same as WinForms version
            return str;
        }

        private void ExecuteCancel()
        {
            // Cancel
        }
    }

    public enum RestrictionType
    {
        Block,
        OnlySpecifiedPorts,
        TcpUdpOut,
        TcpUdpUnrestricted,
        Unrestricted
    }
}