using System;
using System.Threading.Tasks;
using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;
using Xunit;

namespace TelegramOrganizer.Tests.Services
{
    public class DownloadSessionManagerTests
    {
        private readonly Mock<IDatabaseService> _mockDatabase;
        private readonly Mock<ILoggingService> _mockLogger;
        private readonly DownloadSessionManager _sessionManager;

        public DownloadSessionManagerTests()
        {
            _mockDatabase = new Mock<IDatabaseService>();
            _mockLogger = new Mock<ILoggingService>();
            _sessionManager = new DownloadSessionManager(_mockDatabase.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetActiveSessionAsync_ReturnsActiveSession()
        {
            // Arrange
            var expectedSession = new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                IsActive = true
            };

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync(expectedSession);

            // Act
            var result = await _sessionManager.GetActiveSessionAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedSession.Id, result.Id);
            Assert.Equal(expectedSession.GroupName, result.GroupName);
        }

        [Fact]
        public async Task StartSessionAsync_CreatesNewSession()
        {
            // Arrange
            string groupName = "New Group";
            var newSession = new DownloadSession
            {
                Id = 1,
                GroupName = groupName,
                IsActive = true
            };

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync((DownloadSession?)null);

            _mockDatabase.Setup(db => db.CreateSessionAsync(groupName, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(newSession);

            // Act
            var result = await _sessionManager.StartSessionAsync(groupName, "Test Window", "Telegram", 1.0);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(groupName, result.GroupName);
            _mockDatabase.Verify(db => db.CreateSessionAsync(groupName, "Test Window", "Telegram"), Times.Once);
        }

        [Fact]
        public async Task StartSessionAsync_ReusesExistingSessionForSameGroup()
        {
            // Arrange
            string groupName = "Existing Group";
            var existingSession = new DownloadSession
            {
                Id = 1,
                GroupName = groupName,
                IsActive = true
            };

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync(existingSession);

            // Act
            var result = await _sessionManager.StartSessionAsync(groupName, "Test Window", "Telegram", 1.0);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(existingSession.Id, result.Id);
            _mockDatabase.Verify(db => db.CreateSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _mockDatabase.Verify(db => db.UpdateSessionAsync(It.IsAny<DownloadSession>()), Times.Once);
        }

        [Fact]
        public async Task AddFileToSessionAsync_AddsFileToExistingSession()
        {
            // Arrange
            var session = new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                IsActive = true,
                FileCount = 0
            };

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync(session);

            // Act
            var result = await _sessionManager.AddFileToSessionAsync("test.pdf", "Test Group", "/path/test.pdf", 1024);

            // Assert
            Assert.NotNull(result);
            _mockDatabase.Verify(db => db.AddFileToSessionAsync(session.Id, "test.pdf", "/path/test.pdf", 1024), Times.Once);
        }

        [Fact]
        public async Task AddFileToSessionAsync_CreatesNewSessionIfNoneExists()
        {
            // Arrange
            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync((DownloadSession?)null);

            var newSession = new DownloadSession
            {
                Id = 1,
                GroupName = "New Group",
                IsActive = true
            };

            _mockDatabase.Setup(db => db.CreateSessionAsync("New Group", It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(newSession);

            // Act
            var result = await _sessionManager.AddFileToSessionAsync("test.pdf", "New Group");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("New Group", result.GroupName);
            _mockDatabase.Verify(db => db.CreateSessionAsync("New Group", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task EndSessionAsync_EndsSpecifiedSession()
        {
            // Arrange
            int sessionId = 1;
            var session = new DownloadSession { Id = sessionId, GroupName = "Test" };

            _mockDatabase.Setup(db => db.GetSessionAsync(sessionId))
                .ReturnsAsync(session);

            // Act
            await _sessionManager.EndSessionAsync(sessionId);

            // Assert
            _mockDatabase.Verify(db => db.EndSessionAsync(sessionId), Times.Once);
        }

        [Fact]
        public async Task IsSessionActiveAsync_ReturnsTrueWhenSessionExists()
        {
            // Arrange
            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync(new DownloadSession { Id = 1, IsActive = true });

            // Act
            var result = await _sessionManager.IsSessionActiveAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsSessionActiveAsync_ReturnsFalseWhenNoSession()
        {
            // Arrange
            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync((DownloadSession?)null);

            // Act
            var result = await _sessionManager.IsSessionActiveAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetCurrentGroupNameAsync_ReturnsGroupName()
        {
            // Arrange
            var session = new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                IsActive = true
            };

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync(session);

            // Act
            var result = await _sessionManager.GetCurrentGroupNameAsync();

            // Assert
            Assert.Equal("Test Group", result);
        }

        [Fact]
        public void SetDefaultTimeout_SetsTimeout()
        {
            // Arrange
            int newTimeout = 60;

            // Act
            _sessionManager.SetDefaultTimeout(newTimeout);

            // Assert
            Assert.Equal(newTimeout, _sessionManager.GetDefaultTimeout());
        }

        [Fact]
        public void SetDefaultTimeout_IgnoresInvalidValues()
        {
            // Arrange
            int originalTimeout = _sessionManager.GetDefaultTimeout();

            // Act
            _sessionManager.SetDefaultTimeout(2); // Too low
            _sessionManager.SetDefaultTimeout(500); // Too high

            // Assert
            Assert.Equal(originalTimeout, _sessionManager.GetDefaultTimeout());
        }

        [Fact]
        public async Task SessionStarted_EventFires()
        {
            // Arrange
            DownloadSession? firedSession = null;
            _sessionManager.SessionStarted += (sender, session) => firedSession = session;

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync((DownloadSession?)null);

            var newSession = new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                IsActive = true
            };

            _mockDatabase.Setup(db => db.CreateSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(newSession);

            // Act
            await _sessionManager.StartSessionAsync("Test Group");

            // Assert
            Assert.NotNull(firedSession);
            Assert.Equal("Test Group", firedSession.GroupName);
        }

        [Fact]
        public async Task FileAddedToSession_EventFires()
        {
            // Arrange
            string? addedFileName = null;
            DownloadSession? addedSession = null;
            
            _sessionManager.FileAddedToSession += (sender, args) =>
            {
                addedSession = args.session;
                addedFileName = args.fileName;
            };

            var session = new DownloadSession
            {
                Id = 1,
                GroupName = "Test Group",
                IsActive = true
            };

            _mockDatabase.Setup(db => db.GetActiveSessionAsync())
                .ReturnsAsync(session);

            // Act
            await _sessionManager.AddFileToSessionAsync("test.pdf", "Test Group");

            // Assert
            Assert.NotNull(addedFileName);
            Assert.NotNull(addedSession);
            Assert.Equal("test.pdf", addedFileName);
        }

        [Fact]
        public async Task CheckAndEndTimedOutSessionsAsync_CallsDatabaseMethod()
        {
            // Arrange
            _mockDatabase.Setup(db => db.EndTimedOutSessionsAsync())
                .ReturnsAsync(2);

            // Act
            var count = await _sessionManager.CheckAndEndTimedOutSessionsAsync();

            // Assert
            Assert.Equal(2, count);
            _mockDatabase.Verify(db => db.EndTimedOutSessionsAsync(), Times.Once);
        }
    }
}
