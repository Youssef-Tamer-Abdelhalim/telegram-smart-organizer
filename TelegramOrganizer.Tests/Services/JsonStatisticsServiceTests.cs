using TelegramOrganizer.Infra.Services;

namespace TelegramOrganizer.Tests.Services
{
    public class JsonStatisticsServiceTests
    {
        private JsonStatisticsService CreateService()
        {
            return new JsonStatisticsService();
        }

        [Fact]
        public void LoadStatistics_ReturnsValidObject()
        {
            // Arrange
            var service = CreateService();

            // Act
            var stats = service.LoadStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.NotNull(stats.TopGroups);
            Assert.NotNull(stats.FileTypeDistribution);
            Assert.NotNull(stats.DailyActivity);
        }

        [Fact]
        public void LoadStatistics_InitializesWithZeroValues()
        {
            // Arrange
            var service = CreateService();

            // Act
            var stats = service.LoadStatistics();

            // Assert
            Assert.True(stats.TotalFilesOrganized >= 0);
            Assert.True(stats.TotalSizeBytes >= 0);
        }

        [Fact]
        public void RecordFileOrganized_IncrementsFileCount()
        {
            // Arrange
            var service = CreateService();
            var initialStats = service.LoadStatistics();
            var initialCount = initialStats.TotalFilesOrganized;

            // Act
            service.RecordFileOrganized("test.pdf", "TestGroup", 1024);
            var newStats = service.LoadStatistics();

            // Assert
            Assert.Equal(initialCount + 1, newStats.TotalFilesOrganized);
        }

        [Fact]
        public void RecordFileOrganized_AddsTotalSize()
        {
            // Arrange
            var service = CreateService();
            var initialStats = service.LoadStatistics();
            var initialSize = initialStats.TotalSizeBytes;
            long fileSize = 2048;

            // Act
            service.RecordFileOrganized("test.pdf", "TestGroup", fileSize);
            var newStats = service.LoadStatistics();

            // Assert
            Assert.Equal(initialSize + fileSize, newStats.TotalSizeBytes);
        }

        [Fact]
        public void RecordFileOrganized_TracksGroupUsage()
        {
            // Arrange
            var service = CreateService();
            string groupName = "UniqueTestGroup_" + Guid.NewGuid().ToString();

            // Act
            service.RecordFileOrganized("file1.pdf", groupName, 1024);
            service.RecordFileOrganized("file2.pdf", groupName, 1024);
            var stats = service.LoadStatistics();

            // Assert
            Assert.True(stats.TopGroups.ContainsKey(groupName));
            Assert.Equal(2, stats.TopGroups[groupName]);
        }

        [Fact]
        public void RecordFileOrganized_TracksFileTypeDistribution()
        {
            // Arrange
            var service = CreateService();

            // Act
            service.RecordFileOrganized("doc1.pdf", "Group1", 1024);
            service.RecordFileOrganized("doc2.pdf", "Group1", 1024);
            service.RecordFileOrganized("image.jpg", "Group1", 2048);
            var stats = service.LoadStatistics();

            // Assert
            Assert.True(stats.FileTypeDistribution.ContainsKey(".pdf"));
            Assert.True(stats.FileTypeDistribution.ContainsKey(".jpg"));
        }

        [Fact]
        public void RecordFileOrganized_TracksDailyActivity()
        {
            // Arrange
            var service = CreateService();
            DateTime today = DateTime.Today;

            // Act
            service.RecordFileOrganized("test.pdf", "TestGroup", 1024);
            var stats = service.LoadStatistics();

            // Assert
            Assert.True(stats.DailyActivity.ContainsKey(today));
            Assert.True(stats.DailyActivity[today] >= 1);
        }

        [Fact]
        public void ClearStatistics_ResetsAllValues()
        {
            // Arrange
            var service = CreateService();
            service.RecordFileOrganized("test.pdf", "TestGroup", 1024);
            
            // Act
            service.ClearStatistics();
            var stats = service.LoadStatistics();

            // Assert
            Assert.Equal(0, stats.TotalFilesOrganized);
            Assert.Equal(0, stats.TotalSizeBytes);
            Assert.Empty(stats.TopGroups);
            Assert.Empty(stats.FileTypeDistribution);
            Assert.Empty(stats.DailyActivity);
        }

        [Fact]
        public void RecordFileOrganized_HandlesZeroFileSize()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert - Should not throw
            var exception = Record.Exception(() => 
                service.RecordFileOrganized("test.pdf", "TestGroup", 0));
            
            Assert.Null(exception);
        }

        [Fact]
        public void RecordFileOrganized_HandlesLargeFileSize()
        {
            // Arrange
            var service = CreateService();
            var initialStats = service.LoadStatistics();
            var initialSize = initialStats.TotalSizeBytes;
            long largeSize = 10L * 1024 * 1024 * 1024; // 10 GB

            // Act
            service.RecordFileOrganized("large.zip", "TestGroup", largeSize);
            var stats = service.LoadStatistics();

            // Assert
            Assert.True(stats.TotalSizeBytes >= initialSize + largeSize);
        }

        [Fact]
        public void LoadStatistics_LastUpdated_IsRecent()
        {
            // Arrange
            var service = CreateService();

            // Act
            service.RecordFileOrganized("test.pdf", "TestGroup", 1024);
            var stats = service.LoadStatistics();

            // Assert
            var timeDiff = DateTime.Now - stats.LastUpdated;
            Assert.True(timeDiff.TotalMinutes < 5, "LastUpdated should be recent");
        }

        [Fact]
        public void GetFileExtension_ReturnsCorrectExtension()
        {
            // Arrange
            var service = CreateService();

            // Act & Assert
            Assert.Equal(".pdf", service.GetFileExtension("document.pdf"));
            Assert.Equal(".jpg", service.GetFileExtension("photo.jpg"));
        }
    }
}
