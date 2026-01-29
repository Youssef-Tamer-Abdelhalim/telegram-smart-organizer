using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Services
{
    /// <summary>
    /// Main orchestration engine for the Telegram Smart Organizer.
    /// V2.0: All services are now required - no optional fallbacks.
    /// </summary>
    public class SmartOrganizerEngine
    {
        // Required V1.0 Services
        private readonly IFileWatcher _watcher;
        private readonly IContextDetector _contextDetector;
        private readonly IFileOrganizer _fileOrganizer;
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _logger;
        
        // Required V2.0 Services (no longer optional)
        private readonly IDownloadSessionManager _sessionManager;
        private readonly IDownloadBurstDetector _burstDetector;
        private readonly IBackgroundWindowMonitor _windowMonitor;

        // Thread-safe tracking dictionaries
        private readonly ConcurrentDictionary<string, FileContext> _pendingDownloads = new();
        private readonly ConcurrentDictionary<string, DateTime> _processingFiles = new();

        public event EventHandler<string>? OperationCompleted;

        /// <summary>
        /// Creates a new SmartOrganizerEngine with all required V2.0 services.
        /// </summary>
        /// <param name="watcher">File system watcher service</param>
        /// <param name="contextDetector">Window context detection service</param>
        /// <param name="fileOrganizer">File organization service</param>
        /// <param name="settingsService">Application settings service</param>
        /// <param name="loggingService">Logging service</param>
        /// <param name="sessionManager">V2.0: Download session manager (required)</param>
        /// <param name="burstDetector">V2.0: Download burst detector (required)</param>
        /// <param name="windowMonitor">V2.0: Background window monitor (required)</param>
        public SmartOrganizerEngine(
            IFileWatcher watcher,
            IContextDetector contextDetector,
            IFileOrganizer fileOrganizer,
            ISettingsService settingsService,
            ILoggingService loggingService,
            IDownloadSessionManager sessionManager,
            IDownloadBurstDetector burstDetector,
            IBackgroundWindowMonitor windowMonitor)
        {
            // V1.0 Required Services
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _contextDetector = contextDetector ?? throw new ArgumentNullException(nameof(contextDetector));
            _fileOrganizer = fileOrganizer ?? throw new ArgumentNullException(nameof(fileOrganizer));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _logger = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            
            // V2.0 Required Services (no longer optional)
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _burstDetector = burstDetector ?? throw new ArgumentNullException(nameof(burstDetector));
            _windowMonitor = windowMonitor ?? throw new ArgumentNullException(nameof(windowMonitor));

            // Subscribe to burst detection events
            _burstDetector.BurstStarted += OnBurstStarted;
            _burstDetector.BurstContinued += OnBurstContinued;
            _burstDetector.BurstEnded += OnBurstEnded;

            // Subscribe to window monitor events
            _windowMonitor.WindowDetected += OnWindowDetected;
            _windowMonitor.WindowActivated += OnWindowActivated;
        }

        public void Start()
        {
            var settings = _settingsService.LoadSettings();

            _logger.LogInfo($"=== Engine Starting (V2.0) ===");
            _logger.LogInfo($"Downloads Path: {settings.DownloadsFolderPath}");
            _logger.LogInfo($"Destination Path: {settings.DestinationBasePath}");

            // Validate Downloads folder
            if (!ValidateAndFixPaths(settings))
            {
                OperationCompleted?.Invoke(this, "[ERROR] Invalid paths - Please check Settings");
                return;
            }

            // V2.0: Start background window monitor
            _windowMonitor.Start();
            _logger.LogInfo("[V2.0] Background window monitor started");

            // V2.0: Start session timeout checker
            _ = StartSessionTimeoutCheckerAsync();

            try
            {
                _watcher.FileCreated += OnFileCreated;
                _watcher.FileRenamed += OnFileRenamed;
                _watcher.Start(settings.DownloadsFolderPath);

                _logger.LogInfo($"Engine started (V2.0 mode)");
                OperationCompleted?.Invoke(this, $"[ENGINE] Started V2.0 - Watching: {settings.DownloadsFolderPath}");
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError("Failed to start watcher", ex);
                OperationCompleted?.Invoke(this, $"[ERROR] Downloads folder not found: {settings.DownloadsFolderPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to start engine", ex);
                OperationCompleted?.Invoke(this, $"[ERROR] {ex.Message}");
            }
        }

        /// <summary>
        /// V2.0: Background task to check for timed-out sessions periodically.
        /// </summary>
        private async Task StartSessionTimeoutCheckerAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(10000); // Check every 10 seconds
                    int timedOut = await _sessionManager.CheckAndEndTimedOutSessionsAsync();
                    if (timedOut > 0)
                    {
                        _logger.LogInfo($"[V2.0] Ended {timedOut} timed-out session(s)");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("[V2.0] Error in session timeout checker", ex);
                }
            }
        }

        /// <summary>
        /// Validates and attempts to fix paths. Returns false if paths are invalid.
        /// </summary>
        private bool ValidateAndFixPaths(AppSettings settings)
        {
            bool needsSave = false;
            
            // Check Downloads folder
            if (!Directory.Exists(settings.DownloadsFolderPath))
            {
                _logger.LogWarning($"Downloads folder doesn't exist: {settings.DownloadsFolderPath}");
                
                // Try default Downloads folder
                string defaultDownloads = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    "Downloads");
                
                if (Directory.Exists(defaultDownloads))
                {
                    _logger.LogInfo($"Using default Downloads folder: {defaultDownloads}");
                    settings.DownloadsFolderPath = defaultDownloads;
                    needsSave = true;
                    OperationCompleted?.Invoke(this, $"[INFO] Using default Downloads: {defaultDownloads}");
                }
                else
                {
                    _logger.LogError($"No valid Downloads folder found");
                    return false;
                }
            }
            
            // Check/Create Destination folder
            if (!Directory.Exists(settings.DestinationBasePath))
            {
                try
                {
                    Directory.CreateDirectory(settings.DestinationBasePath);
                    _logger.LogInfo($"Created destination folder: {settings.DestinationBasePath}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Couldn't create destination: {ex.Message}");
                    
                    // Use default
                    string defaultDest = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                        "Telegram Organized");
                    
                    try
                    {
                        Directory.CreateDirectory(defaultDest);
                        settings.DestinationBasePath = defaultDest;
                        needsSave = true;
                        _logger.LogInfo($"Using default destination: {defaultDest}");
                    }
                    catch
                    {
                        _logger.LogError("Failed to create any destination folder");
                        return false;
                    }
                }
            }
            
            if (needsSave)
            {
                _settingsService.SaveSettings(settings);
            }
            
            return true;
        }

        public void Stop()
        {
            _logger.LogInfo("Engine stopping...");
            
            // V2.0: Stop window monitor
            _windowMonitor.Stop();
            _logger.LogInfo("[V2.0] Background window monitor stopped");
            
            // V2.0: End any active session
            _ = _sessionManager.EndCurrentSessionAsync();
            
            _watcher.Stop();
            _watcher.FileCreated -= OnFileCreated;
            _watcher.FileRenamed -= OnFileRenamed;
            _logger.LogInfo("Engine stopped");
        }

        private void OnFileCreated(object? sender, FileEventArgs e)
        {
            try
            {
                if (_processingFiles.ContainsKey(e.FileName))
                {
                    _logger.LogDebug($"Skipping duplicate event for: {e.FileName}");
                    return;
                }

                // V2.0: Record file for burst detection
                _burstDetector.RecordDownload(e.FileName);
                
                var burstStatus = _burstDetector.GetCurrentBurstStatus();
                if (burstStatus.IsBurstActive)
                {
                    _logger.LogInfo($"[Burst] {e.FileName} is part of burst ({burstStatus.FileCount} files)");
                }

                string activeWindow = _contextDetector.GetActiveWindowTitle();
                string processName = _contextDetector.GetProcessName();
                
                _logger.LogFileOperation("CREATED", e.FileName, 
                    additionalInfo: $"Window: '{activeWindow}' | Process: {processName}");
                
                OperationCompleted?.Invoke(this, $"[DEBUG] File: {e.FileName} | Window: {activeWindow}");

                string groupName = ExtractTelegramGroupName(activeWindow);
                _logger.LogDebug($"Extracted group name: '{groupName}' from window: '{activeWindow}'");

                // V2.0: Always use session manager (no fallback to V1.0)
                _ = HandleFileCreatedWithSessionAsync(e.FileName, e.FullPath, groupName, activeWindow, processName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnFileCreated for: {e.FileName}", ex);
            }
        }

        /// <summary>
        /// V2.0: Handles file created event using session manager.
        /// This solves the batch download problem by using a shared session context.
        /// </summary>
        private async Task HandleFileCreatedWithSessionAsync(string fileName, string fullPath, string groupName, string windowTitle, string processName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(groupName))
                    groupName = "Unsorted";

                // Add file to session (creates new session if needed)
                var session = await _sessionManager.AddFileToSessionAsync(fileName, groupName, fullPath, 0);
                
                _logger.LogInfo($"[V2.0] File '{fileName}' added to session #{session.Id} for '{groupName}' " +
                              $"(session files: {session.FileCount})");
                
                OperationCompleted?.Invoke(this, $"[SESSION] {fileName} → Session #{session.Id} ({groupName})");

                // For temporary files, just track in session
                if (IsTemporaryFile(fileName))
                {
                    _logger.LogDebug($"[V2.0] Temporary file tracked in session #{session.Id}");
                    return;
                }

                // For direct downloads, organize immediately but use session context
                _processingFiles.TryAdd(fileName, DateTime.Now);

                await Task.Run(async () =>
                {
                    try
                    {
                        bool isReady = await WaitForFileReady(fullPath, 120000);
                        
                        if (isReady && File.Exists(fullPath))
                        {
                            string result = _fileOrganizer.OrganizeFile(fullPath, session.GroupName);
                            _logger.LogFileOperation("ORGANIZED_V2", fileName, groupName: session.GroupName, 
                                additionalInfo: $"Session #{session.Id} | {result}");
                            OperationCompleted?.Invoke(this, $"[SUCCESS] {fileName} → {session.GroupName} (Session #{session.Id})");
                        }
                        else
                        {
                            _logger.LogWarning($"[V2.0] File not ready: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[V2.0] Failed to organize: {fileName}", ex);
                    }
                    finally
                    {
                        _processingFiles.TryRemove(fileName, out _);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[V2.0] Error in HandleFileCreatedWithSessionAsync for: {fileName}", ex);
            }
        }

        public void OnFileRenamed(object? sender, FileEventArgs e)
        {
            try
            {
                _logger.LogFileOperation("RENAMED", e.FileName, oldFileName: e.OldFileName);
                OperationCompleted?.Invoke(this, $"[DEBUG] Rename: {e.OldFileName} -> {e.FileName}");

                // V2.0: Always use session manager (no fallback to V1.0)
                _ = HandleFileRenamedWithSessionAsync(e.FileName, e.FullPath, e.OldFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnFileRenamed for: {e.FileName}", ex);
            }
        }

        /// <summary>
        /// V2.0: Handles file renamed event using session manager.
        /// Gets group name from the active session instead of current window.
        /// </summary>
        private async Task HandleFileRenamedWithSessionAsync(string fileName, string fullPath, string? oldFileName)
        {
            try
            {
                // If still temporary, just update tracking in session
                if (IsTemporaryFile(fileName))
                {
                    _logger.LogDebug($"[V2.0] Temp file renamed: {oldFileName} → {fileName}");
                    return;
                }

                // Final file - get group from active session
                var session = await _sessionManager.GetActiveSessionAsync();
                
                if (session == null)
                {
                    _logger.LogWarning($"[V2.0] No active session for renamed file: {fileName}");
                    
                    // Fallback to current window context
                    string activeWindow = _contextDetector.GetActiveWindowTitle();
                    string groupName = ExtractTelegramGroupName(activeWindow);
                    
                    if (!string.IsNullOrWhiteSpace(groupName) && groupName != "Unsorted")
                    {
                        session = await _sessionManager.StartSessionAsync(groupName, activeWindow);
                        await _sessionManager.AddFileToSessionAsync(fileName, groupName, fullPath, 0);
                    }
                    else
                    {
                        _logger.LogWarning($"[V2.0] Cannot determine group for: {fileName}");
                        return;
                    }
                }

                _logger.LogInfo($"[V2.0] File complete: {fileName} → {session.GroupName} (Session #{session.Id})");
                
                await Task.Run(async () =>
                {
                    try
                    {
                        bool isReady = await WaitForFileReady(fullPath, 30000);
                        
                        if (isReady && File.Exists(fullPath))
                        {
                            string result = _fileOrganizer.OrganizeFile(fullPath, session.GroupName);
                            _logger.LogFileOperation("ORGANIZED_V2", fileName, oldFileName: oldFileName,
                                groupName: session.GroupName, additionalInfo: $"Session #{session.Id} | {result}");
                            OperationCompleted?.Invoke(this, $"[SUCCESS] {fileName} → {session.GroupName} (Session #{session.Id})");
                        }
                        else
                        {
                            _logger.LogWarning($"[V2.0] File not ready after rename: {fileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"[V2.0] Failed to organize after rename: {fileName}", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"[V2.0] Error in HandleFileRenamedWithSessionAsync for: {fileName}", ex);
            }
        }

        /// <summary>
        /// Waits for file to be ready (not locked by another process).
        /// Uses exponential backoff for better handling of large files.
        /// </summary>
        private async Task<bool> WaitForFileReady(string filePath, int timeoutMs)
        {
            var startTime = DateTime.Now;
            int delay = 500;
            long lastSize = -1;
            int sizeStableCount = 0;
            
            _logger.LogDebug($"Waiting for file: {Path.GetFileName(filePath)} (timeout: {timeoutMs}ms)");
            
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        _logger.LogDebug($"File doesn't exist yet: {Path.GetFileName(filePath)}");
                        await Task.Delay(delay);
                        continue;
                    }

                    var fileInfo = new FileInfo(filePath);
                    long currentSize = fileInfo.Length;
                    
                    if (currentSize == lastSize && currentSize > 0)
                    {
                        sizeStableCount++;
                        _logger.LogDebug($"File size stable ({sizeStableCount}/3): {Path.GetFileName(filePath)} = {currentSize} bytes");
                        
                        if (sizeStableCount >= 3)
                        {
                            try
                            {
                                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                                {
                                    _logger.LogDebug($"File is ready: {Path.GetFileName(filePath)}");
                                    return true;
                                }
                            }
                            catch (IOException)
                            {
                                _logger.LogDebug($"File still locked: {Path.GetFileName(filePath)}");
                            }
                        }
                    }
                    else
                    {
                        sizeStableCount = 0;
                        if (lastSize != -1)
                        {
                            _logger.LogDebug($"File still downloading: {Path.GetFileName(filePath)} ({lastSize} -> {currentSize} bytes)");
                        }
                    }
                    
                    lastSize = currentSize;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug($"Error checking file: {ex.Message}");
                }

                await Task.Delay(delay);
                if (delay < 2000) delay = Math.Min(delay + 200, 2000);
            }

            _logger.LogWarning($"Timeout waiting for file: {Path.GetFileName(filePath)}");
            return false;
        }

        /// <summary>
        /// Extracts the group/channel name from Telegram window title.
        /// Handles Arabic, English, and mixed text properly.
        /// Removes notification counts, emojis, and other metadata.
        /// </summary>
        private string ExtractTelegramGroupName(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return "Unsorted";

            string title = windowTitle.Trim();
            string original = title;
            
            // Remove unread count at start: "(123) GroupName" -> "GroupName"
            title = Regex.Replace(title, @"^\(\d+\)\s*", "");
            
            // Remove total message count at end: "GroupName – (3082)" -> "GroupName"
            title = Regex.Replace(title, @"\s*[–—-]\s*\(\d+\)$", "");
            
            // Remove any remaining parenthetical numbers at end
            title = Regex.Replace(title, @"\s*\(\d+\)$", "");
            
            // Remove " - Telegram" or " – Telegram" suffix
            title = Regex.Replace(title, @"\s*[–—-]\s*Telegram$", "", RegexOptions.IgnoreCase);
            
            // Remove emojis and symbols but keep Arabic, English, numbers, and common punctuation
            // Arabic ranges: \u0600-\u06FF, \u0750-\u077F, \uFB50-\uFDFF, \uFE70-\uFEFF
            title = Regex.Replace(title, @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFFa-zA-Z0-9\s\-_\.]+", "");
            
            // Clean up whitespace and special chars
            title = Regex.Replace(title, @"\s+", " ").Trim();
            title = title.Trim(' ', '-', '_', '.');

            if (string.IsNullOrWhiteSpace(title) || title.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
                return "Unsorted";

            _logger.LogDebug($"Group extraction: '{original}' -> '{title}'");
            return title;
        }

        private bool IsTemporaryFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;
                
            string lower = fileName.ToLower();
            return lower.EndsWith(".td") ||
                   lower.EndsWith(".tpart") ||
                   lower.EndsWith(".crdownload") ||
                   lower.EndsWith(".part") ||
                   lower.EndsWith(".tmp") ||
                   lower.EndsWith(".download");
        }

        // ========================================
        // V2.0: Burst Detection Event Handlers
        // ========================================

        private void OnBurstStarted(object? sender, BurstDetectionResult e)
        {
            _logger.LogInfo($"[Burst] STARTED: {e.FileCount} files, {e.DurationSeconds:F1}s");
            OperationCompleted?.Invoke(this, $"[BURST] Started - {e.FileCount} files detected");
        }

        private void OnBurstContinued(object? sender, BurstDetectionResult e)
        {
            _logger.LogDebug($"[Burst] CONTINUED: {e.FileCount} files, avg {e.AverageIntervalSeconds:F1}s/file");
            OperationCompleted?.Invoke(this, $"[BURST] {e.FileCount} files ({e.Confidence:F0}% confidence)");
        }

        private void OnBurstEnded(object? sender, BurstDetectionResult e)
        {
            _logger.LogInfo($"[Burst] ENDED: {e.FileCount} files in {e.DurationSeconds:F1}s (confidence: {e.Confidence:F2})");
            OperationCompleted?.Invoke(this, $"[BURST] Completed - {e.FileCount} files organized");
        }

        // ========================================
        // V2.0: Window Monitor Event Handlers
        // ========================================

        private void OnWindowDetected(object? sender, WindowInfo e)
        {
            _logger.LogInfo($"[WindowMonitor] New window: {e.Title}");
            OperationCompleted?.Invoke(this, $"[WINDOW] Detected: {e.Title}");
        }

        private void OnWindowActivated(object? sender, WindowInfo e)
        {
            _logger.LogDebug($"[WindowMonitor] Window activated: {e.Title}");
            OperationCompleted?.Invoke(this, $"[WINDOW] Active: {e.Title}");
        }
    }
}