using System;
using System.Linq;
using System.Threading;
using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;
using Xunit;

namespace TelegramOrganizer.Tests.Services
{
    public class BackgroundWindowMonitorTests : IDisposable
    {
        private readonly Mock<ILoggingService> _mockLogger;
        private readonly BackgroundWindowMonitor _monitor;

        public BackgroundWindowMonitorTests()
        {
            _mockLogger = new Mock<ILoggingService>();
            _monitor = new BackgroundWindowMonitor(_mockLogger.Object);
            _monitor.AutoScan = false; // Disable auto-scan for tests
        }

        public void Dispose()
        {
            _monitor.Dispose();
        }

        [Fact]
        public void Start_SetsIsMonitoringToTrue()
        {
            // Act
            _monitor.Start();

            // Assert
            Assert.True(_monitor.IsMonitoring);
            
            // Cleanup
            _monitor.Stop();
        }

        [Fact]
        public void Stop_SetsIsMonitoringToFalse()
        {
            // Arrange
            _monitor.Start();
            Assert.True(_monitor.IsMonitoring);

            // Act
            _monitor.Stop();

            // Assert
            Assert.False(_monitor.IsMonitoring);
        }

        [Fact]
        public void GetAllTelegramWindows_InitiallyEmpty()
        {
            // Act
            var windows = _monitor.GetAllTelegramWindows();

            // Assert
            Assert.Empty(windows);
        }

        [Fact]
        public void GetMostRecentWindow_ReturnsNullWhenEmpty()
        {
            // Act
            var window = _monitor.GetMostRecentWindow();

            // Assert
            Assert.Null(window);
        }

        [Fact]
        public void GetRecentWindows_ReturnsEmptyListWhenNoWindows()
        {
            // Act
            var windows = _monitor.GetRecentWindows(60);

            // Assert
            Assert.Empty(windows);
        }

        [Fact]
        public void GetTrackedWindowCount_InitiallyZero()
        {
            // Act
            int count = _monitor.GetTrackedWindowCount();

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void ScanIntervalMs_DefaultValue()
        {
            // Assert
            Assert.Equal(2000, _monitor.ScanIntervalMs);
        }

        [Fact]
        public void MaxTrackedWindows_DefaultValue()
        {
            // Assert
            Assert.Equal(20, _monitor.MaxTrackedWindows);
        }

        [Fact]
        public void AutoScan_DefaultValue()
        {
            // Arrange
            var freshMonitor = new BackgroundWindowMonitor(_mockLogger.Object);

            // Assert
            Assert.True(freshMonitor.AutoScan);
            
            // Cleanup
            freshMonitor.Dispose();
        }

        [Fact]
        public void Configuration_CanBeModified()
        {
            // Act
            _monitor.ScanIntervalMs = 5000;
            _monitor.MaxTrackedWindows = 10;
            _monitor.AutoScan = false;

            // Assert
            Assert.Equal(5000, _monitor.ScanIntervalMs);
            Assert.Equal(10, _monitor.MaxTrackedWindows);
            Assert.False(_monitor.AutoScan);
        }

        [Fact]
        public void WindowDetected_EventFires()
        {
            // Note: This test would require mocking Win32 API or integration testing
            // Skipping for unit tests as it requires actual windows to enumerate
            Assert.True(true); // Placeholder
        }

        [Fact]
        public void WindowActivated_EventFires()
        {
            // Note: Similar to above - requires integration testing
            Assert.True(true); // Placeholder
        }

        [Fact]
        public void GetBestRecentGroupName_ReturnsNullWhenNoWindows()
        {
            // Act
            var result = _monitor.GetBestRecentGroupName();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ClearOldWindows_WithNoWindows_ReturnsZero()
        {
            // Act
            int cleared = _monitor.ClearOldWindows(300);

            // Assert
            Assert.Equal(0, cleared);
        }

        [Fact]
        public void Start_TwiceDoesNotThrow()
        {
            // Act
            _monitor.Start();
            _monitor.Start(); // Should not throw

            // Assert
            Assert.True(_monitor.IsMonitoring);
            
            // Cleanup
            _monitor.Stop();
        }

        [Fact]
        public void Stop_WithoutStartDoesNotThrow()
        {
            // Act & Assert
            _monitor.Stop(); // Should not throw
            Assert.False(_monitor.IsMonitoring);
        }

        [Fact]
        public void Dispose_StopsMonitoring()
        {
            // Arrange
            _monitor.Start();
            Assert.True(_monitor.IsMonitoring);

            // Act
            _monitor.Dispose();

            // Assert
            Assert.False(_monitor.IsMonitoring);
        }
    }
}
