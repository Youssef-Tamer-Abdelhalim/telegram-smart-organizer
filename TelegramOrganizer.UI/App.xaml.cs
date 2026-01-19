using System;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Services;
using TelegramOrganizer.Infra.Services;
using TelegramOrganizer.UI.ViewModels;
using TelegramOrganizer.UI.Views;

namespace TelegramOrganizer.UI
{
    public partial class App : System.Windows.Application
    {
        // ده الكونتينر اللي شايل كل "عفش" البرنامج
        public IServiceProvider Services { get; }
        
        private static App? _instance;
        public static App Instance => _instance ?? throw new InvalidOperationException("App not initialized");

        public App()
        {
            _instance = this;
            Services = ConfigureServices();
            
            // Setup global exception handling
            SetupExceptionHandling();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // 1. Register Core Services (order matters for dependencies)
            
            // Logging Service - needed by all
            services.AddSingleton<ILoggingService, FileLoggingService>();
            
            // Settings Service - needed by FileOrganizer and Engine
            services.AddSingleton<ISettingsService, JsonSettingsService>();
            
            // Persistence Service - needed by Engine (v1.0 compatibility)
            services.AddSingleton<IPersistenceService, JsonPersistenceService>();
            
            // Rules Service - for custom organization rules
            services.AddSingleton<IRulesService, JsonRulesService>();
            
            // Statistics Service - for tracking metrics
            services.AddSingleton<IStatisticsService, JsonStatisticsService>();
            
            // Context Detection
            services.AddSingleton<IContextDetector, Win32ContextDetector>();
            
            // File Watching
            services.AddSingleton<IFileWatcher, WindowsWatcherService>();
            
            // File Organization - depends on ISettingsService, IRulesService, IStatisticsService
            services.AddSingleton<IFileOrganizer, FileOrganizerService>();
            
            // V2.0: Database Service
            services.AddSingleton<IDatabaseService, SQLiteDatabaseService>();
            
            // V2.0: Download Session Manager
            services.AddSingleton<IDownloadSessionManager, DownloadSessionManager>();
            
            // V2.0: Download Burst Detector
            services.AddSingleton<IDownloadBurstDetector, DownloadBurstDetector>();
            
            // V2.0: Background Window Monitor
            services.AddSingleton<IBackgroundWindowMonitor, BackgroundWindowMonitor>();
            
            // Main Engine - depends on all above services
            services.AddSingleton<SmartOrganizerEngine>();

            // Update Service
            services.AddSingleton<IUpdateService, GitHubUpdateService>();
            
            // Error Reporting Service
            services.AddSingleton<IErrorReportingService, ErrorReportingService>();

            // 2. Register ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<RulesViewModel>();
            services.AddTransient<StatisticsViewModel>();

            // 3. Register Views
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();
            services.AddTransient<RulesWindow>();
            services.AddTransient<StatisticsWindow>();

            return services.BuildServiceProvider();
        }

        // دالة البدء
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Log startup
            var logger = Services.GetRequiredService<ILoggingService>();
            logger.LogInfo("=== Application Starting ===");

            // V2.0: Initialize database
            _ = InitializeDatabaseAsync();

            // Load settings to check for start minimized
            var settingsService = Services.GetRequiredService<ISettingsService>();
            var settings = settingsService.LoadSettings();

            // Apply theme
            ApplyTheme(settings.UseDarkTheme);

            // هات الـ MainWindow من الكونتينر واعرضها
            var mainWindow = Services.GetRequiredService<MainWindow>();
            
            if (settings.StartMinimized)
            {
                mainWindow.WindowState = WindowState.Minimized;
                mainWindow.ShowInTaskbar = false;
            }
            
            mainWindow.Show();
            
            // Check for updates in background
            _ = CheckForUpdatesAsync();
        }

