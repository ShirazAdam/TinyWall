using System.Windows.Media.Imaging;

namespace ImmenseWall.ViewModels
{
    public class ProcessInfoViewModel : ViewModelBase
    {
        private string _name;
        private string _services;
        private string _path;
        private BitmapSource _icon;
        private bool _isSelected;
        private ProcessInfo _processInfo;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        public string Path
        {
            get => _path;
            set => SetProperty(ref _path, value);
        }

        public BitmapSource Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public ProcessInfo ProcessInfo
        {
            get => _processInfo;
            set => SetProperty(ref _processInfo, value);
        }
    }
}