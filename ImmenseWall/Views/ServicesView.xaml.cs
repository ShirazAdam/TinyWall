using System.Windows;
using ImmenseWall.ViewModels;

namespace pylorak.TinyWall.Views
{
    public partial class ServicesView : Window
    {
        public ServicesView()
        {
            InitializeComponent();
            DataContext = new ServicesViewModel();
        }
    }
}