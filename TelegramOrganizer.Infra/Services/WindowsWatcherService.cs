using System;
using System.IO;
using TelegramOrganizer.Core.Contracts;

namespace TelegramOrganizer.Infra.Services
{
    public class WindowsWatcherService : IFileWatcher, IDisposable
    {
        private FileSystemWatcher? _watcher;

        public event EventHandler<FileEventArgs>? FileCreated;
        public event EventHandler<FileEventArgs>? FileRenamed;

        public void Start(string path)
        {
            if (_watcher != null) Stop();

            // التحقق من وجود المجلد - وإنشائه لو مش موجود
            if (!Directory.Exists(path))
            {
                System.Diagnostics.Debug.WriteLine($"[FileWatcher] Downloads folder not found, creating: {path}");
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileWatcher] Failed to create folder: {ex.Message}");
                    throw new DirectoryNotFoundException($"Downloads folder not found and could not be created: {path}");
                }
            }

            // إعداد المراقب
            _watcher = new FileSystemWatcher
            {
                Path = path,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                Filter = "*.*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = false
            };

            // الاشتراك في أحداث الويندوز
            _watcher.Created += OnCreated;
            _watcher.Renamed += OnRenamed;

            System.Diagnostics.Debug.WriteLine($"[FileWatcher] Started watching: {path}");
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnCreated;
                _watcher.Renamed -= OnRenamed;
                _watcher.Dispose();
                _watcher = null;
                
                System.Diagnostics.Debug.WriteLine("[FileWatcher] Stopped");
            }
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FileWatcher] Created: {e.Name} at {e.FullPath}");
            
            FileCreated?.Invoke(this, new FileEventArgs
            {
                FileName = e.Name ?? "",
                FullPath = e.FullPath
            });
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FileWatcher] Renamed: {e.OldName} -> {e.Name}");
            
            FileRenamed?.Invoke(this, new FileEventArgs
            {
                FileName = e.Name ?? "",
                FullPath = e.FullPath,
                OldFileName = e.OldName
            });
        }

        public void Dispose()
        {
            Stop();
        }
    }
}