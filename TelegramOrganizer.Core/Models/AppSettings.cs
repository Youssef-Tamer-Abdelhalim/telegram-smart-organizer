using System;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Application settings model with user-configurable options.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Path to the base destination folder for organized files.
        /// Default: Documents/Telegram Organized
        /// </summary>
        public string DestinationBasePath { get; set; } = 
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "Telegram Organized");

        /// <summary>
        /// Path to the folder being monitored for downloads.
        /// Default: Downloads
        /// </summary>
        public string DownloadsFolderPath { get; set; } = 
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "Downloads");

        /// <summary>
        /// Number of days to retain old pending entries before cleanup.
        /// Default: 30 days
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Whether to start the application minimized to system tray.
        /// Default: false
        /// </summary>
        public bool StartMinimized { get; set; } = false;

        /// <summary>
        /// Whether to minimize to system tray instead of taskbar.
        /// Default: true
        /// </summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>
        /// Whether to show notifications when files are organized.
        /// Default: true
        /// </summary>
        public bool ShowNotifications { get; set; } = true;

        /// <summary>
        /// Whether to use dark theme.
        /// Default: false (Light theme)
        /// </summary>
        public bool UseDarkTheme { get; set; } = false;

        /// <summary>
        /// Whether to run the application on Windows startup.
        /// Default: false
        /// </summary>
        public bool RunOnStartup { get; set; } = false;

        /// <summary>
        /// Application version (for migration support).
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        // ========================================
        // V2.0: Burst Detection Settings
        // ========================================

        /// <summary>
        /// V2.0: Time threshold in seconds to consider files as part of same burst.
        /// Files downloaded within this time are treated as batch download.
        /// Default: 5 seconds
        /// </summary>
        public int BurstThresholdSeconds { get; set; } = 5;

        /// <summary>
        /// V2.0: Minimum number of files required to qualify as a burst/batch download.
        /// Default: 2 files
        /// </summary>
        public int MinimumFilesForBurst { get; set; } = 2;

        /// <summary>
        /// V2.0: Maximum duration to track a burst before auto-ending (in seconds).
        /// Prevents very long bursts from staying active indefinitely.
        /// Default: 60 seconds
        /// </summary>
        public int MaxBurstDurationSeconds { get; set; } = 60;

        /// <summary>
        /// V2.0: Session timeout in seconds for download sessions.
        /// If no new files are added within this time, the session ends.
        /// Default: 30 seconds
        /// </summary>
        public int SessionTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// V2.0: Whether to enable smart burst detection.
        /// When enabled, batch downloads use shared context from session.
        /// Default: true
        /// </summary>
        public bool EnableBurstDetection { get; set; } = true;
    }
}
