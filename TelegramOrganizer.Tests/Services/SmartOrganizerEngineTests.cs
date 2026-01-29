using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Core.Services;

namespace TelegramOrganizer.Tests.Services
{
    public class SmartOrganizerEngineTests
    {
        private readonly Mock<IFileWatcher> _mockWatcher;
        private readonly Mock<IContextDetector> _mockContextDetector;
        private readonly Mock<IFileOrganizer> _mockOrganizer;
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly Mock<ILoggingService> _mockLogger;
        
        // V2.0 Required Services
        private readonly Mock<IDownloadSessionManager> _mockSessionManager;
        private readonly Mock<IDownloadBurstDetector> _mockBurstDetector;
        private readonly Mock<IBackgroundWindowMonitor> _mockWindowMonitor;

        public SmartOrganizerEngineTests()
        {
            _mockWatcher = new Mock<IFileWatcher>();
            _mockContextDetector = new Mock<IContextDetector>();
            _mockOrganizer = new Mock<IFileOrganizer>();
            _mockSettings = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILoggingService>();
            
            // V2.0 Services
            _mockSessionManager = new Mock<IDownloadSessionManager>();
            _mockBurstDetector = new Mock<IDownloadBurstDetector>();
            _mockWindowMonitor = new Mock<IBackgroundWindowMonitor>();

            // Setup default settings
            _mockSettings.Setup(s => s.LoadSettings()).Returns(new AppSettings
            {
                DownloadsFolderPath = @"C:\Downloads",
                DestinationBasePath = @"C:\Organized",
                RetentionDays = 30
            });

            // Setup V2.0 service defaults
            _mockSessionManager.Setup(s => s.GetActiveSessionAsync())
                .ReturnsAsync((DownloadSession?)null);
            _mockSessionManager.Setup(s => s.CheckAndEndTimedOutSessionsAsync())
                .ReturnsAsync(0);
            
            _mockBurstDetector.Setup(b => b.GetCurrentBurstStatus())
                .Returns(new BurstDetectionResult { IsBurstActive = false });
        }

        private SmartOrganizerEngine CreateEngine()
        {
            return new SmartOrganizerEngine(
                _mockWatcher.Object,
                _mockContextDetector.Object,
                _mockOrganizer.Object,
                _mockSettings.Object,
                _mockLogger.Object,
                _mockSessionManager.Object,
                _mockBurstDetector.Object,
                _mockWindowMonitor.Object
            );
        }

        [Fact]
        public void Start_InitializesWatcher()
        {
            // Arrange
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            _mockWatcher.Verify(w => w.Start(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void Start_LoadsSettings()
        {
            // Arrange
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            _mockSettings.Verify(s => s.LoadSettings(), Times.AtLeastOnce);
        }

        [Fact]
        public void Start_StartsBackgroundWindowMonitor()
        {
            // Arrange
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            _mockWindowMonitor.Verify(w => w.Start(), Times.Once);
        }

        [Fact]
        public void Stop_StopsWatcher()
        {
            // Arrange
            var engine = CreateEngine();
            engine.Start();

            // Act
            engine.Stop();

            // Assert
            _mockWatcher.Verify(w => w.Stop(), Times.Once);
        }

        [Fact]
        public void Stop_StopsBackgroundWindowMonitor()
        {
            // Arrange
            var engine = CreateEngine();
            engine.Start();

            // Act
            engine.Stop();

            // Assert
            _mockWindowMonitor.Verify(w => w.Stop(), Times.Once);
        }

        [Fact]
        public void Stop_EndsCurrentSession()
        {
            // Arrange
            var engine = CreateEngine();
            engine.Start();

            // Act
            engine.Stop();

            // Assert
            _mockSessionManager.Verify(s => s.EndCurrentSessionAsync(), Times.Once);
        }

        [Fact]
        public void Start_LogsStartupInfo()
        {
            // Arrange
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            _mockLogger.Verify(l => l.LogInfo(It.Is<string>(s => s.Contains("Engine Starting"))), Times.Once);
        }

        [Fact]
        public void OperationCompleted_EventFires_OnStart()
        {
            // Arrange
            var engine = CreateEngine();
            string? receivedMessage = null;
            engine.OperationCompleted += (s, msg) => receivedMessage = msg;

            // Act
            engine.Start();

            // Assert
            Assert.NotNull(receivedMessage);
            Assert.Contains("[ENGINE]", receivedMessage);
        }

        [Fact]
        public void Constructor_ThrowsOnNullWatcher()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new SmartOrganizerEngine(
                null!,
                _mockContextDetector.Object,
                _mockOrganizer.Object,
                _mockSettings.Object,
                _mockLogger.Object,
                _mockSessionManager.Object,
                _mockBurstDetector.Object,
                _mockWindowMonitor.Object
            ));
        }

        [Fact]
        public void Constructor_ThrowsOnNullSessionManager()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new SmartOrganizerEngine(
                _mockWatcher.Object,
                _mockContextDetector.Object,
                _mockOrganizer.Object,
                _mockSettings.Object,
                _mockLogger.Object,
                null!,
                _mockBurstDetector.Object,
                _mockWindowMonitor.Object
            ));
        }

        [Fact]
        public void Constructor_ThrowsOnNullBurstDetector()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new SmartOrganizerEngine(
                _mockWatcher.Object,
                _mockContextDetector.Object,
                _mockOrganizer.Object,
                _mockSettings.Object,
                _mockLogger.Object,
                _mockSessionManager.Object,
                null!,
                _mockWindowMonitor.Object
            ));
        }

        [Fact]
        public void Constructor_ThrowsOnNullWindowMonitor()
        {
            // Assert
            Assert.Throws<ArgumentNullException>(() => new SmartOrganizerEngine(
                _mockWatcher.Object,
                _mockContextDetector.Object,
                _mockOrganizer.Object,
                _mockSettings.Object,
                _mockLogger.Object,
                _mockSessionManager.Object,
                _mockBurstDetector.Object,
                null!
            ));
        }
    }
}
