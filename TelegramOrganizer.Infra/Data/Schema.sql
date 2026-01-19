-- Telegram Smart Organizer v2.0 Database Schema
-- SQLite Database Schema for session tracking and pattern learning
-- Created: January 2025

-- ========================================
-- Download Sessions Table
-- ========================================
-- Tracks download sessions to solve the batch download problem.
-- Each session represents a group of files downloaded from the same Telegram group.
CREATE TABLE IF NOT EXISTS download_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    group_name TEXT NOT NULL,
    start_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    end_time DATETIME,
    file_count INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT 1,
    timeout_seconds INTEGER DEFAULT 30,
    last_activity DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    confidence_score REAL DEFAULT 1.0,
    process_name TEXT,
    window_title TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Index for finding active sessions quickly
CREATE INDEX IF NOT EXISTS idx_sessions_active 
ON download_sessions(is_active, last_activity);

-- Index for session cleanup queries
CREATE INDEX IF NOT EXISTS idx_sessions_group 
ON download_sessions(group_name, start_time);

-- ========================================
-- Session Files Table
-- ========================================
-- Maps files to their download sessions (many-to-one relationship).
CREATE TABLE IF NOT EXISTS session_files (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    file_name TEXT NOT NULL,
    file_path TEXT,
    file_size INTEGER DEFAULT 0,
    added_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    organized_at DATETIME,
    was_organized BOOLEAN DEFAULT 0,
    FOREIGN KEY (session_id) REFERENCES download_sessions(id) ON DELETE CASCADE
);

-- Index for finding files by session
CREATE INDEX IF NOT EXISTS idx_session_files_session 
ON session_files(session_id, added_at);

-- Index for finding unorganized files
CREATE INDEX IF NOT EXISTS idx_session_files_organized 
ON session_files(was_organized, added_at);

-- ========================================
-- File Patterns Table
-- ========================================
-- Stores learned file patterns for smart organization when context is unavailable.
CREATE TABLE IF NOT EXISTS file_patterns (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_extension TEXT,
    file_name_pattern TEXT,
    hour_of_day INTEGER,
    day_of_week INTEGER,
    group_name TEXT NOT NULL,
    confidence_score REAL DEFAULT 0.0,
    times_seen INTEGER DEFAULT 0,
    times_correct INTEGER DEFAULT 0,
    first_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- Index for pattern matching queries
CREATE INDEX IF NOT EXISTS idx_patterns_extension 
ON file_patterns(file_extension, group_name);

-- Index for pattern confidence queries
CREATE INDEX IF NOT EXISTS idx_patterns_confidence 
ON file_patterns(confidence_score DESC, times_seen DESC);

-- Index for time-based patterns
CREATE INDEX IF NOT EXISTS idx_patterns_time 
ON file_patterns(hour_of_day, day_of_week);

-- ========================================
-- File Statistics Table (Enhanced)
-- ========================================
-- Stores detailed statistics about organized files.
CREATE TABLE IF NOT EXISTS file_statistics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_name TEXT NOT NULL,
    file_extension TEXT,
    file_size INTEGER DEFAULT 0,
    source_group TEXT NOT NULL,
    target_folder TEXT NOT NULL,
    was_batch_download BOOLEAN DEFAULT 0,
    session_id INTEGER,
    download_time DATETIME,
    organized_time DATETIME DEFAULT CURRENT_TIMESTAMP,
    rule_applied TEXT,
    confidence_score REAL DEFAULT 1.0,
    FOREIGN KEY (session_id) REFERENCES download_sessions(id) ON DELETE SET NULL
);

-- Index for statistics queries by group
CREATE INDEX IF NOT EXISTS idx_stats_group 
ON file_statistics(source_group, organized_time);

-- Index for statistics queries by time
CREATE INDEX IF NOT EXISTS idx_stats_time 
ON file_statistics(organized_time DESC);

-- Index for file type analysis
CREATE INDEX IF NOT EXISTS idx_stats_extension 
ON file_statistics(file_extension, organized_time);

-- Index for batch download analysis
CREATE INDEX IF NOT EXISTS idx_stats_batch 
ON file_statistics(was_batch_download, session_id);

