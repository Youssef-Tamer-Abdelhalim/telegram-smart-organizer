using System;
using System.Collections.Generic;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Monitors Telegram windows in the background using EnumWindows.
    /// Provides context even when Telegram is not the active window.
    /// </summary>
    public interface IBackgroundWindowMonitor
    {
        /// <summary>
        /// Starts monitoring windows in the background.
        /// Uses a timer to periodically enumerate all windows.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops monitoring windows.
        /// </summary>
        void Stop();

        /// <summary>
        /// Gets whether monitoring is currently active.
        /// </summary>
        bool IsMonitoring { get; }

        /// <summary>
        /// Forces an immediate scan of all windows.
        /// Normally called automatically by the timer.
        /// </summary>
        void ScanWindows();

        /// <summary>
        /// Gets all currently tracked Telegram windows.
        /// </summary>
        List<WindowInfo> GetAllTelegramWindows();

        /// <summary>
        /// Gets the most recently active Telegram window.
        /// </summary>
        WindowInfo? GetMostRecentWindow();

        /// <summary>
        /// Gets recent Telegram windows (within specified seconds).
        /// </summary>
        /// <param name="withinSeconds">Time window in seconds (default: 60)</param>
        List<WindowInfo> GetRecentWindows(int withinSeconds = 60);

        /// <summary>
        /// Gets a window by its handle.
        /// </summary>
        WindowInfo? GetWindowByHandle(IntPtr handle);

        /// <summary>
        /// Tries to get the best group name from recent windows.
        /// Uses confidence scoring to select the most reliable window.
        /// </summary>
        /// <returns>Tuple of (groupName, confidence) or null if no suitable window found</returns>
        (string groupName, double confidence)? GetBestRecentGroupName();

        /// <summary>
        /// Clears old window information (not seen for timeout period).
        /// </summary>
        /// <param name="timeoutSeconds">Age threshold in seconds (default: 300 = 5 minutes)</param>
        int ClearOldWindows(int timeoutSeconds = 300);

        /// <summary>
        /// Gets the total number of tracked windows.
        /// </summary>
        int GetTrackedWindowCount();

        /// <summary>
        /// Fired when a new Telegram window is detected.
        /// </summary>
        event EventHandler<WindowInfo>? WindowDetected;

        /// <summary>
        /// Fired when a Telegram window becomes active.
        /// </summary>
        event EventHandler<WindowInfo>? WindowActivated;

        /// <summary>
        /// Fired when a window is removed from tracking (expired).
        /// </summary>
        event EventHandler<WindowInfo>? WindowRemoved;

        /// <summary>
        /// Configuration: Scan interval in milliseconds.
        /// How often to enumerate windows (default: 2000ms = 2 seconds).
        /// </summary>
        int ScanIntervalMs { get; set; }

        /// <summary>
        /// Configuration: Maximum number of windows to track.
        /// Older windows are evicted when limit is reached (default: 20).
        /// </summary>
        int MaxTrackedWindows { get; set; }

        /// <summary>
        /// Configuration: Whether to scan windows automatically.
        /// If false, must call ScanWindows() manually (default: true).
        /// </summary>
        bool AutoScan { get; set; }
    }
}
