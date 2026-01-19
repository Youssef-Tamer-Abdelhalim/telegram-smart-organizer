namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Information about an available update.
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>Whether an update is available.</summary>
        public bool IsUpdateAvailable { get; set; }
        
        /// <summary>Current installed version.</summary>
        public string CurrentVersion { get; set; } = string.Empty;
        
        /// <summary>Latest available version.</summary>
        public string LatestVersion { get; set; } = string.Empty;
        
        /// <summary>URL to download the update.</summary>
        public string? DownloadUrl { get; set; }
        
        /// <summary>Release notes or changelog.</summary>
        public string? ReleaseNotes { get; set; }
        
        /// <summary>Release date.</summary>
        public DateTime? ReleaseDate { get; set; }
    }

    /// <summary>
    /// Service for checking and handling application updates.
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Gets the current application version.
        /// </summary>
        string CurrentVersion { get; }

        /// <summary>
        /// Checks if a newer version is available.
        /// </summary>
        /// <returns>Information about available updates.</returns>
        Task<UpdateInfo> CheckForUpdatesAsync();

        /// <summary>
        /// Downloads the latest update.
        /// </summary>
        /// <param name="downloadUrl">URL to download from.</param>
        /// <param name="progress">Progress reporter (0-100).</param>
        /// <returns>Path to the downloaded file.</returns>
        Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<int>? progress = null);
    }
}
