using Moq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;

namespace TelegramOrganizer.Tests.Services
{
    public class FileOrganizerServiceTests : IDisposable
    {
        private readonly Mock<ISettingsService> _mockSettings;
        private readonly Mock<IRulesService> _mockRulesService;
        private readonly Mock<IStatisticsService> _mockStatisticsService;
        private readonly FileOrganizerService _service;
        private readonly string _testDirectory;
        private readonly string _destinationDirectory;

        public FileOrganizerServiceTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "TelegramOrganizerTests", Guid.NewGuid().ToString());
            _destinationDirectory = Path.Combine(_testDirectory, "Organized");
            
            Directory.CreateDirectory(_testDirectory);
            Directory.CreateDirectory(_destinationDirectory);

            _mockSettings = new Mock<ISettingsService>();
            _mockSettings.Setup(s => s.LoadSettings()).Returns(new AppSettings
            {
                DestinationBasePath = _destinationDirectory
            });

            _mockRulesService = new Mock<IRulesService>();
            _mockRulesService.Setup(r => r.FindMatchingRule(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()))
                .Returns((OrganizationRule?)null);

            _mockStatisticsService = new Mock<IStatisticsService>();

            _service = new FileOrganizerService(
                _mockSettings.Object,
                _mockRulesService.Object,
                _mockStatisticsService.Object);
        }

        public void Dispose()
        {
            // Cleanup test directories
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }

        [Fact]
        public void OrganizeFile_MovesFileToCorrectFolder()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "test.pdf");
            File.WriteAllText(sourceFile, "test content");
            string groupName = "TestGroup";

            // Act
            string result = _service.OrganizeFile(sourceFile, groupName);

            // Assert
            Assert.Contains("Moved to", result);
            Assert.False(File.Exists(sourceFile), "Source file should be moved");
            
            string expectedPath = Path.Combine(_destinationDirectory, groupName, "test.pdf");
            Assert.True(File.Exists(expectedPath), "File should exist in destination");
        }

        [Fact]
        public void OrganizeFile_CreatesGroupFolder_WhenNotExists()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "document.docx");
            File.WriteAllText(sourceFile, "test content");
            string groupName = "NewGroup";

            // Act
            _service.OrganizeFile(sourceFile, groupName);

            // Assert
            string expectedFolder = Path.Combine(_destinationDirectory, groupName);
            Assert.True(Directory.Exists(expectedFolder), "Group folder should be created");
        }

        [Fact]
        public void OrganizeFile_HandlesFileNotFound()
        {
            // Arrange
            string nonExistentFile = Path.Combine(_testDirectory, "nonexistent.pdf");

            // Act
            string result = _service.OrganizeFile(nonExistentFile, "TestGroup");

            // Assert
            Assert.Contains("Error", result);
            Assert.Contains("not found", result);
        }

        [Fact]
        public void OrganizeFile_HandlesDuplicateFileName()
        {
            // Arrange
            string groupName = "DuplicateTest";
            string destFolder = Path.Combine(_destinationDirectory, groupName);
            Directory.CreateDirectory(destFolder);
            
            // Create existing file in destination
            File.WriteAllText(Path.Combine(destFolder, "duplicate.pdf"), "existing content");
            
            // Create source file
            string sourceFile = Path.Combine(_testDirectory, "duplicate.pdf");
            File.WriteAllText(sourceFile, "new content");

            // Act
            string result = _service.OrganizeFile(sourceFile, groupName);

            // Assert
            Assert.Contains("Moved to", result);
            Assert.True(File.Exists(Path.Combine(destFolder, "duplicate.pdf")));
            Assert.True(File.Exists(Path.Combine(destFolder, "duplicate (1).pdf")));
        }

        [Fact]
        public void OrganizeFile_SanitizesGroupName_WithInvalidChars()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "test.txt");
            File.WriteAllText(sourceFile, "test");
            string invalidGroupName = "Group:Name";

            // Act
            string result = _service.OrganizeFile(sourceFile, invalidGroupName);

            // Assert
            Assert.Contains("Moved to", result);
            Assert.False(File.Exists(sourceFile));
        }

        [Fact]
        public void OrganizeFile_HandlesEnglishGroupName()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "english_test.pdf");
            File.WriteAllText(sourceFile, "test content");
            string groupName = "CS50 Study Group";

            // Act
            string result = _service.OrganizeFile(sourceFile, groupName);

            // Assert
            Assert.Contains("Moved to", result);
            Assert.False(File.Exists(sourceFile), "Source file should be moved");
            
            string expectedFolder = Path.Combine(_destinationDirectory, groupName);
            Assert.True(Directory.Exists(expectedFolder), "English folder should be created");
        }

        [Fact]
        public void OrganizeFile_HandlesEmptyGroupName()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "empty_group.pdf");
            File.WriteAllText(sourceFile, "test content");

            // Act
            string result = _service.OrganizeFile(sourceFile, "");

            // Assert
            Assert.Contains("Moved to", result);
            string expectedFolder = Path.Combine(_destinationDirectory, "Unsorted");
            Assert.True(Directory.Exists(expectedFolder));
        }

        [Fact]
        public void OrganizeFile_HandlesWhitespaceGroupName()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "whitespace_group.pdf");
            File.WriteAllText(sourceFile, "test content");

            // Act
            string result = _service.OrganizeFile(sourceFile, "   ");

            // Assert
            Assert.Contains("Moved to", result);
            string expectedFolder = Path.Combine(_destinationDirectory, "Unsorted");
            Assert.True(Directory.Exists(expectedFolder));
        }

        [Fact]
        public void OrganizeFile_HandlesSpecialCharacters()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "special.pdf");
            File.WriteAllText(sourceFile, "test content");
            string groupName = "Test-Group_123";

            // Act
            string result = _service.OrganizeFile(sourceFile, groupName);

            // Assert
            Assert.Contains("Moved to", result);
            Assert.False(File.Exists(sourceFile));
        }

        [Fact]
        public void OrganizeFile_RecordsStatistics()
        {
            // Arrange
            string sourceFile = Path.Combine(_testDirectory, "stats_test.pdf");
            File.WriteAllText(sourceFile, "test content");
            string groupName = "StatsGroup";

            // Act
            _service.OrganizeFile(sourceFile, groupName);

            // Assert
            _mockStatisticsService.Verify(
                s => s.RecordFileOrganized(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>()), 
                Times.Once);
        }

        [Fact]
        public void OrganizeFile_UsesMatchingRule_WhenFound()
        {
            // Arrange
            var rule = new OrganizationRule
            {
                Id = Guid.NewGuid().ToString(),
                Name = "PDF Rule",
                TargetFolder = "PDFs",
                TimesApplied = 0
            };
            
            _mockRulesService.Setup(r => r.FindMatchingRule("rule_test.pdf", "AnyGroup", It.IsAny<long>()))
                .Returns(rule);

            string sourceFile = Path.Combine(_testDirectory, "rule_test.pdf");
            File.WriteAllText(sourceFile, "test content");

            // Act
            string result = _service.OrganizeFile(sourceFile, "AnyGroup");

            // Assert
            Assert.Contains("Rule: PDF Rule", result);
            string expectedFolder = Path.Combine(_destinationDirectory, "PDFs");
            Assert.True(Directory.Exists(expectedFolder));
            
            _mockRulesService.Verify(r => r.UpdateRule(It.IsAny<OrganizationRule>()), Times.Once);
        }
    }
}
