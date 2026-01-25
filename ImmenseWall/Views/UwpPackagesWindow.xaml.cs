using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class UwpPackagesWindow : Window
    {
        private ObservableCollection<UwpPackageInfo> packages;
        private bool showAllPackages = false;

        public UwpPackagesWindow()
        {
            InitializeComponent();
            packages = new ObservableCollection<UwpPackageInfo>();
            dataGridPackages.ItemsSource = packages;
            
            // Initialize the data grid with proper checkbox column
            ((DataGridCheckBoxColumn)dataGridPackages.Columns[0]).Binding = new System.Windows.Data.Binding("IsSelected");
            
            LoadPackagesAsync();
        }

        private async void LoadPackagesAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    var packageList = UwpPackageList.GetPackages(showAllPackages);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        packages.Clear();
                        foreach (var pkg in packageList)
                        {
                            packages.Add(pkg);
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading UWP packages: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPackagesAsync();
        }

        private void ChkShowAll_Checked(object sender, RoutedEventArgs e)
        {
            showAllPackages = true;
            LoadPackagesAsync();
        }

        private void ChkShowAll_Unchecked(object sender, RoutedEventArgs e)
        {
            showAllPackages = false;
            LoadPackagesAsync();
        }

        private void BtnAddSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedPackages = packages.Where(p => p.IsSelected).ToList();
            
            if (selectedPackages.Count == 0)
            {
                MessageBox.Show("Please select at least one package to add.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // In a real implementation, you would add these packages to the firewall exceptions
            // For now, just show which packages were selected
            string selectedNames = string.Join(", ", selectedPackages.Select(p => p.Name));
            MessageBox.Show($"Selected packages would be added to firewall exceptions:\n{selectedNames}", 
                           "Selected Packages", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class UwpPackageInfo
    {
        public bool IsSelected { get; set; }
        public string Name { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public string Architecture { get; set; }
        public string ResourceId { get; set; }
    }
}