-- ========================================
-- Context Cache Table
-- ========================================
-- Caches recently seen Telegram window contexts for faster lookup.
CREATE TABLE IF NOT EXISTS context_cache (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    window_title TEXT NOT NULL,
    process_name TEXT,
    group_name TEXT NOT NULL,
    confidence_score REAL DEFAULT 1.0,
    times_seen INTEGER DEFAULT 1,
    first_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_seen DATETIME DEFAULT CURRENT_TIMESTAMP,
    was_accurate BOOLEAN DEFAULT 1
);

-- Index for context lookup
CREATE INDEX IF NOT EXISTS idx_context_window 
ON context_cache(window_title, last_seen DESC);

-- Index for accuracy tracking
CREATE INDEX IF NOT EXISTS idx_context_accuracy 
ON context_cache(was_accurate, confidence_score DESC);

-- ========================================
-- Application State Table
-- ========================================
-- Stores key-value pairs for application state (migration from JSON).
CREATE TABLE IF NOT EXISTS app_state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- ========================================
-- Database Version Table
-- ========================================
-- Tracks database schema version for migration support.
CREATE TABLE IF NOT EXISTS schema_version (
    version INTEGER PRIMARY KEY,
    applied_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    description TEXT
);

-- Insert initial version
INSERT OR IGNORE INTO schema_version (version, description) 
VALUES (1, 'Initial v2.0 schema with session tracking and pattern learning');

-- ========================================
-- Views for Common Queries
-- ========================================

-- View: Active sessions with file counts
CREATE VIEW IF NOT EXISTS v_active_sessions AS
SELECT 
    s.id,
    s.group_name,
    s.start_time,
    s.file_count,
    s.last_activity,
    s.confidence_score,
    COUNT(f.id) as actual_file_count,
    (julianday('now') - julianday(s.last_activity)) * 86400 as seconds_since_activity
FROM download_sessions s
LEFT JOIN session_files f ON s.id = f.session_id
WHERE s.is_active = 1
GROUP BY s.id;

-- View: Top groups by file count
CREATE VIEW IF NOT EXISTS v_top_groups AS
SELECT 
    source_group,
    COUNT(*) as file_count,
    SUM(file_size) as total_size,
    MAX(organized_time) as last_organized
FROM file_statistics
GROUP BY source_group
ORDER BY file_count DESC;

-- View: Pattern effectiveness
CREATE VIEW IF NOT EXISTS v_pattern_effectiveness AS
SELECT 
    id,
    file_extension,
    file_name_pattern,
    group_name,
    confidence_score,
    times_seen,
    times_correct,
    ROUND(100.0 * times_correct / NULLIF(times_seen, 0), 2) as accuracy_percent,
    last_seen
FROM file_patterns
WHERE times_seen > 0
ORDER BY confidence_score DESC, times_seen DESC;

-- ========================================
-- Cleanup Triggers
-- ========================================

-- Trigger: Update last_activity on session when file is added
CREATE TRIGGER IF NOT EXISTS trg_session_update_activity
AFTER INSERT ON session_files
BEGIN
    UPDATE download_sessions 
    SET 
        last_activity = CURRENT_TIMESTAMP,
        file_count = (SELECT COUNT(*) FROM session_files WHERE session_id = NEW.session_id)
    WHERE id = NEW.session_id;
END;

-- Trigger: Update session file_count when file is organized
CREATE TRIGGER IF NOT EXISTS trg_session_file_organized
AFTER UPDATE OF was_organized ON session_files
WHEN NEW.was_organized = 1 AND OLD.was_organized = 0
BEGIN
    UPDATE session_files 
    SET organized_at = CURRENT_TIMESTAMP 
    WHERE id = NEW.id;
END;

-- ========================================
-- Initial Data
-- ========================================

-- Insert default app state values
INSERT OR IGNORE INTO app_state (key, value) VALUES ('version', '2.0.0');
INSERT OR IGNORE INTO app_state (key, value) VALUES ('migration_complete', 'false');
INSERT OR IGNORE INTO app_state (key, value) VALUES ('total_files_organized', '0');
INSERT OR IGNORE INTO app_state (key, value) VALUES ('last_cleanup', datetime('now'));

-- ========================================
-- VACUUM and ANALYZE for optimization
-- ========================================
-- Run these periodically for optimal performance
-- VACUUM; -- Reclaim unused space
-- ANALYZE; -- Update query planner statistics
