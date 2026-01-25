using pylorak.Windows;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ImmenseWall.ViewModels
{
    public class UwpPackageItem : ViewModelBase
    {
        public UwpPackageList.Package Package { get; }
        public string Name => Package.Name;
        public string Publisher => Package.PublisherId + ", " + Package.Publisher;

        public UwpPackageItem(UwpPackageList.Package package)
        {
            Package = package;
        }
    }

    public class UwpPackagesViewModel : ViewModelBase
    {
        private string _searchText = string.Empty;
        private bool _isLoading;
        private UwpPackageItem _selectedPackage;
        private readonly bool _multiSelect;

        public ObservableCollection<UwpPackageItem> Packages { get; } = new();
        public List<UwpPackageList.Package> SelectedPackages { get; } = new();

        public UwpPackageItem SelectedPackage
        {
            get => _selectedPackage;
            set => SetField(ref _selectedPackage, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    Task.Run(LoadPackages);
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public UwpPackagesViewModel(bool multiSelect)
        {
            _multiSelect = multiSelect;

            OkCommand = new RelayCommand(ExecuteOk, () => SelectedPackage != null);
            CancelCommand = new RelayCommand(ExecuteCancel);
            ClearSearchCommand = new RelayCommand(ExecuteClearSearch);

            Task.Run(LoadPackages);
        }

        private async Task LoadPackages()
        {
            IsLoading = true;

            await Task.Run(() =>
            {
                var packageList = new UwpPackageList();
                List<UwpPackageList.Package> packages;

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    packages = packageList.Where(p =>
                        p.Name.ToLower().Contains(SearchText.ToLower()) ||
                        p.Publisher.ToLower().Contains(SearchText.ToLower())
                    ).ToList();
                }
                else
                {
                    packages = packageList.ToList();
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Packages.Clear();
                    foreach (var package in packages)
                    {
                        Packages.Add(new UwpPackageItem(package));
                    }
                    IsLoading = false;
                });
            });
        }

        private void ExecuteOk()
        {
            if (SelectedPackage != null)
            {
                SelectedPackages.Add(SelectedPackage.Package);
            }
        }

        private void ExecuteCancel()
        {
            // Cancel
        }

        private void ExecuteClearSearch()
        {
            SearchText = string.Empty;
        }
    }
}