using System;
using System.Collections.Generic;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Represents a download session - a group of files downloaded together from the same Telegram group.
    /// This solves the batch download problem by capturing context once for multiple files.
    /// </summary>
    public class DownloadSession
    {
        /// <summary>Unique session identifier</summary>
        public int Id { get; set; }

        /// <summary>Detected Telegram group/channel name</summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>When the session started (first file detected)</summary>
        public DateTime StartTime { get; set; } = DateTime.Now;

        /// <summary>When the session ended (no new files for timeout period)</summary>
        public DateTime? EndTime { get; set; }

        /// <summary>Number of files in this session</summary>
        public int FileCount { get; set; } = 0;

        /// <summary>Whether this session is still active (accepting new files)</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Session timeout in seconds. If no new files are detected within this period,
        /// the session is considered complete.
        /// Default: 30 seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Last time a file was added to this session</summary>
        public DateTime LastActivity { get; set; } = DateTime.Now;

        /// <summary>Files that belong to this session (for tracking)</summary>
        public List<string> FileNames { get; set; } = new List<string>();

        /// <summary>
        /// Confidence score for the detected group name (0.0 - 1.0).
        /// 1.0 = High confidence (Telegram window was focused)
        /// 0.5 = Medium confidence (Telegram in background)
        /// 0.0 = Low confidence (fallback to pattern matching)
        /// </summary>
        public double ConfidenceScore { get; set; } = 1.0;

        /// <summary>Process name that created this session (for debugging)</summary>
        public string? ProcessName { get; set; }

        /// <summary>Active window title when session was created</summary>
        public string? WindowTitle { get; set; }

        /// <summary>
        /// Checks if the session has timed out (no activity for TimeoutSeconds).
        /// </summary>
        public bool HasTimedOut()
        {
            return IsActive && (DateTime.Now - LastActivity).TotalSeconds > TimeoutSeconds;
        }

        /// <summary>
        /// Updates the session's last activity time.
        /// </summary>
        public void UpdateActivity()
        {
            LastActivity = DateTime.Now;
        }

        /// <summary>
        /// Ends the session and marks it as inactive.
        /// </summary>
        public void End()
        {
            IsActive = false;
            EndTime = DateTime.Now;
        }

        /// <summary>
        /// Adds a file to this session.
        /// </summary>
        public void AddFile(string fileName)
        {
            if (!FileNames.Contains(fileName))
            {
                FileNames.Add(fileName);
                FileCount = FileNames.Count;
                UpdateActivity();
            }
        }
    }
}
