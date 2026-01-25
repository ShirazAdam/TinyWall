using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ImmenseWall;
using ImmenseWall.Properties;
using pylorak.Windows;

namespace ImmenseWall.ViewModels
{
    public class ProcessesViewModel : ViewModelBase
    {
        private readonly bool _multiSelect;
        private ObservableCollection<ProcessInfoViewModel> _processes;
        private ObservableCollection<ProcessInfoViewModel> _selectedProcesses;
        private string _searchText;
        private bool _isLoading;
        private string _selectedSortColumn;
        private bool _sortAscending = true;
        private ProcessInfoViewModel _selectedProcess;

        public ObservableCollection<ProcessInfoViewModel> Processes
        {
            get => _processes;
            set => SetProperty(ref _processes, value);
        }

        public ObservableCollection<ProcessInfoViewModel> SelectedProcesses
        {
            get => _selectedProcesses;
            set => SetProperty(ref _selectedProcesses, value);
        }

        public ProcessInfoViewModel SelectedProcess
        {
            get => _selectedProcess;
            set => SetProperty(ref _selectedProcess, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    Task.Run(() => LoadProcessesAsync());
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
                    SortProcesses();
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
                    SortProcesses();
                }
            }
        }

        public ICommand LoadProcessesCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand SortCommand { get; }

        public ProcessesViewModel(bool multiSelect)
        {
            _multiSelect = multiSelect;
            Processes = new ObservableCollection<ProcessInfoViewModel>();
            SelectedProcesses = new ObservableCollection<ProcessInfoViewModel>();

            LoadProcessesCommand = new RelayCommand(async () => await LoadProcessesAsync());
            SelectCommand = new RelayCommand(SelectProcess, CanSelect);
            CancelCommand = new RelayCommand(() => OnDialogClosed?.Invoke(false));
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            SearchCommand = new RelayCommand(() => Task.Run(() => LoadProcessesAsync()));
            SortCommand = new RelayCommand<string>(SortByColumn);

            Task.Run(() => LoadProcessesAsync());
        }

        public event Action<bool> OnDialogClosed;

        private bool CanSelect()
        {
            if (_multiSelect)
                return SelectedProcesses.Any();
            else
                return SelectedProcess != null;
        }

        private void SelectProcess()
        {
            if (_multiSelect)
            {
                // For multi-select, add all selected processes
                foreach (var process in Processes.Where(p => p.IsSelected))
                {
                    SelectedProcesses.Add(process);
                }
            }
            else if (SelectedProcess != null)
            {
                SelectedProcesses.Add(SelectedProcess);
            }
            OnDialogClosed?.Invoke(true);
        }

        private async Task LoadProcessesAsync()
        {
            IsLoading = true;

            await Task.Run(() =>
            {
                var packageList = new UwpPackageList();
                var servicePids = new ServicePidMap();
                var processes = Process.GetProcesses();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    processes = processes.Where(p => p.ProcessName.ToLower()
                        .Contains(SearchText.ToLower())).ToArray();
                }

                var processViewModels = new System.Collections.Generic.List<ProcessInfoViewModel>();

                foreach (var process in processes)
                {
                    try
                    {
                        using (process)
                        {
                            var pid = unchecked((uint)process.Id);
                            var processInfo = ProcessInfo.Create(pid, packageList, servicePids);

                            if (string.IsNullOrEmpty(processInfo.Path))
                                continue;

                            // Check for duplicates
                            bool skip = processViewModels.Any(pvm =>
                                pvm.ProcessInfo.Package == processInfo.Package &&
                                pvm.ProcessInfo.Path == processInfo.Path &&
                                pvm.ProcessInfo.Services.SetEquals(processInfo.Services));

                            if (skip)
                                continue;

                            var viewModel = new ProcessInfoViewModel
                            {
                                Name = processInfo.Package.HasValue ?
                                    processInfo.Package.Value.Name : process.ProcessName,
                                Services = string.Join(", ", processInfo.Services.ToArray()),
                                Path = processInfo.Path,
                                ProcessInfo = processInfo
                            };

                            // Set icon
                            if (processInfo.Package.HasValue)
                            {
                                viewModel.Icon = Resources.Icons.store.ToBitmapSource();
                            }
                            else if (processInfo.Path == "System")
                            {
                                viewModel.Icon = Resources.Icons.windows_small.ToBitmapSource();
                            }
                            else if (NetworkPath.IsNetworkPath(processInfo.Path))
                            {
                                viewModel.Icon = Resources.Icons.network_drive_small.ToBitmapSource();
                            }
                            else if (System.IO.Path.IsPathRooted(processInfo.Path) &&
                                     System.IO.File.Exists(processInfo.Path))
                            {
                                viewModel.Icon = Utils.GetIconContained(processInfo.Path, 16, 16)
                                    .ToBitmapSource();
                            }

                            processViewModels.Add(viewModel);
                        }
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }

                // Update UI on main thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Processes.Clear();
                    foreach (var vm in processViewModels)
                    {
                        Processes.Add(vm);
                    }
                    SortProcesses();
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

        private void SortProcesses()
        {
            if (string.IsNullOrEmpty(SelectedSortColumn))
                return;

            var sorted = SelectedSortColumn switch
            {
                "Name" => SortAscending ?
                    Processes.OrderBy(p => p.Name) :
                    Processes.OrderByDescending(p => p.Name),
                "Services" => SortAscending ?
                    Processes.OrderBy(p => p.Services) :
                    Processes.OrderByDescending(p => p.Services),
                "Path" => SortAscending ?
                    Processes.OrderBy(p => p.Path) :
                    Processes.OrderByDescending(p => p.Path),
                _ => Processes.OrderBy(p => p.Name)
            };

            Processes = new ObservableCollection<ProcessInfoViewModel>(sorted);
        }
    }
}