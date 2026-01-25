using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class ApplicationExceptionWindow : Window
    {
        public ApplicationExceptionWindow(Exception ex, string appName = "")
        {
            InitializeComponent();
            LoadExceptionDetails(ex, appName);
        }

        private void LoadExceptionDetails(Exception ex, string appName)
        {
            txtAppName.Text = appName;
            txtExceptionType.Text = ex.GetType().Name;
            txtExceptionMsg.Text = ex.Message;
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Application: {appName}");
            sb.AppendLine($"Exception Type: {ex.GetType().Name}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine($"Source: {ex.Source}");
            sb.AppendLine($"Target Site: {ex.TargetSite?.Name}");
            sb.AppendLine();
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(ex.StackTrace);
            
            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("Inner Exception:");
                sb.AppendLine($"Type: {ex.InnerException.GetType().Name}");
                sb.AppendLine($"Message: {ex.InnerException.Message}");
                sb.AppendLine($"Stack Trace: {ex.InnerException.StackTrace}");
            }
            
            txtExceptionDetails.Text = sb.ToString();
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(txtExceptionDetails.Text);
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}