using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Services
{
    public class SmartOrganizerEngine
    {
        private readonly IFileWatcher _watcher;
        private readonly IContextDetector _contextDetector;
        private readonly IFileOrganizer _fileOrganizer;
        private readonly IPersistenceService _persistenceService;
        private readonly ISettingsService _settingsService;
        private readonly ILoggingService _logger;

        private readonly ConcurrentDictionary<string, FileContext> _pendingDownloads = new();
        private readonly ConcurrentDictionary<string, DateTime> _processingFiles = new();

        public event EventHandler<string>? OperationCompleted;

        public SmartOrganizerEngine(
            IFileWatcher watcher,
            IContextDetector contextDetector,
            IFileOrganizer fileOrganizer,
            IPersistenceService persistenceService,
            ISettingsService settingsService,
            ILoggingService loggingService)
        {
            _watcher = watcher;
            _contextDetector = contextDetector;
            _fileOrganizer = fileOrganizer;
            _persistenceService = persistenceService;
            _settingsService = settingsService;
            _logger = loggingService;
        }

        public void Start()
        {
            var settings = _settingsService.LoadSettings();

            _logger.LogInfo($"=== Engine Starting ===");
            _logger.LogInfo($"Downloads Path: {settings.DownloadsFolderPath}");
            _logger.LogInfo($"Destination Path: {settings.DestinationBasePath}");

            // Validate Downloads folder
            if (!ValidateAndFixPaths(settings))
            {
                OperationCompleted?.Invoke(this, "[ERROR] Invalid paths - Please check Settings");
                return;
            }

            LoadPersistedState(settings.DownloadsFolderPath);

            int cleaned = _persistenceService.CleanupOldEntries(settings.RetentionDays);
            if (cleaned > 0)
            {
                _logger.LogInfo($"Cleaned up {cleaned} old entries");
                OperationCompleted?.Invoke(this, $"[CLEANUP] Removed {cleaned} old entries");
            }

            try
            {
                _watcher.FileCreated += OnFileCreated;
                _watcher.FileRenamed += OnFileRenamed;
                _watcher.Start(settings.DownloadsFolderPath);

                _logger.LogInfo($"Engine started. Pending downloads: {_pendingDownloads.Count}");
                OperationCompleted?.Invoke(this, $"[ENGINE] Started - Watching: {settings.DownloadsFolderPath}");
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
            _watcher.Stop();
            _watcher.FileCreated -= OnFileCreated;
            _watcher.FileRenamed -= OnFileRenamed;
            _logger.LogInfo("Engine stopped");
        }

        private void LoadPersistedState(string downloadsPath)
        {
            try
            {
                var state = _persistenceService.LoadState();
                _logger.LogDebug($"Loaded state with {state.PendingDownloads.Count} entries");

                foreach (var kvp in state.PendingDownloads)
                {
                    string fullPath = Path.Combine(downloadsPath, kvp.Key);
                    if (File.Exists(fullPath))
                    {
                        _pendingDownloads.TryAdd(kvp.Key, kvp.Value);
                        _logger.LogDebug($"Restored pending: {kvp.Key} -> {kvp.Value.DetectedGroupName}");
                    }
                    else
                    {
                        _persistenceService.RemoveEntry(kvp.Key);
                        _logger.LogDebug($"Removed orphan: {kvp.Key}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load persisted state", ex);
                OperationCompleted?.Invoke(this, $"[WARNING] Failed to load state: {ex.Message}");
            }
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

                string activeWindow = _contextDetector.GetActiveWindowTitle();
                string processName = _contextDetector.GetProcessName();
                
                _logger.LogFileOperation("CREATED", e.FileName, 
                    additionalInfo: $"Window: '{activeWindow}' | Process: {processName}");
                
                OperationCompleted?.Invoke(this, $"[DEBUG] File: {e.FileName} | Window: {activeWindow}");

                string groupName = ExtractTelegramGroupName(activeWindow);
                _logger.LogDebug($"Extracted group name: '{groupName}' from window: '{activeWindow}'");

                var context = new FileContext
                {
                    OriginalTempName = e.FileName,
                    DetectedGroupName = string.IsNullOrWhiteSpace(groupName) ? "Unsorted" : groupName
                };

                // Check if it's a direct download (non-temp file)
                if (!IsTemporaryFile(e.FileName))
                {
                    _logger.LogInfo($"Direct download detected: {e.FileName} -> {context.DetectedGroupName}");
                    
                    // Mark as processing
                    _processingFiles.TryAdd(e.FileName, DateTime.Now);
                    
                    // Also add to pending in case we need to track it
                    _pendingDownloads.TryAdd(e.FileName, context);
                    _persistenceService.AddOrUpdateEntry(e.FileName, context);
                    
                    Task.Run(async () =>
                    {
                        try
                        {
                            // Wait longer for file to be fully written (up to 2 minutes for large files)
                            bool isReady = await WaitForFileReady(e.FullPath, 120000);
                            
                            if (isReady && File.Exists(e.FullPath))
                            {
                                string result = _fileOrganizer.OrganizeFile(e.FullPath, context.DetectedGroupName);
                                _logger.LogFileOperation("ORGANIZED", e.FileName, groupName: context.DetectedGroupName, 
                                    additionalInfo: result);
                                OperationCompleted?.Invoke(this, $"[SUCCESS] {e.FileName} -> {context.DetectedGroupName}");
                                
                                // Cleanup
                                _pendingDownloads.TryRemove(e.FileName, out _);
                                _persistenceService.RemoveEntry(e.FileName);
                            }
                            else
                            {
                                _logger.LogWarning($"File not ready or doesn't exist: {e.FileName}");
                                OperationCompleted?.Invoke(this, $"[PENDING] {e.FileName} - Still downloading...");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to organize direct download: {e.FileName}", ex);
                        }
                        finally
                        {
                            _processingFiles.TryRemove(e.FileName, out _);
                        }
                    });
                    return;
                }

                // Temporary file - add to tracking
                bool added = _pendingDownloads.TryAdd(e.FileName, context);
                _logger.LogFileOperation("TRACKING", e.FileName, groupName: context.DetectedGroupName,
                    additionalInfo: $"Added to pending: {added} | Total pending: {_pendingDownloads.Count}");

                if (added)
                {
                    _persistenceService.AddOrUpdateEntry(e.FileName, context);
                }

                OperationCompleted?.Invoke(this, $"[TRACKING] {e.FileName} (Group: {groupName})");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnFileCreated for: {e.FileName}", ex);
            }
        }

        private void OnFileRenamed(object? sender, FileEventArgs e)
        {
            try
            {
                _logger.LogFileOperation("RENAMED", e.FileName, oldFileName: e.OldFileName);
                OperationCompleted?.Invoke(this, $"[DEBUG] Rename: {e.OldFileName} -> {e.FileName}");

                // Handle temp file renames (still downloading)
                if (IsTemporaryFile(e.FileName))
                {
                    if (_pendingDownloads.TryRemove(e.OldFileName ?? "", out FileContext? tempContext) && tempContext != null)
                    {
                        tempContext.OriginalTempName = e.FileName;
                        _pendingDownloads.TryAdd(e.FileName, tempContext);

                        _persistenceService.RemoveEntry(e.OldFileName ?? "");
                        _persistenceService.AddOrUpdateEntry(e.FileName, tempContext);

                        _logger.LogDebug($"Updated temp tracking: {e.OldFileName} -> {e.FileName}");
                        OperationCompleted?.Invoke(this, $"[UPDATE] Still downloading: {e.FileName}");
                    }
                    return;
                }

                // Final file - try to find in pending
                if (_pendingDownloads.TryRemove(e.OldFileName ?? "", out FileContext? finalContext) && finalContext != null)
                {
                    _logger.LogInfo($"Found in pending: {e.OldFileName} -> organizing to {finalContext.DetectedGroupName}");
                    
                    Task.Run(async () =>
                    {
                        try
                        {
                            bool isReady = await WaitForFileReady(e.FullPath, 30000);
                            
                            if (isReady && File.Exists(e.FullPath))
                            {
                                string result = _fileOrganizer.OrganizeFile(e.FullPath, finalContext.DetectedGroupName);
                                _persistenceService.RemoveEntry(e.OldFileName ?? "");
                                
                                _logger.LogFileOperation("ORGANIZED", e.FileName, oldFileName: e.OldFileName,
                                    groupName: finalContext.DetectedGroupName, additionalInfo: result);
                                OperationCompleted?.Invoke(this, $"[SUCCESS] {e.FileName} -> {finalContext.DetectedGroupName}");
                            }
                            else
                            {
                                _logger.LogWarning($"File not ready after rename: {e.FileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to organize after rename: {e.FileName}", ex);
                        }
                    });
                }
                else
                {
                    // Not in pending - try with current context
                    _logger.LogDebug($"File not in pending list: {e.OldFileName}");
                    
                    string activeWindow = _contextDetector.GetActiveWindowTitle();
                    string groupName = ExtractTelegramGroupName(activeWindow);
                    
                    if (!string.IsNullOrWhiteSpace(groupName) && groupName != "Unsorted")
                    {
                        _logger.LogDebug($"Fallback - using current window: '{groupName}'");
                        
                        Task.Run(async () =>
                        {
                            try
                            {
                                bool isReady = await WaitForFileReady(e.FullPath, 30000);
                                
                                if (isReady && File.Exists(e.FullPath))
                                {
                                    string result = _fileOrganizer.OrganizeFile(e.FullPath, groupName);
                                    _logger.LogFileOperation("ORGANIZED_FALLBACK", e.FileName, 
                                        groupName: groupName, additionalInfo: result);
                                    OperationCompleted?.Invoke(this, $"[SUCCESS] {e.FileName} -> {groupName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Failed fallback organize: {e.FileName}", ex);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in OnFileRenamed for: {e.FileName}", ex);
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
    }
}