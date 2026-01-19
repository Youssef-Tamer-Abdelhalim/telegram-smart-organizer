using System;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Represents a learned file pattern for smart organization.
    /// Used by the pattern analyzer to predict group names when context is unavailable.
    /// </summary>
    public class FilePattern
    {
        /// <summary>Unique pattern identifier</summary>
        public int Id { get; set; }

        /// <summary>File extension (e.g., ".pdf", ".jpg")</summary>
        public string? FileExtension { get; set; }

        /// <summary>File name pattern (e.g., "document", "photo")</summary>
        public string? FileNamePattern { get; set; }

        /// <summary>Hour of day when file was downloaded (0-23)</summary>
        public int? HourOfDay { get; set; }

        /// <summary>Day of week when file was downloaded (0=Sunday, 6=Saturday)</summary>
        public int? DayOfWeek { get; set; }

        /// <summary>Detected/predicted group name</summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this pattern (0.0 - 1.0).
        /// Calculated as: TimesCorrect / TimesSeen
        /// </summary>
        public double ConfidenceScore { get; set; } = 0.0;

        /// <summary>How many times this pattern was seen</summary>
        public int TimesSeen { get; set; } = 0;

        /// <summary>How many times this pattern correctly predicted the group</summary>
        public int TimesCorrect { get; set; } = 0;

        /// <summary>When this pattern was first learned</summary>
        public DateTime FirstSeen { get; set; } = DateTime.Now;

        /// <summary>When this pattern was last seen</summary>
        public DateTime LastSeen { get; set; } = DateTime.Now;

        /// <summary>
        /// Updates the pattern with a new observation.
        /// </summary>
        /// <param name="wasCorrect">Whether the prediction was correct</param>
        public void UpdatePattern(bool wasCorrect)
        {
            TimesSeen++;
            if (wasCorrect)
            {
                TimesCorrect++;
            }
            
            ConfidenceScore = TimesSeen > 0 ? (double)TimesCorrect / TimesSeen : 0.0;
            LastSeen = DateTime.Now;
        }

        /// <summary>
        /// Checks if this pattern matches the given file information.
        /// </summary>
        public bool Matches(string fileName, string fileExtension, DateTime downloadTime)
        {
            // Check file extension
            if (!string.IsNullOrEmpty(FileExtension) && 
                !fileExtension.Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check file name pattern
            if (!string.IsNullOrEmpty(FileNamePattern) && 
                !fileName.Contains(FileNamePattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check hour of day
            if (HourOfDay.HasValue && downloadTime.Hour != HourOfDay.Value)
            {
                return false;
            }

            // Check day of week
            if (DayOfWeek.HasValue && (int)downloadTime.DayOfWeek != DayOfWeek.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a display-friendly description of this pattern.
        /// </summary>
        public string GetDescription()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(FileExtension))
                parts.Add($"Extension: {FileExtension}");

            if (!string.IsNullOrEmpty(FileNamePattern))
                parts.Add($"Name: *{FileNamePattern}*");

            if (HourOfDay.HasValue)
                parts.Add($"Hour: {HourOfDay}:00");

            if (DayOfWeek.HasValue)
                parts.Add($"Day: {(System.DayOfWeek)DayOfWeek.Value}");

            return parts.Count > 0 
                ? string.Join(", ", parts) 
                : "Any file";
        }
    }
}
