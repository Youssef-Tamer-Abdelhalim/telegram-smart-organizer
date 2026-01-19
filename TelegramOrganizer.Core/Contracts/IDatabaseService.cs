using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Core database service interface for SQLite operations.
    /// Provides abstraction for all database operations in v2.0.
    /// </summary>
    public interface IDatabaseService
    {
        // ========================================
        // Database Initialization
        // ========================================

        /// <summary>
        /// Initializes the database, creates tables if they don't exist.
        /// Should be called on application startup.
        /// </summary>
        Task InitializeDatabaseAsync();

        /// <summary>
        /// Gets the database file path.
        /// </summary>
        string GetDatabasePath();

        /// <summary>
        /// Closes the database connection.
        /// </summary>
        void CloseConnection();

        // ========================================
        // Download Sessions
        // ========================================

        /// <summary>
        /// Creates a new download session.
        /// </summary>
        Task<DownloadSession> CreateSessionAsync(string groupName, string? windowTitle = null, string? processName = null);

        /// <summary>
        /// Gets the currently active session (if any).
        /// </summary>
        Task<DownloadSession?> GetActiveSessionAsync();

        /// <summary>
        /// Gets a session by ID.
        /// </summary>
        Task<DownloadSession?> GetSessionAsync(int sessionId);

        /// <summary>
        /// Updates an existing session.
        /// </summary>
        Task UpdateSessionAsync(DownloadSession session);

        /// <summary>
        /// Ends a session (marks it as inactive).
        /// </summary>
        Task EndSessionAsync(int sessionId);

        /// <summary>
        /// Adds a file to a session.
        /// </summary>
        Task AddFileToSessionAsync(int sessionId, string fileName, string? filePath = null, long fileSize = 0);

        /// <summary>
        /// Gets all sessions, optionally filtered by active status.
        /// </summary>
        Task<List<DownloadSession>> GetSessionsAsync(bool? activeOnly = null, int limit = 100);

        /// <summary>
        /// Ends timed-out sessions (sessions with no activity for timeout period).
        /// </summary>
        /// <returns>Number of sessions ended</returns>
        Task<int> EndTimedOutSessionsAsync();

        /// <summary>
        /// Deletes old sessions (older than retention days).
        /// </summary>
        Task<int> DeleteOldSessionsAsync(int retentionDays = 30);

        // ========================================
        // File Patterns
        // ========================================

        /// <summary>
        /// Adds or updates a file pattern.
        /// </summary>
        Task SavePatternAsync(FilePattern pattern);

        /// <summary>
        /// Gets all patterns matching the given file information.
        /// </summary>
        Task<List<FilePattern>> GetMatchingPatternsAsync(string fileName, string fileExtension, DateTime downloadTime);

        /// <summary>
        /// Gets the best matching pattern (highest confidence).
        /// </summary>
        Task<FilePattern?> GetBestPatternAsync(string fileName, string fileExtension, DateTime downloadTime);

        /// <summary>
        /// Updates a pattern's accuracy statistics.
        /// </summary>
        Task UpdatePatternAccuracyAsync(int patternId, bool wasCorrect);

        /// <summary>
        /// Gets all patterns for a specific group.
        /// </summary>
        Task<List<FilePattern>> GetPatternsForGroupAsync(string groupName);

        /// <summary>
        /// Deletes patterns with low confidence (below threshold).
        /// </summary>
        Task<int> DeleteLowConfidencePatternsAsync(double confidenceThreshold = 0.3, int minTimesSeen = 10);

        // ========================================
        // Statistics
        // ========================================

        /// <summary>
        /// Records a file organization event.
        /// </summary>
        Task RecordFileStatisticAsync(
            string fileName,
            string fileExtension,
            long fileSize,
            string sourceGroup,
            string targetFolder,
            bool wasBatchDownload,
            int? sessionId = null,
            string? ruleApplied = null,
            double confidenceScore = 1.0);

        /// <summary>
        /// Gets total files organized count.
        /// </summary>
        Task<int> GetTotalFilesOrganizedAsync();

        /// <summary>
        /// Gets total size of organized files in bytes.
        /// </summary>
        Task<long> GetTotalSizeOrganizedAsync();

        /// <summary>
        /// Gets top groups by file count.
        /// </summary>
        Task<Dictionary<string, int>> GetTopGroupsAsync(int limit = 10);

        /// <summary>
        /// Gets file type distribution.
        /// </summary>
        Task<Dictionary<string, int>> GetFileTypeDistributionAsync();

        /// <summary>
        /// Gets daily activity for the last N days.
        /// </summary>
        Task<Dictionary<DateTime, int>> GetDailyActivityAsync(int days = 30);

        /// <summary>
        /// Gets batch download statistics.
        /// </summary>
        Task<(int batchCount, int singleCount, double batchPercentage)> GetBatchDownloadStatsAsync();

        // ========================================
        // Context Cache
        // ========================================

        /// <summary>
        /// Adds or updates a context cache entry.
        /// </summary>
        Task SaveContextCacheAsync(string windowTitle, string groupName, string? processName = null, double confidenceScore = 1.0);

        /// <summary>
        /// Gets a cached context by window title.
        /// </summary>
        Task<(string groupName, double confidence)?> GetCachedContextAsync(string windowTitle);

        /// <summary>
        /// Updates context cache accuracy.
        /// </summary>
        Task UpdateContextAccuracyAsync(string windowTitle, bool wasAccurate);

        /// <summary>
        /// Clears old cache entries (older than retention hours).
        /// </summary>
        Task<int> ClearOldContextCacheAsync(int retentionHours = 24);

        // ========================================
        // Application State
        // ========================================

        /// <summary>
        /// Gets an application state value.
        /// </summary>
        Task<string?> GetStateValueAsync(string key);

        /// <summary>
        /// Sets an application state value.
        /// </summary>
        Task SetStateValueAsync(string key, string value);

        /// <summary>
        /// Gets all application state values.
        /// </summary>
        Task<Dictionary<string, string>> GetAllStateAsync();

        // ========================================
        // Migration & Maintenance
        // ========================================

        /// <summary>
        /// Gets the current database schema version.
        /// </summary>
        Task<int> GetSchemaVersionAsync();

        /// <summary>
        /// Runs database maintenance (VACUUM, ANALYZE).
        /// </summary>
        Task RunMaintenanceAsync();

        /// <summary>
        /// Gets database size in bytes.
        /// </summary>
        Task<long> GetDatabaseSizeAsync();

        /// <summary>
        /// Exports database to JSON for backup.
        /// </summary>
        Task<string> ExportToJsonAsync();

        /// <summary>
        /// Checks database integrity.
        /// </summary>
        Task<bool> CheckIntegrityAsync();
    }
}
