using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SQLite;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// SQLite implementation of IDatabaseService.
    /// Provides all database operations for v2.0.
    /// </summary>
    public partial class SQLiteDatabaseService : IDatabaseService
    {
        private readonly string _databasePath;
        private SQLiteAsyncConnection? _connection;
        private readonly object _lock = new object();

        public SQLiteDatabaseService()
        {
            // Store database in AppData/Local/TelegramOrganizer
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _databasePath = Path.Combine(appFolder, "organizer.db");
        }

        // ========================================
        // Database Initialization
        // ========================================

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                // Initialize SQLite PCL
                SQLitePCL.Batteries_V2.Init();

                // Create connection
                _connection = new SQLiteAsyncConnection(_databasePath, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

                // Execute schema from embedded SQL file
                await ExecuteSchemaAsync();

                System.Diagnostics.Debug.WriteLine($"[Database] Initialized at: {_databasePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Database] Initialization failed: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteSchemaAsync()
        {
            if (_connection == null) return;

            // Create tables using SQLite-net attributes
            await _connection.CreateTableAsync<DownloadSessionEntity>();
            await _connection.CreateTableAsync<SessionFileEntity>();
            await _connection.CreateTableAsync<FilePatternEntity>();
            await _connection.CreateTableAsync<FileStatisticEntity>();
            await _connection.CreateTableAsync<ContextCacheEntity>();
            await _connection.CreateTableAsync<AppStateEntity>();
            await _connection.CreateTableAsync<SchemaVersionEntity>();

            // Check if initial version is inserted
            var version = await _connection.Table<SchemaVersionEntity>()
                .Where(v => v.Version == 1)
                .FirstOrDefaultAsync();

            if (version == null)
            {
                await _connection.InsertAsync(new SchemaVersionEntity
                {
                    Version = 1,
                    AppliedAt = DateTime.Now,
                    Description = "Initial v2.0 schema with session tracking and pattern learning"
                });

                // Insert default app state values
                await SetStateValueAsync("version", "2.0.0");
                await SetStateValueAsync("migration_complete", "false");
                await SetStateValueAsync("total_files_organized", "0");
                await SetStateValueAsync("last_cleanup", DateTime.Now.ToString("O"));
            }
        }

        public string GetDatabasePath() => _databasePath;

        public void CloseConnection()
        {
            _connection?.CloseAsync().Wait();
            _connection = null;
        }

        // ========================================
        // Download Sessions
        // ========================================

        public async Task<DownloadSession> CreateSessionAsync(string groupName, string? windowTitle = null, string? processName = null)
        {
            if (_connection == null) throw new InvalidOperationException("Database not initialized");

            var entity = new DownloadSessionEntity
            {
                GroupName = groupName,
                StartTime = DateTime.Now,
                IsActive = true,
                TimeoutSeconds = 30,
                LastActivity = DateTime.Now,
                ConfidenceScore = 1.0,
                ProcessName = processName,
                WindowTitle = windowTitle
            };

            await _connection.InsertAsync(entity);

            return entity.ToModel();
        }

        public async Task<DownloadSession?> GetActiveSessionAsync()
        {
            if (_connection == null) return null;

            var entity = await _connection.Table<DownloadSessionEntity>()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.LastActivity)
                .FirstOrDefaultAsync();

            if (entity == null) return null;

            // Load file names
            var files = await _connection.Table<SessionFileEntity>()
                .Where(f => f.SessionId == entity.Id)
                .ToListAsync();

            var session = entity.ToModel();
            session.FileNames = files.Select(f => f.FileName).ToList();
            session.FileCount = files.Count;

            return session;
        }

        public async Task<DownloadSession?> GetSessionAsync(int sessionId)
        {
            if (_connection == null) return null;

            var entity = await _connection.Table<DownloadSessionEntity>()
                .Where(s => s.Id == sessionId)
                .FirstOrDefaultAsync();

            if (entity == null) return null;

            var files = await _connection.Table<SessionFileEntity>()
                .Where(f => f.SessionId == sessionId)
                .ToListAsync();

            var session = entity.ToModel();
            session.FileNames = files.Select(f => f.FileName).ToList();
            session.FileCount = files.Count;

            return session;
        }

        public async Task UpdateSessionAsync(DownloadSession session)
        {
            if (_connection == null) return;

            var entity = DownloadSessionEntity.FromModel(session);
            await _connection.UpdateAsync(entity);
        }

        public async Task EndSessionAsync(int sessionId)
        {
            if (_connection == null) return;

            var entity = await _connection.Table<DownloadSessionEntity>()
                .Where(s => s.Id == sessionId)
                .FirstOrDefaultAsync();

            if (entity != null)
            {
                entity.IsActive = false;
                entity.EndTime = DateTime.Now;
                await _connection.UpdateAsync(entity);
            }
        }

        public async Task AddFileToSessionAsync(int sessionId, string fileName, string? filePath = null, long fileSize = 0)
        {
            if (_connection == null) return;

            var file = new SessionFileEntity
            {
                SessionId = sessionId,
                FileName = fileName,
                FilePath = filePath,
                FileSize = fileSize,
                AddedAt = DateTime.Now
            };

            await _connection.InsertAsync(file);

            // Update session last activity and file count
            var session = await _connection.Table<DownloadSessionEntity>()
                .Where(s => s.Id == sessionId)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                session.LastActivity = DateTime.Now;
                session.FileCount++;
                await _connection.UpdateAsync(session);
            }
        }

        public async Task<List<DownloadSession>> GetSessionsAsync(bool? activeOnly = null, int limit = 100)
        {
            if (_connection == null) return new List<DownloadSession>();

            var query = _connection.Table<DownloadSessionEntity>();

            if (activeOnly.HasValue)
            {
                query = query.Where(s => s.IsActive == activeOnly.Value);
            }

            var entities = await query
                .OrderByDescending(s => s.StartTime)
                .Take(limit)
                .ToListAsync();

            var sessions = new List<DownloadSession>();
            foreach (var entity in entities)
            {
                var session = entity.ToModel();
                
                var files = await _connection.Table<SessionFileEntity>()
                    .Where(f => f.SessionId == entity.Id)
                    .ToListAsync();
                
                session.FileNames = files.Select(f => f.FileName).ToList();
                session.FileCount = files.Count;
                
                sessions.Add(session);
            }

            return sessions;
        }

        public async Task<int> EndTimedOutSessionsAsync()
        {
            if (_connection == null) return 0;

            var activeSessions = await _connection.Table<DownloadSessionEntity>()
                .Where(s => s.IsActive)
                .ToListAsync();

            int count = 0;
            foreach (var session in activeSessions)
            {
                var timeSinceActivity = (DateTime.Now - session.LastActivity).TotalSeconds;
                if (timeSinceActivity > session.TimeoutSeconds)
                {
                    session.IsActive = false;
                    session.EndTime = DateTime.Now;
                    await _connection.UpdateAsync(session);
                    count++;
                }
            }

            return count;
        }

        public async Task<int> DeleteOldSessionsAsync(int retentionDays = 30)
        {
            if (_connection == null) return 0;

            var cutoffDate = DateTime.Now.AddDays(-retentionDays);

            var oldSessions = await _connection.Table<DownloadSessionEntity>()
                .Where(s => s.StartTime < cutoffDate && !s.IsActive)
                .ToListAsync();

            foreach (var session in oldSessions)
            {
                // Delete associated files first
                await _connection.ExecuteAsync(
                    "DELETE FROM session_files WHERE session_id = ?", 
                    session.Id);
                
                // Delete session
                await _connection.DeleteAsync(session);
            }

            return oldSessions.Count;
        }

        // ========================================
        // File Patterns
        // ========================================

        public async Task SavePatternAsync(FilePattern pattern)
        {
            if (_connection == null) return;

            var entity = FilePatternEntity.FromModel(pattern);

            if (entity.Id > 0)
            {
                await _connection.UpdateAsync(entity);
            }
            else
            {
                await _connection.InsertAsync(entity);
            }
        }

        public async Task<List<FilePattern>> GetMatchingPatternsAsync(string fileName, string fileExtension, DateTime downloadTime)
        {
            if (_connection == null) return new List<FilePattern>();

            var allPatterns = await _connection.Table<FilePatternEntity>().ToListAsync();
            
            var matchingPatterns = allPatterns
                .Select(e => e.ToModel())
                .Where(p => p.Matches(fileName, fileExtension, downloadTime))
                .OrderByDescending(p => p.ConfidenceScore)
                .ThenByDescending(p => p.TimesSeen)
                .ToList();

            return matchingPatterns;
        }

        public async Task<FilePattern?> GetBestPatternAsync(string fileName, string fileExtension, DateTime downloadTime)
        {
            var patterns = await GetMatchingPatternsAsync(fileName, fileExtension, downloadTime);
            return patterns.FirstOrDefault();
        }

        public async Task UpdatePatternAccuracyAsync(int patternId, bool wasCorrect)
        {
            if (_connection == null) return;

            var entity = await _connection.Table<FilePatternEntity>()
                .Where(p => p.Id == patternId)
                .FirstOrDefaultAsync();

            if (entity != null)
            {
                var pattern = entity.ToModel();
                pattern.UpdatePattern(wasCorrect);
                
                entity = FilePatternEntity.FromModel(pattern);
                await _connection.UpdateAsync(entity);
            }
        }

        public async Task<List<FilePattern>> GetPatternsForGroupAsync(string groupName)
        {
            if (_connection == null) return new List<FilePattern>();

            var entities = await _connection.Table<FilePatternEntity>()
                .Where(p => p.GroupName == groupName)
                .OrderByDescending(p => p.ConfidenceScore)
                .ToListAsync();

            return entities.Select(e => e.ToModel()).ToList();
        }

        public async Task<int> DeleteLowConfidencePatternsAsync(double confidenceThreshold = 0.3, int minTimesSeen = 10)
        {
            if (_connection == null) return 0;

            var lowConfidencePatterns = await _connection.Table<FilePatternEntity>()
                .Where(p => p.ConfidenceScore < confidenceThreshold && p.TimesSeen >= minTimesSeen)
                .ToListAsync();

            foreach (var pattern in lowConfidencePatterns)
            {
                await _connection.DeleteAsync(pattern);
            }

            return lowConfidencePatterns.Count;
        }

        // ========================================
        // Statistics (to be continued)
        // ========================================

        public async Task RecordFileStatisticAsync(
            string fileName,
            string fileExtension,
            long fileSize,
            string sourceGroup,
            string targetFolder,
            bool wasBatchDownload,
            int? sessionId = null,
            string? ruleApplied = null,
            double confidenceScore = 1.0)
        {
            if (_connection == null) return;

            var stat = new FileStatisticEntity
            {
                FileName = fileName,
                FileExtension = fileExtension,
                FileSize = fileSize,
                SourceGroup = sourceGroup,
                TargetFolder = targetFolder,
                WasBatchDownload = wasBatchDownload,
                SessionId = sessionId,
                OrganizedTime = DateTime.Now,
                RuleApplied = ruleApplied,
                ConfidenceScore = confidenceScore
            };

            await _connection.InsertAsync(stat);

            // Update total count in app state
            var currentCount = await GetTotalFilesOrganizedAsync();
            await SetStateValueAsync("total_files_organized", (currentCount + 1).ToString());
        }

        public async Task<int> GetTotalFilesOrganizedAsync()
        {
            if (_connection == null) return 0;

            var count = await _connection.Table<FileStatisticEntity>().CountAsync();
            return count;
        }

        public async Task<long> GetTotalSizeOrganizedAsync()
        {
            if (_connection == null) return 0;

            var stats = await _connection.Table<FileStatisticEntity>().ToListAsync();
            return stats.Sum(s => s.FileSize);
        }

        public async Task<Dictionary<string, int>> GetTopGroupsAsync(int limit = 10)
        {
            if (_connection == null) return new Dictionary<string, int>();

            var groups = await _connection.QueryAsync<GroupCountResult>(
                @"SELECT source_group as GroupName, COUNT(*) as Count 
                  FROM file_statistics 
                  GROUP BY source_group 
                  ORDER BY Count DESC 
                  LIMIT ?", limit);

            return groups.ToDictionary(g => g.GroupName, g => g.Count);
        }

        public async Task<Dictionary<string, int>> GetFileTypeDistributionAsync()
        {
            if (_connection == null) return new Dictionary<string, int>();

            var types = await _connection.QueryAsync<FileTypeResult>(
                @"SELECT file_extension as FileExtension, COUNT(*) as Count 
                  FROM file_statistics 
                  GROUP BY file_extension 
                  ORDER BY Count DESC");

            return types.ToDictionary(t => t.FileExtension ?? "unknown", t => t.Count);
        }

        public async Task<Dictionary<DateTime, int>> GetDailyActivityAsync(int days = 30)
        {
            if (_connection == null) return new Dictionary<DateTime, int>();

            var cutoffDate = DateTime.Now.AddDays(-days).Date;

            var activity = await _connection.QueryAsync<DailyActivityResult>(
                @"SELECT DATE(organized_time) as Date, COUNT(*) as Count 
                  FROM file_statistics 
                  WHERE organized_time >= ? 
                  GROUP BY DATE(organized_time) 
                  ORDER BY Date", cutoffDate);

            return activity
                .Where(a => !string.IsNullOrEmpty(a.Date))
                .ToDictionary(a => DateTime.Parse(a.Date), a => a.Count);
        }

        public async Task<(int batchCount, int singleCount, double batchPercentage)> GetBatchDownloadStatsAsync()
        {
            if (_connection == null) return (0, 0, 0.0);

            var batchCount = await _connection.Table<FileStatisticEntity>()
                .Where(s => s.WasBatchDownload)
                .CountAsync();

            var singleCount = await _connection.Table<FileStatisticEntity>()
                .Where(s => !s.WasBatchDownload)
                .CountAsync();

            var total = batchCount + singleCount;
            var percentage = total > 0 ? (double)batchCount / total * 100.0 : 0.0;

            return (batchCount, singleCount, percentage);
        }

        // Continue with remaining methods in next message...

        public async Task SaveContextCacheAsync(string windowTitle, string groupName, string? processName = null, double confidenceScore = 1.0)
        {
            if (_connection == null) return;

            var existing = await _connection.Table<ContextCacheEntity>()
                .Where(c => c.WindowTitle == windowTitle)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.GroupName = groupName;
                existing.ProcessName = processName;
                existing.ConfidenceScore = confidenceScore;
                existing.TimesSeen++;
                existing.LastSeen = DateTime.Now;
                await _connection.UpdateAsync(existing);
            }
            else
            {
                var cache = new ContextCacheEntity
                {
                    WindowTitle = windowTitle,
                    GroupName = groupName,
                    ProcessName = processName,
                    ConfidenceScore = confidenceScore,
                    TimesSeen = 1,
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now,
                    WasAccurate = true
                };
                await _connection.InsertAsync(cache);
            }
        }

        public async Task<(string groupName, double confidence)?> GetCachedContextAsync(string windowTitle)
        {
            if (_connection == null) return null;

            var cache = await _connection.Table<ContextCacheEntity>()
                .Where(c => c.WindowTitle == windowTitle)
                .FirstOrDefaultAsync();

            if (cache == null) return null;

            return (cache.GroupName, cache.ConfidenceScore);
        }

        public async Task UpdateContextAccuracyAsync(string windowTitle, bool wasAccurate)
        {
            if (_connection == null) return;

            var cache = await _connection.Table<ContextCacheEntity>()
                .Where(c => c.WindowTitle == windowTitle)
                .FirstOrDefaultAsync();

            if (cache != null)
            {
                cache.WasAccurate = wasAccurate;
                await _connection.UpdateAsync(cache);
            }
        }

        public async Task<int> ClearOldContextCacheAsync(int retentionHours = 24)
        {
            if (_connection == null) return 0;

            var cutoffDate = DateTime.Now.AddHours(-retentionHours);

            var oldCaches = await _connection.Table<ContextCacheEntity>()
                .Where(c => c.LastSeen < cutoffDate)
                .ToListAsync();

            foreach (var cache in oldCaches)
            {
                await _connection.DeleteAsync(cache);
            }

            return oldCaches.Count;
        }

        public async Task<string?> GetStateValueAsync(string key)
        {
            if (_connection == null) return null;

            var state = await _connection.Table<AppStateEntity>()
                .Where(s => s.Key == key)
                .FirstOrDefaultAsync();

            return state?.Value;
        }

        public async Task SetStateValueAsync(string key, string value)
        {
            if (_connection == null) return;

            var existing = await _connection.Table<AppStateEntity>()
                .Where(s => s.Key == key)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.Now;
                await _connection.UpdateAsync(existing);
            }
            else
            {
                var state = new AppStateEntity
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.Now
                };
                await _connection.InsertAsync(state);
            }
        }

        public async Task<Dictionary<string, string>> GetAllStateAsync()
        {
            if (_connection == null) return new Dictionary<string, string>();

            var states = await _connection.Table<AppStateEntity>().ToListAsync();
            return states.ToDictionary(s => s.Key, s => s.Value);
        }

        public async Task<int> GetSchemaVersionAsync()
        {
            if (_connection == null) return 0;

            var versions = await _connection.Table<SchemaVersionEntity>()
                .OrderByDescending(v => v.Version)
                .ToListAsync();

            return versions.FirstOrDefault()?.Version ?? 0;
        }

        public async Task RunMaintenanceAsync()
        {
            if (_connection == null) return;

            await _connection.ExecuteAsync("VACUUM");
            await _connection.ExecuteAsync("ANALYZE");
        }

        public async Task<long> GetDatabaseSizeAsync()
        {
            if (!File.Exists(_databasePath)) return 0;
            
            await Task.CompletedTask;
            return new FileInfo(_databasePath).Length;
        }

        public async Task<string> ExportToJsonAsync()
        {
            if (_connection == null) return "{}";

            var export = new
            {
                Sessions = await GetSessionsAsync(null, 1000),
                Statistics = await _connection.Table<FileStatisticEntity>().ToListAsync(),
                Patterns = await _connection.Table<FilePatternEntity>().ToListAsync(),
                State = await GetAllStateAsync(),
                ExportedAt = DateTime.Now
            };

            return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task<bool> CheckIntegrityAsync()
        {
            if (_connection == null) return false;

            try
            {
                var result = await _connection.ExecuteScalarAsync<string>("PRAGMA integrity_check");
                return result == "ok";
            }
            catch
            {
                return false;
            }
        }

        // ========================================
        // Helper Classes for Query Results
        // ========================================

        private class GroupCountResult
        {
            public string GroupName { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private class FileTypeResult
        {
            public string? FileExtension { get; set; }
            public int Count { get; set; }
        }

        private class DailyActivityResult
        {
            public string Date { get; set; } = string.Empty;
            public int Count { get; set; }
        }
    }
}
