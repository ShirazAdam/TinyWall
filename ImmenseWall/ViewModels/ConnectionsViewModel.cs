using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImmenseWall.Models;
using pylorak.Windows.NetStat;
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

        public ConnectionsViewModel()
        {
        }

        [RelayCommand]
        public async Task Refresh()
        {
            var list = new ObservableCollection<ConnectionItem>();
            
            await Task.Run(() => 
            {
                // TCP IPv4
                var tcp4 = NetStat.GetExtendedTcp4Table(false);
                foreach (TcpRow row in tcp4)
                {
                    list.Add(new ConnectionItem 
                    {
                        ProcessId = row.ProcessId,
                        Protocol = "TCP",
                        LocalAddress = row.LocalEndPoint.ToString(),
                        RemoteAddress = row.RemoteEndPoint.ToString(),
                        State = row.State.ToString(),
                        ProcessName = row.ProcessId.ToString() // Placeholder until we have name resolution
                    });
                }
                
                // TCP IPv6
                var tcp6 = NetStat.GetExtendedTcp6Table(false);
                foreach (TcpRow row in tcp6)
                {
                     list.Add(new ConnectionItem 
                    {
                        ProcessId = row.ProcessId,
                        Protocol = "TCPv6",
                        LocalAddress = row.LocalEndPoint.ToString(),
                        RemoteAddress = row.RemoteEndPoint.ToString(),
                        State = row.State.ToString(),
                        ProcessName = row.ProcessId.ToString()
                    });
                }
                
                // UDP IPv4
                var udp4 = NetStat.GetExtendedUdp4Table(false);
                foreach (UdpRow row in udp4)
                {
                     list.Add(new ConnectionItem 
                    {
                        ProcessId = row.ProcessId,
                        Protocol = "UDP",
                        LocalAddress = row.LocalEndPoint.ToString(),
                        RemoteAddress = "*",
                        State = "n/a",
                        ProcessName = row.ProcessId.ToString()
                    });
                }
                
                 // UDP IPv6
                var udp6 = NetStat.GetExtendedUdp6Table(false);
                 foreach (UdpRow row in udp6)
                {
                     list.Add(new ConnectionItem 
                    {
                        ProcessId = row.ProcessId,
                        Protocol = "UDPv6",
                        LocalAddress = row.LocalEndPoint.ToString(),
                        RemoteAddress = "*",
                        State = "n/a",
                        ProcessName = row.ProcessId.ToString()
                    });
                }
            });

            Connections = list;
        }
    }
}
