using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramOrganizer.Core.Contracts
{
    public interface IFileOrganizer
    {
        /// <summary>
        /// Moves a file to a folder named after the group.
        /// Handles directory creation and name collisions automatically.
        /// </summary>
        /// <param name="filePath">Full path to the source file.</param>
        /// <param name="groupName">Target group name (will be sanitized).</param>
        /// <returns>The result message (e.g., "Moved to CS50").</returns>
        string OrganizeFile(string filePath, string groupName);
    }
}