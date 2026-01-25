using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace pylorak.TinyWall
{
    public partial class SettingsWindow : Window
    {
        private ControllerSettings originalSettings;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            LoadLanguages();
        }

        private void LoadSettings()
        {
            originalSettings = ActiveConfig.Controller.Clone();
            
            // General tab
            chkStartWithWindows.IsChecked = originalSettings.StartWithWindows;
            chkMinimizeToTray.IsChecked = originalSettings.MinimizeToTray;
            chkShowNotifications.IsChecked = originalSettings.ShowNotifications;
            
            // Language
            cmbLanguage.SelectedItem = originalSettings.Language;
            
            // Logging
            chkEnableLogging.IsChecked = originalSettings.EnableLogging;
            chkLogToFile.IsChecked = originalSettings.LogToFile;
            chkLogToConsole.IsChecked = originalSettings.LogToConsole;
            
            // Firewall tab
            chkAutoApplyRules.IsChecked = originalSettings.AutoApplyRules;
            chkConfirmOutbound.IsChecked = originalSettings.ConfirmOutbound;
            chkBlockUnresolved.IsChecked = originalSettings.BlockUnresolvedDomains;
            
            // Blocking
            chkBlockP2P.IsChecked = originalSettings.BlockP2P;
            chkBlockScanning.IsChecked = originalSettings.BlockScanning;
            chkBlockICMP.IsChecked = originalSettings.BlockICMP;
            
            // Security tab
            chkRequirePassword.IsChecked = originalSettings.HasPassword;
            chkProtectService.IsChecked = originalSettings.ProtectService;
            chkHideProcesses.IsChecked = originalSettings.HideProcesses;
        }

        private void LoadLanguages()
        {
            var languages = new List<string> { "auto", "en", "de", "fr", "es", "it", "ja", "ko", "nl", "pl", "pt-BR", "ru", "zh", "ar", "bg", "hu", "tr" };
            
            cmbLanguage.ItemsSource = languages;
            cmbLanguage.SelectedItem = originalSettings.Language;
        }

        private void SaveSettings()
        {
            // General tab
            ActiveConfig.Controller.StartWithWindows = chkStartWithWindows.IsChecked == true;
            ActiveConfig.Controller.MinimizeToTray = chkMinimizeToTray.IsChecked == true;
            ActiveConfig.Controller.ShowNotifications = chkShowNotifications.IsChecked == true;
            
            // Language
            ActiveConfig.Controller.Language = cmbLanguage.SelectedItem?.ToString() ?? "en";
            
            // Logging
            ActiveConfig.Controller.EnableLogging = chkEnableLogging.IsChecked == true;
            ActiveConfig.Controller.LogToFile = chkLogToFile.IsChecked == true;
            ActiveConfig.Controller.LogToConsole = chkLogToConsole.IsChecked == true;
            
            // Firewall tab
            ActiveConfig.Controller.AutoApplyRules = chkAutoApplyRules.IsChecked == true;
            ActiveConfig.Controller.ConfirmOutbound = chkConfirmOutbound.IsChecked == true;
            ActiveConfig.Controller.BlockUnresolvedDomains = chkBlockUnresolved.IsChecked == true;
            
            // Blocking
            ActiveConfig.Controller.BlockP2P = chkBlockP2P.IsChecked == true;
            ActiveConfig.Controller.BlockScanning = chkBlockScanning.IsChecked == true;
            ActiveConfig.Controller.BlockICMP = chkBlockICMP.IsChecked == true;
            
            // Security tab
            ActiveConfig.Controller.HasPassword = chkRequirePassword.IsChecked == true;
            ActiveConfig.Controller.ProtectService = chkProtectService.IsChecked == true;
            ActiveConfig.Controller.HideProcesses = chkHideProcesses.IsChecked == true;
            
            // Handle password change if needed
            if (chkRequirePassword.IsChecked == true)
            {
                string newPassword = txtPassword.Password;
                string confirmPassword = txtPasswordConfirm.Password;
                
                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (newPassword != confirmPassword)
                    {
                        MessageBox.Show("Passwords do not match.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    ActiveConfig.Controller.SetPassword(newPassword);
                }
            }
            else
            {
                // Clear password if unchecked
                ActiveConfig.Controller.ClearPassword();
            }
            
            ActiveConfig.Controller.Save();
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Revert changes
            ActiveConfig.Controller = originalSettings;
            DialogResult = false;
            Close();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
        }
    }
}