        /// <summary>
        /// V2.0: Initializes the SQLite database and runs migration if needed.
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            try
            {
                var logger = Services.GetRequiredService<ILoggingService>();
                var database = Services.GetRequiredService<IDatabaseService>();
                
                logger.LogInfo("[V2.0] Initializing SQLite database...");
                
                // Initialize database schema
                await database.InitializeDatabaseAsync();
                
                logger.LogInfo($"[V2.0] Database initialized at: {database.GetDatabasePath()}");
                
                // Check integrity
                bool isValid = await database.CheckIntegrityAsync();
                if (!isValid)
                {
                    logger.LogWarning("[V2.0] Database integrity check failed!");
                }
                
                // Run migration from JSON to SQLite
                var migration = new TelegramOrganizer.Infra.Data.Migrations.JsonToSQLiteMigration(
                    database,
                    Services.GetRequiredService<IPersistenceService>(),
                    Services.GetRequiredService<ISettingsService>(),
                    Services.GetRequiredService<IStatisticsService>(),
                    Services.GetRequiredService<IRulesService>(),
                    logger);
                
                // Create backup before migration
                migration.CreateBackup();
                
                // Migrate
                var result = await migration.MigrateIfNeededAsync();
                
                if (result.Success)
                {
                    logger.LogInfo($"[V2.0] Migration: {result}");
                }
                else
                {
                    logger.LogError($"[V2.0] Migration failed: {result.Message}", null);
                }
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILoggingService>();
                logger.LogError("[V2.0] Failed to initialize database", ex);
            }
        }

        /// <summary>
        /// Applies the specified theme to the application.
        /// </summary>
        public void ApplyTheme(bool useDarkTheme)
        {
            var themePath = useDarkTheme 
                ? "Themes/DarkTheme.xaml" 
                : "Themes/LightTheme.xaml";

            var newTheme = new ResourceDictionary
            {
                Source = new Uri(themePath, UriKind.Relative)
            };

            // Clear existing theme and apply new one
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(newTheme);

            var logger = Services.GetRequiredService<ILoggingService>();
            logger.LogInfo($"Theme applied: {(useDarkTheme ? "Dark" : "Light")}");
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                await Task.Delay(3000); // Wait for app to fully load
                
                var updateService = Services.GetRequiredService<IUpdateService>();
                var updateInfo = await updateService.CheckForUpdatesAsync();
                
                if (updateInfo.IsUpdateAvailable)
                {
                    var logger = Services.GetRequiredService<ILoggingService>();
                    logger.LogInfo($"Update available: {updateInfo.LatestVersion}");
                    
                    // Show update notification on UI thread
                    Current.Dispatcher.Invoke(() =>
                    {
                        var result = System.Windows.MessageBox.Show(
                            $"A new version ({updateInfo.LatestVersion}) is available!\n\n" +
                            $"Current version: {updateInfo.CurrentVersion}\n\n" +
                            "Would you like to download it now?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes && !string.IsNullOrEmpty(updateInfo.DownloadUrl))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = updateInfo.DownloadUrl,
                                UseShellExecute = true
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILoggingService>();
                logger.LogError("Failed to check for updates", ex);
            }
        }

        private void SetupExceptionHandling()
        {
            // Handle UI thread exceptions
            DispatcherUnhandledException += (s, e) =>
            {
                HandleException(e.Exception, "UI Thread Exception");
                e.Handled = true;
            };

            // Handle non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    HandleException(ex, "AppDomain Unhandled Exception");
                }
            };

            // Handle task exceptions
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                HandleException(e.Exception, "Task Unobserved Exception");
                e.SetObserved();
            };
        }

        private void HandleException(Exception ex, string source)
        {
            try
            {
                var logger = Services.GetRequiredService<ILoggingService>();
                logger.LogError($"[{source}] {ex.Message}", ex);

                var errorService = Services.GetRequiredService<IErrorReportingService>();
                errorService.ReportError(ex, source);

                Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"An unexpected error occurred:\n\n{ex.Message}\n\n" +
                        "The error has been logged. The application will try to continue.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
            catch
            {
                // Last resort - just show message
                System.Windows.MessageBox.Show($"Critical error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}