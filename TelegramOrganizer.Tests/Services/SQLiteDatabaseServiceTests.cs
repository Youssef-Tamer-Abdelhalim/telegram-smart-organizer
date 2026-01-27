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
    /// Each test uses its own isolated database file for proper test isolation.
    /// Database files are stored in bin/TestDatabases and cleaned up after each test.
    /// </summary>
    public class SQLiteDatabaseServiceTests : IAsyncLifetime
    {
        private readonly SQLiteDatabaseService _databaseService;
        private readonly string _testDatabasePath;

        public SQLiteDatabaseServiceTests()
        {
            // Create unique database path for each test instance
            var testId = Guid.NewGuid().ToString("N")[..8];
            
            // Use bin/TestDatabases folder instead of temp folder for reliability
            var testFolder = Path.Combine(
                AppContext.BaseDirectory, 
                "TestDatabases"
            );
            
            if (!Directory.Exists(testFolder))
            {
                Directory.CreateDirectory(testFolder);
            }
            
            _testDatabasePath = Path.Combine(testFolder, $"test_{testId}.db");
            _databaseService = new SQLiteDatabaseService(_testDatabasePath);
        }

        public async Task InitializeAsync()
        {
            // Initialize database before each test
            await _databaseService.InitializeDatabaseAsync();
        }

        public Task DisposeAsync()
        {
            // Close connection
            _databaseService.CloseConnection();
            
            // Force garbage collection to release database file handles
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Delete the test database file
            try
            {
                if (File.Exists(_testDatabasePath))
                {
                    File.Delete(_testDatabasePath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
            
            return Task.CompletedTask;
        }

        [Fact]
        public void InitializeDatabaseAsync_CreatesDatabase()
        {
            // Assert - database should already be created by InitializeAsync
            Assert.True(File.Exists(_testDatabasePath));
        }

        [Fact]
        public async Task CreateSessionAsync_CreatesNewSession()
        {
            // Arrange
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
            var session = await _databaseService.CreateSessionAsync("Test Group");

            // Act
            await _databaseService.AddFileToSessionAsync(session.Id, "test.pdf", "/path/to/test.pdf", 1024);

            // Assert
            var updated = await _databaseService.GetSessionAsync(session.Id);
            Assert.NotNull(updated);
            Assert.Equal(1, updated.FileCount);
        }

        [Fact]
        public async Task SavePatternAsync_SavesNewPattern()
        {
            // Arrange
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

        [Fact]
        public async Task RecordFileStatisticAsync_RecordsStatistic()
        {
            // Arrange
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

        [Fact]
        public async Task GetTopGroupsAsync_ReturnsTopGroups()
        {
            // Arrange
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
            await _databaseService.SetStateValueAsync("test_key", "test_value");

            // Act
            var value = await _databaseService.GetStateValueAsync("test_key");

            // Assert
            Assert.Equal("test_value", value);
        }

        [Fact]
        public async Task GetSchemaVersionAsync_ReturnsVersion()
        {
            // Act
            var version = await _databaseService.GetSchemaVersionAsync();

            // Assert
            Assert.Equal(1, version);
        }

        [Fact]
        public async Task CheckIntegrityAsync_ReturnsTrue()
        {
            // Act
            var isValid = await _databaseService.CheckIntegrityAsync();

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task EndTimedOutSessionsAsync_EndsTimedOutSessions()
        {
            // Arrange
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

        [Fact]
        public async Task DeleteOldSessionsAsync_DeletesOldSessions()
        {
            // Arrange - Create a session with an old start time directly
            var oldSession = await _databaseService.CreateSessionAsync("Old Group");
            
            // End the session first
            await _databaseService.EndSessionAsync(oldSession.Id);
            
            // Refetch to get the updated session
            var sessionToUpdate = await _databaseService.GetSessionAsync(oldSession.Id);
            Assert.NotNull(sessionToUpdate);
            
            // Set the start time to 60 days ago (before retention period)
            sessionToUpdate.StartTime = DateTime.Now.AddDays(-60);
            await _databaseService.UpdateSessionAsync(sessionToUpdate);

            // Act - Delete sessions older than 30 days
            var count = await _databaseService.DeleteOldSessionsAsync(30);

            // Assert
            Assert.Equal(1, count);
            
            // Verify session is actually deleted
            var deletedSession = await _databaseService.GetSessionAsync(oldSession.Id);
            Assert.Null(deletedSession);
        }

        [Fact]
        public async Task GetDailyActivityAsync_ReturnsActivity()
        {
            // Arrange - Record some file statistics
            await _databaseService.RecordFileStatisticAsync("file1.pdf", ".pdf", 1024, "Group A", "GroupA", false);
            await _databaseService.RecordFileStatisticAsync("file2.pdf", ".pdf", 1024, "Group A", "GroupA", false);

            // Act
            var activity = await _databaseService.GetDailyActivityAsync(30);

            // Assert - Activity should contain today's date with 2 files
            Assert.NotEmpty(activity);
            
            // Check that we have activity for today
            var todayActivity = activity.FirstOrDefault(kvp => kvp.Key.Date == DateTime.Now.Date);
            Assert.True(todayActivity.Value >= 2, $"Expected at least 2 files for today, got {todayActivity.Value}. Activity keys: {string.Join(", ", activity.Keys.Select(k => k.ToString("yyyy-MM-dd")))}");
        }

        [Fact]
        public async Task GetBatchDownloadStatsAsync_ReturnsStats()
        {
            // Arrange
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
