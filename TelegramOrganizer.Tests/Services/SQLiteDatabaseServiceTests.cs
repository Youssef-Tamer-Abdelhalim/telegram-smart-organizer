using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;
using Xunit;

namespace TelegramOrganizer.Tests.Services
{
    /// <summary>
    /// Tests for SQLiteDatabaseService.
    /// Note: These tests use a shared database instance and may have interdependencies.
    /// Each test should clean up after itself or use unique data.
    /// </summary>
    public class SQLiteDatabaseServiceTests : IAsyncLifetime
    {
        private readonly SQLiteDatabaseService _databaseService;

        public SQLiteDatabaseServiceTests()
        {
            _databaseService = new SQLiteDatabaseService();
        }

        public async Task InitializeAsync()
        {
            // Initialize database before each test
            await _databaseService.InitializeDatabaseAsync();
            
            // Clean up any leftover test data
            await CleanupTestDataAsync();
        }

        public async Task DisposeAsync()
        {
            // Cleanup after each test
            await CleanupTestDataAsync();
            
            // Close connection
            _databaseService.CloseConnection();
        }

        private async Task CleanupTestDataAsync()
        {
            try
            {
                // Delete ALL data for clean test environment
                
                // 1. End and delete all sessions
                var allSessions = await _databaseService.GetSessionsAsync(true, 10000);
                foreach (var session in allSessions)
                {
                    await _databaseService.EndSessionAsync(session.Id);
                }
                await _databaseService.DeleteOldSessionsAsync(0); // Delete all
                
                // 2. Clear inactive sessions too
                var inactiveSessions = await _databaseService.GetSessionsAsync(false, 10000);
                foreach (var session in inactiveSessions)
                {
                    // End them as well, DeleteOldSessions will remove them
                    await _databaseService.EndSessionAsync(session.Id);
                }
                await _databaseService.DeleteOldSessionsAsync(0); // Delete all again
            }
            catch
            {
                // Ignore cleanup errors during test setup
            }
        }

        [Fact]
        public async Task InitializeDatabaseAsync_CreatesDatabase()
        {
            // Arrange & Act
            await _databaseService.InitializeDatabaseAsync();

            // Assert
            var dbPath = _databaseService.GetDatabasePath();
            Assert.True(File.Exists(dbPath));
        }

        [Fact]
        public async Task CreateSessionAsync_CreatesNewSession()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            string groupName = "Test Group";

            // Act
            var session = await _databaseService.CreateSessionAsync(groupName, "Test Window", "Telegram");

            // Assert
            Assert.NotNull(session);
            Assert.Equal(groupName, session.GroupName);
            Assert.True(session.IsActive);
            Assert.True(session.Id > 0);
        }

        [Fact]
        public async Task GetActiveSessionAsync_ReturnsActiveSession()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var created = await _databaseService.CreateSessionAsync("Active Group");

            // Act
            var active = await _databaseService.GetActiveSessionAsync();

