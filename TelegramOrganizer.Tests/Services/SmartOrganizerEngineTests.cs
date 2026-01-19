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
        private readonly Mock<IPersistenceService> _mockPersistence;
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly Mock<ILoggingService> _mockLogger;

        public SmartOrganizerEngineTests()
        {
            _mockWatcher = new Mock<IFileWatcher>();
            _mockContextDetector = new Mock<IContextDetector>();
            _mockOrganizer = new Mock<IFileOrganizer>();
            _mockPersistence = new Mock<IPersistenceService>();
            _mockSettings = new Mock<ISettingsService>();
            _mockLogger = new Mock<ILoggingService>();

            // Setup default settings
            _mockSettings.Setup(s => s.LoadSettings()).Returns(new AppSettings
            {
                DownloadsFolderPath = @"C:\Downloads",
                DestinationBasePath = @"C:\Organized",
                RetentionDays = 30
            });

            // Setup default persistence
            _mockPersistence.Setup(p => p.LoadState()).Returns(new AppState());
            _mockPersistence.Setup(p => p.CleanupOldEntries(It.IsAny<int>())).Returns(0);
        }

        private SmartOrganizerEngine CreateEngine()
        {
            return new SmartOrganizerEngine(
                _mockWatcher.Object,
                _mockContextDetector.Object,
                _mockOrganizer.Object,
                _mockPersistence.Object,
                _mockSettings.Object,
                _mockLogger.Object
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
        public void Start_LoadsPersistedState()
        {
            // Arrange
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            _mockPersistence.Verify(p => p.LoadState(), Times.Once);
        }

        [Fact]
        public void Start_CleansUpOldEntries()
        {
            // Arrange
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            _mockPersistence.Verify(p => p.CleanupOldEntries(30), Times.Once);
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
        public void Start_RestoresPendingDownloads_FromPersistedState()
        {
            // Arrange
            var persistedState = new AppState();
            persistedState.PendingDownloads["test.pdf.td"] = new FileContext
            {
                OriginalTempName = "test.pdf.td",
                DetectedGroupName = "TestGroup"
            };
            
            _mockPersistence.Setup(p => p.LoadState()).Returns(persistedState);
            
            // The file doesn't exist, so it should be removed
            _mockPersistence.Setup(p => p.RemoveEntry(It.IsAny<string>()));
            
            var engine = CreateEngine();

            // Act
            engine.Start();

            // Assert
            // Since file doesn't exist, it should try to remove the orphan entry
            _mockPersistence.Verify(p => p.RemoveEntry("test.pdf.td"), Times.Once);
        }
    }
}
