using System.Windows;

namespace pylorak.TinyWall.Views
{
    public partial class ProcessesView : Window
    {
        public ProcessesView(bool multiSelect)
        {
            InitializeComponent();
            DataContext = new ViewModels.ProcessesViewModel(multiSelect);
        }

        public static System.Collections.Generic.List<ProcessInfo> ShowDialog(bool multiSelect, Window owner = null)
        {
            var dialog = new ProcessesView(multiSelect);
            var viewModel = (ViewModels.ProcessesViewModel)dialog.DataContext;

            if (owner != null)
            {
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            var result = new System.Collections.Generic.List<ProcessInfo>();
            viewModel.OnDialogClosed += (dialogResult) =>
            {
                if (dialogResult)
                {
                    foreach (var process in viewModel.SelectedProcesses)
                    {
                        result.Add(process.ProcessInfo);
                    }
                }
                dialog.Close();
            };

            dialog.ShowDialog();
            return result;
        }
    }
}