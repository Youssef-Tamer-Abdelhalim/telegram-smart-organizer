using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using TelegramOrganizer.Core.Contracts;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// GitHub-based update service that checks releases for new versions.
    /// </summary>
    public class GitHubUpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILoggingService _logger;
        
        // Configure your GitHub repository here
        private const string GitHubOwner = "yourusername";
        private const string GitHubRepo = "telegram-smart-organizer";
        private const string GitHubApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

        public string CurrentVersion { get; }

        public GitHubUpdateService(ILoggingService logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "TelegramSmartOrganizer");
            
            // Get current version from assembly
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var result = new UpdateInfo
            {
                CurrentVersion = CurrentVersion,
                IsUpdateAvailable = false
            };

            try
            {
                _logger.LogDebug($"Checking for updates... Current version: {CurrentVersion}");

                var response = await _httpClient.GetAsync(GitHubApiUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"GitHub API returned: {response.StatusCode}");
                    return result;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Parse version from tag_name (e.g., "v1.0.1" -> "1.0.1")
                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string latestVersion = tagName.TrimStart('v', 'V');
                
                result.LatestVersion = latestVersion;
                result.ReleaseNotes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
                result.DownloadUrl = root.TryGetProperty("html_url", out var url) ? url.GetString() : null;
                
                if (root.TryGetProperty("published_at", out var publishedAt))
                {
                    result.ReleaseDate = DateTime.Parse(publishedAt.GetString() ?? "");
                }

                // Compare versions
                if (TryParseVersion(CurrentVersion, out var current) && 
                    TryParseVersion(latestVersion, out var latest))
                {
                    result.IsUpdateAvailable = latest > current;
                }

                _logger.LogDebug($"Update check complete. Latest: {latestVersion}, Update available: {result.IsUpdateAvailable}");
                
                // Try to get direct download URL for installer
                if (result.IsUpdateAvailable && root.TryGetProperty("assets", out var assets))
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                        {
                            result.DownloadUrl = asset.GetProperty("browser_download_url").GetString();
                            break;
                        }
                    }
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogDebug($"Network error checking for updates: {ex.Message}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to check for updates", ex);
                return result;
            }
        }

        public async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            try
            {
                _logger.LogInfo($"Downloading update from: {downloadUrl}");

                var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var downloadPath = Path.Combine(Path.GetTempPath(), "TelegramSmartOrganizer_Update.exe");

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[8192];
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var percentage = (int)((totalBytesRead * 100L) / totalBytes);
                        progress.Report(percentage);
                    }
                }

                _logger.LogInfo($"Update downloaded to: {downloadPath}");
                return downloadPath;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to download update", ex);
                return null;
            }
        }

        private static bool TryParseVersion(string versionString, out Version version)
        {
            // Handle versions like "1.0.0" or "1.0"
            var parts = versionString.Split('.');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out int major) &&
                    int.TryParse(parts[1], out int minor))
                {
                    int build = parts.Length > 2 && int.TryParse(parts[2], out int b) ? b : 0;
                    version = new Version(major, minor, build);
                    return true;
                }
            }
            
            version = new Version(0, 0, 0);
            return false;
        }
    }
}
