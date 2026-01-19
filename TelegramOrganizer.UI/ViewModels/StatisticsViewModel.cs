using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.UI.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly IStatisticsService _statisticsService;

        [ObservableProperty]
        private int _totalFilesOrganized;

        [ObservableProperty]
        private string _totalSizeFormatted = "0 B";

        [ObservableProperty]
        private ObservableCollection<GroupStat> _topGroups = new();

        [ObservableProperty]
        private ObservableCollection<FileTypeStat> _fileTypes = new();

        [ObservableProperty]
        private ObservableCollection<DailyActivityStat> _dailyActivity = new();

        [ObservableProperty]
        private string _lastUpdated = "Never";

        public StatisticsViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            LoadStatistics();
        }

        [RelayCommand]
        private void Refresh()
        {
            LoadStatistics();
        }

        [RelayCommand]
        private void ClearStats()
        {
            _statisticsService.ClearStatistics();
            LoadStatistics();
        }

        private void LoadStatistics()
        {
            var stats = _statisticsService.LoadStatistics();

            TotalFilesOrganized = stats.TotalFilesOrganized;
            TotalSizeFormatted = FormatBytes(stats.TotalSizeBytes);
            LastUpdated = stats.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");

            // Top Groups
            TopGroups = new ObservableCollection<GroupStat>(
                stats.TopGroups
                    .OrderByDescending(x => x.Value)
                    .Take(10)
                    .Select((x, i) => new GroupStat 
                    { 
                        Rank = i + 1,
                        Name = x.Key, 
                        Count = x.Value,
                        Percentage = stats.TotalFilesOrganized > 0 
                            ? (double)x.Value / stats.TotalFilesOrganized * 100 
                            : 0
                    })
            );

            // File Types
            FileTypes = new ObservableCollection<FileTypeStat>(
                stats.FileTypeDistribution
                    .OrderByDescending(x => x.Value)
                    .Take(10)
                    .Select(x => new FileTypeStat 
                    { 
                        Extension = x.Key, 
                        Count = x.Value,
                        Percentage = stats.TotalFilesOrganized > 0 
                            ? (double)x.Value / stats.TotalFilesOrganized * 100 
                            : 0
                    })
            );

            // Daily Activity (last 7 days)
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Now.Date.AddDays(-i))
                .Reverse()
                .ToList();

            DailyActivity = new ObservableCollection<DailyActivityStat>(
                last7Days.Select(date => new DailyActivityStat
                {
                    Date = date.ToString("MM/dd"),
                    DayName = date.ToString("ddd"),
                    Count = stats.DailyActivity.TryGetValue(date, out int count) ? count : 0
                })
            );
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }

    public class GroupStat
    {
        public int Rank { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class FileTypeStat
    {
        public string Extension { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    public class DailyActivityStat
    {
        public string Date { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
