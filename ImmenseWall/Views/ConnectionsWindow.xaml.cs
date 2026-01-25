using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class ConnectionsWindow : Window
    {
        private ObservableCollection<ConnectionInfo> connections;
        private bool showAllConnections = false;

        public ConnectionsWindow()
        {
            InitializeComponent();
            connections = new ObservableCollection<ConnectionInfo>();
            dataGridConnections.ItemsSource = connections;
            LoadConnections();
        }

        private void LoadConnections()
        {
            connections.Clear();
            var activeConnections = IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpConnections()
                .Where(c => showAllConnections || IsFirewallRelated(c))
                .Select(c => new ConnectionInfo
                {
                    Protocol = "TCP",
                    LocalAddress = c.LocalEndPoint.Address.ToString(),
                    LocalPort = c.LocalEndPoint.Port.ToString(),
                    RemoteAddress = c.RemoteEndPoint.Address.ToString(),
                    RemotePort = c.RemoteEndPoint.Port.ToString(),
                    State = c.State.ToString(),
                    PID = GetProcessIdByTcpConnection(c).ToString(),
                    ProcessName = GetProcessNameByPid(GetProcessIdByTcpConnection(c)),
                    ImagePath = GetProcessPathByPid(GetProcessIdByTcpConnection(c))
                })
                .Concat(
                    IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Select(l => new ConnectionInfo
                    {
                        Protocol = "TCP-LISTEN",
                        LocalAddress = l.Address.ToString(),
                        LocalPort = l.Port.ToString(),
                        RemoteAddress = "*",
                        RemotePort = "*",
                        State = "LISTENING",
                        PID = GetProcessIdByTcpListener(l).ToString(),
                        ProcessName = GetProcessNameByPid(GetProcessIdByTcpListener(l)),
                        ImagePath = GetProcessPathByPid(GetProcessIdByTcpListener(l))
                    })
                )
                .Concat(
                    IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveUdpListeners()
                    .Select(l => new ConnectionInfo
                    {
                        Protocol = "UDP-LISTEN",
                        LocalAddress = l.Address.ToString(),
                        LocalPort = l.Port.ToString(),
                        RemoteAddress = "*",
                        RemotePort = "*",
                        State = "LISTENING",
                        PID = GetProcessIdByUdpListener(l).ToString(),
                        ProcessName = GetProcessNameByPid(GetProcessIdByUdpListener(l)),
                        ImagePath = GetProcessPathByPid(GetProcessIdByUdpListener(l))
                    })
                );

            foreach (var conn in activeConnections)
            {
                connections.Add(conn);
            }
        }

        private bool IsFirewallRelated(TcpConnectionInformation connection)
        {
            // For now, we'll consider all connections as potentially firewall related
            // In a real implementation, this would check against actual firewall rules
            return true;
        }

        private int GetProcessIdByTcpConnection(TcpConnectionInformation connection)
        {
            // Implementation would go here to get process ID by connection
            // This is a simplified approach - in reality, you'd need to query netstat or similar
            return GetProcessIdByPort(connection.LocalEndPoint.Port);
        }

        private int GetProcessIdByTcpListener(System.Net.IPEndPoint endpoint)
        {
            return GetProcessIdByPort(endpoint.Port);
        }

        private int GetProcessIdByUdpListener(System.Net.IPEndPoint endpoint)
        {
            return GetProcessIdByPort(endpoint.Port);
        }

        private int GetProcessIdByPort(int port)
        {
            try
            {
                // This is a simplified approach - in a real implementation, you'd need to use netstat or a system API
                // For now, we'll return a placeholder
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        private string GetProcessNameByPid(int pid)
        {
            try
            {
                if (pid <= 0) return "N/A";
                using (var process = Process.GetProcessById(pid))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetProcessPathByPid(int pid)
        {
            try
            {
                if (pid <= 0) return "N/A";
                using (var process = Process.GetProcessById(pid))
                {
                    return process.MainModule?.FileName ?? "N/A";
                }
            }
            catch
            {
                return "Unknown";
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadConnections();
        }

        private void ChkShowAll_Checked(object sender, RoutedEventArgs e)
        {
            showAllConnections = true;
            LoadConnections();
        }

        private void ChkShowAll_Unchecked(object sender, RoutedEventArgs e)
        {
            showAllConnections = false;
            LoadConnections();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class ConnectionInfo
    {
        public string Protocol { get; set; }
        public string LocalAddress { get; set; }
        public string LocalPort { get; set; }
        public string RemoteAddress { get; set; }
        public string RemotePort { get; set; }
        public string State { get; set; }
        public string PID { get; set; }
        public string ProcessName { get; set; }
        public string ImagePath { get; set; }
    }
}