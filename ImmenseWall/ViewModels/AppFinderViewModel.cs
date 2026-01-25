using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ImmenseWall.Properties;
using pylorak.TinyWall.ViewModels;

namespace ImmenseWall.ViewModels
{
    public class FoundApplication : ViewModelBase
    {
        private bool _isSelected;
        public Application Application { get; }
        public string Name => Application.Name;
        public string IconKey => Application.Name;
        public bool IsRecommended => Application.HasFlag("TWUI:Recommended");

        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public FoundApplication(Application application)
        {
            Application = application;
            IsSelected = IsRecommended;
        }
    }

    public class AppFinderViewModel : ViewModelBase
    {
        private bool _isSearching;
        private string _statusText;
        private bool _isSelectImportantVisible;
        private CancellationTokenSource _cancellationTokenSource;

        public ObservableCollection<FoundApplication> FoundApplications { get; } = new();
        public List<FirewallExceptionV3> SelectedExceptions { get; } = new();
        public ICommand StartStopDetectionCommand { get; }
        public ICommand SelectImportantCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public bool IsSearching
        {
            get => _isSearching;
            private set => SetField(ref _isSearching, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetField(ref _statusText, value);
        }

        public bool IsSelectImportantVisible
        {
            get => _isSelectImportantVisible;
            private set => SetField(ref _isSelectImportantVisible, value);
        }

        public AppFinderViewModel()
        {
            IsSelectImportantVisible = false;
            StatusText = Resources.Messages.Ready;

            StartStopDetectionCommand = new RelayCommand(ExecuteStartStopDetection);
            SelectImportantCommand = new RelayCommand(ExecuteSelectImportant);
            SelectAllCommand = new RelayCommand(ExecuteSelectAll);
            DeselectAllCommand = new RelayCommand(ExecuteDeselectAll);
            OkCommand = new RelayCommand(ExecuteOk);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private async void ExecuteStartStopDetection()
        {
            if (!IsSearching)
            {
                IsSearching = true;
                FoundApplications.Clear();
                StatusText = Resources.Messages.Searching;

                _cancellationTokenSource = new CancellationTokenSource();
                await Task.Run(() => PerformSearch(_cancellationTokenSource.Token));
            }
            else
            {
                _cancellationTokenSource?.Cancel();
                IsSearching = false;
                StatusText = Resources.Messages.SearchStopped;
            }
        }

        private void PerformSearch(CancellationToken cancellationToken)
        {
            try
            {
                // First, do a fast search
                foreach (var app in GlobalInstances.AppDatabase.KnownApplications)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    if (app.HasFlag("TWUI:Special")) continue;

                    foreach (var subject in app.Components.Select(id => id.SearchForFile()).SelectMany(subjects => subjects))
                    {
                        if (subject is not ExecutableSubject exe) continue;

                        // Add to results
                        pylorak.TinyWall.App.Current.Dispatcher.Invoke(() =>
                        {
                            if (!FoundApplications.Any(fa => fa.Application.Name.Equals(app.Name)))
                            {
                                FoundApplications.Add(new FoundApplication(app));
                            }
                        });
                    }
                }

                if (cancellationToken.IsCancellationRequested) return;

                // Do slow search
                PerformSlowSearch(cancellationToken);

                pylorak.TinyWall.App.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = Resources.Messages.SearchResults;
                    IsSearching = false;
                });
            }
            catch (OperationCanceledException)
            {
                pylorak.TinyWall.App.Current.Dispatcher.Invoke(() =>
                {
                    StatusText = Resources.Messages.SearchCancelled;
                    IsSearching = false;
                });
            }
        }

        private void PerformSlowSearch(CancellationToken cancellationToken)
        {
            // Implementation similar to WinForms version
            // This would search directories for executables
            // Simplified for brevity
        }

        private void ExecuteSelectImportant()
        {
            foreach (var app in FoundApplications.Where(fa => fa.IsRecommended))
            {
                app.IsSelected = true;
            }
        }

        private void ExecuteSelectAll()
        {
            foreach (var app in FoundApplications)
            {
                app.IsSelected = true;
            }
        }

        private void ExecuteDeselectAll()
        {
            foreach (var app in FoundApplications)
            {
                app.IsSelected = false;
            }
        }

        private void ExecuteOk()
        {
            // Populate SelectedExceptions from selected applications
            SelectedExceptions.Clear();
            foreach (var foundApp in FoundApplications.Where(fa => fa.IsSelected))
            {
                // Add exceptions for this application
                // Similar to WinForms logic
            }
        }

        private void ExecuteCancel()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}