using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceProcess;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class ServicesWindow : Window
    {
        private ObservableCollection<ServiceInfo> services;
        private bool showAllServices = false;

        public ServicesWindow()
        {
            InitializeComponent();
            services = new ObservableCollection<ServiceInfo>();
            dataGridServices.ItemsSource = services;
            
            // Initialize the data grid with proper checkbox column
            ((DataGridCheckBoxColumn)dataGridServices.Columns[0]).Binding = new System.Windows.Data.Binding("IsSelected");
            
            LoadServices();
        }

        private void LoadServices()
        {
            try
            {
                ServiceController[] scServices;
                if (showAllServices)
                {
                    scServices = ServiceController.GetServices();
                }
                else
                {
                    scServices = ServiceController.GetServices()
                        .Where(s => s.ServiceType == ServiceType.Win32OwnProcess || 
                                   s.ServiceType == ServiceType.Win32ShareProcess)
                        .ToArray();
                }

                services.Clear();
                foreach (var svc in scServices)
                {
                    services.Add(new ServiceInfo
                    {
                        Name = svc.ServiceName,
                        DisplayName = svc.DisplayName,
                        Status = svc.Status.ToString(),
                        StartType = GetServiceStartType(svc.ServiceName),
                        ServiceType = svc.ServiceType.ToString(),
                        BinaryPath = GetServiceBinaryPath(svc.ServiceName),
                        IsSelected = false
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading services: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetServiceStartType(string serviceName)
        {
            try
            {
                // In a real implementation, you would query the registry or WMI to get the start type
                // For now, we'll return a placeholder
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetServiceBinaryPath(string serviceName)
        {
            try
            {
                // In a real implementation, you would query the registry to get the binary path
                // For now, we'll return a placeholder
                return "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadServices();
        }

        private void ChkShowAll_Checked(object sender, RoutedEventArgs e)
        {
            showAllServices = true;
            LoadServices();
        }

        private void ChkShowAll_Unchecked(object sender, RoutedEventArgs e)
        {
            showAllServices = false;
            LoadServices();
        }

        private void BtnAddSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedServices = services.Where(s => s.IsSelected).ToList();
            
            if (selectedServices.Count == 0)
            {
                MessageBox.Show("Please select at least one service to add.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // In a real implementation, you would add these services to the firewall exceptions
            // For now, just show which services were selected
            string selectedNames = string.Join(", ", selectedServices.Select(s => s.DisplayName));
            MessageBox.Show($"Selected services would be added to firewall exceptions:\n{selectedNames}", 
                           "Selected Services", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class ServiceInfo
    {
        public bool IsSelected { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Status { get; set; }
        public string StartType { get; set; }
        public string ServiceType { get; set; }
        public string BinaryPath { get; set; }
    }
}