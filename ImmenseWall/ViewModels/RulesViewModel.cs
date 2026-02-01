using CommunityToolkit.Mvvm.ComponentModel;
using ImmenseWall.Models;
using System.Collections.ObjectModel;

namespace ImmenseWall.ViewModels
{
    public partial class RulesViewModel : ObservableObject
    {
        private readonly ServerConfiguration _serverConfig;

        [ObservableProperty]
        private ObservableCollection<FirewallExceptionV3> _exceptions;

        public RulesViewModel(ServerConfiguration serverConfig)
        {
            _serverConfig = serverConfig;
            _exceptions = new ObservableCollection<FirewallExceptionV3>(_serverConfig.ActiveProfile.AppExceptions);
        }
    }
}
