using System;
using System.Collections.Generic;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Represents the complete application state that needs to be persisted.
    /// Contains all pending downloads and metadata.
    /// </summary>
    public class AppState
    {
        /// <summary>
        /// Dictionary of pending downloads.
        /// Key: Temp file name (e.g., "doc.pdf.td")
        /// Value: FileContext with group name and metadata
        /// </summary>
        public Dictionary<string, FileContext> PendingDownloads { get; set; } = new();

        /// <summary>
        /// Timestamp of when this state was last saved.
        /// </summary>
        public DateTime LastSavedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Application version that created this state (for future migration support).
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Total number of files successfully organized (lifetime counter).
        /// </summary>
        public int TotalFilesOrganized { get; set; } = 0;
    }
}
