using ImmenseWall.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Input;

namespace pylorak.TinyWall.ViewModels
{
    public class ServicesViewModel : ViewModelBase
    {
        private ObservableCollection<ServiceInfoViewModel> _services;
        private ServiceInfoViewModel _selectedService;
        private string _searchText;
        private bool _isLoading;
        private string _selectedSortColumn;
        private bool _sortAscending = true;

        public ObservableCollection<ServiceInfoViewModel> Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        public ServiceInfoViewModel SelectedService
        {
            get => _selectedService;
            set => SetProperty(ref _selectedService, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    Task.Run(() => LoadServicesAsync());
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string SelectedSortColumn
        {
            get => _selectedSortColumn;
            set
            {
                if (SetProperty(ref _selectedSortColumn, value))
                {
                    SortServices();
                }
            }
        }

        public bool SortAscending
        {
            get => _sortAscending;
            set
            {
                if (SetProperty(ref _sortAscending, value))
                {
                    SortServices();
                }
            }
        }

        public ICommand LoadServicesCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand SortCommand { get; }

        public ServicesViewModel()
        {
            Services = new ObservableCollection<ServiceInfoViewModel>();

            LoadServicesCommand = new RelayCommand(async () => await LoadServicesAsync());
            SelectCommand = new RelayCommand(SelectService, () => SelectedService != null);
            CancelCommand = new RelayCommand(() => OnDialogClosed?.Invoke(null));
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            SearchCommand = new RelayCommand(() => Task.Run(() => LoadServicesAsync()));
            SortCommand = new RelayCommand<string>(SortByColumn);

            Task.Run(() => LoadServicesAsync());
        }

        public event Action<ServiceSubject> OnDialogClosed;

        private string GetServiceExecutable(string serviceName)
        {
            try
            {
                string imagePath;
                using (RegistryKey keyHklm = Registry.LocalMachine)
                {
                    using RegistryKey key = keyHklm.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName) ??
                                            throw new InvalidOperationException();
                    imagePath = (string)key.GetValue("ImagePath");
                }

                imagePath = imagePath.Replace("\"", string.Empty);

                while (true)
                {
                    if (System.IO.File.Exists(imagePath))
                        return imagePath;

                    int i = imagePath.LastIndexOf(' ');
                    if (i == -1)
                        break;

                    imagePath = imagePath.Substring(0, i);
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void SelectService()
        {
            if (SelectedService != null)
            {
                var serviceSubject = new ServiceSubject(
                    SelectedService.ExecutablePath,
                    SelectedService.ServiceName);
                OnDialogClosed?.Invoke(serviceSubject);
            }
        }

        private async Task LoadServicesAsync()
        {
            IsLoading = true;

            await Task.Run(() =>
            {
                var services = ServiceController.GetServices();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    services = services.Where(s =>
                        s.ServiceName.ToLower().Contains(SearchText.ToLower()) ||
                        s.DisplayName.ToLower().Contains(SearchText.ToLower())
                    ).ToArray();
                }

                var serviceViewModels = new System.Collections.Generic.List<ServiceInfoViewModel>();

                foreach (var service in services)
                {
                    try
                    {
                        var viewModel = new ServiceInfoViewModel
                        {
                            DisplayName = service.DisplayName,
                            ServiceName = service.ServiceName,
                            ExecutablePath = GetServiceExecutable(service.ServiceName)
                        };

                        serviceViewModels.Add(viewModel);
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Services.Clear();
                    foreach (var vm in serviceViewModels)
                    {
                        Services.Add(vm);
                    }
                    SortServices();
                });
            });

            IsLoading = false;
        }

        private void SortByColumn(string columnName)
        {
            if (SelectedSortColumn == columnName)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                SelectedSortColumn = columnName;
                SortAscending = true;
            }
        }

        private void SortServices()
        {
            if (string.IsNullOrEmpty(SelectedSortColumn))
                return;

            var sorted = SelectedSortColumn switch
            {
                "DisplayName" => SortAscending ?
                    Services.OrderBy(s => s.DisplayName) :
                    Services.OrderByDescending(s => s.DisplayName),
                "ServiceName" => SortAscending ?
                    Services.OrderBy(s => s.ServiceName) :
                    Services.OrderByDescending(s => s.ServiceName),
                "ExecutablePath" => SortAscending ?
                    Services.OrderBy(s => s.ExecutablePath) :
                    Services.OrderByDescending(s => s.ExecutablePath),
                _ => Services.OrderBy(s => s.DisplayName)
            };

            Services = new ObservableCollection<ServiceInfoViewModel>(sorted);
        }

        public static ServiceSubject ChooseService(System.Windows.Window parent = null)
        {
            var dialog = new ServicesView();
            var viewModel = new ServicesViewModel();
            dialog.DataContext = viewModel;

            ServiceSubject result = null;
            viewModel.OnDialogClosed += (service) =>
            {
                result = service;
                dialog.Close();
            };

            if (parent != null)
            {
                dialog.Owner = parent;
                dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            }

            dialog.ShowDialog();
            return result;
        }
    }
}