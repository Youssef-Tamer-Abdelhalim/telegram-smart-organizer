using System;
using System.IO;
using System.Text;
using TelegramOrganizer.Core.Contracts;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// File-based logging service for debugging.
    /// Writes detailed logs to a file in AppData.
    /// </summary>
    public class FileLoggingService : ILoggingService
    {
        private readonly string _logFilePath;
        private readonly object _lock = new();

        public FileLoggingService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(appDataPath, "TelegramOrganizer");
            
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            // Create a new log file for each day
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            _logFilePath = Path.Combine(appFolder, $"log_{date}.txt");
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogDebug(string message)
        {
            WriteLog("DEBUG", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            var sb = new StringBuilder();
            sb.Append(message);
            
            if (exception != null)
            {
                sb.AppendLine();
                sb.AppendLine($"  Exception: {exception.GetType().Name}");
                sb.AppendLine($"  Message: {exception.Message}");
                sb.AppendLine($"  StackTrace: {exception.StackTrace}");
            }
            
            WriteLog("ERROR", sb.ToString());
        }

        public void LogFileOperation(string operation, string fileName, string? oldFileName = null, 
            string? groupName = null, string? additionalInfo = null)
        {
            var sb = new StringBuilder();
            sb.Append($"[{operation}] ");
            sb.Append($"File: {fileName}");
            
            if (!string.IsNullOrEmpty(oldFileName))
            {
                sb.Append($" | OldName: {oldFileName}");
            }
            
            if (!string.IsNullOrEmpty(groupName))
            {
                sb.Append($" | Group: {groupName}");
            }
            
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                sb.Append($" | {additionalInfo}");
            }

            WriteLog("FILE", sb.ToString());
        }

        private void WriteLog(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    string threadId = Environment.CurrentManagedThreadId.ToString().PadLeft(3);
                    string logLine = $"[{timestamp}] [{threadId}] [{level,-5}] {message}";
                    
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                    
                    // Also write to Debug output for IDE
                    System.Diagnostics.Debug.WriteLine(logLine);
                }
                catch
                {
                    // Ignore logging errors
                }
            }
        }

        /// <summary>
        /// Gets the path to the current log file.
        /// </summary>
        public string GetLogFilePath() => _logFilePath;
    }
}
