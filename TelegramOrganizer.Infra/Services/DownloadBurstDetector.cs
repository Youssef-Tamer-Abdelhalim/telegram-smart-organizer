using System;
using System.Collections.Generic;
using System.Linq;
using TelegramOrganizer.Core.Contracts;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// Detects when multiple files are downloaded in rapid succession (burst).
    /// Uses time-window based detection with configurable thresholds.
    /// </summary>
    public class DownloadBurstDetector : IDownloadBurstDetector
    {
        private readonly ILoggingService _logger;
        private readonly List<DownloadEvent> _recentDownloads = new();
        private readonly object _lock = new object();

        private DateTime? _currentBurstStart;
        private bool _isBurstActive;

        // Events
        public event EventHandler<BurstDetectionResult>? BurstStarted;
        public event EventHandler<BurstDetectionResult>? BurstContinued;
        public event EventHandler<BurstDetectionResult>? BurstEnded;

        // Configuration
        public int BurstThresholdSeconds { get; set; } = 5;
        public int MinimumFilesForBurst { get; set; } = 2;
        public int MaxBurstDurationSeconds { get; set; } = 60;

        public DownloadBurstDetector(ILoggingService logger)
        {
            _logger = logger;
        }

        public bool IsBurstDownload(string fileName, DateTime? detectedTime = null)
        {
            lock (_lock)
            {
                DateTime now = detectedTime ?? DateTime.Now;

                // Clean up old downloads outside the time window
                CleanupOldDownloads(now);

                // Need at least 1 existing download to form a burst with the new file
                if (_recentDownloads.Count >= MinimumFilesForBurst - 1)
                {
                    var lastDownload = _recentDownloads.LastOrDefault();
                    if (lastDownload != null)
                    {
                        double timeSinceLast = (now - lastDownload.Time).TotalSeconds;
                        bool isBurst = timeSinceLast <= BurstThresholdSeconds;
                        
                        _logger.LogDebug($"[BurstDetector] IsBurstDownload: {fileName}, count={_recentDownloads.Count}, timeSinceLast={timeSinceLast:F1}s, isBurst={isBurst}");
                        
                        return isBurst;
                    }
                }

                return false;
            }
        }

        public void RecordDownload(string fileName, DateTime? detectedTime = null)
        {
            lock (_lock)
            {
                DateTime now = detectedTime ?? DateTime.Now;

                // Clean up old downloads
                CleanupOldDownloads(now);

                // Add new download
                _recentDownloads.Add(new DownloadEvent
                {
                    FileName = fileName,
                    Time = now
                });

                _logger.LogDebug($"[BurstDetector] Recorded: {fileName} (total in window: {_recentDownloads.Count})");

                // Check if this starts or continues a burst
                CheckBurstStatus(now);
            }
        }

        public BurstDetectionResult GetCurrentBurstStatus()
        {
            lock (_lock)
            {
                return CreateBurstResult();
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                if (_isBurstActive)
                {
                    EndBurst();
                }

                _recentDownloads.Clear();
                _currentBurstStart = null;
                _isBurstActive = false;

                _logger.LogDebug("[BurstDetector] Reset");
            }
        }

        public int GetCurrentBurstCount()
        {
            lock (_lock)
            {
                return _isBurstActive ? _recentDownloads.Count : 0;
            }
        }

        public double? GetBurstTimeRemaining()
        {
            lock (_lock)
            {
                if (!_isBurstActive || _recentDownloads.Count == 0)
                    return null;

                var lastDownload = _recentDownloads.LastOrDefault();
                if (lastDownload == null)
                    return null;

                double elapsed = (DateTime.Now - lastDownload.Time).TotalSeconds;
                double remaining = BurstThresholdSeconds - elapsed;

                return remaining > 0 ? remaining : 0;
            }
        }

        // ========================================
        // Private Helper Methods
        // ========================================

        private void CleanupOldDownloads(DateTime now)
        {
            // Remove downloads older than burst threshold
            _recentDownloads.RemoveAll(d => (now - d.Time).TotalSeconds > BurstThresholdSeconds);

            // Also remove if burst has exceeded max duration
            if (_currentBurstStart.HasValue)
            {
                double burstDuration = (now - _currentBurstStart.Value).TotalSeconds;
                if (burstDuration > MaxBurstDurationSeconds)
                {
                    _logger.LogInfo($"[BurstDetector] Max duration ({MaxBurstDurationSeconds}s) exceeded, ending burst");
                    EndBurst();
                    _recentDownloads.Clear();
                }
            }
        }

        private void CheckBurstStatus(DateTime now)
        {
            int downloadCount = _recentDownloads.Count;

            if (downloadCount >= MinimumFilesForBurst)
            {
                if (!_isBurstActive)
                {
                    // Start new burst
                    StartBurst(now);
                }
                else
                {
                    // Continue existing burst
                    ContinueBurst();
                }
            }
            else
            {
                // Not enough files for a burst
                if (_isBurstActive)
                {
                    EndBurst();
                }
            }
        }

        private void StartBurst(DateTime now)
        {
            _isBurstActive = true;
            _currentBurstStart = _recentDownloads.FirstOrDefault()?.Time ?? now;

            var result = CreateBurstResult();
            
            _logger.LogInfo($"[BurstDetector] Burst STARTED: {result}");
            
            BurstStarted?.Invoke(this, result);
        }

        private void ContinueBurst()
        {
            var result = CreateBurstResult();
            
            _logger.LogDebug($"[BurstDetector] Burst CONTINUED: {result}");
            
            BurstContinued?.Invoke(this, result);
        }

        private void EndBurst()
        {
            if (!_isBurstActive)
                return;

            var result = CreateBurstResult();
            
            _logger.LogInfo($"[BurstDetector] Burst ENDED: {result}");
            
            _isBurstActive = false;
            _currentBurstStart = null;

            BurstEnded?.Invoke(this, result);
        }

        private BurstDetectionResult CreateBurstResult()
        {
            return new BurstDetectionResult
            {
                IsBurstActive = _isBurstActive,
                FileCount = _recentDownloads.Count,
                BurstStartTime = _currentBurstStart ?? _recentDownloads.FirstOrDefault()?.Time,
                LastFileTime = _recentDownloads.LastOrDefault()?.Time,
                FileNames = _recentDownloads.Select(d => d.FileName).ToList()
            };
        }

        // ========================================
        // Helper Classes
        // ========================================

        private class DownloadEvent
        {
            public string FileName { get; set; } = string.Empty;
            public DateTime Time { get; set; }
        }
    }
}
