using pylorak.TinyWall.Resources;
using pylorak.Windows;
using pylorak.Windows.NetStat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace pylorak.TinyWall.ViewModels
{
    public class ConnectionItem : ViewModelBase
    {
        public ProcessInfo ProcessInfo { get; }
        public string Protocol { get; }
        public string LocalPort { get; }
        public string LocalAddress { get; }
        public string RemotePort { get; }
        public string RemoteAddress { get; }
        public string State { get; }
        public string Direction { get; }
        public DateTime Timestamp { get; }
        public string ServiceNames { get; }
        public string IconKey { get; }

        public ConnectionItem(ProcessInfo pi, string protocol, IPEndPoint localEp,
                            IPEndPoint remoteEp, string state, DateTime ts, RuleDirection dir)
        {
            ProcessInfo = pi;
            Protocol = protocol;
            LocalPort = localEp.Port.ToString(CultureInfo.InvariantCulture).PadLeft(5);
            LocalAddress = localEp.Address.ToString();
            RemotePort = remoteEp.Port.ToString(CultureInfo.InvariantCulture).PadLeft(5);
            RemoteAddress = remoteEp.Address.ToString();
            State = state;
            Timestamp = ts;
            Direction = GetDirectionText(dir);
            ServiceNames = string.Join(", ", pi.Services.ToArray());
            IconKey = DetermineIconKey(pi);
        }

        private string GetDirectionText(RuleDirection dir)
        {
            return dir switch
            {
                RuleDirection.In => Messages.TrafficIn,
                RuleDirection.Out => Messages.TrafficOut,
                _ => string.Empty
            };
        }

        private string DetermineIconKey(ProcessInfo pi)
        {
            if (pi.Package.HasValue) return "store";
            if (pi.Path == "System") return "system";
            if (NetworkPath.IsNetworkPath(pi.Path)) return "network-drive";
            if (Path.IsPathRooted(pi.Path) && File.Exists(pi.Path)) return pi.Path;
            return string.Empty;
        }
    }

    public class ConnectionsViewModel : ViewModelBase
    {
        private readonly TinyWallController _controller;
        private string _searchText = string.Empty;
        private bool _showListen = true;
        private bool _showActive = true;
        private bool _showBlocked;
        private bool _isLoading;
        private ConnectionItem _selectedConnection;

        public ObservableCollection<ConnectionItem> Connections { get; } = new();
        public ConnectionItem SelectedConnection
        {
            get => _selectedConnection;
            set => SetField(ref _selectedConnection, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    Task.Run(UpdateConnectionsList);
                }
            }
        }

        public bool ShowListen
        {
            get => _showListen;
            set
            {
                if (SetField(ref _showListen, value))
                {
                    Task.Run(UpdateConnectionsList);
                }
            }
        }

        public bool ShowActive
        {
            get => _showActive;
            set
            {
                if (SetField(ref _showActive, value))
                {
                    Task.Run(UpdateConnectionsList);
                }
            }
        }

        public bool ShowBlocked
        {
            get => _showBlocked;
            set
            {
                if (SetField(ref _showBlocked, value))
                {
                    Task.Run(UpdateConnectionsList);
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand CloseProcessCommand { get; }
        public ICommand UnblockCommand { get; }
        public ICommand CopyRemoteAddressCommand { get; }
        public ICommand VirusTotalCommand { get; }
        public ICommand ProcessLibraryCommand { get; }
        public ICommand FileNameSearchCommand { get; }
        public ICommand RemoteAddressSearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public ConnectionsViewModel(TinyWallController controller)
        {
            _controller = controller;

            RefreshCommand = new RelayCommand(async () => await UpdateConnectionsList());
            CloseProcessCommand = new RelayCommand(ExecuteCloseProcess, CanExecuteCloseProcess);
            UnblockCommand = new RelayCommand(ExecuteUnblock, CanExecuteUnblock);
            CopyRemoteAddressCommand = new RelayCommand(ExecuteCopyRemoteAddress, () => SelectedConnection != null);
            VirusTotalCommand = new RelayCommand(ExecuteVirusTotal, () => SelectedConnection != null);
            ProcessLibraryCommand = new RelayCommand(ExecuteProcessLibrary, () => SelectedConnection != null);
            FileNameSearchCommand = new RelayCommand(ExecuteFileNameSearch, () => SelectedConnection != null);
            RemoteAddressSearchCommand = new RelayCommand(ExecuteRemoteAddressSearch, () => SelectedConnection != null);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch);
        }

        public async Task UpdateConnectionsList()
        {
            IsLoading = true;

            await Task.Run(() =>
            {
                var connections = new List<ConnectionItem>();
                var packageList = new UwpPackageList();
                var procCache = new Dictionary<uint, string>();
                var servicePids = new ServicePidMap();
                var now = DateTime.Now;

                // Get TCP connections
                ProcessTcpConnections(connections, packageList, procCache, servicePids, now);

                // Get UDP connections
                if (ShowListen)
                {
                    ProcessUdpConnections(connections, packageList, procCache, servicePids, now);
                }

                // Get blocked connections
                if (ShowBlocked)
                {
                    ProcessBlockedConnections(connections, packageList, servicePids);
                }

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var searchLower = SearchText.ToLower();
                    connections = connections.Where(c =>
                        c.ProcessInfo.Path?.ToLower().Contains(searchLower) == true ||
                        c.RemoteAddress?.ToLower().Contains(searchLower) == true ||
                        c.ServiceNames?.ToLower().Contains(searchLower) == true
                    ).ToList();
                }

                // Update UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Connections.Clear();
                    foreach (var conn in connections)
                    {
                        Connections.Add(conn);
                    }
                    IsLoading = false;
                });
            });
        }

        private void ProcessTcpConnections(List<ConnectionItem> connections, UwpPackageList packageList,
                                         Dictionary<uint, string> procCache, ServicePidMap servicePids, DateTime now)
        {
            // Similar to WinForms implementation
            // Process TCP4 and TCP6 tables
        }

        private void ProcessUdpConnections(List<ConnectionItem> connections, UwpPackageList packageList,
                                         Dictionary<uint, string> procCache, ServicePidMap servicePids, DateTime now)
        {
            // Process UDP connections
        }

        private void ProcessBlockedConnections(List<ConnectionItem> connections, UwpPackageList packageList,
                                             ServicePidMap servicePids)
        {
            // Process firewall log entries
        }

        private bool CanExecuteCloseProcess()
        {
            return SelectedConnection != null && SelectedConnection.ProcessInfo.Pid != 0;
        }

        private void ExecuteCloseProcess()
        {
            if (SelectedConnection == null) return;

            try
            {
                using var proc = Process.GetProcessById(unchecked((int)SelectedConnection.ProcessInfo.Pid));
                if (!proc.CloseMainWindow())
                    proc.Kill();

                Task.Run(UpdateConnectionsList);
            }
            catch
            {
                // Handle exception
            }
        }

        private bool CanExecuteUnblock()
        {
            return SelectedConnection != null;
        }

        private void ExecuteUnblock()
        {
            if (!_controller.EnsureUnlockedServer() || SelectedConnection == null) return;

            var selection = new List<ProcessInfo> { SelectedConnection.ProcessInfo };
            _controller.WhitelistProcesses(selection);
        }

        private void ExecuteCopyRemoteAddress()
        {
            if (SelectedConnection != null)
            {
                Clipboard.SetText(SelectedConnection.RemoteAddress);
            }
        }

        private void ExecuteVirusTotal()
        {
            if (SelectedConnection != null)
            {
                const string URL_TEMPLATE = @"https://www.virustotal.com/latest-scan/{0}";
                try
                {
                    var hash = Hasher.HashFile(SelectedConnection.ProcessInfo.Path);
                    var url = string.Format(CultureInfo.InvariantCulture, URL_TEMPLATE, hash);
                    Utils.StartProcess(url, string.Empty, false);
                }
                catch
                {
                    // Show error message
                }
            }
        }

        private void ExecuteProcessLibrary()
        {
            // Similar pattern for other web searches
        }

        private void ExecuteFileNameSearch()
        {
            // Search for filename
        }

        private void ExecuteRemoteAddressSearch()
        {
            // Search for remote address
        }

        private void ExecuteClearSearch()
        {
            SearchText = string.Empty;
        }
    }
}