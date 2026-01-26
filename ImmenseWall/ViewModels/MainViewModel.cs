using CommunityToolkit.Mvvm.ComponentModel;

namespace ImmenseWall.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = "ImmenseWall";

        public MainViewModel()
        {
        }
    }
}
