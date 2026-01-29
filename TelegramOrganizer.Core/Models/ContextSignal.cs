using System;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Represents a context signal from a specific detection source.
    /// Used by the multi-source context detector to combine signals via weighted voting.
    /// </summary>
    public class ContextSignal
    {
        /// <summary>
        /// Source of this signal (e.g., "Foreground", "Background", "Pattern", "Session")
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// The detected context/group name from this source
        /// </summary>
        public string DetectedContext { get; set; } = string.Empty;

        /// <summary>
        /// Weight of this signal source in voting (0.0 - 1.0)
        /// Higher weight means more influence on final decision
        /// </summary>
        public double Weight { get; set; }

        /// <summary>
        /// Original weight before any boost was applied
        /// </summary>
        public double OriginalWeight { get; set; }

        /// <summary>
        /// Confidence in this detection (0.0 - 1.0)
        /// Combined with weight for final voting score
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// When this signal was captured
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// Additional metadata about the signal (optional)
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Whether this signal was boosted (session priority boost)
        /// </summary>
        public bool WasBoosted { get; set; }

        /// <summary>
        /// Calculates the effective voting power of this signal.
        /// </summary>
        public double GetVotingPower()
        {
            return Weight * Confidence;
        }

        /// <summary>
        /// Checks if this signal is valid (has context and positive voting power).
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(DetectedContext) &&
                   DetectedContext != "Unsorted" &&
                   Weight > 0 &&
                   Confidence > 0;
        }

        /// <summary>
        /// Gets the age of this signal in seconds.
        /// </summary>
        public double GetAgeInSeconds()
        {
            return (DateTime.Now - Timestamp).TotalSeconds;
        }

        public override string ToString()
        {
            var boostIndicator = WasBoosted ? " [BOOSTED]" : "";
            return $"[{Source}] {DetectedContext} (weight: {Weight:F2}, confidence: {Confidence:F2}, power: {GetVotingPower():F2}){boostIndicator}";
        }
    }

    /// <summary>
    /// Result of multi-source context detection including the winning context and vote breakdown.
    /// </summary>
    public class MultiSourceDetectionResult
    {
        /// <summary>
        /// The detected context (group name) with highest weighted vote
        /// </summary>
        public string DetectedContext { get; set; } = "Unsorted";

        /// <summary>
        /// Overall confidence in the detection (0.0 - 1.0)
        /// </summary>
        public double OverallConfidence { get; set; }

        /// <summary>
        /// All signals that contributed to this detection
        /// </summary>
        public List<ContextSignal> Signals { get; set; } = new();

        /// <summary>
        /// Breakdown of signal sources and their contributions
        /// Key: Source name, Value: Voting power contributed
        /// </summary>
        public Dictionary<string, double> SignalBreakdown { get; set; } = new();

        /// <summary>
        /// The winning context's total vote score
        /// </summary>
        public double WinningScore { get; set; }

        /// <summary>
        /// Time taken for detection in milliseconds
        /// </summary>
        public double DetectionTimeMs { get; set; }

        /// <summary>
        /// Whether session priority boost was applied for this detection.
        /// This happens when an active session exists but foreground is weak/missing.
        /// </summary>
        public bool SessionBoostApplied { get; set; }

        /// <summary>
        /// Reason why session boost was applied (for logging/debugging)
        /// </summary>
        public string? SessionBoostReason { get; set; }

        /// <summary>
        /// Whether multiple sources agreed on the context
        /// </summary>
        public bool HasConsensus => Signals.Count(s => s.DetectedContext == DetectedContext) > 1;

        /// <summary>
        /// Number of sources that contributed valid signals
        /// </summary>
        public int ValidSignalCount => Signals.Count(s => s.IsValid());

        public override string ToString()
        {
            var boostInfo = SessionBoostApplied ? " [SESSION BOOSTED]" : "";
            return $"Detected: '{DetectedContext}' (confidence: {OverallConfidence:F2}, " +
                   $"score: {WinningScore:F2}, signals: {ValidSignalCount}, " +
                   $"consensus: {(HasConsensus ? "Yes" : "No")}){boostInfo}";
        }
    }
}
