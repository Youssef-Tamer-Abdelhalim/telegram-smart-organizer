using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Interop;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// Monitors Telegram windows in the background using periodic scanning.
    /// Maintains a cache of recent windows for context detection.
    /// </summary>
    public class BackgroundWindowMonitor : IBackgroundWindowMonitor, IDisposable
    {
        private readonly ILoggingService _logger;
        private readonly Dictionary<IntPtr, WindowInfo> _trackedWindows = new();
        private readonly object _lock = new object();
        private Timer? _scanTimer;
        private bool _isMonitoring;

        // Events
        public event EventHandler<WindowInfo>? WindowDetected;
        public event EventHandler<WindowInfo>? WindowActivated;
        public event EventHandler<WindowInfo>? WindowRemoved;

        // Configuration
        public int ScanIntervalMs { get; set; } = 2000; // 2 seconds
        public int MaxTrackedWindows { get; set; } = 20;
        public bool AutoScan { get; set; } = true;

        public bool IsMonitoring => _isMonitoring;

        public BackgroundWindowMonitor(ILoggingService logger)
        {
            _logger = logger;
        }

        // ========================================
        // Start / Stop
        // ========================================

        public void Start()
        {
            lock (_lock)
            {
                if (_isMonitoring)
                {
                    _logger.LogWarning("[WindowMonitor] Already monitoring");
                    return;
                }

                _isMonitoring = true;

                if (AutoScan)
                {
                    _scanTimer = new Timer(
                        callback: _ => ScanWindows(),
                        state: null,
                        dueTime: 0, // Immediate first scan
                        period: ScanIntervalMs);

                    _logger.LogInfo($"[WindowMonitor] Started with scan interval {ScanIntervalMs}ms");
                }
                else
                {
                    _logger.LogInfo("[WindowMonitor] Started (manual scan mode)");
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                if (!_isMonitoring)
                    return;

                _isMonitoring = false;
                _scanTimer?.Dispose();
                _scanTimer = null;

                _logger.LogInfo($"[WindowMonitor] Stopped (tracked {_trackedWindows.Count} windows)");
            }
        }

        // ========================================
        // Window Scanning
        // ========================================

        public void ScanWindows()
        {
            try
            {
                // Get all Telegram windows
                var currentWindows = Win32WindowEnumerator.EnumerateTelegramWindows();

                lock (_lock)
                {
                    // Process each detected window
                    foreach (var window in currentWindows)
                    {
                        if (_trackedWindows.TryGetValue(window.Handle, out var existing))
                        {
                            // Update existing window
                            bool wasActive = existing.IsActive;
                            existing.UpdateLastSeen(window.IsActive, window.IsVisible);
                            existing.Title = window.Title; // Title might have changed

                            // Fire event if window became active
                            if (!wasActive && window.IsActive)
                            {
                                _logger.LogDebug($"[WindowMonitor] Window activated: {existing}");
                                WindowActivated?.Invoke(this, existing);
                            }
                        }
                        else
                        {
                            // New window detected
                            _trackedWindows[window.Handle] = window;
                            _logger.LogInfo($"[WindowMonitor] New window detected: {window}");
                            WindowDetected?.Invoke(this, window);

                            // Enforce max windows limit (LRU eviction)
                            EnforceMaxWindowsLimit();
                        }
                    }

                    _logger.LogDebug($"[WindowMonitor] Scan complete: {currentWindows.Count} Telegram windows found, {_trackedWindows.Count} tracked");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[WindowMonitor] Error during window scan", ex);
            }
        }

        private void EnforceMaxWindowsLimit()
        {
            while (_trackedWindows.Count > MaxTrackedWindows)
            {
                // Remove oldest window (by LastSeen)
                var oldest = _trackedWindows.Values
                    .OrderBy(w => w.LastSeen)
                    .FirstOrDefault();

                if (oldest != null)
                {
                    _trackedWindows.Remove(oldest.Handle);
                    _logger.LogDebug($"[WindowMonitor] Evicted old window: {oldest.Title}");
                    WindowRemoved?.Invoke(this, oldest);
                }
            }
        }

        // ========================================
        // Window Retrieval
        // ========================================

        public List<WindowInfo> GetAllTelegramWindows()
        {
            lock (_lock)
            {
                return _trackedWindows.Values
                    .OrderByDescending(w => w.LastSeen)
                    .ToList();
            }
        }

        public WindowInfo? GetMostRecentWindow()
        {
            lock (_lock)
            {
                return _trackedWindows.Values
                    .OrderByDescending(w => w.LastSeen)
                    .FirstOrDefault();
            }
        }

        public List<WindowInfo> GetRecentWindows(int withinSeconds = 60)
        {
            lock (_lock)
            {
                var cutoff = DateTime.Now.AddSeconds(-withinSeconds);
                return _trackedWindows.Values
                    .Where(w => w.LastSeen >= cutoff)
                    .OrderByDescending(w => w.LastSeen)
                    .ToList();
            }
        }

        public WindowInfo? GetWindowByHandle(IntPtr handle)
        {
            lock (_lock)
            {
                return _trackedWindows.TryGetValue(handle, out var window) ? window : null;
            }
        }

        public (string groupName, double confidence)? GetBestRecentGroupName()
        {
            lock (_lock)
            {
                // Get recent windows (last 60 seconds)
                var recentWindows = GetRecentWindows(60);

                if (recentWindows.Count == 0)
                    return null;

                // Find window with highest confidence that has an extracted group name
                var best = recentWindows
                    .Where(w => !string.IsNullOrEmpty(w.ExtractedGroupName))
                    .OrderByDescending(w => w.ConfidenceScore)
                    .ThenByDescending(w => w.LastSeen)
                    .FirstOrDefault();

                if (best != null && !string.IsNullOrEmpty(best.ExtractedGroupName))
                {
                    _logger.LogDebug($"[WindowMonitor] Best recent group: '{best.ExtractedGroupName}' (confidence {best.ConfidenceScore:F2})");
                    return (best.ExtractedGroupName, best.ConfidenceScore);
                }

                // Fallback: extract from most recent window
                var mostRecent = recentWindows.FirstOrDefault();
                if (mostRecent != null)
                {
                    // Extract group name from title (simplified version)
                    string extracted = ExtractGroupNameFromTitle(mostRecent.Title);
                    if (!string.IsNullOrEmpty(extracted) && extracted != "Unsorted")
                    {
                        mostRecent.ExtractedGroupName = extracted;
                        _logger.LogDebug($"[WindowMonitor] Extracted from recent: '{extracted}' (confidence {mostRecent.ConfidenceScore:F2})");
                        return (extracted, mostRecent.ConfidenceScore);
                    }
                }

                return null;
            }
        }

        // ========================================
        // Cleanup
        // ========================================

        public int ClearOldWindows(int timeoutSeconds = 300)
        {
            lock (_lock)
            {
                var expiredWindows = _trackedWindows.Values
                    .Where(w => w.IsExpired(timeoutSeconds))
                    .ToList();

                foreach (var window in expiredWindows)
                {
                    _trackedWindows.Remove(window.Handle);
                    _logger.LogDebug($"[WindowMonitor] Removed expired window: {window.Title}");
                    WindowRemoved?.Invoke(this, window);
                }

                if (expiredWindows.Count > 0)
                {
                    _logger.LogInfo($"[WindowMonitor] Cleared {expiredWindows.Count} expired windows");
                }

                return expiredWindows.Count;
            }
        }

        public int GetTrackedWindowCount()
        {
            lock (_lock)
            {
                return _trackedWindows.Count;
            }
        }

        // ========================================
        // Helper Methods
        // ========================================

        private string ExtractGroupNameFromTitle(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return "Unsorted";

            // Simple extraction - remove "- Telegram" suffix and unread counts
            string title = windowTitle.Trim();

            // Remove unread count at start: "(123) GroupName"
            title = System.Text.RegularExpressions.Regex.Replace(title, @"^\(\d+\)\s*", "");

            // Remove "- Telegram" or "– Telegram" suffix
            title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*[–—-]\s*Telegram$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Remove message count at end: "GroupName – (3082)"
            title = System.Text.RegularExpressions.Regex.Replace(title, @"\s*[–—-]\s*\(\d+\)$", "");

            title = title.Trim();

            return string.IsNullOrWhiteSpace(title) || title.Equals("Telegram", StringComparison.OrdinalIgnoreCase)
                ? "Unsorted"
                : title;
        }

        // ========================================
        // IDisposable
        // ========================================

        public void Dispose()
        {
            Stop();
            _scanTimer?.Dispose();
        }
    }
}
