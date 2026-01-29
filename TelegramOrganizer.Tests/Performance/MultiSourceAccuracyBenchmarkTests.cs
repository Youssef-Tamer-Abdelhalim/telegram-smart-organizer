using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;
using Xunit;
using Xunit.Abstractions;

namespace TelegramOrganizer.Tests.Performance
{
    /// <summary>
    /// Accuracy benchmark tests for multi-source context detection.
    /// Tests batch download scenarios and measures detection accuracy.
    /// </summary>
    public class MultiSourceAccuracyBenchmarkTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IContextDetector> _mockForeground;
        private readonly Mock<IBackgroundWindowMonitor> _mockBackground;
        private readonly Mock<IDatabaseService> _mockDatabase;
        private readonly Mock<IDownloadSessionManager> _mockSessionManager;
        private readonly Mock<ILoggingService> _mockLogger;

        public MultiSourceAccuracyBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
            _mockForeground = new Mock<IContextDetector>();
            _mockBackground = new Mock<IBackgroundWindowMonitor>();
            _mockDatabase = new Mock<IDatabaseService>();
            _mockSessionManager = new Mock<IDownloadSessionManager>();
            _mockLogger = new Mock<ILoggingService>();
            
            _mockBackground.Setup(b => b.IsMonitoring).Returns(true);
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
        }

        private MultiSourceContextDetector CreateDetector()
        {
            return new MultiSourceContextDetector(
                _mockForeground.Object,
                _mockBackground.Object,
                _mockDatabase.Object,
                _mockSessionManager.Object,
                _mockLogger.Object
            );
        }

        /// <summary>
        /// Benchmark: 50-file batch download with consistent foreground.
        /// Target: 95%+ accuracy when foreground is stable.
        /// </summary>
        [Fact]
        public async Task Accuracy_BatchDownload_50Files_StableForeground()
        {
            // Arrange
            const int fileCount = 50;
            const string expectedGroup = "Study Group";
            int correctCount = 0;

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{expectedGroup} - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((expectedGroup, 0.8));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = expectedGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            for (int i = 0; i < fileCount; i++)
            {
                var result = await detector.DetectContextAsync($"file_{i:D3}.pdf", DateTime.Now);
                if (result == expectedGroup)
                    correctCount++;
            }

            var accuracy = (double)correctCount / fileCount * 100;

            // Assert
            _output.WriteLine($"Batch Download (50 files, stable foreground):");
            _output.WriteLine($"  Correct: {correctCount}/{fileCount}");
            _output.WriteLine($"  Accuracy: {accuracy:F1}%");
            _output.WriteLine($"  Target: >= 95%");
            
            Assert.True(accuracy >= 95, $"Accuracy {accuracy:F1}% is below target 95%");
        }

        /// <summary>
        /// Benchmark: 50-file batch download with changing foreground.
        /// Simulates user switching apps during batch download.
        /// Target: 90%+ accuracy using background monitor and session.
        /// </summary>
        [Fact]
        public async Task Accuracy_BatchDownload_50Files_ChangingForeground()
        {
            // Arrange
            const int fileCount = 50;
            const string expectedGroup = "Download Group";
            int correctCount = 0;
            int foregroundChangeIndex = 20; // User switches app after 20 files

            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((expectedGroup, 0.85));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = expectedGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = expectedGroup,
                    ConfidenceScore = 0.7,
                    TimesSeen = 10,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();

            // Act
            for (int i = 0; i < fileCount; i++)
            {
                // Simulate foreground changing after 20 files
                if (i < foregroundChangeIndex)
                {
                    _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{expectedGroup} - Telegram");
                }
                else
                {
                    _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Chrome - Google");
                    _mockForeground.Setup(f => f.GetProcessName()).Returns("chrome");
                }

                var result = await detector.DetectContextAsync($"file_{i:D3}.pdf", DateTime.Now);
                if (result == expectedGroup)
                    correctCount++;
            }

            var accuracy = (double)correctCount / fileCount * 100;

            // Assert
            _output.WriteLine($"Batch Download (50 files, foreground changes at file 20):");
            _output.WriteLine($"  Correct: {correctCount}/{fileCount}");
            _output.WriteLine($"  Accuracy: {accuracy:F1}%");
            _output.WriteLine($"  Target: >= 90%");
            
            Assert.True(accuracy >= 90, $"Accuracy {accuracy:F1}% is below target 90%");
        }

        /// <summary>
        /// Benchmark: 100-file batch download with all sources agreeing.
        /// Target: 100% accuracy when all sources agree.
        /// </summary>
        [Fact]
        public async Task Accuracy_BatchDownload_100Files_AllSourcesAgree()
        {
            // Arrange
            const int fileCount = 100;
            const string expectedGroup = "Consensus Group";
            int correctCount = 0;

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{expectedGroup} - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((expectedGroup, 0.9));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = expectedGroup,
                ConfidenceScore = 0.95,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = expectedGroup,
                    ConfidenceScore = 0.8,
                    TimesSeen = 50,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();

            // Act
            for (int i = 0; i < fileCount; i++)
            {
                var result = await detector.DetectContextAsync($"file_{i:D3}.pdf", DateTime.Now);
                if (result == expectedGroup)
                    correctCount++;
            }

            var accuracy = (double)correctCount / fileCount * 100;

            // Assert
            _output.WriteLine($"Batch Download (100 files, all sources agree):");
            _output.WriteLine($"  Correct: {correctCount}/{fileCount}");
            _output.WriteLine($"  Accuracy: {accuracy:F1}%");
            _output.WriteLine($"  Target: 100%");
            
            Assert.Equal(100, accuracy);
        }

        /// <summary>
        /// Benchmark: Mixed scenario with conflicting sources.
        /// Tests weighted voting when sources disagree.
        /// Target: Correct detection based on highest weighted vote.
        /// </summary>
        [Fact]
        public async Task Accuracy_MixedScenario_ConflictingSources()
        {
            // Arrange - Foreground vs Background + Session conflict
            const string foregroundGroup = "Foreground Group";
            const string backgroundGroup = "Background Group";
            
            // Background + Session (combined weight: 0.3 + 0.4 = 0.7) > Foreground (0.5)
            // But confidence also matters

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{foregroundGroup} - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((backgroundGroup, 0.9));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = backgroundGroup,
                ConfidenceScore = 0.95,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Conflicting Sources Test:");
            _output.WriteLine($"  Foreground: {foregroundGroup} (weight: 0.5, confidence: 0.95)");
            _output.WriteLine($"  Background: {backgroundGroup} (weight: 0.3, confidence: 0.9)");
            _output.WriteLine($"  Session: {backgroundGroup} (weight: 0.4, confidence: 0.95)");
            _output.WriteLine($"  Winner: {result.DetectedContext}");
            _output.WriteLine($"  Score: {result.WinningScore:F3}");
            _output.WriteLine($"  Has Consensus: {result.HasConsensus}");
            
            // Background + Session should win due to combined weight
            Assert.Equal(backgroundGroup, result.DetectedContext);
            Assert.True(result.HasConsensus);
        }

        /// <summary>
        /// Benchmark: Detection performance under load.
        /// Target: < 100ms per detection even with all sources.
        /// </summary>
        [Fact]
        public async Task Performance_Detection_Under100ms()
        {
            // Arrange
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Test Group - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Test Group", 0.8));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Test Group",
                    ConfidenceScore = 0.7,
                    TimesSeen = 10,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();
            var times = new List<double>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var result = await detector.DetectContextWithDetailsAsync($"file_{i}.pdf", DateTime.Now);
                times.Add(result.DetectionTimeMs);
            }

            var avgTime = times.Average();
            var maxTime = times.Max();
            var p95Time = times.OrderBy(t => t).ElementAt(94); // 95th percentile

            // Assert
            _output.WriteLine($"Detection Performance (100 iterations):");
            _output.WriteLine($"  Average: {avgTime:F2}ms");
            _output.WriteLine($"  Max: {maxTime:F2}ms");
            _output.WriteLine($"  P95: {p95Time:F2}ms");
            _output.WriteLine($"  Target: < 100ms");
            
            Assert.True(avgTime < 100, $"Average detection time {avgTime:F2}ms exceeds 100ms");
            Assert.True(p95Time < 100, $"P95 detection time {p95Time:F2}ms exceeds 100ms");
        }

        /// <summary>
        /// Benchmark: Confidence scoring accuracy.
        /// Tests that confidence is higher when sources agree.
        /// </summary>
        [Fact]
        public async Task Accuracy_ConfidenceScoring_HigherWithConsensus()
        {
            // Arrange - Test with consensus
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Agreed Group - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Agreed Group", 0.9));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Agreed Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act - Get confidence with consensus
            var consensusResult = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);
            var consensusConfidence = consensusResult.OverallConfidence;

            // Change to single source only
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync((DownloadSession?)null);

            var singleResult = await detector.DetectContextWithDetailsAsync("test2.pdf", DateTime.Now);
            var singleConfidence = singleResult.OverallConfidence;

            // Assert
            _output.WriteLine($"Confidence Scoring Test:");
            _output.WriteLine($"  With Consensus (3 sources): {consensusConfidence:F2}");
            _output.WriteLine($"  Single Source: {singleConfidence:F2}");
            _output.WriteLine($"  Consensus should be higher");
            
            Assert.True(consensusConfidence > singleConfidence, 
                $"Consensus confidence ({consensusConfidence:F2}) should be higher than single ({singleConfidence:F2})");
            Assert.True(consensusResult.HasConsensus);
            Assert.False(singleResult.HasConsensus);
        }

        /// <summary>
        /// Benchmark: Session timeout scenario.
        /// Tests detection accuracy when session is old.
        /// Note: With group mismatch detection, if foreground and session differ,
        /// the session boost applies. This test verifies the age penalty by using
        /// matching groups.
        /// </summary>
        [Fact]
        public async Task Accuracy_OldSession_ReducedWeight()
        {
            // Arrange - Old session (25 seconds old, 30 second timeout)
            // Use SAME group name to test age penalty without triggering group mismatch boost
            const string sameGroup = "Same Group";
            
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{sameGroup} - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = sameGroup, // Same as foreground - tests pure age penalty
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now.AddSeconds(-25), // 25 seconds old
                TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Old Session Test:");
            _output.WriteLine($"  Foreground: {sameGroup}");
            _output.WriteLine($"  Session (25s old): {sameGroup}");
            _output.WriteLine($"  Winner: {result.DetectedContext}");
            _output.WriteLine($"  Session Boost Applied: {result.SessionBoostApplied}");
            
            // Both point to same group, so result should be that group
            Assert.Equal(sameGroup, result.DetectedContext);
            // No boost should be applied since groups match
            Assert.False(result.SessionBoostApplied);
            
            // Verify session has age-reduced confidence
            var sessionSignal = result.Signals.Find(s => s.Source == "Session");
            Assert.NotNull(sessionSignal);
            _output.WriteLine($"  Session Confidence: {sessionSignal!.Confidence:F2} (should be < 0.9 due to age)");
            Assert.True(sessionSignal.Confidence < 0.9, "Session confidence should be reduced due to 25s age");
        }

        /// <summary>
        /// Benchmark: Pattern learning contribution.
        /// Tests that patterns with more observations get higher confidence.
        /// </summary>
        [Fact]
        public async Task Accuracy_PatternLearning_HighObservationBonus()
        {
            // Arrange - Pattern only (no other sources)
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Chrome - Google");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("chrome");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync((DownloadSession?)null);
            
            // High observation pattern
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Learned Group",
                    ConfidenceScore = 0.6,
                    TimesSeen = 100, // High observation count
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("document.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Pattern Learning Test:");
            _output.WriteLine($"  Pattern: Learned Group (confidence: 0.6, seen: 100x)");
            _output.WriteLine($"  Detected: {result.DetectedContext}");
            _output.WriteLine($"  Overall Confidence: {result.OverallConfidence:F2}");
            
            Assert.Equal("Learned Group", result.DetectedContext);
            Assert.True(result.OverallConfidence > 0.6, "Confidence should get bonus for high observations");
        }

        /// <summary>
        /// Summary benchmark test that reports overall accuracy metrics.
        /// </summary>
        [Fact]
        public async Task Summary_OverallAccuracyMetrics()
        {
            var metrics = new Dictionary<string, (int correct, int total, double accuracy)>();
            
            // Test 1: Stable foreground
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Group A - Telegram");
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Group A", 0.8));
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1, GroupName = "Group A", ConfidenceScore = 0.9, LastActivity = DateTime.Now, TimeoutSeconds = 30
            });
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();
            int correct = 0;
            for (int i = 0; i < 50; i++)
            {
                var result = await detector.DetectContextAsync($"file_{i}.pdf", DateTime.Now);
                if (result == "Group A") correct++;
            }
            metrics["Stable Foreground"] = (correct, 50, correct / 50.0 * 100);

            // Reset
            detector.ResetStatistics();

            // Test 2: No foreground
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Chrome");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("chrome");
            
            correct = 0;
            for (int i = 0; i < 50; i++)
            {
                var result = await detector.DetectContextAsync($"file_{i}.pdf", DateTime.Now);
                if (result == "Group A") correct++;
            }
            metrics["No Foreground (Background+Session)"] = (correct, 50, correct / 50.0 * 100);

            // Output summary
            _output.WriteLine("=== Multi-Source Detection Accuracy Summary ===");
            _output.WriteLine("");
            foreach (var metric in metrics)
            {
                _output.WriteLine($"{metric.Key}:");
                _output.WriteLine($"  {metric.Value.correct}/{metric.Value.total} = {metric.Value.accuracy:F1}%");
            }
            _output.WriteLine("");
            _output.WriteLine($"Overall Average: {metrics.Values.Average(m => m.accuracy):F1}%");
            _output.WriteLine($"Target: >= 90%");

            var overallAccuracy = metrics.Values.Average(m => m.accuracy);
            Assert.True(overallAccuracy >= 90, $"Overall accuracy {overallAccuracy:F1}% below target 90%");
        }
    }
}
