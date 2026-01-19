using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using TelegramOrganizer.Core.Contracts;

namespace TelegramOrganizer.Infra.Services
{
    /// <summary>
    /// Local file-based error reporting service.
    /// </summary>
    public class ErrorReportingService : IErrorReportingService
    {
        private readonly string _errorLogsPath;
        private readonly string _errorReportsFile;
        private readonly ILoggingService _logger;
        private readonly object _lock = new();
        private List<ErrorReport> _cachedReports;

        public ErrorReportingService(ILoggingService logger)
        {
            _logger = logger;
            
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _errorLogsPath = Path.Combine(appDataPath, "TelegramOrganizer", "ErrorLogs");
            _errorReportsFile = Path.Combine(_errorLogsPath, "error_reports.json");

            if (!Directory.Exists(_errorLogsPath))
            {
                Directory.CreateDirectory(_errorLogsPath);
            }

            _cachedReports = LoadReports();
        }

        public void ReportError(Exception exception, string source, Dictionary<string, string>? additionalData = null)
        {
            try
            {
                var report = new ErrorReport
                {
                    Timestamp = DateTime.Now,
                    Source = source,
                    Message = exception.Message,
                    ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                    StackTrace = exception.StackTrace,
                    InnerException = exception.InnerException?.ToString(),
                    AppVersion = GetAppVersion(),
                    OsVersion = Environment.OSVersion.ToString(),
                    AdditionalData = additionalData ?? new Dictionary<string, string>()
                };

                // Add to cached reports
                lock (_lock)
                {
                    _cachedReports.Add(report);
                    
                    // Keep only last 100 reports
                    if (_cachedReports.Count > 100)
                    {
                        _cachedReports = _cachedReports.OrderByDescending(r => r.Timestamp).Take(100).ToList();
                    }
                    
                    SaveReports();
                }

                // Also write to a daily error file
                WriteToDailyErrorFile(report);

                _logger.LogError($"Error reported: [{source}] {exception.Message}");
            }
            catch (Exception ex)
            {
                // Don't let error reporting cause more errors
                _logger.LogError($"Failed to report error: {ex.Message}");
            }
        }

        public List<ErrorReport> GetErrorReports()
        {
            lock (_lock)
            {
                return _cachedReports.OrderByDescending(r => r.Timestamp).ToList();
            }
        }

        public List<ErrorReport> GetRecentErrors(int days = 7)
        {
            var cutoff = DateTime.Now.AddDays(-days);
            lock (_lock)
            {
                return _cachedReports
                    .Where(r => r.Timestamp >= cutoff)
                    .OrderByDescending(r => r.Timestamp)
                    .ToList();
            }
        }

        public void ClearErrorReports()
        {
            lock (_lock)
            {
                _cachedReports.Clear();
                SaveReports();
            }

            _logger.LogInfo("Error reports cleared");
        }

        public string GetErrorLogsPath()
        {
            return _errorLogsPath;
        }

        private List<ErrorReport> LoadReports()
        {
            try
            {
                if (File.Exists(_errorReportsFile))
                {
                    string json = File.ReadAllText(_errorReportsFile);
                    return JsonSerializer.Deserialize<List<ErrorReport>>(json) ?? new List<ErrorReport>();
                }
            }
            catch
            {
                // If file is corrupted, start fresh
            }
            
            return new List<ErrorReport>();
        }

        private void SaveReports()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_cachedReports, options);
                File.WriteAllText(_errorReportsFile, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        private void WriteToDailyErrorFile(ErrorReport report)
        {
            try
            {
                string dailyFile = Path.Combine(_errorLogsPath, $"errors_{DateTime.Now:yyyy-MM-dd}.txt");
                
                string entry = $"""
                    ================================================================================
                    Timestamp: {report.Timestamp:yyyy-MM-dd HH:mm:ss.fff}
                    Source: {report.Source}
                    Type: {report.ExceptionType}
                    Message: {report.Message}
                    App Version: {report.AppVersion}
                    OS: {report.OsVersion}
                    
                    Stack Trace:
                    {report.StackTrace ?? "N/A"}
                    
                    Inner Exception:
                    {report.InnerException ?? "N/A"}
                    
                    """;

                File.AppendAllText(dailyFile, entry);
            }
            catch
            {
                // Ignore
            }
        }

        private string GetAppVersion()
        {
            try
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
