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

            // ========================================
            // 1. Core Infrastructure Services
            // ========================================
            
            // Logging Service - needed by all services
            services.AddSingleton<ILoggingService, FileLoggingService>();
            
            // Settings Service - needed by FileOrganizer and Engine
            services.AddSingleton<ISettingsService, JsonSettingsService>();
            
            // Rules Service - for custom organization rules
            services.AddSingleton<IRulesService, JsonRulesService>();
            
            // Statistics Service - for tracking metrics (JSON for UI display)
            services.AddSingleton<IStatisticsService, JsonStatisticsService>();
            
            // Context Detection - Win32 API for window detection
            services.AddSingleton<IContextDetector, Win32ContextDetector>();
            
            // File Watching - FileSystemWatcher wrapper
            services.AddSingleton<IFileWatcher, WindowsWatcherService>();
            
            // File Organization - moves files to organized folders
            services.AddSingleton<IFileOrganizer, FileOrganizerService>();

            // ========================================
            // 2. V2.0 Required Services (No longer optional)
            // ========================================
            
            // SQLite Database - primary data storage for v2.0
            services.AddSingleton<IDatabaseService, SQLiteDatabaseService>();
            
            // Download Session Manager - handles batch downloads
            services.AddSingleton<IDownloadSessionManager, DownloadSessionManager>();
            
            // Download Burst Detector - detects rapid file downloads
            services.AddSingleton<IDownloadBurstDetector, DownloadBurstDetector>();
            
            // Background Window Monitor - tracks Telegram windows in background
            services.AddSingleton<IBackgroundWindowMonitor, BackgroundWindowMonitor>();
            
            // ========================================
            // 3. Week 4: Multi-Source Context Detector
            // ========================================
            
            // Multi-Source Context Detector - combines signals from multiple sources
            services.AddSingleton<IMultiSourceContextDetector, MultiSourceContextDetector>();
            
            // ========================================
            // 4. Legacy Services (For migration only)
            // ========================================
            
            // JSON Persistence - kept for migration from v1.0 to v2.0
            services.AddSingleton<IPersistenceService, JsonPersistenceService>();

            // ========================================
            // 5. Main Engine (V2.0 Week 4 - Multi-Source Detection)
            // ========================================
            
            services.AddSingleton<SmartOrganizerEngine>(sp =>
            {
                return new SmartOrganizerEngine(
                    sp.GetRequiredService<IFileWatcher>(),
                    sp.GetRequiredService<IContextDetector>(),
                    sp.GetRequiredService<IFileOrganizer>(),
                    sp.GetRequiredService<ISettingsService>(),
                    sp.GetRequiredService<ILoggingService>(),
                    sp.GetRequiredService<IDownloadSessionManager>(),
                    sp.GetRequiredService<IDownloadBurstDetector>(),
                    sp.GetRequiredService<IBackgroundWindowMonitor>(),
                    sp.GetRequiredService<IMultiSourceContextDetector>() // Week 4
                );
            });

            // ========================================
            // 6. Additional Services
            // ========================================
            
            // Update Service - checks for new versions
            services.AddSingleton<IUpdateService, GitHubUpdateService>();
            
            // Error Reporting Service - generates error logs
            services.AddSingleton<IErrorReportingService, ErrorReportingService>();

            // ========================================
            // 7. ViewModels
            // ========================================
            
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<RulesViewModel>();
            services.AddTransient<StatisticsViewModel>();

            // ========================================
            // 8. Views
            // ========================================
            
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
            logger.LogInfo("[Version] V2.0 - Full Integration (All services required)");

            // V2.0: Initialize database and run migration
            _ = InitializeDatabaseAndMigrateAsync();

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
        /// V2.0: Initializes the SQLite database and runs migration from JSON if needed.
        /// This is the primary data initialization for the application.
        /// </summary>
        private async Task InitializeDatabaseAndMigrateAsync()
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
                    logger.LogWarning("[V2.0] Database integrity check failed! Attempting repair...");
                    await database.RunMaintenanceAsync();
                }
                
                // Run migration from JSON to SQLite (one-time operation)
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
                else if (!result.AlreadyMigrated)
                {
                    logger.LogError($"[V2.0] Migration failed: {result.Message}", null);
                }
                
                // Log database stats
                var dbSize = await database.GetDatabaseSizeAsync();
                var schemaVersion = await database.GetSchemaVersionAsync();
                logger.LogInfo($"[V2.0] Database ready - Size: {dbSize / 1024.0:F2} KB, Schema: v{schemaVersion}");
            }
            catch (Exception ex)
            {
                var logger = Services.GetRequiredService<ILoggingService>();
                logger.LogError("[V2.0] Failed to initialize database", ex);
                
                // Show error to user but don't crash
                Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show(
                        $"Database initialization failed:\n\n{ex.Message}\n\n" +
                        "The application will continue but some features may not work correctly.",
                        "Database Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
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