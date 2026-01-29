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
    /// 
    /// Includes Session Priority Boost to maintain batch download consistency when
    /// users switch away from Telegram during large file downloads.
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
        private int _sessionBoostCount;

        // Default weights (configurable)
        public double ForegroundWeight { get; set; } = 0.5;
        public double BackgroundWeight { get; set; } = 0.3;
        public double PatternWeight { get; set; } = 0.2;
        public double SessionWeight { get; set; } = 0.4;
        public double MinimumConfidenceThreshold { get; set; } = 0.3;
        public int MaxSignalAgeSeconds { get; set; } = 30;

        // Session Priority Boost configuration
        public bool UseSessionPriorityBoost { get; set; } = true;
        public double ForegroundWeakThreshold { get; set; } = 0.3;
        public double SessionBoostMultiplier { get; set; } = 2.0;
        public double OtherSignalsReductionMultiplier { get; set; } = 0.5;

        // Statistics properties
        public int TotalDetections => _totalDetections;
        public int ConsensusDetections => _consensusDetections;
        public double AverageDetectionTimeMs => _totalDetections > 0 ? _totalDetectionTimeMs / _totalDetections : 0;
        public int SessionBoostCount => _sessionBoostCount;

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
                    // Perform weighted voting (with session priority boost if applicable)
                    var (detectedContext, score, breakdown, boostApplied, boostReason) = PerformWeightedVoting(validSignals);
                    
                    result.DetectedContext = detectedContext;
                    result.WinningScore = score;
                    result.SignalBreakdown = breakdown;
                    result.SessionBoostApplied = boostApplied;
                    result.SessionBoostReason = boostReason;
                    
                    // Calculate overall confidence
                    result.OverallConfidence = CalculateOverallConfidence(validSignals, detectedContext);
                    
                    // Track session boost usage
                    if (boostApplied)
                    {
                        lock (_lock)
                        {
                            _sessionBoostCount++;
                        }
                    }
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
                    foregroundSignal.OriginalWeight = foregroundSignal.Weight;
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
                    backgroundSignal.OriginalWeight = backgroundSignal.Weight;
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
                    sessionSignal.OriginalWeight = sessionSignal.Weight;
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
                    patternSignal.OriginalWeight = patternSignal.Weight;
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
                _logger.LogDebug("[MultiSource] Session: No active session found");
                return null;
            }

            // Apply age penalty to session
            var sessionAge = (DateTime.Now - session.LastActivity).TotalSeconds;
            var agePenalty = Math.Max(0, 1 - (sessionAge / (session.TimeoutSeconds * 2)));
            var adjustedConfidence = session.ConfidenceScore * agePenalty;

            _logger.LogInfo($"[MultiSource] Session #{session.Id} analysis:");
            _logger.LogInfo($"[MultiSource]   Group: '{session.GroupName}'");
            _logger.LogInfo($"[MultiSource]   Files: {session.FileCount}");
            _logger.LogInfo($"[MultiSource]   Age: {sessionAge:F1}s (timeout: {session.TimeoutSeconds}s)");
            _logger.LogInfo($"[MultiSource]   Base Confidence: {session.ConfidenceScore:F2}");
            _logger.LogInfo($"[MultiSource]   Age Penalty: {agePenalty:F2} (formula: 1 - {sessionAge:F1}/{session.TimeoutSeconds * 2})");
            _logger.LogInfo($"[MultiSource]   Adjusted Confidence: {adjustedConfidence:F2}");
            _logger.LogInfo($"[MultiSource]   Min Threshold: {MinimumConfidenceThreshold:F2}");

            if (adjustedConfidence < MinimumConfidenceThreshold)
            {
                _logger.LogWarning($"[MultiSource] Session #{session.Id} FILTERED OUT: confidence {adjustedConfidence:F2} < threshold {MinimumConfidenceThreshold:F2}");
                return null;
            }

            _logger.LogInfo($"[MultiSource] Session #{session.Id} ACCEPTED with confidence {adjustedConfidence:F2}");

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

        private (string context, double score, Dictionary<string, double> breakdown, bool boostApplied, string? boostReason) 
            PerformWeightedVoting(List<ContextSignal> signals)
        {
            bool boostApplied = false;
            string? boostReason = null;

            // Check if session priority boost should be applied
            if (UseSessionPriorityBoost)
            {
                var sessionSignal = signals.FirstOrDefault(s => s.Source == "Session" && s.IsValid());
                var foregroundSignal = signals.FirstOrDefault(s => s.Source == "Foreground");

                if (sessionSignal != null)
                {
                    bool shouldBoost = false;
                    
                    // Scenario 1: Foreground is weak or missing (user switched to non-Telegram app)
                    // In this case, boost the session to maintain batch consistency
                    double foregroundPower = foregroundSignal?.GetVotingPower() ?? 0;
                    bool foregroundWeak = foregroundPower < ForegroundWeakThreshold;

                    if (foregroundWeak)
                    {
                        // User is NOT in Telegram - apply boost to maintain batch
                        shouldBoost = true;
                        boostReason = foregroundSignal == null 
                            ? "Foreground missing (user switched apps) - maintaining batch consistency" 
                            : $"Foreground weak (power: {foregroundPower:F2} < threshold: {ForegroundWeakThreshold:F2}) - maintaining batch consistency";
                        
                        _logger.LogInfo($"[MultiSource] Session Priority Boost: User switched away from Telegram during batch");
                    }
                    // Scenario 2: Foreground is strong Telegram but DIFFERENT group than session
                    // This is the CRITICAL decision point:
                    // - If user is in a different Telegram group, they likely started a NEW batch
                    // - We should NOT boost - let the foreground (new group) win
                    // - The session manager will handle ending the old session and starting a new one
                    else if (foregroundSignal != null && 
                             !string.IsNullOrEmpty(foregroundSignal.DetectedContext) &&
                             !foregroundSignal.DetectedContext.Equals(sessionSignal.DetectedContext, 
                                                                      StringComparison.OrdinalIgnoreCase))
                    {
                        // User is in Telegram but looking at a DIFFERENT group
                        // This indicates a NEW BATCH, not a continuation of the old batch
                        // DO NOT BOOST - let the foreground win
                        
                        _logger.LogInfo($"[MultiSource] Session Priority Boost: Group mismatch detected");
                        _logger.LogInfo($"[MultiSource]   Session: '{sessionSignal.DetectedContext}'");
                        _logger.LogInfo($"[MultiSource]   Foreground: '{foregroundSignal.DetectedContext}'");
                        _logger.LogInfo($"[MultiSource]   Decision: NO BOOST - User is actively viewing different group (new batch)");
                        _logger.LogWarning($"[MultiSource] User switched from '{sessionSignal.DetectedContext}' " +
                                         $"to '{foregroundSignal.DetectedContext}' in Telegram - " +
                                         $"treating as NEW batch start (not continuing old batch)");
                        
                        // Do NOT boost - let the new foreground group win
                        shouldBoost = false;
                    }

                    if (shouldBoost)
                    {
                        _logger.LogInfo($"[MultiSource] Session Priority Boost ACTIVATED - {boostReason}");
                        _logger.LogInfo($"[MultiSource] Active session: '{sessionSignal.DetectedContext}' " +
                                      $"(original weight: {sessionSignal.Weight:F2} -> boosted: {sessionSignal.Weight * SessionBoostMultiplier:F2})");

                        // Boost session signal
                        sessionSignal.Weight *= SessionBoostMultiplier;
                        sessionSignal.WasBoosted = true;

                        // Reduce other signals to prevent interference
                        foreach (var signal in signals.Where(s => s.Source != "Session"))
                        {
                            var originalWeight = signal.Weight;
                            signal.Weight *= OtherSignalsReductionMultiplier;
                            _logger.LogDebug($"[MultiSource] Reduced {signal.Source} weight: {originalWeight:F2} -> {signal.Weight:F2}");
                        }

                        _logger.LogInfo($"[MultiSource] Boost applied - Session now dominant for batch consistency");
                        boostApplied = true;
                    }
                }
            }

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
                return ("Unsorted", 0, new Dictionary<string, double>(), boostApplied, boostReason);
            }

            var winner = votes.First();
            
            // Build breakdown showing all sources (not just winner)
            var breakdown = signals.ToDictionary(s => s.Source, s => s.GetVotingPower());

            _logger.LogDebug($"[MultiSource] Voting results: {string.Join(", ", votes.Select(v => $"{v.Context}={v.TotalScore:F2}"))}");

            return (winner.Context, winner.TotalScore, breakdown, boostApplied, boostReason);
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
                _sessionBoostCount = 0;
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

            // Check process name first - most reliable
            if (processName.Equals("Telegram", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("Telegram.exe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check window title - look for " - Telegram" suffix which is the standard Telegram format
            // This avoids false positives like "TelegramOrganizer - Visual Studio"
            if (windowTitle.EndsWith(" - Telegram", StringComparison.OrdinalIgnoreCase) ||
                windowTitle.EndsWith(" – Telegram", StringComparison.OrdinalIgnoreCase)) // em-dash
            {
                return true;
            }

            // Also check for just "Telegram" as the title (main window with no chat open)
            if (windowTitle.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
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
