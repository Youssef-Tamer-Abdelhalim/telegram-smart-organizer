using System;
using SQLite;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    // ========================================
    // Database Entity Classes
    // These classes map to SQLite tables using SQLite-net attributes
    // ========================================

    [Table("download_sessions")]
    internal class DownloadSessionEntity
    {
        [PrimaryKey, AutoIncrement]
        [Column("id")]
        public int Id { get; set; }

        [Column("group_name")]
        public string GroupName { get; set; } = string.Empty;

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime? EndTime { get; set; }

        [Column("file_count")]
        public int FileCount { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("timeout_seconds")]
        public int TimeoutSeconds { get; set; }

        [Column("last_activity")]
        public DateTime LastActivity { get; set; }

        [Column("confidence_score")]
        public double ConfidenceScore { get; set; }

        [Column("process_name")]
        public string? ProcessName { get; set; }

        [Column("window_title")]
        public string? WindowTitle { get; set; }

        public DownloadSession ToModel()
        {
            return new DownloadSession
            {
                Id = Id,
                GroupName = GroupName,
                StartTime = StartTime,
                EndTime = EndTime,
                FileCount = FileCount,
                IsActive = IsActive,
                TimeoutSeconds = TimeoutSeconds,
                LastActivity = LastActivity,
                ConfidenceScore = ConfidenceScore,
                ProcessName = ProcessName,
                WindowTitle = WindowTitle
            };
        }

        public static DownloadSessionEntity FromModel(DownloadSession model)
        {
            return new DownloadSessionEntity
            {
                Id = model.Id,
                GroupName = model.GroupName,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                FileCount = model.FileCount,
                IsActive = model.IsActive,
                TimeoutSeconds = model.TimeoutSeconds,
                LastActivity = model.LastActivity,
                ConfidenceScore = model.ConfidenceScore,
                ProcessName = model.ProcessName,
                WindowTitle = model.WindowTitle
            };
        }
    }

    [Table("session_files")]
    internal class SessionFileEntity
    {
        [PrimaryKey, AutoIncrement]
        [Column("id")]
        public int Id { get; set; }

        [Column("session_id"), Indexed]
        public int SessionId { get; set; }

        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Column("file_path")]
        public string? FilePath { get; set; }

        [Column("file_size")]
        public long FileSize { get; set; }

        [Column("added_at")]
        public DateTime AddedAt { get; set; }

        [Column("organized_at")]
        public DateTime? OrganizedAt { get; set; }

        [Column("was_organized")]
        public bool WasOrganized { get; set; }
    }

    [Table("file_patterns")]
    internal class FilePatternEntity
    {
        [PrimaryKey, AutoIncrement]
        [Column("id")]
        public int Id { get; set; }

        [Column("file_extension"), Indexed]
        public string? FileExtension { get; set; }

        [Column("file_name_pattern")]
        public string? FileNamePattern { get; set; }

        [Column("hour_of_day")]
        public int? HourOfDay { get; set; }

        [Column("day_of_week")]
        public int? DayOfWeek { get; set; }

        [Column("group_name"), Indexed]
        public string GroupName { get; set; } = string.Empty;

        [Column("confidence_score")]
        public double ConfidenceScore { get; set; }

        [Column("times_seen")]
        public int TimesSeen { get; set; }

        [Column("times_correct")]
        public int TimesCorrect { get; set; }

        [Column("first_seen")]
        public DateTime FirstSeen { get; set; }

        [Column("last_seen")]
        public DateTime LastSeen { get; set; }

        public FilePattern ToModel()
        {
            return new FilePattern
            {
                Id = Id,
                FileExtension = FileExtension,
                FileNamePattern = FileNamePattern,
                HourOfDay = HourOfDay,
                DayOfWeek = DayOfWeek,
                GroupName = GroupName,
                ConfidenceScore = ConfidenceScore,
                TimesSeen = TimesSeen,
                TimesCorrect = TimesCorrect,
                FirstSeen = FirstSeen,
                LastSeen = LastSeen
            };
        }

        public static FilePatternEntity FromModel(FilePattern model)
        {
            return new FilePatternEntity
            {
                Id = model.Id,
                FileExtension = model.FileExtension,
                FileNamePattern = model.FileNamePattern,
                HourOfDay = model.HourOfDay,
                DayOfWeek = model.DayOfWeek,
                GroupName = model.GroupName,
                ConfidenceScore = model.ConfidenceScore,
                TimesSeen = model.TimesSeen,
                TimesCorrect = model.TimesCorrect,
                FirstSeen = model.FirstSeen,
                LastSeen = model.LastSeen
            };
        }
    }

    [Table("file_statistics")]
    internal class FileStatisticEntity
    {
        [PrimaryKey, AutoIncrement]
        [Column("id")]
        public int Id { get; set; }

        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Column("file_extension"), Indexed]
        public string? FileExtension { get; set; }

        [Column("file_size")]
        public long FileSize { get; set; }

        [Column("source_group"), Indexed]
        public string SourceGroup { get; set; } = string.Empty;

        [Column("target_folder")]
        public string TargetFolder { get; set; } = string.Empty;

        [Column("was_batch_download")]
        public bool WasBatchDownload { get; set; }

        [Column("session_id")]
        public int? SessionId { get; set; }

        [Column("download_time")]
        public DateTime? DownloadTime { get; set; }

        [Column("organized_time"), Indexed]
        public DateTime OrganizedTime { get; set; }

        [Column("rule_applied")]
        public string? RuleApplied { get; set; }

        [Column("confidence_score")]
        public double ConfidenceScore { get; set; }
    }

    [Table("context_cache")]
    internal class ContextCacheEntity
    {
        [PrimaryKey, AutoIncrement]
        [Column("id")]
        public int Id { get; set; }

        [Column("window_title"), Indexed]
        public string WindowTitle { get; set; } = string.Empty;

        [Column("process_name")]
        public string? ProcessName { get; set; }

        [Column("group_name")]
        public string GroupName { get; set; } = string.Empty;

        [Column("confidence_score")]
        public double ConfidenceScore { get; set; }

        [Column("times_seen")]
        public int TimesSeen { get; set; }

        [Column("first_seen")]
        public DateTime FirstSeen { get; set; }

        [Column("last_seen")]
        public DateTime LastSeen { get; set; }

        [Column("was_accurate")]
        public bool WasAccurate { get; set; }
    }

    [Table("app_state")]
    internal class AppStateEntity
    {
        [PrimaryKey]
        [Column("key")]
        public string Key { get; set; } = string.Empty;

        [Column("value")]
        public string Value { get; set; } = string.Empty;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("schema_version")]
    internal class SchemaVersionEntity
    {
        [PrimaryKey]
        [Column("version")]
        public int Version { get; set; }

        [Column("applied_at")]
        public DateTime AppliedAt { get; set; }

        [Column("description")]
        public string? Description { get; set; }
    }
}
