using System;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Interface for application logging to help debug issues.
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Logs an informational message.
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Logs a debug message with detailed context.
        /// </summary>
        void LogDebug(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message with optional exception.
        /// </summary>
        void LogError(string message, Exception? exception = null);

        /// <summary>
        /// Logs a file operation with all relevant details.
        /// </summary>
        void LogFileOperation(string operation, string fileName, string? oldFileName = null, 
            string? groupName = null, string? additionalInfo = null);
    }
}
