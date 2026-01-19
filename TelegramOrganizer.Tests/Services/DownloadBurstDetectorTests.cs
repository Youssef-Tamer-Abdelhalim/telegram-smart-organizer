using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Infra.Services;
using Xunit;

namespace TelegramOrganizer.Tests.Services
{
    public class DownloadBurstDetectorTests
    {
        private readonly Mock<ILoggingService> _mockLogger;
        private readonly DownloadBurstDetector _detector;

        public DownloadBurstDetectorTests()
        {
            _mockLogger = new Mock<ILoggingService>();
            _detector = new DownloadBurstDetector(_mockLogger.Object);
        }

        [Fact]
        public void RecordDownload_SingleFile_NotABurst()
        {
            // Arrange & Act
            _detector.RecordDownload("file1.pdf");

            // Assert
            // After first file, checking if next file would be a burst
            // With only 1 file recorded and MinimumFilesForBurst=2, next file WILL be a burst
            Assert.True(_detector.IsBurstDownload("file2.pdf"));
            
            // But the status itself is not a burst yet (need 2 files recorded)
            var status = _detector.GetCurrentBurstStatus();
            Assert.False(status.IsBurstActive);
        }

        [Fact]
        public void RecordDownload_TwoFilesRapidly_DetectsBurst()
        {
            // Arrange & Act
            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf"); // Within default 5 second threshold

            // Assert
            var status = _detector.GetCurrentBurstStatus();
            Assert.True(status.IsBurstActive);
            Assert.Equal(2, status.FileCount);
        }

        [Fact]
        public void RecordDownload_MultipleFiles_MaintainsBurst()
        {
            // Arrange & Act
            for (int i = 1; i <= 5; i++)
            {
                _detector.RecordDownload($"file{i}.pdf");
            }

            // Assert
            var status = _detector.GetCurrentBurstStatus();
            Assert.True(status.IsBurstActive);
            Assert.Equal(5, status.FileCount);
            Assert.Contains("file1.pdf", status.FileNames);
            Assert.Contains("file5.pdf", status.FileNames);
        }

        [Fact]
        public void IsBurstDownload_WithinThreshold_ReturnsTrue()
        {
            // Arrange
            _detector.RecordDownload("file1.pdf");

            // Act
            bool isBurst = _detector.IsBurstDownload("file2.pdf");

            // Assert
            Assert.True(isBurst);
        }

        [Fact]
        public void IsBurstDownload_OutsideThreshold_ReturnsFalse()
        {
            // Arrange
            _detector.BurstThresholdSeconds = 2;
            var now = DateTime.Now;
            _detector.RecordDownload("file1.pdf", now);

            // Act - 3 seconds later
            bool isBurst = _detector.IsBurstDownload("file2.pdf", now.AddSeconds(3));

            // Assert
            Assert.False(isBurst);
        }

        [Fact]
        public void GetCurrentBurstCount_ReturnsCorrectCount()
        {
            // Arrange
            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf");
            _detector.RecordDownload("file3.pdf");

            // Act
            int count = _detector.GetCurrentBurstCount();

            // Assert
            Assert.Equal(3, count);
        }

        [Fact]
        public void Reset_ClearsBurstState()
        {
            // Arrange
            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf");
            Assert.True(_detector.GetCurrentBurstStatus().IsBurstActive);

            // Act
            _detector.Reset();

            // Assert
            var status = _detector.GetCurrentBurstStatus();
            Assert.False(status.IsBurstActive);
            Assert.Equal(0, status.FileCount);
            Assert.Equal(0, _detector.GetCurrentBurstCount());
        }

        [Fact]
        public void BurstStarted_EventFires()
        {
            // Arrange
            BurstDetectionResult? firedResult = null;
            _detector.BurstStarted += (sender, result) => firedResult = result;

            // Act
            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf"); // Should trigger BurstStarted

            // Assert
            Assert.NotNull(firedResult);
            Assert.True(firedResult.IsBurstActive);
            Assert.Equal(2, firedResult.FileCount);
        }

        [Fact]
        public void BurstContinued_EventFires()
        {
            // Arrange
            int continuedCount = 0;
            _detector.BurstContinued += (sender, result) => continuedCount++;

            // Act
            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf"); // Starts burst
            _detector.RecordDownload("file3.pdf"); // Should trigger BurstContinued
            _detector.RecordDownload("file4.pdf"); // Should trigger BurstContinued again

            // Assert
            Assert.Equal(2, continuedCount);
        }

        [Fact]
        public void BurstEnded_EventFires_OnReset()
        {
            // Arrange
            BurstDetectionResult? endedResult = null;
            _detector.BurstEnded += (sender, result) => endedResult = result;

            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf");

            // Act
            _detector.Reset();

            // Assert
            Assert.NotNull(endedResult);
            Assert.Equal(2, endedResult.FileCount);
        }

