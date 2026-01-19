using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Service for tracking and managing organization statistics.
    /// </summary>
    public interface IStatisticsService
    {
        /// <summary>
        /// Loads statistics from storage.
        /// </summary>
        OrganizationStatistics LoadStatistics();
        
        /// <summary>
        /// Saves statistics to storage.
        /// </summary>
        void SaveStatistics(OrganizationStatistics stats);
        
        /// <summary>
        /// Records a file organization event.
        /// </summary>
        /// <param name="fileName">File name</param>
        /// <param name="groupName">Group/channel name</param>
        /// <param name="fileSize">File size in bytes</param>
        void RecordFileOrganized(string fileName, string groupName, long fileSize);
        
        /// <summary>
        /// Gets the file extension from filename.
        /// </summary>
        string GetFileExtension(string fileName);
        
        /// <summary>
        /// Clears all statistics.
        /// </summary>
        void ClearStatistics();
    }
}
