using System;
using System.IO;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Data.Migrations
{
    /// <summary>
    /// Migrates data from JSON files (v1.0) to SQLite database (v2.0).
    /// This is a one-time migration that runs automatically on first launch of v2.0.
    /// </summary>
    public class JsonToSQLiteMigration
    {
        private readonly IDatabaseService _databaseService;
        private readonly IPersistenceService _jsonPersistence;
        private readonly ISettingsService _settingsService;
        private readonly IStatisticsService _statisticsService;
        private readonly IRulesService _rulesService;
        private readonly ILoggingService _logger;

        public JsonToSQLiteMigration(
            IDatabaseService databaseService,
            IPersistenceService jsonPersistence,
            ISettingsService settingsService,
            IStatisticsService statisticsService,
            IRulesService rulesService,
            ILoggingService logger)
        {
            _databaseService = databaseService;
            _jsonPersistence = jsonPersistence;
            _settingsService = settingsService;
            _statisticsService = statisticsService;
            _rulesService = rulesService;
            _logger = logger;
        }

        /// <summary>
        /// Checks if migration is needed and executes it if necessary.
        /// </summary>
        public async Task<MigrationResult> MigrateIfNeededAsync()
        {
            try
            {
                // Check if migration already completed
                var migrationComplete = await _databaseService.GetStateValueAsync("migration_complete");
                if (migrationComplete == "true")
                {
                    _logger.LogInfo("[Migration] Already migrated to SQLite. Skipping.");
                    return new MigrationResult
                    {
                        Success = true,
                        AlreadyMigrated = true,
                        Message = "Migration already completed"
                    };
                }

                _logger.LogInfo("[Migration] Starting JSON to SQLite migration...");
                var result = await ExecuteMigrationAsync();

                if (result.Success)
                {
                    // Mark migration as complete
                    await _databaseService.SetStateValueAsync("migration_complete", "true");
                    await _databaseService.SetStateValueAsync("migration_date", DateTime.Now.ToString("O"));
                    
                    _logger.LogInfo($"[Migration] Completed successfully: {result.Message}");
                }
                else
                {
                    _logger.LogError($"[Migration] Failed: {result.Message}", null);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("[Migration] Unexpected error during migration", ex);
                return new MigrationResult
                {
                    Success = false,
                    Message = $"Migration failed: {ex.Message}"
                };
            }
        }

        private async Task<MigrationResult> ExecuteMigrationAsync()
        {
            var result = new MigrationResult();
            int totalMigrated = 0;

            try
            {
                // 1. Migrate pending downloads from state.json
                _logger.LogInfo("[Migration] Migrating pending downloads...");
                var state = _jsonPersistence.LoadState();
                
                if (state.PendingDownloads.Count > 0)
                {
                    // Create a session for historical pending downloads
                    var session = await _databaseService.CreateSessionAsync(
                        "Historical Downloads",
                        "Migrated from v1.0",
                        "Migration");

                    foreach (var kvp in state.PendingDownloads)
                    {
                        try
                        {
                            await _databaseService.AddFileToSessionAsync(
                                session.Id,
                                kvp.Key,
                                null,
                                0);
                            
                            result.PendingDownloadsMigrated++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"[Migration] Failed to migrate pending download '{kvp.Key}': {ex.Message}");
                        }
                    }

                    // End the historical session
                    await _databaseService.EndSessionAsync(session.Id);
                }

                _logger.LogInfo($"[Migration] Migrated {result.PendingDownloadsMigrated} pending downloads");

                // 2. Migrate statistics from statistics.json
                _logger.LogInfo("[Migration] Migrating statistics...");
                var stats = _statisticsService.LoadStatistics();

                // Migrate top groups as file patterns
                foreach (var group in stats.TopGroups)
                {
                    try
                    {
                        // Create a pattern for each frequently used group
                        var pattern = new FilePattern
                        {
                            GroupName = group.Key,
                            ConfidenceScore = 0.5, // Medium confidence since it's historical
                            TimesSeen = group.Value,
                            TimesCorrect = group.Value, // Assume all were correct
                            FirstSeen = DateTime.Now.AddDays(-30), // Approximate
                            LastSeen = DateTime.Now
                        };

                        await _databaseService.SavePatternAsync(pattern);
                        result.PatternsMigrated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[Migration] Failed to migrate pattern for '{group.Key}': {ex.Message}");
                    }
                }

                // Migrate file type distribution as patterns
                foreach (var fileType in stats.FileTypeDistribution)
                {
                    try
                    {
                        // Try to find most common group for this file type
                        var mostCommonGroup = stats.TopGroups.Keys.FirstOrDefault() ?? "Unknown";

                        var pattern = new FilePattern
                        {
                            FileExtension = fileType.Key,
                            GroupName = mostCommonGroup,
                            ConfidenceScore = 0.3, // Lower confidence for extension-only patterns
                            TimesSeen = fileType.Value,
                            TimesCorrect = (int)(fileType.Value * 0.7), // Assume 70% accuracy
                            FirstSeen = DateTime.Now.AddDays(-30),
                            LastSeen = DateTime.Now
                        };

                        await _databaseService.SavePatternAsync(pattern);
                        result.PatternsMigrated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[Migration] Failed to migrate file type pattern '{fileType.Key}': {ex.Message}");
                    }
                }

                _logger.LogInfo($"[Migration] Migrated {result.PatternsMigrated} patterns");

                // 3. Migrate total counts to app state
                await _databaseService.SetStateValueAsync(
                    "total_files_organized", 
                    stats.TotalFilesOrganized.ToString());
                
                await _databaseService.SetStateValueAsync(
                    "total_size_bytes", 
                    stats.TotalSizeBytes.ToString());

                result.StatisticsMigrated = 2; // Total files + total size

                _logger.LogInfo($"[Migration] Migrated {result.StatisticsMigrated} statistics");

                // 4. Migrate settings to app state
                _logger.LogInfo("[Migration] Migrating settings...");
                var settings = _settingsService.LoadSettings();

                await _databaseService.SetStateValueAsync("downloads_folder", settings.DownloadsFolderPath);
                await _databaseService.SetStateValueAsync("destination_base", settings.DestinationBasePath);
                await _databaseService.SetStateValueAsync("retention_days", settings.RetentionDays.ToString());
                await _databaseService.SetStateValueAsync("start_minimized", settings.StartMinimized.ToString());
                await _databaseService.SetStateValueAsync("minimize_to_tray", settings.MinimizeToTray.ToString());
                await _databaseService.SetStateValueAsync("show_notifications", settings.ShowNotifications.ToString());
                await _databaseService.SetStateValueAsync("use_dark_theme", settings.UseDarkTheme.ToString());
                await _databaseService.SetStateValueAsync("run_on_startup", settings.RunOnStartup.ToString());

                result.SettingsMigrated = 8;

                _logger.LogInfo($"[Migration] Migrated {result.SettingsMigrated} settings");

                // 5. Rules are already in JSON format and will stay there for now
                // (They can be accessed through IRulesService which still uses JSON)

                totalMigrated = result.PendingDownloadsMigrated + 
                               result.PatternsMigrated + 
                               result.StatisticsMigrated + 
                               result.SettingsMigrated;

                result.Success = true;
                result.Message = $"Successfully migrated {totalMigrated} items from JSON to SQLite";

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Migration failed after migrating {totalMigrated} items: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Creates a backup of all JSON files before migration.
        /// </summary>
        public void CreateBackup()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
                string backupFolder = Path.Combine(appFolder, "Backup_v1.0_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                if (!Directory.Exists(backupFolder))
                {
                    Directory.CreateDirectory(backupFolder);
                }

                // Backup all JSON files
                var jsonFiles = new[] { "state.json", "settings.json", "statistics.json", "rules.json" };
                
                foreach (var file in jsonFiles)
                {
                    string sourcePath = Path.Combine(appFolder, file);
                    if (File.Exists(sourcePath))
                    {
                        string destPath = Path.Combine(backupFolder, file);
                        File.Copy(sourcePath, destPath, true);
                        _logger.LogInfo($"[Migration] Backed up {file}");
                    }
                }

                _logger.LogInfo($"[Migration] Backup created at: {backupFolder}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[Migration] Failed to create backup: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Result of a migration operation.
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public bool AlreadyMigrated { get; set; }
        public string Message { get; set; } = string.Empty;
        public int PendingDownloadsMigrated { get; set; }
        public int PatternsMigrated { get; set; }
        public int StatisticsMigrated { get; set; }
        public int SettingsMigrated { get; set; }
        public int RulesMigrated { get; set; }

        public int TotalMigrated => PendingDownloadsMigrated + PatternsMigrated + 
                                   StatisticsMigrated + SettingsMigrated + RulesMigrated;

        public override string ToString()
        {
            if (AlreadyMigrated)
                return "Migration already completed";

            if (!Success)
                return $"Migration failed: {Message}";

            return $"Migration successful: {TotalMigrated} items migrated " +
                   $"(Pending: {PendingDownloadsMigrated}, Patterns: {PatternsMigrated}, " +
                   $"Stats: {StatisticsMigrated}, Settings: {SettingsMigrated})";
        }
    }
}
