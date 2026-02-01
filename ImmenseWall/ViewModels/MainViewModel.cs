using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ImmenseWall.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "ImmenseWall";

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        private readonly IServiceProvider _serviceProvider;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            Navigate("Status");
        }

        [RelayCommand]
        private void Navigate(string destination)
        {
            switch (destination)
            {
                case "Status":
                    CurrentViewModel = _serviceProvider.GetRequiredService<StatusViewModel>();
                    break;
                case "Rules":
                    CurrentViewModel = _serviceProvider.GetRequiredService<RulesViewModel>();
                    break;
                case "Settings":
                    CurrentViewModel = _serviceProvider.GetRequiredService<SettingsViewModel>();
                    break;
                case "Connections":
                    CurrentViewModel = _serviceProvider.GetRequiredService<ConnectionsViewModel>();
                    break;
                case "Lock":
                    // Toggle Lock logic here
                    break;
                default:
                    break;
            }
            Title = $"ImmenseWall - {destination}";
        }
    }
}
