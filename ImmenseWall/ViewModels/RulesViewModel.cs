using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImmenseWall.Models;
using ImmenseWall.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ImmenseWall.ViewModels
{
    public partial class RulesViewModel : ObservableObject
    {
        private readonly ServerConfiguration _serverConfig;

        [ObservableProperty]
        private ObservableCollection<RuleViewModel> _exceptions;

        [ObservableProperty]
        private RuleViewModel? _selectedRule;

        public RulesViewModel(ServerConfiguration serverConfig)
        {
            _serverConfig = serverConfig;
            Exceptions = new ObservableCollection<RuleViewModel>();
            LoadRules();
        }

        private void LoadRules()
        {
            Exceptions.Clear();
            if (_serverConfig.ActiveProfile?.AppExceptions != null)
            {
                foreach (var ex in _serverConfig.ActiveProfile.AppExceptions)
                {
                    Exceptions.Add(new RuleViewModel(ex));
                }
            }
        }

        [RelayCommand]
        private void AddRule()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Executable",
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var subject = new ExecutableSubject(dialog.FileName);
                var newException = new FirewallExceptionV3(subject, new UnrestrictedPolicy { LocalNetworkOnly = false });
                
                _serverConfig.ActiveProfile.AppExceptions.Add(newException);
                SaveConfig();

                Exceptions.Add(new RuleViewModel(newException));
            }
        }

        [RelayCommand]
        private void DeleteRule()
        {
            if (SelectedRule != null)
            {
                _serverConfig.ActiveProfile.AppExceptions.Remove(SelectedRule.Exception);
                SaveConfig();
                Exceptions.Remove(SelectedRule);
            }
        }

        [RelayCommand]
        private void EditRule()
        {
            if (SelectedRule != null)
            {
                var vm = new RuleEditViewModel(SelectedRule.Exception);
                var view = new Views.RuleEditView { DataContext = vm };
                
                // Set owner to main window
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null && mainWindow != view)
                {
                    view.Owner = mainWindow;
                }

                if (view.ShowDialog() == true)
                {
                    SaveConfig();
                }
            }
        }

        private void SaveConfig()
        {
            // TODO: Centralize config path logic
            var path = Path.Combine(ServerConfiguration.AppDataPath, "config.json");
            _serverConfig.Save(path);
        }
    }
}
