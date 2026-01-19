using System;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Service for managing download sessions.
    /// This is the main interface used by SmartOrganizerEngine to handle batch downloads.
    /// </summary>
    public interface IDownloadSessionManager
    {
        // ========================================
        // Session Management
        // ========================================

        /// <summary>
        /// Gets the currently active session, or null if no active session exists.
        /// </summary>
        Task<DownloadSession?> GetActiveSessionAsync();

        /// <summary>
        /// Starts a new download session with the given group name.
        /// If an active session already exists for the same group, it will be reused.
        /// </summary>
        /// <param name="groupName">Telegram group/channel name</param>
        /// <param name="windowTitle">Active window title (for debugging)</param>
        /// <param name="processName">Process name (for debugging)</param>
        /// <param name="confidenceScore">Confidence in the group name (0.0-1.0)</param>
        /// <returns>The created or existing session</returns>
        Task<DownloadSession> StartSessionAsync(
            string groupName, 
            string? windowTitle = null, 
            string? processName = null,
            double confidenceScore = 1.0);

        /// <summary>
        /// Adds a file to the current active session.
        /// If no active session exists, creates a new one with the given group name.
        /// </summary>
        /// <param name="fileName">File name to add</param>
        /// <param name="groupName">Fallback group name if no active session</param>
        /// <param name="filePath">Full file path (optional)</param>
        /// <param name="fileSize">File size in bytes (optional)</param>
        /// <returns>The session the file was added to</returns>
        Task<DownloadSession> AddFileToSessionAsync(
            string fileName, 
            string groupName, 
            string? filePath = null, 
            long fileSize = 0);

        /// <summary>
        /// Ends the currently active session.
        /// </summary>
        Task EndCurrentSessionAsync();

        /// <summary>
        /// Ends a specific session by ID.
        /// </summary>
        Task EndSessionAsync(int sessionId);

        /// <summary>
        /// Checks if there is an active session.
        /// </summary>
        Task<bool> IsSessionActiveAsync();

        /// <summary>
        /// Gets the group name from the current active session, or null if no active session.
        /// </summary>
        Task<string?> GetCurrentGroupNameAsync();

        // ========================================
        // Session Lifecycle
        // ========================================

        /// <summary>
        /// Checks for timed-out sessions and ends them automatically.
        /// Should be called periodically (e.g., every 10 seconds).
        /// </summary>
        /// <returns>Number of sessions that were ended due to timeout</returns>
        Task<int> CheckAndEndTimedOutSessionsAsync();

        /// <summary>
        /// Gets the remaining time before the current session times out (in seconds).
        /// Returns null if no active session.
        /// </summary>
        Task<double?> GetSessionTimeoutRemainingAsync();

        // ========================================
        // Session History
        // ========================================

        /// <summary>
        /// Gets recent sessions (last N sessions).
        /// </summary>
        /// <param name="limit">Maximum number of sessions to return</param>
        /// <param name="includeActive">Whether to include active sessions</param>
        Task<System.Collections.Generic.List<DownloadSession>> GetRecentSessionsAsync(
            int limit = 10, 
            bool includeActive = true);

        /// <summary>
        /// Gets a session by ID.
        /// </summary>
        Task<DownloadSession?> GetSessionByIdAsync(int sessionId);

        // ========================================
        // Statistics
        // ========================================

        /// <summary>
        /// Gets the total number of sessions created.
        /// </summary>
        Task<int> GetTotalSessionsCountAsync();

        /// <summary>
        /// Gets the average number of files per session.
        /// </summary>
        Task<double> GetAverageFilesPerSessionAsync();

        /// <summary>
        /// Gets the most active group (most sessions).
        /// </summary>
        Task<(string groupName, int sessionCount)?> GetMostActiveGroupAsync();

        // ========================================
        // Configuration
        // ========================================

        /// <summary>
        /// Sets the default session timeout in seconds.
        /// Default: 30 seconds
        /// </summary>
        void SetDefaultTimeout(int timeoutSeconds);

        /// <summary>
        /// Gets the default session timeout in seconds.
        /// </summary>
        int GetDefaultTimeout();

        // ========================================
        // Events
        // ========================================

        /// <summary>
        /// Fired when a new session is started.
        /// </summary>
        event EventHandler<DownloadSession>? SessionStarted;

        /// <summary>
        /// Fired when a session is ended.
        /// </summary>
        event EventHandler<DownloadSession>? SessionEnded;

        /// <summary>
        /// Fired when a file is added to a session.
        /// </summary>
        event EventHandler<(DownloadSession session, string fileName)>? FileAddedToSession;

        /// <summary>
        /// Fired when a session times out.
        /// </summary>
        event EventHandler<DownloadSession>? SessionTimedOut;
    }
}
