using ImmenseWall.ViewModels;
using System;
using System.Windows.Input;

namespace pylorak.TinyWall.ViewModels
{
    public class ServiceInfoViewModel : ViewModelBase
    {
        private string _displayName;
        private string _serviceName;
        private string _executablePath;
        private bool _isSelected;

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string ServiceName
        {
            get => _serviceName;
            set => SetProperty(ref _serviceName, value);
        }

        public string ExecutablePath
        {
            get => _executablePath;
            set => SetProperty(ref _executablePath, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}