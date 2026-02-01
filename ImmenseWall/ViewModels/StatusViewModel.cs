using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace ImmenseWall.ViewModels
{
    public partial class StatusViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _statusMessage = "Firewall Status: Normal";

        [ObservableProperty]
        private Models.FirewallMode _currentMode = Models.FirewallMode.Normal;

        public static Models.FirewallMode[] Modes => (Models.FirewallMode[])Enum.GetValues(typeof(Models.FirewallMode));

        public StatusViewModel()
        {
        }
    }
}
