using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// JSON-based implementation of IStatisticsService.
    /// </summary>
    public class JsonStatisticsService : IStatisticsService
    {
        private readonly string _statsFilePath;
        private readonly object _lock = new();
        private OrganizationStatistics _cachedStats;

        public JsonStatisticsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _statsFilePath = Path.Combine(appFolder, "statistics.json");
            _cachedStats = LoadStatistics();
        }

        public OrganizationStatistics LoadStatistics()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_statsFilePath))
                    {
                        var newStats = new OrganizationStatistics();
                        SaveStatistics(newStats);
                        return newStats;
                    }

                    string json = File.ReadAllText(_statsFilePath);
                    var stats = JsonSerializer.Deserialize<OrganizationStatistics>(json);
                    _cachedStats = stats ?? new OrganizationStatistics();
                    return _cachedStats;
                }
                catch
                {
                    return new OrganizationStatistics();
                }
            }
        }

        public void SaveStatistics(OrganizationStatistics stats)
        {
            lock (_lock)
            {
                try
                {
                    stats.LastUpdated = DateTime.Now;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(stats, options);
                    File.WriteAllText(_statsFilePath, json);
                    _cachedStats = stats;
                }
                catch
                {
                    // Log error
                }
            }
        }

        public void RecordFileOrganized(string fileName, string groupName, long fileSize)
        {
            var stats = LoadStatistics();

            // Increment total
            stats.TotalFilesOrganized++;
            stats.TotalSizeBytes += fileSize;

            // Update group statistics
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                if (stats.TopGroups.ContainsKey(groupName))
                {
                    stats.TopGroups[groupName]++;
                }
                else
                {
                    stats.TopGroups[groupName] = 1;
                }

                // Keep only top 20
                if (stats.TopGroups.Count > 20)
                {
                    var sorted = stats.TopGroups.OrderByDescending(x => x.Value).Take(20);
                    stats.TopGroups = sorted.ToDictionary(x => x.Key, x => x.Value);
                }
            }

            // Update file type statistics
            string extension = GetFileExtension(fileName);
            if (!string.IsNullOrWhiteSpace(extension))
            {
                if (stats.FileTypeDistribution.ContainsKey(extension))
                {
                    stats.FileTypeDistribution[extension]++;
                }
                else
                {
                    stats.FileTypeDistribution[extension] = 1;
                }
            }

            // Update daily activity
            var today = DateTime.Now.Date;
            if (stats.DailyActivity.ContainsKey(today))
            {
                stats.DailyActivity[today]++;
            }
            else
            {
                stats.DailyActivity[today] = 1;
            }

            // Keep only last 30 days
            var cutoffDate = DateTime.Now.Date.AddDays(-30);
            var recentDays = stats.DailyActivity
                .Where(x => x.Key >= cutoffDate)
                .OrderBy(x => x.Key)
                .ToDictionary(x => x.Key, x => x.Value);
            stats.DailyActivity = recentDays;

            SaveStatistics(stats);
        }

        public string GetFileExtension(string fileName)
        {
            try
            {
                string ext = Path.GetExtension(fileName);
                return string.IsNullOrWhiteSpace(ext) ? "No Extension" : ext.ToLower();
            }
            catch
            {
                return "Unknown";
            }
        }

        public void ClearStatistics()
        {
            SaveStatistics(new OrganizationStatistics());
        }
    }
}
