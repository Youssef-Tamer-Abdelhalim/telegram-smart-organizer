using System;
using System.Collections.Generic;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Interface for persisting and retrieving application state.
    /// Handles saving/loading pending downloads to survive app restarts.
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// Saves the current application state to persistent storage.
        /// </summary>
        /// <param name="state">The application state to save.</param>
        void SaveState(AppState state);

        /// <summary>
        /// Loads the application state from persistent storage.
        /// Returns empty state if no saved state exists or if loading fails.
        /// </summary>
        /// <returns>The loaded application state.</returns>
        AppState LoadState();

        /// <summary>
        /// Adds or updates a single pending download entry.
        /// More efficient than saving the entire state for single-item updates.
        /// </summary>
        /// <param name="fileName">The temp file name (key).</param>
        /// <param name="context">The file context to save.</param>
        void AddOrUpdateEntry(string fileName, FileContext context);

        /// <summary>
        /// Removes a pending download entry after completion or cancellation.
        /// </summary>
        /// <param name="fileName">The temp file name to remove.</param>
        void RemoveEntry(string fileName);

        /// <summary>
        /// Removes entries older than the specified retention period.
        /// </summary>
        /// <param name="retentionDays">Number of days to retain entries.</param>
        /// <returns>Number of entries removed.</returns>
        int CleanupOldEntries(int retentionDays = 30);
    }
}
