using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImmenseWall.Models;
using pylorak.Windows.NetStat;
using ImmenseWall.Services; // Added for Controller and IconHelper
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System.Net.NetworkInformation; // For TcpState

namespace ImmenseWall.ViewModels
{
    public partial class ConnectionsViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ConnectionItem> _connections = new();

        private readonly Controller _controller;

        public ConnectionsViewModel(Controller controller)
        {
            _controller = controller;
        }

        [RelayCommand]
        public async Task Refresh()
        {
            var list = new ObservableCollection<ConnectionItem>();
            
            await Task.Run(() => 
            {
                // Helper function to create ConnectionItem
                ConnectionItem CreateItem(uint pid, string protocol, string local, string remote, string state)
                {
                    var item = new ConnectionItem
                    {
                        ProcessId = pid,
                        Protocol = protocol,
                        LocalAddress = local,
                        RemoteAddress = remote,
                        State = state,
                        ProcessName = pid.ToString() // Default to PID
                    };

                    try
                    {
                        string path = _controller.TryGetProcessPath(pid); // Converted to uint in Controller
                        if (!string.IsNullOrEmpty(path))
                        {
                            item.ProcessName = System.IO.Path.GetFileName(path);
                            // Verify file exists before trying to get icon to avoid crashes/errors
                            if (System.IO.File.Exists(path))
                            {
                                var icon = Services.IconHelper.GetIcon(path);
                                if (icon != null)
                                {
                                    // Freeze the image source to allow it to be accessed from the UI thread
                                    if (icon.CanFreeze) icon.Freeze();
                                    item.Icon = icon;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors during process/icon resolution
                    }

                    return item;
                }

                // TCP IPv4
                var tcp4 = NetStat.GetExtendedTcp4Table(false);
                foreach (TcpRow row in tcp4)
                {
                    list.Add(CreateItem(row.ProcessId, "TCP", row.LocalEndPoint.ToString(), row.RemoteEndPoint.ToString(), row.State.ToString()));
                }
                
                // TCP IPv6
                var tcp6 = NetStat.GetExtendedTcp6Table(false);
                foreach (TcpRow row in tcp6)
                {
                    list.Add(CreateItem(row.ProcessId, "TCPv6", row.LocalEndPoint.ToString(), row.RemoteEndPoint.ToString(), row.State.ToString()));
                }
                
                // UDP IPv4
                var udp4 = NetStat.GetExtendedUdp4Table(false);
                foreach (UdpRow row in udp4)
                {
                    list.Add(CreateItem(row.ProcessId, "UDP", row.LocalEndPoint.ToString(), "*", "n/a"));
                }
                
                // UDP IPv6
                var udp6 = NetStat.GetExtendedUdp6Table(false);
                foreach (UdpRow row in udp6)
                {
                    list.Add(CreateItem(row.ProcessId, "UDPv6", row.LocalEndPoint.ToString(), "*", "n/a"));
                }
            });

            Connections = list;
        }
    }
}
