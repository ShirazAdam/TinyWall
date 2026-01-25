using System.Windows;

namespace ImmenseWall
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize WPF-specific resources
            InitializeResources();

            // Show main window or dialog as needed
            var mainWindow = new ImmenseWall.Views.SettingsView();
            mainWindow.ShowDialog();
        }

        private void InitializeResources()
        {
            // Add any WPF-specific resource dictionaries here
            Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new System.Uri("Themes/Generic.xaml", System.UriKind.Relative)
            });
        }
    }
}