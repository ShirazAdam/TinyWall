using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class ProcessesWindow : Window
    {
        private ObservableCollection<ProcessInfo> processes;
        private bool showAllProcesses = false;

        public ProcessesWindow()
        {
            InitializeComponent();
            processes = new ObservableCollection<ProcessInfo>();
            dataGridProcesses.ItemsSource = processes;
            
            // Initialize the data grid with proper checkbox column
            ((DataGridCheckBoxColumn)dataGridProcesses.Columns[0]).Binding = new System.Windows.Data.Binding("IsSelected");
            
            LoadProcesses();
        }

        private void LoadProcesses()
        {
            try
            {
                Process[] procList;
                if (showAllProcesses)
                {
                    procList = Process.GetProcesses();
                }
                else
                {
                    procList = Process.GetProcesses()
                        .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                        .ToArray();
                }

                processes.Clear();
                foreach (var proc in procList)
                {
                    try
                    {
                        processes.Add(new ProcessInfo
                        {
                            ProcessName = proc.ProcessName,
                            PID = proc.Id,
                            MemoryUsage = FormatBytes(proc.WorkingSet64),
                            CPUUsage = "N/A", // Getting CPU usage requires more complex calculations
                            SessionID = proc.SessionId.ToString(),
                            PriorityClass = proc.PriorityClass.ToString(),
                            ExecutablePath = GetProcessPath(proc),
                            IsSelected = false
                        });
                    }
                    catch
                    {
                        // Some processes may not be accessible due to security restrictions
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading processes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }

        private string GetProcessPath(Process process)
        {
            try
            {
                return process.MainModule?.FileName ?? "N/A";
            }
            catch
            {
                return "Access Denied";
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadProcesses();
        }

        private void ChkShowAll_Checked(object sender, RoutedEventArgs e)
        {
            showAllProcesses = true;
            LoadProcesses();
        }

        private void ChkShowAll_Unchecked(object sender, RoutedEventArgs e)
        {
            showAllProcesses = false;
            LoadProcesses();
        }

        private void BtnAddSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedProcesses = processes.Where(p => p.IsSelected).ToList();
            
            if (selectedProcesses.Count == 0)
            {
                MessageBox.Show("Please select at least one process to add.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // In a real implementation, you would add these processes to the firewall exceptions
            // For now, just show which processes were selected
            string selectedNames = string.Join(", ", selectedProcesses.Select(p => p.ProcessName));
            MessageBox.Show($"Selected processes would be added to firewall exceptions:\n{selectedNames}", 
                           "Selected Processes", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class ProcessInfo
    {
        public bool IsSelected { get; set; }
        public string ProcessName { get; set; }
        public int PID { get; set; }
        public string MemoryUsage { get; set; }
        public string CPUUsage { get; set; }
        public string SessionID { get; set; }
        public string PriorityClass { get; set; }
        public string ExecutablePath { get; set; }
    }
}