using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramOrganizer.Core.Contracts;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// JSON-based implementation of IPersistenceService.
    /// Saves application state to a JSON file in the app data folder.
    /// </summary>
    public class JsonPersistenceService : IPersistenceService
    {
        private readonly string _filePath;
        private readonly object _lock = new();
        private AppState _cachedState;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public JsonPersistenceService()
        {
            // Store in AppData/Local/TelegramOrganizer
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _filePath = Path.Combine(appFolder, "state.json");
        }

        public void SaveState(AppState state)
        {
            lock (_lock)
            {
                try
                {
                    state.LastSavedAt = DateTime.Now;
                    string json = JsonSerializer.Serialize(state, _jsonOptions);
                    File.WriteAllText(_filePath, json);
                    _cachedState = state;
                }
                catch (Exception ex)
                {
                    // Log error but don't crash - persistence is non-critical
                    System.Diagnostics.Debug.WriteLine($"[Persistence] Save failed: {ex.Message}");
                }
            }
        }

        public AppState LoadState()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        _cachedState = new AppState();
                        return _cachedState;
                    }

                    string json = File.ReadAllText(_filePath);
                    _cachedState = JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
                    return _cachedState;
                }
                catch (Exception ex)
                {
                    // Corrupted file - start fresh
                    System.Diagnostics.Debug.WriteLine($"[Persistence] Load failed: {ex.Message}");
                    _cachedState = new AppState();
                    return _cachedState;
                }
            }
        }

        public void AddOrUpdateEntry(string fileName, FileContext context)
        {
            lock (_lock)
            {
                if (_cachedState == null)
                {
                    _cachedState = LoadState();
                }

                _cachedState.PendingDownloads[fileName] = context;
                SaveState(_cachedState);
            }
        }

        public void RemoveEntry(string fileName)
        {
            lock (_lock)
            {
                if (_cachedState == null)
                {
                    _cachedState = LoadState();
                }

                if (_cachedState.PendingDownloads.Remove(fileName))
                {
                    _cachedState.TotalFilesOrganized++;
                    SaveState(_cachedState);
                }
            }
        }

        public int CleanupOldEntries(int retentionDays = 30)
        {
            lock (_lock)
            {
                if (_cachedState == null)
                {
                    _cachedState = LoadState();
                }

                var cutoffDate = DateTime.Now.AddDays(-retentionDays);
                var keysToRemove = new List<string>();

                foreach (var kvp in _cachedState.PendingDownloads)
                {
                    if (kvp.Value.CapturedAt < cutoffDate)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _cachedState.PendingDownloads.Remove(key);
                }

                if (keysToRemove.Count > 0)
                {
                    SaveState(_cachedState);
                }

                return keysToRemove.Count;
            }
        }
    }
}
