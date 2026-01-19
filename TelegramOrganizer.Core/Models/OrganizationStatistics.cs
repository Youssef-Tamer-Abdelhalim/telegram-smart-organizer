using System;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Statistics about file organization.
    /// </summary>
    public class OrganizationStatistics
    {
        /// <summary>Total files organized since installation</summary>
        public int TotalFilesOrganized { get; set; } = 0;
        
        /// <summary>Total size of organized files in bytes</summary>
        public long TotalSizeBytes { get; set; } = 0;
        
        /// <summary>Most used groups (top 10)</summary>
        public Dictionary<string, int> TopGroups { get; set; } = new();
        
        /// <summary>File types distribution</summary>
        public Dictionary<string, int> FileTypeDistribution { get; set; } = new();
        
        /// <summary>Files organized per day (last 30 days)</summary>
        public Dictionary<DateTime, int> DailyActivity { get; set; } = new();
        
        /// <summary>Last updated timestamp</summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
