using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// Manages download sessions to solve the batch download problem.
    /// This service handles session lifecycle, timeout management, and file tracking.
    /// </summary>
    public class DownloadSessionManager : IDownloadSessionManager
    {
        private readonly IDatabaseService _database;
        private readonly ILoggingService _logger;
        private int _defaultTimeoutSeconds = 30;

        // Events
        public event EventHandler<DownloadSession>? SessionStarted;
        public event EventHandler<DownloadSession>? SessionEnded;
        public event EventHandler<(DownloadSession session, string fileName)>? FileAddedToSession;
        public event EventHandler<DownloadSession>? SessionTimedOut;

        public DownloadSessionManager(
            IDatabaseService database,
            ILoggingService logger)
        {
            _database = database;
            _logger = logger;
        }

        // ========================================
        // Session Management
        // ========================================

        public async Task<DownloadSession?> GetActiveSessionAsync()
        {
            try
            {
                return await _database.GetActiveSessionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to get active session", ex);
                return null;
            }
        }

        public async Task<DownloadSession> StartSessionAsync(
            string groupName,
            string? windowTitle = null,
            string? processName = null,
            double confidenceScore = 1.0)
        {
            try
            {
                // Check if there's already an active session for the same group
                var activeSession = await GetActiveSessionAsync();

                if (activeSession != null && activeSession.GroupName == groupName)
                {
                    // Reuse existing session, just update activity
                    activeSession.UpdateActivity();
                    await _database.UpdateSessionAsync(activeSession);

                    _logger.LogInfo($"[SessionManager] Reusing active session #{activeSession.Id} for '{groupName}'");
                    return activeSession;
                }

                // End any other active sessions
                if (activeSession != null)
                {
                    _logger.LogInfo($"[SessionManager] Ending previous session #{activeSession.Id} for '{activeSession.GroupName}'");
                    await EndSessionAsync(activeSession.Id);
                }

                // Create new session
                var newSession = await _database.CreateSessionAsync(groupName, windowTitle, processName);
                newSession.TimeoutSeconds = _defaultTimeoutSeconds;
                newSession.ConfidenceScore = confidenceScore;
                await _database.UpdateSessionAsync(newSession);

                _logger.LogInfo($"[SessionManager] Started new session #{newSession.Id} for '{groupName}' " +
                              $"(confidence: {confidenceScore:F2}, timeout: {_defaultTimeoutSeconds}s)");

                // Fire event
                SessionStarted?.Invoke(this, newSession);

                return newSession;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SessionManager] Failed to start session for '{groupName}'", ex);
                throw;
            }
        }

        public async Task<DownloadSession> AddFileToSessionAsync(
            string fileName,
            string groupName,
            string? filePath = null,
            long fileSize = 0)
        {
            try
            {
                // Get or create active session
                var session = await GetActiveSessionAsync();

                if (session == null || session.GroupName != groupName)
                {
                    // Create new session if none exists or group changed
                    session = await StartSessionAsync(groupName);
                }

                // Add file to session
                await _database.AddFileToSessionAsync(session.Id, fileName, filePath, fileSize);

                // Update session model
                session.AddFile(fileName);
                await _database.UpdateSessionAsync(session);

                _logger.LogDebug($"[SessionManager] Added '{fileName}' to session #{session.Id} " +
                               $"(total files: {session.FileCount})");

                // Fire event
                FileAddedToSession?.Invoke(this, (session, fileName));

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SessionManager] Failed to add file '{fileName}' to session", ex);
                throw;
            }
        }

        public async Task EndCurrentSessionAsync()
        {
            try
            {
                var session = await GetActiveSessionAsync();
                if (session != null)
                {
                    await EndSessionAsync(session.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to end current session", ex);
            }
        }

        public async Task EndSessionAsync(int sessionId)
        {
            try
            {
                var session = await _database.GetSessionAsync(sessionId);
                if (session == null) return;

                await _database.EndSessionAsync(sessionId);

                _logger.LogInfo($"[SessionManager] Ended session #{sessionId} for '{session.GroupName}' " +
                              $"(files: {session.FileCount}, duration: {(DateTime.Now - session.StartTime).TotalSeconds:F1}s)");

                // Fire event
                SessionEnded?.Invoke(this, session);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SessionManager] Failed to end session #{sessionId}", ex);
            }
        }

        public async Task<bool> IsSessionActiveAsync()
        {
            var session = await GetActiveSessionAsync();
            return session != null;
        }

        public async Task<string?> GetCurrentGroupNameAsync()
        {
            var session = await GetActiveSessionAsync();
            return session?.GroupName;
        }

        // ========================================
        // Session Lifecycle
        // ========================================

        public async Task<int> CheckAndEndTimedOutSessionsAsync()
        {
            try
            {
                int count = await _database.EndTimedOutSessionsAsync();

                if (count > 0)
                {
                    _logger.LogInfo($"[SessionManager] Ended {count} timed-out session(s)");

                    // Fire events for timed-out sessions
                    // Note: We can't get the session details after they're ended,
                    // but we could query them before ending if needed
                }

                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to check for timed-out sessions", ex);
                return 0;
            }
        }

        public async Task<double?> GetSessionTimeoutRemainingAsync()
        {
            try
            {
                var session = await GetActiveSessionAsync();
                if (session == null) return null;

                var elapsed = (DateTime.Now - session.LastActivity).TotalSeconds;
                var remaining = session.TimeoutSeconds - elapsed;

                return remaining > 0 ? remaining : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to get timeout remaining", ex);
                return null;
            }
        }

        // ========================================
        // Session History
        // ========================================

        public async Task<List<DownloadSession>> GetRecentSessionsAsync(int limit = 10, bool includeActive = true)
        {
            try
            {
                return await _database.GetSessionsAsync(
                    activeOnly: includeActive ? null : false,
                    limit: limit);
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to get recent sessions", ex);
                return new List<DownloadSession>();
            }
        }

        public async Task<DownloadSession?> GetSessionByIdAsync(int sessionId)
        {
            try
            {
                return await _database.GetSessionAsync(sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[SessionManager] Failed to get session #{sessionId}", ex);
                return null;
            }
        }

        // ========================================
        // Statistics
        // ========================================

        public async Task<int> GetTotalSessionsCountAsync()
        {
            try
            {
                var sessions = await _database.GetSessionsAsync(null, 10000);
                return sessions.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to get total sessions count", ex);
                return 0;
            }
        }

        public async Task<double> GetAverageFilesPerSessionAsync()
        {
            try
            {
                var sessions = await _database.GetSessionsAsync(null, 1000);
                if (sessions.Count == 0) return 0;

                return sessions.Average(s => s.FileCount);
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to get average files per session", ex);
                return 0;
            }
        }

        public async Task<(string groupName, int sessionCount)?> GetMostActiveGroupAsync()
        {
            try
            {
                var sessions = await _database.GetSessionsAsync(null, 1000);
                if (sessions.Count == 0) return null;

                var groupCounts = sessions
                    .GroupBy(s => s.GroupName)
                    .Select(g => new { GroupName = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .FirstOrDefault();

                if (groupCounts == null) return null;

                return (groupCounts.GroupName, groupCounts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError("[SessionManager] Failed to get most active group", ex);
                return null;
            }
        }

        // ========================================
        // Configuration
        // ========================================

        public void SetDefaultTimeout(int timeoutSeconds)
        {
            if (timeoutSeconds < 5 || timeoutSeconds > 300)
            {
                _logger.LogWarning($"[SessionManager] Invalid timeout value: {timeoutSeconds}s. Must be between 5-300s.");
                return;
            }

            _defaultTimeoutSeconds = timeoutSeconds;
            _logger.LogInfo($"[SessionManager] Default session timeout set to {timeoutSeconds}s");
        }

        public int GetDefaultTimeout()
        {
            return _defaultTimeoutSeconds;
        }
    }
}
