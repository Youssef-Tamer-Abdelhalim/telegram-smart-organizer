using System;
using System.IO;
using System.Text.RegularExpressions;
using TelegramOrganizer.Core.Contracts;

namespace TelegramOrganizer.Infra.Services
{
    public class FileOrganizerService : IFileOrganizer
    {
        private readonly ISettingsService _settingsService;
        private readonly IRulesService _rulesService;
        private readonly IStatisticsService _statisticsService;

        public FileOrganizerService(
            ISettingsService settingsService,
            IRulesService rulesService,
            IStatisticsService statisticsService)
        {
            _settingsService = settingsService;
            _rulesService = rulesService;
            _statisticsService = statisticsService;
        }

        public string OrganizeFile(string filePath, string groupName)
        {
            try
            {
                if (!File.Exists(filePath)) return "Error: File not found.";

                // Get current settings to use the configured destination path
                var settings = _settingsService.LoadSettings();
                string baseDestination = settings.DestinationBasePath;
                
                // Get file info for statistics and rules
                var fileInfo = new FileInfo(filePath);
                string fileName = fileInfo.Name;
                long fileSize = fileInfo.Length;

                // 1. Check if any custom rule matches this file
                string targetFolder = groupName;
                var matchingRule = _rulesService.FindMatchingRule(fileName, groupName, fileSize);
                
                if (matchingRule != null)
                {
                    // Rule matched - use rule's target folder
                    targetFolder = matchingRule.TargetFolder;
                    
                    // Update rule statistics
                    matchingRule.TimesApplied++;
                    _rulesService.UpdateRule(matchingRule);
                }

                // 2. Sanitize folder name
                string safeFolderName = SanitizeFolderName(targetFolder);
                if (string.IsNullOrWhiteSpace(safeFolderName)) safeFolderName = "Unsorted";

                // 3. Create destination folder
                string destinationFolder = Path.Combine(baseDestination, safeFolderName);
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                // 4. Get unique file path
                string destPath = Path.Combine(destinationFolder, fileName);
                destPath = GetUniqueFilePath(destPath);

                // 5. Move the file
                File.Move(filePath, destPath);

                // 6. Record statistics
                _statisticsService.RecordFileOrganized(fileName, safeFolderName, fileSize);

                string resultMessage = matchingRule != null 
                    ? $"Moved to: {safeFolderName} (Rule: {matchingRule.Name})"
                    : $"Moved to: {safeFolderName}";

                return resultMessage;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Sanitizes folder name for use in file system.
        /// Handles Arabic, English, and mixed text properly.
        /// </summary>
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

            // Remove emojis and symbols but keep Arabic, English, numbers, and basic punctuation
            name = Regex.Replace(name, @"[^\u0600-\u06FF\u0750-\u077F\uFB50-\uFDFF\uFE70-\uFEFFa-zA-Z0-9\s\-_\.]+", "");

            // Clean up multiple spaces
            name = Regex.Replace(name, @"\s+", " ");

            // Trim leading/trailing whitespace and special chars
            name = name.Trim(' ', '-', '_', '.');

            // If empty after cleaning, return Unsorted
            if (string.IsNullOrWhiteSpace(name))
                return "Unsorted";

            // Limit length to 100 characters
            if (name.Length > 100)
                name = name.Substring(0, 100).Trim();

            return name;
        }

        /// <summary>
        /// Gets a unique file path by adding (1), (2), etc. if file exists.
        /// </summary>
        private string GetUniqueFilePath(string path)
        {
            if (!File.Exists(path)) return path;

            string folder = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            string ext = Path.GetExtension(path);
            int count = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(folder, $"{name} ({count}){ext}");
                count++;
            } while (File.Exists(newPath));

            return newPath;
        }
    }
}