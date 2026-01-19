namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Represents an error report entry.
    /// </summary>
    public class ErrorReport
    {
        /// <summary>Unique identifier for the error.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>When the error occurred.</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        /// <summary>Source of the error (e.g., "UI Thread", "Background Task").</summary>
        public string Source { get; set; } = string.Empty;
        
        /// <summary>Error message.</summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>Exception type name.</summary>
        public string ExceptionType { get; set; } = string.Empty;
        
        /// <summary>Full stack trace.</summary>
        public string? StackTrace { get; set; }
        
        /// <summary>Inner exception details if any.</summary>
        public string? InnerException { get; set; }
        
        /// <summary>Application version when error occurred.</summary>
        public string AppVersion { get; set; } = string.Empty;
        
        /// <summary>Operating system information.</summary>
        public string OsVersion { get; set; } = string.Empty;
        
        /// <summary>Additional context data.</summary>
        public Dictionary<string, string> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Service for reporting and tracking application errors.
    /// </summary>
    public interface IErrorReportingService
    {
        /// <summary>
        /// Reports an exception.
        /// </summary>
        /// <param name="exception">The exception to report.</param>
        /// <param name="source">Where the error originated.</param>
        /// <param name="additionalData">Optional additional context.</param>
        void ReportError(Exception exception, string source, Dictionary<string, string>? additionalData = null);

        /// <summary>
        /// Gets all recorded error reports.
        /// </summary>
        List<ErrorReport> GetErrorReports();

        /// <summary>
        /// Gets error reports from the last N days.
        /// </summary>
        List<ErrorReport> GetRecentErrors(int days = 7);

        /// <summary>
        /// Clears all error reports.
        /// </summary>
        void ClearErrorReports();

        /// <summary>
        /// Gets the path to the error logs folder.
        /// </summary>
        string GetErrorLogsPath();
    }
}