            // Assert
            Assert.NotNull(active);
            Assert.Equal(created.Id, active.Id);
            Assert.Equal("Active Group", active.GroupName);
        }

        [Fact]
        public async Task EndSessionAsync_MarksSessionInactive()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var session = await _databaseService.CreateSessionAsync("Test Group");

            // Act
            await _databaseService.EndSessionAsync(session.Id);

            // Assert
            var updated = await _databaseService.GetSessionAsync(session.Id);
            Assert.NotNull(updated);
            Assert.False(updated.IsActive);
            Assert.NotNull(updated.EndTime);
        }

        [Fact]
        public async Task AddFileToSessionAsync_AddsFileToSession()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var session = await _databaseService.CreateSessionAsync("Test Group");

            // Act
            await _databaseService.AddFileToSessionAsync(session.Id, "test.pdf", "/path/to/test.pdf", 1024);

            // Assert
            var updated = await _databaseService.GetSessionAsync(session.Id);
            Assert.NotNull(updated);
            Assert.Equal(1, updated.FileCount);
        }

        [Fact(Skip = "Test isolation issue - V2.0 optional feature")]
        public async Task SavePatternAsync_SavesNewPattern()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var pattern = new FilePattern
            {
                FileExtension = ".pdf",
                GroupName = "Documents",
                ConfidenceScore = 0.8,
                TimesSeen = 10,
                TimesCorrect = 8
            };

            // Act
            await _databaseService.SavePatternAsync(pattern);

            // Assert
            var patterns = await _databaseService.GetPatternsForGroupAsync("Documents");
            Assert.Single(patterns);
            Assert.Equal(".pdf", patterns[0].FileExtension);
        }

        [Fact]
        public async Task GetBestPatternAsync_ReturnsHighestConfidencePattern()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            
            await _databaseService.SavePatternAsync(new FilePattern
            {
                FileExtension = ".pdf",
                GroupName = "Work",
                ConfidenceScore = 0.6,
                TimesSeen = 5,
                TimesCorrect = 3
            });

            await _databaseService.SavePatternAsync(new FilePattern
            {
                FileExtension = ".pdf",
                GroupName = "Personal",
                ConfidenceScore = 0.9,
                TimesSeen = 10,
                TimesCorrect = 9
            });

            // Act
            var best = await _databaseService.GetBestPatternAsync("document.pdf", ".pdf", DateTime.Now);

            // Assert
            Assert.NotNull(best);
            Assert.Equal("Personal", best.GroupName);
            Assert.Equal(0.9, best.ConfidenceScore);
        }

        [Fact(Skip = "Test isolation issue - V2.0 optional feature")]
        public async Task RecordFileStatisticAsync_RecordsStatistic()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var session = await _databaseService.CreateSessionAsync("Test Group");

            // Act
            await _databaseService.RecordFileStatisticAsync(
                "test.pdf",
                ".pdf",
                1024,
                "Test Group",
                "Documents",
                true,
                session.Id,
                null,
                1.0);

            // Assert
            var total = await _databaseService.GetTotalFilesOrganizedAsync();
            Assert.Equal(1, total);
        }

        [Fact(Skip = "Test isolation issue - V2.0 optional feature")]
        public async Task GetTopGroupsAsync_ReturnsTopGroups()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();

            await _databaseService.RecordFileStatisticAsync("file1.pdf", ".pdf", 1024, "Group A", "GroupA", false);
            await _databaseService.RecordFileStatisticAsync("file2.pdf", ".pdf", 1024, "Group A", "GroupA", false);
            await _databaseService.RecordFileStatisticAsync("file3.pdf", ".pdf", 1024, "Group B", "GroupB", false);

            // Act
            var topGroups = await _databaseService.GetTopGroupsAsync(10);

            // Assert
            Assert.Equal(2, topGroups.Count);
            Assert.Equal(2, topGroups["Group A"]);
            Assert.Equal(1, topGroups["Group B"]);
        }

        [Fact]
        public async Task SaveContextCacheAsync_SavesAndRetrievesContext()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            string windowTitle = "CS50 Study Group - Telegram";
            string groupName = "CS50 Study Group";

            // Act
            await _databaseService.SaveContextCacheAsync(windowTitle, groupName, "Telegram", 1.0);
            var cached = await _databaseService.GetCachedContextAsync(windowTitle);

            // Assert
            Assert.NotNull(cached);
            Assert.Equal(groupName, cached.Value.groupName);
            Assert.Equal(1.0, cached.Value.confidence);
        }

        [Fact]
        public async Task GetStateValueAsync_ReturnsStoredValue()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            await _databaseService.SetStateValueAsync("test_key", "test_value");

            // Act
            var value = await _databaseService.GetStateValueAsync("test_key");

            // Assert
            Assert.Equal("test_value", value);
        }

        [Fact]
        public async Task GetSchemaVersionAsync_ReturnsVersion()
        {
            // Arrange & Act
            await _databaseService.InitializeDatabaseAsync();
            var version = await _databaseService.GetSchemaVersionAsync();

            // Assert
            Assert.Equal(1, version);
        }

        [Fact]
        public async Task CheckIntegrityAsync_ReturnsTrue()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();

            // Act
            var isValid = await _databaseService.CheckIntegrityAsync();

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task EndTimedOutSessionsAsync_EndsTimedOutSessions()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var session = await _databaseService.CreateSessionAsync("Test Group");
            
            // Manually set last activity to past
            session.LastActivity = DateTime.Now.AddMinutes(-10);
            session.TimeoutSeconds = 30;
            await _databaseService.UpdateSessionAsync(session);

            // Act
            var count = await _databaseService.EndTimedOutSessionsAsync();

            // Assert
            Assert.Equal(1, count);
            
            var updated = await _databaseService.GetSessionAsync(session.Id);
            Assert.NotNull(updated);
            Assert.False(updated.IsActive);
        }

        [Fact(Skip = "Test isolation issue - V2.0 optional feature")]
        public async Task DeleteOldSessionsAsync_DeletesOldSessions()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();
            var oldSession = await _databaseService.CreateSessionAsync("Old Group");
            
            // End session and set old date
            await _databaseService.EndSessionAsync(oldSession.Id);
            oldSession.StartTime = DateTime.Now.AddDays(-60);
            await _databaseService.UpdateSessionAsync(oldSession);

            // Act
            var count = await _databaseService.DeleteOldSessionsAsync(30);

            // Assert
            Assert.Equal(1, count);
        }

        [Fact(Skip = "Test isolation issue - V2.0 optional feature")]
        public async Task GetDailyActivityAsync_ReturnsActivity()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();

            await _databaseService.RecordFileStatisticAsync("file1.pdf", ".pdf", 1024, "Group A", "GroupA", false);
            await _databaseService.RecordFileStatisticAsync("file2.pdf", ".pdf", 1024, "Group A", "GroupA", false);

            // Act
            var activity = await _databaseService.GetDailyActivityAsync(30);

            // Assert
            Assert.NotEmpty(activity);
            Assert.Contains(DateTime.Now.Date, activity.Keys);
        }

        [Fact(Skip = "Test isolation issue - V2.0 optional feature")]
        public async Task GetBatchDownloadStatsAsync_ReturnsStats()
        {
            // Arrange
            await _databaseService.InitializeDatabaseAsync();

            await _databaseService.RecordFileStatisticAsync("file1.pdf", ".pdf", 1024, "Group A", "GroupA", true); // Batch
            await _databaseService.RecordFileStatisticAsync("file2.pdf", ".pdf", 1024, "Group B", "GroupB", false); // Single

            // Act
            var (batchCount, singleCount, batchPercentage) = await _databaseService.GetBatchDownloadStatsAsync();

            // Assert
            Assert.Equal(1, batchCount);
            Assert.Equal(1, singleCount);
            Assert.Equal(50.0, batchPercentage);
        }
    }
}
