using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Detects when multiple files are being downloaded in a burst (rapid succession).
    /// This helps identify batch downloads and ensures they use the same context/session.
    /// </summary>
    public interface IDownloadBurstDetector
    {
        /// <summary>
        /// Checks if a file download is part of a burst (multiple files within threshold time).
        /// </summary>
        /// <param name="fileName">The file name being downloaded</param>
        /// <param name="detectedTime">When the file was detected (default: now)</param>
        /// <returns>True if this is part of a burst, false otherwise</returns>
        bool IsBurstDownload(string fileName, DateTime? detectedTime = null);

        /// <summary>
        /// Records a file download event for burst detection.
        /// </summary>
        /// <param name="fileName">The file name being downloaded</param>
        /// <param name="detectedTime">When the file was detected (default: now)</param>
        void RecordDownload(string fileName, DateTime? detectedTime = null);

        /// <summary>
        /// Gets the current burst status and information.
        /// </summary>
        /// <returns>Burst detection result with details</returns>
        BurstDetectionResult GetCurrentBurstStatus();

        /// <summary>
        /// Clears all recorded downloads and resets burst detection.
        /// </summary>
        void Reset();

        /// <summary>
        /// Gets the number of files downloaded in the current burst window.
        /// </summary>
        int GetCurrentBurstCount();

        /// <summary>
        /// Gets the time remaining until the current burst expires (in seconds).
        /// Returns null if no active burst.
        /// </summary>
        double? GetBurstTimeRemaining();

        /// <summary>
        /// Fired when a new burst is detected (first file after idle period).
        /// </summary>
        event EventHandler<BurstDetectionResult>? BurstStarted;

        /// <summary>
        /// Fired when a burst continues (additional file within threshold).
        /// </summary>
        event EventHandler<BurstDetectionResult>? BurstContinued;

        /// <summary>
        /// Fired when a burst ends (timeout reached with no new files).
        /// </summary>
        event EventHandler<BurstDetectionResult>? BurstEnded;

        /// <summary>
        /// Configuration: Time threshold in seconds to consider files as part of same burst.
        /// Default: 5 seconds
        /// </summary>
        int BurstThresholdSeconds { get; set; }

        /// <summary>
        /// Configuration: Minimum number of files to qualify as a burst.
        /// Default: 2 files
        /// </summary>
        int MinimumFilesForBurst { get; set; }

        /// <summary>
        /// Configuration: Maximum time to track a burst before auto-ending (in seconds).
        /// Default: 60 seconds
        /// </summary>
        int MaxBurstDurationSeconds { get; set; }
    }

    /// <summary>
    /// Result of burst detection containing status and statistics.
    /// </summary>
    public class BurstDetectionResult
    {
        /// <summary>Whether files are currently being downloaded in a burst</summary>
        public bool IsBurstActive { get; set; }

        /// <summary>Number of files in the current/last burst</summary>
        public int FileCount { get; set; }

        /// <summary>When the burst started</summary>
        public DateTime? BurstStartTime { get; set; }

        /// <summary>When the last file in the burst was detected</summary>
        public DateTime? LastFileTime { get; set; }

        /// <summary>List of files in the current burst</summary>
        public List<string> FileNames { get; set; } = new List<string>();

        /// <summary>Duration of the burst in seconds</summary>
        public double DurationSeconds
        {
            get
            {
                if (BurstStartTime == null || LastFileTime == null)
                    return 0;
                return (LastFileTime.Value - BurstStartTime.Value).TotalSeconds;
            }
        }

        /// <summary>Average time between files in seconds</summary>
        public double AverageIntervalSeconds
        {
            get
            {
                if (FileCount <= 1) return 0;
                return DurationSeconds / (FileCount - 1);
            }
        }

        /// <summary>Confidence that this is a genuine batch download (0.0 - 1.0)</summary>
        public double Confidence
        {
            get
            {
                // Higher file count = higher confidence
                // Shorter average interval = higher confidence
                if (FileCount < 2) return 0.0;
                if (FileCount >= 10) return 1.0;
                
                double fileCountScore = Math.Min(FileCount / 10.0, 1.0);
                double intervalScore = AverageIntervalSeconds < 2.0 ? 1.0 : Math.Max(0.0, 1.0 - (AverageIntervalSeconds / 10.0));
                
                return (fileCountScore + intervalScore) / 2.0;
            }
        }

        public override string ToString()
        {
            if (!IsBurstActive && FileCount == 0)
                return "No burst detected";

            return $"Burst: {FileCount} files in {DurationSeconds:F1}s (avg {AverageIntervalSeconds:F1}s/file, confidence {Confidence:F2})";
        }
    }
}
