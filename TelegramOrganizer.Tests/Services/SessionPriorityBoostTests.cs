using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;
using Xunit;
using Xunit.Abstractions;

namespace TelegramOrganizer.Tests.Services
{
    /// <summary>
    /// Tests for Session Priority Boost feature.
    /// This feature ensures batch downloads maintain consistency when users switch away from Telegram.
    /// </summary>
    public class SessionPriorityBoostTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IContextDetector> _mockForeground;
        private readonly Mock<IBackgroundWindowMonitor> _mockBackground;
        private readonly Mock<IDatabaseService> _mockDatabase;
        private readonly Mock<IDownloadSessionManager> _mockSessionManager;
        private readonly Mock<ILoggingService> _mockLogger;

        public SessionPriorityBoostTests(ITestOutputHelper output)
        {
            _output = output;
            _mockForeground = new Mock<IContextDetector>();
            _mockBackground = new Mock<IBackgroundWindowMonitor>();
            _mockDatabase = new Mock<IDatabaseService>();
            _mockSessionManager = new Mock<IDownloadSessionManager>();
            _mockLogger = new Mock<ILoggingService>();

            _mockBackground.Setup(b => b.IsMonitoring).Returns(true);
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

        // ========================================
        // Test 1: Session boost when foreground missing
        // ========================================

        [Fact]
        public async Task SessionBoost_ForegroundMissing_BoostsSession()
        {
            // Arrange: Active session exists, but user switched to VS Code (no Telegram foreground)
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Program.cs - Visual Studio Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Linear Algebra", 0.7));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Linear Algebra",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30,
                FileCount = 3
            });

            // Pattern points to a different group (old pattern)
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Old Pattern Group",
                    ConfidenceScore = 0.8,
                    TimesSeen = 50,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("file4.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Detected: {result.DetectedContext}");
            _output.WriteLine($"Session Boost Applied: {result.SessionBoostApplied}");
            _output.WriteLine($"Boost Reason: {result.SessionBoostReason}");
            _output.WriteLine($"Winning Score: {result.WinningScore:F2}");

