using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// Multi-source context detector that combines signals from foreground window,
    /// background monitor, pattern learning, and active sessions using weighted voting.
    /// </summary>
    public class MultiSourceContextDetector : IMultiSourceContextDetector
    {
        private readonly IContextDetector _foregroundDetector;
        private readonly IBackgroundWindowMonitor _backgroundMonitor;
        private readonly IDatabaseService _database;
        private readonly IDownloadSessionManager _sessionManager;
        private readonly ILoggingService _logger;

        // Last detection results (for GetLast* methods)
        private MultiSourceDetectionResult? _lastResult;
        private readonly object _lock = new();

        // Statistics
        private int _totalDetections;
        private int _consensusDetections;
        private double _totalDetectionTimeMs;

        // Default weights (configurable)
        public double ForegroundWeight { get; set; } = 0.5;
        public double BackgroundWeight { get; set; } = 0.3;
        public double PatternWeight { get; set; } = 0.2;
        public double SessionWeight { get; set; } = 0.4;
        public double MinimumConfidenceThreshold { get; set; } = 0.3;
        public int MaxSignalAgeSeconds { get; set; } = 30;

        // Statistics properties
        public int TotalDetections => _totalDetections;
        public int ConsensusDetections => _consensusDetections;
        public double AverageDetectionTimeMs => _totalDetections > 0 ? _totalDetectionTimeMs / _totalDetections : 0;

        public MultiSourceContextDetector(
            IContextDetector foregroundDetector,
            IBackgroundWindowMonitor backgroundMonitor,
            IDatabaseService database,
            IDownloadSessionManager sessionManager,
            ILoggingService logger)
        {
            _foregroundDetector = foregroundDetector ?? throw new ArgumentNullException(nameof(foregroundDetector));
            _backgroundMonitor = backgroundMonitor ?? throw new ArgumentNullException(nameof(backgroundMonitor));
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> DetectContextAsync(string fileName, DateTime downloadTime)
        {
            var result = await DetectContextWithDetailsAsync(fileName, downloadTime);
            return result.DetectedContext;
        }

        public async Task<MultiSourceDetectionResult> DetectContextWithDetailsAsync(string fileName, DateTime downloadTime)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new MultiSourceDetectionResult();

            try
            {
                // Collect signals from all sources
                var signals = await CollectAllSignalsAsync(fileName, downloadTime);
                result.Signals = signals;

                // Filter valid signals
                var validSignals = signals.Where(s => s.IsValid() && s.Confidence >= MinimumConfidenceThreshold).ToList();

                if (validSignals.Count == 0)
                {
                    _logger.LogDebug("[MultiSource] No valid signals found, using 'Unsorted'");
                    result.DetectedContext = "Unsorted";
                    result.OverallConfidence = 0;
                }
                else
                {
                    // Perform weighted voting
                    var (detectedContext, score, breakdown) = PerformWeightedVoting(validSignals);
                    
                    result.DetectedContext = detectedContext;
                    result.WinningScore = score;
                    result.SignalBreakdown = breakdown;
                    
                    // Calculate overall confidence
                    result.OverallConfidence = CalculateOverallConfidence(validSignals, detectedContext);
                }

                stopwatch.Stop();
                result.DetectionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                // Update statistics
                lock (_lock)
                {
                    _lastResult = result;
                    _totalDetections++;
                    _totalDetectionTimeMs += result.DetectionTimeMs;
                    if (result.HasConsensus)
                        _consensusDetections++;
                }

                _logger.LogInfo($"[MultiSource] {result}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("[MultiSource] Detection failed", ex);
                stopwatch.Stop();
                result.DetectionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                result.DetectedContext = "Unsorted";
                return result;
            }
        }

        public async Task<List<ContextSignal>> CollectAllSignalsAsync(string fileName, DateTime downloadTime)
        {
            var signals = new List<ContextSignal>();

            // Signal 1: Foreground Window
            try
            {
                var foregroundSignal = CollectForegroundSignal();
                if (foregroundSignal != null)
                {
                    signals.Add(foregroundSignal);
                    _logger.LogDebug($"[MultiSource] Foreground: {foregroundSignal}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[MultiSource] Foreground signal error: {ex.Message}");
            }

            // Signal 2: Background Monitor
            try
            {
                var backgroundSignal = CollectBackgroundSignal();
                if (backgroundSignal != null)
                {
                    signals.Add(backgroundSignal);
                    _logger.LogDebug($"[MultiSource] Background: {backgroundSignal}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[MultiSource] Background signal error: {ex.Message}");
            }

            // Signal 3: Active Session
            try
            {
                var sessionSignal = await CollectSessionSignalAsync();
                if (sessionSignal != null)
                {
                    signals.Add(sessionSignal);
                    _logger.LogDebug($"[MultiSource] Session: {sessionSignal}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[MultiSource] Session signal error: {ex.Message}");
            }

            // Signal 4: Pattern Learning
            try
            {
                var patternSignal = await CollectPatternSignalAsync(fileName, downloadTime);
                if (patternSignal != null)
                {
                    signals.Add(patternSignal);
                    _logger.LogDebug($"[MultiSource] Pattern: {patternSignal}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"[MultiSource] Pattern signal error: {ex.Message}");
            }

            return signals;
        }

        private ContextSignal? CollectForegroundSignal()
        {
            var windowTitle = _foregroundDetector.GetActiveWindowTitle();
            var processName = _foregroundDetector.GetProcessName();

            // Only use if it's a Telegram window
            if (!IsTelegramWindow(windowTitle, processName))
            {
                return null;
            }

            var groupName = ExtractTelegramGroupName(windowTitle);
            if (string.IsNullOrWhiteSpace(groupName) || groupName == "Unsorted")
            {
                return null;
            }

            return new ContextSignal
            {
                Source = "Foreground",
                DetectedContext = groupName,
                Weight = ForegroundWeight,
                Confidence = 0.95, // High confidence - user is actively looking at this
                Timestamp = DateTime.Now,
                Metadata = $"Window: {windowTitle}"
            };
        }

        private ContextSignal? CollectBackgroundSignal()
        {
            if (!_backgroundMonitor.IsMonitoring)
            {
                return null;
            }

            var bestGroup = _backgroundMonitor.GetBestRecentGroupName();
            if (bestGroup == null)
            {
                // Try most recent window
                var recentWindow = _backgroundMonitor.GetMostRecentWindow();
                if (recentWindow == null || string.IsNullOrEmpty(recentWindow.ExtractedGroupName))
                {
                    return null;
                }

                // Apply age penalty
                var ageSeconds = recentWindow.GetAgeInSeconds();
                var agePenalty = Math.Max(0, 1 - (ageSeconds / MaxSignalAgeSeconds));
                var adjustedConfidence = recentWindow.ConfidenceScore * agePenalty;

                if (adjustedConfidence < MinimumConfidenceThreshold)
                {
                    return null;
                }

                return new ContextSignal
                {
                    Source = "Background",
                    DetectedContext = recentWindow.ExtractedGroupName,
                    Weight = BackgroundWeight,
                    Confidence = adjustedConfidence,
                    Timestamp = recentWindow.LastSeen,
                    Metadata = $"Age: {ageSeconds:F1}s, Window: {recentWindow.Title}"
                };
            }

            return new ContextSignal
            {
                Source = "Background",
                DetectedContext = bestGroup.Value.groupName,
                Weight = BackgroundWeight,
                Confidence = bestGroup.Value.confidence,
                Timestamp = DateTime.Now,
                Metadata = "Best recent group"
            };
        }

        private async Task<ContextSignal?> CollectSessionSignalAsync()
        {
            var session = await _sessionManager.GetActiveSessionAsync();
            if (session == null)
            {
                return null;
            }

            // Apply age penalty to session
            var sessionAge = (DateTime.Now - session.LastActivity).TotalSeconds;
            var agePenalty = Math.Max(0, 1 - (sessionAge / (session.TimeoutSeconds * 2)));
            var adjustedConfidence = session.ConfidenceScore * agePenalty;

            if (adjustedConfidence < MinimumConfidenceThreshold)
            {
                return null;
            }

            return new ContextSignal
            {
                Source = "Session",
                DetectedContext = session.GroupName,
                Weight = SessionWeight,
                Confidence = adjustedConfidence,
                Timestamp = session.LastActivity,
                Metadata = $"Session #{session.Id}, Files: {session.FileCount}, Age: {sessionAge:F1}s"
            };
        }

        private async Task<ContextSignal?> CollectPatternSignalAsync(string fileName, DateTime downloadTime)
        {
            var extension = Path.GetExtension(fileName);
            var pattern = await _database.GetBestPatternAsync(fileName, extension, downloadTime);

            if (pattern == null || pattern.ConfidenceScore < MinimumConfidenceThreshold)
            {
                return null;
            }

            // Patterns with more observations get slightly higher confidence
            var observationBonus = Math.Min(0.1, pattern.TimesSeen / 100.0);
            var adjustedConfidence = Math.Min(1.0, pattern.ConfidenceScore + observationBonus);

            return new ContextSignal
            {
                Source = "Pattern",
                DetectedContext = pattern.GroupName,
                Weight = PatternWeight,
                Confidence = adjustedConfidence,
                Timestamp = pattern.LastSeen,
                Metadata = $"Pattern: {pattern.GetDescription()}, Seen: {pattern.TimesSeen}x"
            };
        }

        private (string context, double score, Dictionary<string, double> breakdown) PerformWeightedVoting(List<ContextSignal> signals)
        {
            // Group signals by detected context and calculate total weighted score
            var votes = signals
                .GroupBy(s => s.DetectedContext)
                .Select(g => new
                {
                    Context = g.Key,
                    TotalScore = g.Sum(s => s.GetVotingPower()),
                    Contributors = g.ToDictionary(s => s.Source, s => s.GetVotingPower())
                })
                .OrderByDescending(v => v.TotalScore)
                .ToList();

            if (votes.Count == 0)
            {
                return ("Unsorted", 0, new Dictionary<string, double>());
            }

            var winner = votes.First();
            
            // Build breakdown showing all sources (not just winner)
            var breakdown = signals.ToDictionary(s => s.Source, s => s.GetVotingPower());

            _logger.LogDebug($"[MultiSource] Voting results: {string.Join(", ", votes.Select(v => $"{v.Context}={v.TotalScore:F2}"))}");

            return (winner.Context, winner.TotalScore, breakdown);
        }

        private double CalculateOverallConfidence(List<ContextSignal> signals, string winningContext)
        {
            var winningSignals = signals.Where(s => s.DetectedContext == winningContext).ToList();
            if (winningSignals.Count == 0)
            {
                return 0;
            }

            // Base confidence is average of winning signals
            var avgConfidence = winningSignals.Average(s => s.Confidence);
            
            // Bonus for consensus (multiple sources agreeing)
            var consensusBonus = winningSignals.Count > 1 ? 0.1 : 0;
            
            // Penalty for conflicting signals
            var conflictingCount = signals.Count(s => s.DetectedContext != winningContext && s.IsValid());
            var conflictPenalty = conflictingCount * 0.05;

            return Math.Max(0, Math.Min(1.0, avgConfidence + consensusBonus - conflictPenalty));
        }

        public async Task RecordFeedbackAsync(string fileName, string detectedContext, string? actualContext, bool wasCorrect)
        {
            try
            {
                // Save pattern for learning
                var extension = Path.GetExtension(fileName);
                var correctContext = wasCorrect ? detectedContext : (actualContext ?? detectedContext);

                var pattern = new FilePattern
                {
                    FileExtension = extension,
                    GroupName = correctContext,
                    ConfidenceScore = wasCorrect ? 0.6 : 0.4, // Initial confidence
                    TimesSeen = 1,
                    TimesCorrect = wasCorrect ? 1 : 0,
                    FirstSeen = DateTime.Now,
                    LastSeen = DateTime.Now
                };

                await _database.SavePatternAsync(pattern);
                _logger.LogDebug($"[MultiSource] Recorded feedback: {fileName} -> {correctContext} (correct: {wasCorrect})");
            }
            catch (Exception ex)
            {
                _logger.LogError("[MultiSource] Failed to record feedback", ex);
            }
        }

        public double GetLastConfidenceScore()
        {
            lock (_lock)
            {
                return _lastResult?.OverallConfidence ?? 0;
            }
        }

        public Dictionary<string, double> GetLastSignalBreakdown()
        {
            lock (_lock)
            {
                return _lastResult?.SignalBreakdown ?? new Dictionary<string, double>();
            }
        }

        public List<ContextSignal> GetLastSignals()
        {
            lock (_lock)
            {
                return _lastResult?.Signals ?? new List<ContextSignal>();
            }
        }

        public void ResetStatistics()
        {
            lock (_lock)
            {
                _totalDetections = 0;
                _consensusDetections = 0;
                _totalDetectionTimeMs = 0;
                _lastResult = null;
            }
        }

        // ========================================
        // Helper Methods
        // ========================================

        private bool IsTelegramWindow(string windowTitle, string processName)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return false;

            return processName.Contains("Telegram", StringComparison.OrdinalIgnoreCase) ||
                   windowTitle.Contains("Telegram", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the group/channel name from Telegram window title.
        /// Handles Arabic, English, and mixed text properly.
        /// </summary>
        private string ExtractTelegramGroupName(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return "Unsorted";

            string title = windowTitle.Trim();
            
            // Remove unread count at start: "(123) GroupName" -> "GroupName"
            title = Regex.Replace(title, @"^\(\d+\)\s*", "");
            
            // Remove total message count at end: "GroupName – (3082)" -> "GroupName"
            title = Regex.Replace(title, @"\s*[–—-]\s*\(\d+\)$", "");
            
            // Remove any remaining parenthetical numbers at end
            title = Regex.Replace(title, @"\s*\(\d+\)$", "");
            
            // Remove " - Telegram" or " – Telegram" suffix
            title = Regex.Replace(title, @"\s*[–—-]\s*Telegram$", "", RegexOptions.IgnoreCase);
            
            // Remove emojis and symbols but keep Arabic, English, numbers, and common punctuation
            title = Regex.Replace(title, @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFFa-zA-Z0-9\s\-_\.]+", "");
            
            // Clean up whitespace and special chars
            title = Regex.Replace(title, @"\s+", " ").Trim();
            title = title.Trim(' ', '-', '_', '.');

            if (string.IsNullOrWhiteSpace(title) || title.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
                return "Unsorted";

            return title;
        }
    }
}
