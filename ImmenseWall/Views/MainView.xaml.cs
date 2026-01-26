using System.Windows;
using ImmenseWall.ViewModels;

namespace ImmenseWall.Views
{
    public partial class MainView : Window
    {
        public MainView(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
