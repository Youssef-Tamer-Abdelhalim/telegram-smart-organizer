using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Drawing;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Services;
using TelegramOrganizer.UI.Views;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramOrganizer.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SmartOrganizerEngine _engine;
        private readonly IContextDetector _contextDetector;
        private readonly ISettingsService _settingsService;
        private readonly IRulesService _rulesService;
        private readonly IStatisticsService _statisticsService;
        private readonly ILoggingService _logger;
        private readonly IUpdateService _updateService;
        private readonly IErrorReportingService _errorService;

        private DispatcherTimer? _timer;
        private NotifyIcon? _notifyIcon;

        [ObservableProperty]
        private string _currentWindowTitle = "Waiting...";

        [ObservableProperty]
        private string _processName = "N/A";

        [ObservableProperty]
        private string _lastLog = "Engine Started...";

        [ObservableProperty]
        private int _pendingDownloads = 0;

        [ObservableProperty]
        private string _appVersion = "1.0.0";

        public MainViewModel(
            SmartOrganizerEngine engine, 
            IContextDetector contextDetector, 
            ISettingsService settingsService,
            IRulesService rulesService,
            IStatisticsService statisticsService,
            ILoggingService loggingService,
            IUpdateService updateService,
            IErrorReportingService errorService)
        {
            _engine = engine;
            _contextDetector = contextDetector;
            _settingsService = settingsService;
            _rulesService = rulesService;
            _statisticsService = statisticsService;
            _logger = loggingService;
            _updateService = updateService;
            _errorService = errorService;

            // Set app version
            _appVersion = _updateService.CurrentVersion;

            InitializeSystemTray();

            _engine.OperationCompleted += (s, message) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    LastLog = message;
                    
                    var settings = _settingsService.LoadSettings();
                    if (settings.ShowNotifications && message.Contains("[SUCCESS]"))
                    {
                        _notifyIcon?.ShowBalloonTip(2000, "File Organized", message, ToolTipIcon.Info);
                    }
                });
            };

            _engine.Start();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => UpdateDebugInfo();
            _timer.Start();
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "Telegram Smart Organizer"
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Statistics", null, (s, e) => OpenStatistics());
            contextMenu.Items.Add("Rules", null, (s, e) => OpenRules());
            contextMenu.Items.Add("Settings", null, (s, e) => OpenSettings());
            contextMenu.Items.Add("Logs", null, (s, e) => OpenLogFile());
            contextMenu.Items.Add("Error Reports", null, (s, e) => OpenErrorReports());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Check for Updates", null, async (s, e) => await CheckForUpdatesManually());
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.WindowState = System.Windows.WindowState.Normal;
                mainWindow.ShowInTaskbar = true;
                mainWindow.Activate();
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(
                new SettingsViewModel(_settingsService));
            settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
            settingsWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenRules()
        {
            var rulesWindow = new RulesWindow(
                new RulesViewModel(_rulesService));
            rulesWindow.Owner = System.Windows.Application.Current.MainWindow;
            rulesWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenStatistics()
        {
            var statsWindow = new StatisticsWindow(
                new StatisticsViewModel(_statisticsService));
            statsWindow.Owner = System.Windows.Application.Current.MainWindow;
            statsWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenLogFile()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
                
                if (Directory.Exists(appFolder))
                {
                    Process.Start("explorer.exe", appFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to open log folder", ex);
            }
        }

        [RelayCommand]
        private void OpenErrorReports()
        {
            try
            {
                string errorLogsPath = _errorService.GetErrorLogsPath();
                
                if (Directory.Exists(errorLogsPath))
                {
                    Process.Start("explorer.exe", errorLogsPath);
                }
                else
                {
                    System.Windows.MessageBox.Show("No error reports found.", "Error Reports", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to open error reports folder", ex);
            }
        }

        [RelayCommand]
        private async Task CheckForUpdatesManually()
        {
            try
            {
                LastLog = "[UPDATE] Checking for updates...";
                
                var updateInfo = await _updateService.CheckForUpdatesAsync();
                
                if (updateInfo.IsUpdateAvailable)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"A new version ({updateInfo.LatestVersion}) is available!\n\n" +
                        $"Current version: {updateInfo.CurrentVersion}\n\n" +
                        "Would you like to download it now?",
                        "Update Available",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes && !string.IsNullOrEmpty(updateInfo.DownloadUrl))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = updateInfo.DownloadUrl,
                            UseShellExecute = true
                        });
                    }
                    
                    LastLog = $"[UPDATE] New version available: {updateInfo.LatestVersion}";
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"You are running the latest version ({updateInfo.CurrentVersion}).",
                        "No Updates",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    
                    LastLog = "[UPDATE] You have the latest version";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to check for updates", ex);
                LastLog = "[UPDATE] Failed to check for updates";
                
                System.Windows.MessageBox.Show(
                    "Failed to check for updates. Please check your internet connection.",
                    "Update Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        private void ExitApplication()
        {
            _logger.LogInfo("Application exiting...");
            _notifyIcon?.Dispose();
            _engine.Stop();
            System.Windows.Application.Current.Shutdown();
        }

        public void MinimizeToTray()
        {
            var settings = _settingsService.LoadSettings();
            if (settings.MinimizeToTray)
            {
                System.Windows.Application.Current.MainWindow?.Hide();
            }
        }

        private void UpdateDebugInfo()
        {
            if (_contextDetector != null)
            {
                CurrentWindowTitle = _contextDetector.GetActiveWindowTitle();
                ProcessName = _contextDetector.GetProcessName();
            }
        }

        public void Cleanup()
        {
            _logger.LogInfo("MainViewModel cleanup...");
            _notifyIcon?.Dispose();
            _timer?.Stop();
            _engine?.Stop();
        }
    }
}