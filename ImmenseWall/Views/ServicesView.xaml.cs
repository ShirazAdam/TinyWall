using System.Windows;

namespace pylorak.TinyWall.Views
{
    public partial class ServicesView : Window
    {
        public ServicesView()
        {
            InitializeComponent();
            DataContext = new ViewModels.ServicesViewModel();
        }
    }
}