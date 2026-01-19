using TelegramOrganizer.Core.Models;
using TelegramOrganizer.Infra.Services;

namespace TelegramOrganizer.Tests.Services
{
    public class JsonSettingsServiceTests
    {
        private JsonSettingsService CreateService()
        {
            return new JsonSettingsService();
        }

        [Fact]
        public void LoadSettings_ReturnsValidObject()
        {
            // Arrange
            var service = CreateService();

            // Act
            var settings = service.LoadSettings();

            // Assert
            Assert.NotNull(settings);
        }

        [Fact]
        public void LoadSettings_ReturnsValidDestinationPath()
        {
            // Arrange
            var service = CreateService();

            // Act
            var settings = service.LoadSettings();

            // Assert
            Assert.False(string.IsNullOrEmpty(settings.DestinationBasePath));
        }

        [Fact]
        public void LoadSettings_ReturnsValidDownloadsPath()
        {
            // Arrange
            var service = CreateService();

            // Act
            var settings = service.LoadSettings();

            // Assert
            Assert.False(string.IsNullOrEmpty(settings.DownloadsFolderPath));
        }

        [Fact]
        public void SaveSettings_ThenLoadSettings_PreservesRetentionDays()
        {
            // Arrange
            var service = CreateService();
            var originalSettings = service.LoadSettings();
            var originalRetention = originalSettings.RetentionDays;
            
            // Change retention to unique value
            int newRetention = 99;
            originalSettings.RetentionDays = newRetention;

            // Act
            service.SaveSettings(originalSettings);
            var loadedSettings = service.LoadSettings();

            // Assert
            Assert.Equal(newRetention, loadedSettings.RetentionDays);
            
            // Cleanup - restore original
            loadedSettings.RetentionDays = originalRetention;
            service.SaveSettings(loadedSettings);
        }

        [Fact]
        public void SaveSettings_ThenLoadSettings_PreservesMinimizeToTray()
        {
            // Arrange
            var service = CreateService();
            var settings = service.LoadSettings();
            var original = settings.MinimizeToTray;
            
            settings.MinimizeToTray = !original;

            // Act
            service.SaveSettings(settings);
            var loaded = service.LoadSettings();

            // Assert
            Assert.Equal(!original, loaded.MinimizeToTray);
            
            // Cleanup
            loaded.MinimizeToTray = original;
            service.SaveSettings(loaded);
        }

        [Fact]
        public void LoadSettings_HasPositiveRetentionDays()
        {
            // Arrange
            var service = CreateService();

            // Act
            var settings = service.LoadSettings();

            // Assert
            Assert.True(settings.RetentionDays > 0);
        }

        [Fact]
        public void SaveSettings_PreservesCustomPaths()
        {
            // Arrange
            var service = CreateService();
            var settings = service.LoadSettings();
            var originalDest = settings.DestinationBasePath;
            var originalDownloads = settings.DownloadsFolderPath;
            
            string customDest = @"D:\TestOrganized_" + Guid.NewGuid().ToString();
            settings.DestinationBasePath = customDest;

            // Act
            service.SaveSettings(settings);
            var loadedSettings = service.LoadSettings();

            // Assert
            Assert.Equal(customDest, loadedSettings.DestinationBasePath);
            
            // Cleanup
            loadedSettings.DestinationBasePath = originalDest;
            loadedSettings.DownloadsFolderPath = originalDownloads;
            service.SaveSettings(loadedSettings);
        }
    }
}
