using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class AppFinderWindow : Window
    {
        private List<AppInfo> apps = new List<AppInfo>();
        private string selectedAppPath = string.Empty;

        public string SelectedAppPath
        {
            get { return selectedAppPath; }
        }

        public AppFinderWindow()
        {
            InitializeComponent();
            LoadApplications();
        }

        private void LoadApplications()
        {
            apps.Clear();

            // Common application directories
            string[] commonPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "x86"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };

            foreach (string path in commonPaths.Where(p => !string.IsNullOrEmpty(p)))
            {
                if (Directory.Exists(path))
                {
                    FindExecutables(path);
                }
            }

            dataGridApps.ItemsSource = apps;
        }

        private void FindExecutables(string directory)
        {
            try
            {
                string[] exeFiles = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);

                foreach (string exe in exeFiles.Take(100)) // Limit to prevent performance issues
                {
                    if (!apps.Any(a => a.Path.Equals(exe, StringComparison.OrdinalIgnoreCase)))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(exe);
                        apps.Add(new AppInfo { Name = fileName, Path = exe });
                    }
                }

                // Recursively search subdirectories (with depth limit to prevent performance issues)
                if (directory.Split(Path.DirectorySeparatorChar).Length < 5) // Limit recursion depth
                {
                    foreach (string subDir in Directory.GetDirectories(directory))
                    {
                        FindExecutables(subDir);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories that can't be accessed
            }
            catch (DirectoryNotFoundException)
            {
                // Skip directories that don't exist
            }
        }

        private void TxtSearch_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            FilterApplications();
        }

        private void FilterApplications()
        {
            string searchTerm = txtSearch.Text.ToLower();
            
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                dataGridApps.ItemsSource = apps;
            }
            else
            {
                var filteredApps = apps.Where(app =>
                    app.Name.ToLower().Contains(searchTerm) ||
                    app.Path.ToLower().Contains(searchTerm)
                ).ToList();

                dataGridApps.ItemsSource = filteredApps;
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedAppPath = openFileDialog.FileName;
                DialogResult = true;
                Close();
            }
        }

        private void DataGridApps_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnOK.IsEnabled = dataGridApps.SelectedItem != null;
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridApps.SelectedItem is AppInfo selectedApp)
            {
                selectedAppPath = selectedApp.Path;
                DialogResult = true;
                Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class AppInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
    }
}