using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Multi-source context detector that combines signals from multiple sources
    /// using weighted voting to determine the best context for file organization.
    /// 
    /// Signal Sources:
    /// 1. Foreground Window - Direct user focus (highest weight)
    /// 2. Background Monitor - Recent Telegram activity (medium weight)
    /// 3. Pattern Learning - Historical patterns from database (lower weight)
    /// 4. Active Session - Current download session context (medium-high weight)
    /// </summary>
    public interface IMultiSourceContextDetector
    {
        /// <summary>
        /// Detects context using multiple signal sources with weighted voting.
        /// This is the main detection method that combines all available signals.
        /// </summary>
        /// <param name="fileName">Name of the file being organized</param>
        /// <param name="downloadTime">Time when the download was detected</param>
        /// <returns>The detected context (group name) with highest weighted vote</returns>
        Task<string> DetectContextAsync(string fileName, DateTime downloadTime);

        /// <summary>
        /// Detects context and returns detailed result including confidence and signal breakdown.
        /// Use this method when you need full diagnostic information.
        /// </summary>
        /// <param name="fileName">Name of the file being organized</param>
        /// <param name="downloadTime">Time when the download was detected</param>
        /// <returns>Detailed detection result with all contributing signals</returns>
        Task<MultiSourceDetectionResult> DetectContextWithDetailsAsync(string fileName, DateTime downloadTime);

        /// <summary>
        /// Gets the confidence score from the most recent detection (0.0 to 1.0).
        /// </summary>
        double GetLastConfidenceScore();

        /// <summary>
        /// Gets breakdown of signal sources that contributed to the last detection.
        /// Key: Source name (e.g., "Foreground", "Background", "Pattern")
        /// Value: Voting power contributed by that source
        /// </summary>
        Dictionary<string, double> GetLastSignalBreakdown();

        /// <summary>
        /// Gets all signals from the last detection.
        /// </summary>
        List<ContextSignal> GetLastSignals();

        /// <summary>
        /// Collects signals from all available sources without voting.
        /// Useful for debugging and understanding what each source detects.
        /// </summary>
        /// <param name="fileName">Name of the file</param>
        /// <param name="downloadTime">Download time</param>
        /// <returns>List of all collected signals</returns>
        Task<List<ContextSignal>> CollectAllSignalsAsync(string fileName, DateTime downloadTime);

        /// <summary>
        /// Records feedback about a detection to improve pattern learning.
        /// Call this after file organization to teach the system.
        /// </summary>
        /// <param name="fileName">Name of the file that was organized</param>
        /// <param name="detectedContext">Context that was used for organization</param>
        /// <param name="actualContext">Actual correct context (if different from detected)</param>
        /// <param name="wasCorrect">Whether the detection was correct</param>
        Task RecordFeedbackAsync(string fileName, string detectedContext, string? actualContext, bool wasCorrect);

        // ========================================
        // Configuration
        // ========================================

        /// <summary>
        /// Gets or sets the weight for foreground window signals (default: 0.5).
        /// Range: 0.0 to 1.0
        /// </summary>
        double ForegroundWeight { get; set; }

        /// <summary>
        /// Gets or sets the weight for background monitor signals (default: 0.3).
        /// Range: 0.0 to 1.0
        /// </summary>
        double BackgroundWeight { get; set; }

        /// <summary>
        /// Gets or sets the weight for pattern-based signals (default: 0.2).
        /// Range: 0.0 to 1.0
        /// </summary>
        double PatternWeight { get; set; }

        /// <summary>
        /// Gets or sets the weight for active session signals (default: 0.4).
        /// Range: 0.0 to 1.0
        /// </summary>
        double SessionWeight { get; set; }

        /// <summary>
        /// Gets or sets the minimum confidence threshold for a signal to be considered valid.
        /// Signals below this threshold are excluded from voting (default: 0.3).
        /// </summary>
        double MinimumConfidenceThreshold { get; set; }

        /// <summary>
        /// Gets or sets the maximum age in seconds for a signal to be considered relevant.
        /// Older signals receive reduced confidence (default: 30 seconds).
        /// </summary>
        int MaxSignalAgeSeconds { get; set; }

        // ========================================
        // Statistics
        // ========================================

        /// <summary>
        /// Gets the total number of detections performed.
        /// </summary>
        int TotalDetections { get; }

        /// <summary>
        /// Gets the number of detections where multiple sources agreed.
        /// </summary>
        int ConsensusDetections { get; }

        /// <summary>
        /// Gets the average detection time in milliseconds.
        /// </summary>
        double AverageDetectionTimeMs { get; }

        /// <summary>
        /// Resets all statistics.
        /// </summary>
        void ResetStatistics();
    }
}
