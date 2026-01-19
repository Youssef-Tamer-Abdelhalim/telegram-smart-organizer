using System;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Core.Contracts
{
    /// <summary>
    /// Interface for managing application settings and configuration.
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Loads settings from persistent storage.
        /// </summary>
        /// <returns>Application settings.</returns>
        AppSettings LoadSettings();

        /// <summary>
        /// Saves settings to persistent storage.
        /// </summary>
        /// <param name="settings">Settings to save.</param>
        void SaveSettings(AppSettings settings);

        /// <summary>
        /// Event raised when settings are changed.
        /// </summary>
        event EventHandler<AppSettings> SettingsChanged;
    }
}
