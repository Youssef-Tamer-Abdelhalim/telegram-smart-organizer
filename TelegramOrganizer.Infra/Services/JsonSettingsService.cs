using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// JSON-based implementation of ISettingsService.
    /// Saves user settings to a JSON file in AppData.
    /// </summary>
    public class JsonSettingsService : ISettingsService
    {
        private readonly string _filePath;
        private readonly object _lock = new();
        private AppSettings _cachedSettings;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public event EventHandler<AppSettings> SettingsChanged;

        public JsonSettingsService()
        {
            // Store in AppData/Local/TelegramOrganizer
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _filePath = Path.Combine(appFolder, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        // Create default settings
                        _cachedSettings = new AppSettings();
                        SaveSettings(_cachedSettings);
                        return _cachedSettings;
                    }

                    string json = File.ReadAllText(_filePath);
                    _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    // Corrupted settings - use defaults
                    System.Diagnostics.Debug.WriteLine($"[Settings] Load failed: {ex.Message}");
                    _cachedSettings = new AppSettings();
                    return _cachedSettings;
                }
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(settings, _jsonOptions);
                    File.WriteAllText(_filePath, json);
                    _cachedSettings = settings;

                    // Raise event to notify subscribers
                    SettingsChanged?.Invoke(this, settings);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Settings] Save failed: {ex.Message}");
                }
            }
        }
    }
}
