using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using Microsoft.Win32;
using System.IO;

namespace TelegramOrganizer.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly TelegramOrganizer.Core.Contracts.ISettingsService _settingsService;
        private TelegramOrganizer.Core.Models.AppSettings _settings;

        [ObservableProperty]
        private string _destinationBasePath = string.Empty;

        [ObservableProperty]
        private string _downloadsFolderPath = string.Empty;

        [ObservableProperty]
        private int _retentionDays;

        [ObservableProperty]
        private bool _startMinimized;

        [ObservableProperty]
        private bool _minimizeToTray;

        [ObservableProperty]
        private bool _showNotifications;

        [ObservableProperty]
        private bool _useDarkTheme;

        [ObservableProperty]
        private bool _runOnStartup;

        public SettingsViewModel(TelegramOrganizer.Core.Contracts.ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settings = new TelegramOrganizer.Core.Models.AppSettings();
            LoadSettings();
        }

        private void LoadSettings()
        {
            _settings = _settingsService.LoadSettings();

            DestinationBasePath = _settings.DestinationBasePath;
            DownloadsFolderPath = _settings.DownloadsFolderPath;
            RetentionDays = _settings.RetentionDays;
            StartMinimized = _settings.StartMinimized;
            MinimizeToTray = _settings.MinimizeToTray;
            ShowNotifications = _settings.ShowNotifications;
            UseDarkTheme = _settings.UseDarkTheme;
            RunOnStartup = _settings.RunOnStartup;
        }

        [RelayCommand]
        private void BrowseDestination()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select destination folder for organized files",
                SelectedPath = DestinationBasePath,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DestinationBasePath = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private void BrowseDownloads()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select downloads folder to monitor",
                SelectedPath = DownloadsFolderPath,
                ShowNewFolderButton = false
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadsFolderPath = dialog.SelectedPath;
            }
        }

        [RelayCommand]
        private void Save()
        {
            bool themeChanged = _settings.UseDarkTheme != UseDarkTheme;
            
            _settings.DestinationBasePath = DestinationBasePath;
            _settings.DownloadsFolderPath = DownloadsFolderPath;
            _settings.RetentionDays = RetentionDays;
            _settings.StartMinimized = StartMinimized;
            _settings.MinimizeToTray = MinimizeToTray;
            _settings.ShowNotifications = ShowNotifications;
            _settings.UseDarkTheme = UseDarkTheme;
            _settings.RunOnStartup = RunOnStartup;

            _settingsService.SaveSettings(_settings);

            // Handle RunOnStartup registry setting
            SetStartupRegistryKey(RunOnStartup);

            // Apply theme change immediately
            if (themeChanged)
            {
                App.Instance.ApplyTheme(UseDarkTheme);
            }

            System.Windows.MessageBox.Show("Settings saved successfully!", 
                "Settings Saved", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        [RelayCommand]
        private void Reset()
        {
            var result = System.Windows.MessageBox.Show("Reset all settings to default values?", 
                "Reset Settings", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _settings = new TelegramOrganizer.Core.Models.AppSettings();
                _settingsService.SaveSettings(_settings);
                LoadSettings();
                
                // Apply default theme
                App.Instance.ApplyTheme(_settings.UseDarkTheme);
            }
        }

        private void SetStartupRegistryKey(bool enable)
        {
            try
            {
                string appName = "TelegramSmartOrganizer";
                string appPath = AppContext.BaseDirectory + "TelegramSmartOrganizer.exe";

                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                
                if (enable)
                {
                    key?.SetValue(appName, $"\"{appPath}\"");
                }
                else
                {
                    key?.DeleteValue(appName, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Failed to set startup registry: {ex.Message}");
            }
        }
    }
}