            Assert.Equal("Linear Algebra", result.DetectedContext);
            Assert.True(result.SessionBoostApplied, "Session boost should be applied when foreground is missing");
            Assert.Contains("missing", result.SessionBoostReason, StringComparison.OrdinalIgnoreCase);
        }

        // ========================================
        // Test 2: Session boost when foreground is weak
        // ========================================

        [Fact]
        public async Task SessionBoost_ForegroundWeak_BoostsSession()
        {
            // Arrange: Foreground exists but not Telegram (weak power = 0)
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Google Chrome");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("chrome");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Study Group", 0.7));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Study Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            Assert.Equal("Study Group", result.DetectedContext);
            Assert.True(result.SessionBoostApplied);
        }

        // ========================================
        // Test 3: No boost when foreground is strong
        // ========================================

        [Fact]
        public async Task SessionBoost_ForegroundStrong_NoBoost()
        {
            // Arrange: Telegram is active foreground (strong signal)
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Linear Algebra - Telegram");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Linear Algebra", 0.8));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Linear Algebra",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            Assert.Equal("Linear Algebra", result.DetectedContext);
            Assert.False(result.SessionBoostApplied, "No boost needed when foreground is strong Telegram window");
        }

        // ========================================
        // Test 4: No boost when no active session
        // ========================================

        [Fact]
        public async Task SessionBoost_NoSession_NormalVoting()
        {
            // Arrange: No active session, foreground is not Telegram
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Background Group", 0.8));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync((DownloadSession?)null);

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Pattern Group",
                    ConfidenceScore = 0.7,
                    TimesSeen = 20,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            Assert.False(result.SessionBoostApplied, "No boost when no active session");
            // Background should win over pattern due to higher weight * confidence
            Assert.Equal("Background Group", result.DetectedContext);
        }

        // ========================================
        // Test 5: Batch download maintains consistency (real-world scenario)
        // ========================================

        [Fact]
        public async Task SessionBoost_BatchDownload_MaintainsConsistency()
        {
            // Simulate: 10-file batch download, user switches apps at file 5
            const int totalFiles = 10;
            const int switchAtFile = 5;
            const string expectedGroup = "Linear Algebra - Lecture Eight";
            int correctCount = 0;

            var detector = CreateDetector();

            // Setup session (active throughout)
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 32,
                GroupName = expectedGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30,
                FileCount = 3
            });

            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((expectedGroup, 0.7));
            
            // Pattern points to wrong group
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Project Running",
                    ConfidenceScore = 0.8,
                    TimesSeen = 100,
                    LastSeen = DateTime.Now
                });

            // Act: Download 10 files
            for (int i = 1; i <= totalFiles; i++)
            {
                // Files 1-5: Telegram foreground | Files 6-10: VS Code foreground
                if (i <= switchAtFile)
                {
                    _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{expectedGroup} - Telegram");
                    _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
                }
                else
                {
                    _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Program.cs - Visual Studio Code");
                    _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
                }

                var result = await detector.DetectContextAsync($"Lecture_Notes_V{i}.pdf", DateTime.Now);
                
                _output.WriteLine($"File {i}: Detected '{result}' " +
                    $"(Foreground: {(i <= switchAtFile ? "Telegram" : "VS Code")})");

                if (result == expectedGroup)
                    correctCount++;
            }

            // Assert: ALL files should go to same folder
            _output.WriteLine($"\nCorrect: {correctCount}/{totalFiles}");
            Assert.Equal(totalFiles, correctCount);
        }

        // ========================================
        // Test 6: Boost can be disabled via config
        // ========================================

        [Fact]
        public async Task SessionBoost_DisabledViaConfig_NoBoost()
        {
            // Arrange
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Session Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Pattern Group",
                    ConfidenceScore = 0.9,
                    TimesSeen = 100,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();
            detector.UseSessionPriorityBoost = false; // DISABLE boost

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            Assert.False(result.SessionBoostApplied, "Boost should not be applied when disabled");
            // Without boost, pattern (0.2 * 0.9 = 0.18) vs session (0.4 * 0.9 = 0.36)
            // Session should still win but without the boost flag
        }

        // ========================================
        // Test 7: Custom threshold configuration
        // ========================================

        [Fact]
        public async Task SessionBoost_CustomThreshold_RespectsConfig()
        {
            // Arrange: Set very high threshold so even weak foreground doesn't trigger boost
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Test Group", 0.7));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();
            
            // Test with threshold = 0.0 (foreground power 0 is NOT below 0, so no boost)
            detector.ForegroundWeakThreshold = 0.0;

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert: With threshold 0.0, foreground power (0) is not < 0, so boost won't apply
            // Actually 0 < 0 is false, so no boost
            Assert.False(result.SessionBoostApplied);
        }

        // ========================================
        // Test 8: Custom multiplier configuration
        // ========================================

        [Fact]
        public async Task SessionBoost_CustomMultiplier_RespectsConfig()
        {
            // Arrange
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Session Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();
            detector.SessionBoostMultiplier = 3.0; // Triple boost instead of double

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            Assert.True(result.SessionBoostApplied);
            // Session weight should be 0.4 * 3.0 = 1.2
            var sessionSignal = result.Signals.Find(s => s.Source == "Session");
            Assert.NotNull(sessionSignal);
            Assert.Equal(1.2, sessionSignal!.Weight, 2);
            Assert.True(sessionSignal.WasBoosted);
        }

        // ========================================
        // Test 9: Real-world scenario - 5 large files
        // ========================================

        [Fact]
        public async Task SessionBoost_RealWorldScenario_5LargeFiles()
        {
            // Exact reproduction of the reported bug:
            // - User downloads 5 large PDFs from "Linear Algebra - Lecture Eight"
            // - Files 1-3 complete while Telegram is foreground
            // - User switches to VS Code
            // - Files 4-5 complete while VS Code is foreground
            // - EXPECTED: All 5 files go to "Linear Algebra - Lecture Eight"

            const string expectedGroup = "Linear Algebra - Lecture Eight";
            var results = new List<(string fileName, string detectedGroup, bool boostApplied)>();

            var detector = CreateDetector();

            // Setup: Active session for the batch
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 32,
                GroupName = expectedGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30,
                FileCount = 3
            });

            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((expectedGroup, 0.7));
            
            // Pattern points to wrong group
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Project Running",
                    ConfidenceScore = 0.8,
                    TimesSeen = 100,
                    LastSeen = DateTime.Now
                });

            // Act: Download 10 files
            for (int i = 1; i <= 5; i++)
            {
                // Files 1-3: Telegram foreground | Files 4-5: VS Code foreground
                if (i <= 3)
                {
                    _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{expectedGroup} - Telegram");
                    _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
                }
                else
                {
                    _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Program.cs - Visual Studio Code");
                    _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
                }

                var result = await detector.DetectContextWithDetailsAsync($"Lecture_Notes_V{i}.pdf", DateTime.Now);
                results.Add(($"Lecture_Notes_V{i}.pdf", result.DetectedContext, result.SessionBoostApplied));
            }

            // Assert: ALL files should go to same folder
            foreach (var (fileName, detectedGroup, boostApplied) in results)
            {
                _output.WriteLine($"{fileName} -> {detectedGroup} (Boost: {boostApplied})");
            }

            // Assert: ALL files should go to same folder
            Assert.All(results, r => Assert.Equal(expectedGroup, r.detectedGroup));
        }

        // ========================================
        // Test 10: Statistics tracking
        // ========================================

        [Fact]
        public async Task SessionBoost_Statistics_TracksBoostCount()
        {
            // Arrange
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Test Group", 0.7));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();
            detector.ResetStatistics();

            // Act: Perform 5 detections that trigger boost
            for (int i = 0; i < 5; i++)
            {
                await detector.DetectContextAsync($"file{i}.pdf", DateTime.Now);
            }

            // Assert
            Assert.Equal(5, detector.SessionBoostCount);
            Assert.Equal(5, detector.TotalDetections);
        }

        // ========================================
        // Test 11: Boost result includes reason
        // ========================================

        [Fact]
        public async Task SessionBoost_Logging_ShowsBoostDecision()
        {
            // Arrange
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Notepad");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("notepad");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Test Group", 0.7));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            Assert.True(result.SessionBoostApplied);
            Assert.NotNull(result.SessionBoostReason);
            Assert.NotEmpty(result.SessionBoostReason!);
            
            _output.WriteLine($"Boost Reason: {result.SessionBoostReason}");
        }

        // ================================================
        // Test 12: Session beats strong pattern when boosted
        // ================================================

        [Fact]
        public async Task SessionBoost_BeatsStrongPattern_WhenBoosted()
        {
            // Arrange: Strong pattern that would normally win
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("Chrome - Google");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("chrome");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            
            // Session with lower base score
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = "Current Session Group",
                ConfidenceScore = 0.8,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            // Very strong pattern (high confidence, many observations)
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = "Frequent Pattern Group",
                    ConfidenceScore = 0.95,
                    TimesSeen = 500,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("document.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Winner: {result.DetectedContext}");
            _output.WriteLine($"Boost Applied: {result.SessionBoostApplied}");
            _output.WriteLine($"Signal Breakdown:");
            foreach (var kvp in result.SignalBreakdown)
            {
                _output.WriteLine($"  {kvp.Key}: {kvp.Value:F3}");
            }

            Assert.Equal("Current Session Group", result.DetectedContext);
            Assert.True(result.SessionBoostApplied);
        }

        // ========================================
        // Test 13: Performance - boost doesn't slow detection
        // ========================================

        [Fact]
        public async Task SessionBoost_Performance_StillUnder100ms()
        {
            // Arrange
            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns(("Test Group", 0.7));
            
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
                    GroupName = "Other Group",
                    ConfidenceScore = 0.8,
                    TimesSeen = 100,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();
            var times = new List<double>();

            // Act: 100 detections with boost
            for (int i = 0; i < 100; i++)
            {
                var result = await detector.DetectContextWithDetailsAsync($"file{i}.pdf", DateTime.Now);
                times.Add(result.DetectionTimeMs);
                Assert.True(result.SessionBoostApplied); // Confirm boost is applied
            }

            // Assert
            var avgTime = times.Average();
            var maxTime = times.Max();

            _output.WriteLine($"Average detection time: {avgTime:F2}ms");
            _output.WriteLine($"Max detection time: {maxTime:F2}ms");

            Assert.True(avgTime < 100, $"Average {avgTime:F2}ms exceeds 100ms");
            Assert.True(maxTime < 100, $"Max {maxTime:F2}ms exceeds 100ms");
        }

        // ========================================
        // Test 14: Telegram group mismatch - NO BOOST (new behavior)
        // When user switches to different Telegram group, treat as new batch
        // ========================================

        [Fact]
        public async Task SessionBoost_TelegramGroupMismatch_NoBoost_NewBatch()
        {
            // Scenario: User started download in "Linear Algebra" group
            // Then switched to "Credit M&O&P Spring" group in Telegram
            // NEW BEHAVIOR: This is a NEW BATCH - foreground (new group) should win
            // NO boost should be applied
            
            const string sessionGroup = "Linear Algebra";
            const string foregroundGroup = "Credit MOP Spring";

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{foregroundGroup} - Telegram");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
            
            // Background should not interfere - user moved to new group
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = sessionGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30,
                FileCount = 3
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("file4.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Session Group: {sessionGroup}");
            _output.WriteLine($"Foreground Group: {foregroundGroup}");
            _output.WriteLine($"Detected: {result.DetectedContext}");
            _output.WriteLine($"Boost Applied: {result.SessionBoostApplied}");
            _output.WriteLine($"Boost Reason: {result.SessionBoostReason ?? "N/A"}");
            _output.WriteLine("Signal breakdown:");
            foreach (var s in result.Signals)
                _output.WriteLine($"  {s.Source}: '{s.DetectedContext}' power={s.GetVotingPower():F3}");

            // NEW BEHAVIOR: Foreground wins - this is a new batch
            Assert.Equal(foregroundGroup, result.DetectedContext);
            Assert.False(result.SessionBoostApplied, "No boost when user switches to different Telegram group (new batch)");
        }

        // ========================================
        // Test 15: NEW - No boost when same Telegram group
        // ========================================

        [Fact]
        public async Task SessionBoost_TelegramSameGroup_NoBoost()
        {
            // Scenario: User stays in the same group - no boost needed
            const string sameGroup = "Linear Algebra";

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{sameGroup} - Telegram");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((sameGroup, 0.8));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = sameGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            var detector = CreateDetector();

            // Act
            var result = await detector.DetectContextWithDetailsAsync("test.pdf", DateTime.Now);

            // Assert
            _output.WriteLine($"Detected: {result.DetectedContext}");
            _output.WriteLine($"Boost Applied: {result.SessionBoostApplied}");

            Assert.Equal(sameGroup, result.DetectedContext);
            Assert.False(result.SessionBoostApplied, "No boost needed when foreground and session are same group");
        }

        // ========================================
        // Test 16: Real-world group switch - 5 files (UPDATED BEHAVIOR)
        // ========================================

        [Fact]
        public async Task SessionBoost_RealWorld_GroupSwitch_5Files()
        {
            // Scenario: User downloads from Group A, switches to VS Code, then returns to Group B
            // Files 1-3: Group A foreground (no boost - groups match)
            // File 4: VS Code foreground (boost applied - weak foreground)
            // File 5: Group B foreground (NO boost - new batch starting!)
            //
            // UPDATED BEHAVIOR: File 5 goes to NEW group (not old session)

            const string sessionGroup = "Linear Algebra";
            const string differentGroup = "Credit MOP Spring";
            var results = new List<(string fileName, string detectedGroup, bool boostApplied, string? reason)>();

            var detector = CreateDetector();

            // Setup session
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 32,
                GroupName = sessionGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30,
                FileCount = 0
            });

            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            // Files 1-3: Same group
            for (int i = 1; i <= 3; i++)
            {
                _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{sessionGroup} - Telegram");
                _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
                var result = await detector.DetectContextWithDetailsAsync($"File{i}.pdf", DateTime.Now);
                results.Add(($"File{i}.pdf", result.DetectedContext, result.SessionBoostApplied, result.SessionBoostReason));
            }

            // File 4: VS Code
            {
                _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns("VS Code");
                _mockForeground.Setup(f => f.GetProcessName()).Returns("Code");
                var result = await detector.DetectContextWithDetailsAsync("File4.pdf", DateTime.Now);
                results.Add(("File4.pdf", result.DetectedContext, result.SessionBoostApplied, result.SessionBoostReason));
            }

            // File 5: Different Telegram group
            {
                _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{differentGroup} - Telegram");
                _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
                var result = await detector.DetectContextWithDetailsAsync("File5.pdf", DateTime.Now);
                results.Add(("File5.pdf", result.DetectedContext, result.SessionBoostApplied, result.SessionBoostReason));
            }

            // Output
            _output.WriteLine("=== Results ===");
            foreach (var r in results)
                _output.WriteLine($"{r.fileName} -> {r.detectedGroup} (boost: {r.boostApplied})");

            // Assertions
            Assert.Equal(sessionGroup, results[0].detectedGroup); // File 1
            Assert.Equal(sessionGroup, results[1].detectedGroup); // File 2
            Assert.Equal(sessionGroup, results[2].detectedGroup); // File 3
            Assert.Equal(sessionGroup, results[3].detectedGroup); // File 4 - boosted
            Assert.Equal(differentGroup, results[4].detectedGroup); // File 5 - NEW GROUP!

            Assert.False(results[0].boostApplied); // Same group
            Assert.False(results[1].boostApplied); // Same group
            Assert.False(results[2].boostApplied); // Same group
            Assert.True(results[3].boostApplied);  // VS Code - boost
            Assert.False(results[4].boostApplied); // Different Telegram - NO boost (new batch)
        }

        // ========================================
        // Test 17: Group mismatch - foreground wins (UPDATED BEHAVIOR)
        // ========================================

        [Fact]
        public async Task SessionBoost_GroupMismatch_ForegroundWins()
        {
            // UPDATED BEHAVIOR: When user switches to different Telegram group,
            // treat as NEW BATCH - foreground wins, no boost applied
            
            const string sessionGroup = "Session Group";
            const string differentGroup = "Different Group";

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{differentGroup} - Telegram");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");
            
            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((differentGroup, 0.9));
            
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = sessionGroup,
                ConfidenceScore = 0.8,
                LastActivity = DateTime.Now,
                TimeoutSeconds = 30
            });

            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new FilePattern
                {
                    GroupName = differentGroup,
                    ConfidenceScore = 0.95,
                    TimesSeen = 200,
                    LastSeen = DateTime.Now
                });

            var detector = CreateDetector();
            var result = await detector.DetectContextWithDetailsAsync("document.pdf", DateTime.Now);

            _output.WriteLine($"Session: {sessionGroup}");
            _output.WriteLine($"Foreground: {differentGroup}");
            _output.WriteLine($"Winner: {result.DetectedContext}");
            _output.WriteLine($"Boost: {result.SessionBoostApplied}");

            // UPDATED: Foreground wins - this is a new batch
            Assert.Equal(differentGroup, result.DetectedContext);
            Assert.False(result.SessionBoostApplied, "No boost when user switches to different Telegram group");
        }

        // ========================================
        // Test 19: Edge Case: Old session + different Telegram group = NEW BATCH
        // ========================================

        [Fact]
        public async Task SessionBoost_EdgeCase_OldSessionStillActive_WhenNewBatchStarts()
        {
            // When user returns to Telegram with DIFFERENT group while old session active:
            // UPDATED BEHAVIOR: NO boost - treat as new batch starting
            
            const string oldGroup = "Linear Algebra";
            const string newGroup = "Credit MOP Spring";

            var detector = CreateDetector();

            _mockSessionManager.Setup(s => s.GetActiveSessionAsync()).ReturnsAsync(new DownloadSession
            {
                Id = 1,
                GroupName = oldGroup,
                ConfidenceScore = 0.9,
                LastActivity = DateTime.Now.AddSeconds(-20),
                TimeoutSeconds = 30,
                FileCount = 5
            });

            _mockBackground.Setup(b => b.GetBestRecentGroupName()).Returns((ValueTuple<string, double>?)null);
            _mockBackground.Setup(b => b.GetMostRecentWindow()).Returns((WindowInfo?)null);
            _mockDatabase.Setup(d => d.GetBestPatternAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
                .ReturnsAsync((FilePattern?)null);

            _mockForeground.Setup(f => f.GetActiveWindowTitle()).Returns($"{newGroup} - Telegram");
            _mockForeground.Setup(f => f.GetProcessName()).Returns("Telegram");

            var result = await detector.DetectContextWithDetailsAsync("NewBatch_File1.pdf", DateTime.Now);

            _output.WriteLine($"Old Session: '{oldGroup}'");
            _output.WriteLine($"Current Foreground: '{newGroup}'");
            _output.WriteLine($"Detected: '{result.DetectedContext}'");
            _output.WriteLine($"Boost Applied: {result.SessionBoostApplied}");

            // UPDATED: User is in different Telegram group = new batch
            Assert.Equal(newGroup, result.DetectedContext);
            Assert.False(result.SessionBoostApplied, "No boost - user switched to different Telegram group (new batch)");
        }
    }
}
