using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramOrganizer.Core.Contracts
{
    public interface IContextDetector
    {
        /// <summary>
        /// Returns the title of the currently active window (e.g., "CS50 - Telegram").
        /// </summary>
        string GetActiveWindowTitle();

        /// <summary>
        /// (Optional) Returns the process name (e.g., "Telegram").
        /// This helps ensure we are actually capturing Telegram, not Chrome.
        /// </summary>
        string GetProcessName();
    }
}