        [Fact]
        public void MinimumFilesForBurst_Configuration_Works()
        {
            // Arrange
            _detector.MinimumFilesForBurst = 3;

            // Act
            _detector.RecordDownload("file1.pdf");
            _detector.RecordDownload("file2.pdf");

            // Assert - Not a burst yet (only 2 files, need 3)
            var status = _detector.GetCurrentBurstStatus();
            Assert.False(status.IsBurstActive);

            // Add third file
            _detector.RecordDownload("file3.pdf");
            status = _detector.GetCurrentBurstStatus();
            Assert.True(status.IsBurstActive);
        }

        [Fact]
        public void BurstThresholdSeconds_Configuration_Works()
        {
            // Arrange
            _detector.BurstThresholdSeconds = 10; // 10 second threshold
            var now = DateTime.Now;

            // Act
            _detector.RecordDownload("file1.pdf", now);
            _detector.RecordDownload("file2.pdf", now.AddSeconds(8)); // Within 10s

            // Assert
            var status = _detector.GetCurrentBurstStatus();
            Assert.True(status.IsBurstActive);
        }

        [Fact]
        public void BurstDetectionResult_CalculatesConfidence()
        {
            // Arrange & Act - Many files quickly = high confidence
            for (int i = 1; i <= 10; i++)
            {
                _detector.RecordDownload($"file{i}.pdf");
            }

            // Assert
            var status = _detector.GetCurrentBurstStatus();
            Assert.True(status.Confidence >= 0.8); // High confidence for 10 files
        }

        [Fact]
        public void BurstDetectionResult_CalculatesDuration()
        {
            // Arrange
            var now = DateTime.Now;

            // Act
            _detector.RecordDownload("file1.pdf", now);
            _detector.RecordDownload("file2.pdf", now.AddSeconds(2));
            _detector.RecordDownload("file3.pdf", now.AddSeconds(4));

            // Assert
            var status = _detector.GetCurrentBurstStatus();
            Assert.True(status.DurationSeconds >= 3.9 && status.DurationSeconds <= 4.1);
            Assert.True(status.AverageIntervalSeconds >= 1.9 && status.AverageIntervalSeconds <= 2.1);
        }

        [Fact]
        public void GetBurstTimeRemaining_ReturnsCorrectValue()
        {
            // Arrange
            _detector.BurstThresholdSeconds = 10;
            var now = DateTime.Now;

            _detector.RecordDownload("file1.pdf", now);
            _detector.RecordDownload("file2.pdf", now);

            // Act - Check immediately after last file (same timestamp)
            var remaining = _detector.GetBurstTimeRemaining();

            // Assert
            Assert.NotNull(remaining);
            // Should be close to threshold since both files have same timestamp
            Assert.True(remaining.Value >= 9.5 && remaining.Value <= 10.1);
        }

        [Fact]
        public void GetBurstTimeRemaining_ReturnsNull_WhenNoBurst()
        {
            // Arrange - No burst active

            // Act
            double? remaining = _detector.GetBurstTimeRemaining();

            // Assert
            Assert.Null(remaining);
        }

        [Fact]
        public void CleanupOldDownloads_RemovesExpiredFiles()
        {
            // Arrange
            _detector.BurstThresholdSeconds = 3; // Shorter threshold
            var now = DateTime.Now;

            _detector.RecordDownload("file1.pdf", now);
            _detector.RecordDownload("file2.pdf", now.AddSeconds(1));
            
            // Verify we have 2 files before cleanup
            Assert.Equal(2, _detector.GetCurrentBurstCount());

            // Act - Record a file 6 seconds later (well outside 3s threshold)
            // This will trigger cleanup in RecordDownload
            _detector.RecordDownload("file3.pdf", now.AddSeconds(6));

            // Assert - Only file3 should remain after automatic cleanup
            var status = _detector.GetCurrentBurstStatus();
            Assert.Equal(1, status.FileCount);
            Assert.Contains("file3.pdf", status.FileNames);
            Assert.DoesNotContain("file1.pdf", status.FileNames);
            Assert.DoesNotContain("file2.pdf", status.FileNames);
        }

        [Fact]
        public void MaxBurstDurationSeconds_EndsLongBursts()
        {
            // Arrange
            _detector.MaxBurstDurationSeconds = 10;
            var now = DateTime.Now;

            _detector.RecordDownload("file1.pdf", now);
            _detector.RecordDownload("file2.pdf", now.AddSeconds(2));

            // Act - Add file after max duration
            _detector.RecordDownload("file3.pdf", now.AddSeconds(15));

            // Assert - Burst should have ended
            var status = _detector.GetCurrentBurstStatus();
            Assert.False(status.IsBurstActive);
        }
    }
}
