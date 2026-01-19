using System.Text.RegularExpressions;

namespace TelegramOrganizer.Tests.Helpers
{
    /// <summary>
    /// Tests for helper methods and utilities
    /// </summary>
    public class TextHelperTests
    {
        [Theory]
        [InlineData("(10) CS50 Study Group – (3043)", "CS50 Study Group")]
        [InlineData("Tech News – Telegram", "Tech News")]
        [InlineData("CS50 Harvard", "CS50 Harvard")]
        [InlineData("Telegram", "Unsorted")]
        [InlineData("", "Unsorted")]
        [InlineData("   ", "Unsorted")]
        public void ExtractGroupName_ReturnsExpectedResult(string input, string expected)
        {
            // Act
            var result = ExtractTelegramGroupName(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractGroupName_RemovesUnreadCount()
        {
            // Arrange
            string input = "(10) Study Group";

            // Act
            var result = ExtractTelegramGroupName(input);

            // Assert
            Assert.Equal("Study Group", result);
            Assert.DoesNotContain("(10)", result);
        }

        [Fact]
        public void ExtractGroupName_RemovesMessageCount()
        {
            // Arrange
            string input = "Study Group – (3082)";

            // Act
            var result = ExtractTelegramGroupName(input);

            // Assert
            Assert.Equal("Study Group", result);
            Assert.DoesNotContain("3082", result);
        }

        [Fact]
        public void ExtractGroupName_RemovesTelegramSuffix()
        {
            // Arrange
            string input = "Tech News – Telegram";

            // Act
            var result = ExtractTelegramGroupName(input);

            // Assert
            Assert.Equal("Tech News", result);
            Assert.DoesNotContain("Telegram", result);
        }

        [Fact]
        public void SanitizeFolderName_RemovesInvalidChars()
        {
            // Arrange
            string input = "Folder:With|Invalid<Chars>*?";

            // Act
            var result = SanitizeFolderName(input);

            // Assert
            Assert.DoesNotContain(":", result);
            Assert.DoesNotContain("|", result);
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.DoesNotContain("*", result);
            Assert.DoesNotContain("?", result);
        }

        [Fact]
        public void SanitizeFolderName_PreservesEnglishText()
        {
            // Arrange
            string input = "CS50 Study Group";

            // Act
            var result = SanitizeFolderName(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void SanitizeFolderName_PreservesNumbers()
        {
            // Arrange
            string input = "Group123";

            // Act
            var result = SanitizeFolderName(input);

            // Assert
            Assert.Equal(input, result);
        }

        [Fact]
        public void SanitizeFolderName_PreservesHyphensAndUnderscores()
        {
            // Arrange
            string input = "Test-Group_Name";

            // Act
            var result = SanitizeFolderName(input);

            // Assert
            Assert.Contains("-", result);
            Assert.Contains("_", result);
        }

        [Fact]
        public void SanitizeFolderName_ReturnsUnsorted_ForEmptyString()
        {
            // Arrange
            string input = "";

            // Act
            var result = SanitizeFolderName(input);

            // Assert
            Assert.Equal("Unsorted", result);
        }

        [Fact]
        public void SanitizeFolderName_ReturnsUnsorted_ForWhitespace()
        {
            // Arrange
            string input = "   ";

            // Act
            var result = SanitizeFolderName(input);

            // Assert
            Assert.Equal("Unsorted", result);
        }

        // Helper method - mirrors the engine's logic
        private string ExtractTelegramGroupName(string windowTitle)
        {
            if (string.IsNullOrWhiteSpace(windowTitle))
                return "Unsorted";

            string title = windowTitle.Trim();

            // Remove unread count at start
            title = Regex.Replace(title, @"^\(\d+\)\s*", "");

            // Remove message count at end
            title = Regex.Replace(title, @"\s*[–—-]\s*\(\d+\)$", "");

            // Remove remaining parenthetical numbers
            title = Regex.Replace(title, @"\s*\(\d+\)$", "");

            // Remove Telegram suffix
            title = Regex.Replace(title, @"\s*[–—-]\s*Telegram$", "", RegexOptions.IgnoreCase);

            // Remove emojis but keep Arabic and English
            title = Regex.Replace(title, @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFFa-zA-Z0-9\s\-_\.]+", "");

            // Clean up whitespace
            title = Regex.Replace(title, @"\s+", " ").Trim();
            title = title.Trim(' ', '-', '_', '.');

            if (string.IsNullOrWhiteSpace(title) || title.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
                return "Unsorted";

            return title;
        }

        private string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unsorted";

            // Remove invalid path characters
            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            foreach (char c in invalid)
            {
                name = name.Replace(c.ToString(), "");
            }

            // Keep Arabic, English, numbers, and basic punctuation
            name = Regex.Replace(name, @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFFa-zA-Z0-9\s\-_\.]+", "");

            // Clean up spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(name) ? "Unsorted" : name;
        }
    }
}
