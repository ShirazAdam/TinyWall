using CommunityToolkit.Mvvm.ComponentModel;
using ImmenseWall.Models;

namespace ImmenseWall.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ServerConfiguration _config;

        public SettingsViewModel(ServerConfiguration config)
        {
            _config = config;
        }

        public bool EnableBlocklists
        {
            get => _config.Blocklists.EnableBlocklists;
            set
            {
                if (_config.Blocklists.EnableBlocklists != value)
                {
                    _config.Blocklists.EnableBlocklists = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnablePortBlocklist
        {
            get => _config.Blocklists.EnablePortBlocklist;
            set
            {
                if (_config.Blocklists.EnablePortBlocklist != value)
                {
                    _config.Blocklists.EnablePortBlocklist = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool EnableHostsBlocklist
        {
            get => _config.Blocklists.EnableHostsBlocklist;
            set
            {
                if (_config.Blocklists.EnableHostsBlocklist != value)
                {
                    _config.Blocklists.EnableHostsBlocklist = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool LockHostsFile
        {
            get => _config.LockHostsFile;
            set
            {
                if (_config.LockHostsFile != value)
                {
                    _config.LockHostsFile = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoUpdateCheck
        {
            get => _config.AutoUpdateCheck;
            set
            {
                if (_config.AutoUpdateCheck != value)
                {
                    _config.AutoUpdateCheck = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AllowLocalSubnet
        {
            get => _config.ActiveProfile.AllowLocalSubnet;
            set
            {
                if (_config.ActiveProfile.AllowLocalSubnet != value)
                {
                    _config.ActiveProfile.AllowLocalSubnet = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool DisplayOffBlock
        {
            get => _config.ActiveProfile.DisplayOffBlock;
            set
            {
                if (_config.ActiveProfile.DisplayOffBlock != value)
                {
                    _config.ActiveProfile.DisplayOffBlock = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
