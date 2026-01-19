using System;

namespace TelegramOrganizer.Core.Models
{
    /// <summary>
    /// Represents information about a window (typically Telegram).
    /// Used for tracking windows in the background even when they're not active.
    /// </summary>
    public class WindowInfo
    {
        /// <summary>Window handle (HWND)</summary>
        public IntPtr Handle { get; set; }

        /// <summary>Window title (e.g., "CS50 Study Group - Telegram")</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Process name (e.g., "Telegram")</summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>Process ID</summary>
        public int ProcessId { get; set; }

        /// <summary>When this window was last seen/active</summary>
        public DateTime LastSeen { get; set; } = DateTime.Now;

        /// <summary>Whether this window is currently the foreground window</summary>
        public bool IsActive { get; set; }

        /// <summary>Whether this window is visible (not minimized/hidden)</summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Extracted group name from window title.
        /// Null if not yet extracted or not a Telegram window.
        /// </summary>
        public string? ExtractedGroupName { get; set; }

        /// <summary>
        /// Confidence score for the extracted group name (0.0 - 1.0).
        /// 1.0 = Window was active when detected
        /// 0.7 = Window was visible but not active
        /// 0.5 = Window was in background
        /// </summary>
        public double ConfidenceScore { get; set; } = 1.0;

        /// <summary>Number of times this window has been seen</summary>
        public int SeenCount { get; set; } = 1;

        /// <summary>When this window was first detected</summary>
        public DateTime FirstSeen { get; set; } = DateTime.Now;

        /// <summary>
        /// Checks if this window info represents a Telegram window.
        /// </summary>
        public bool IsTelegramWindow()
        {
            return ProcessName.Contains("Telegram", StringComparison.OrdinalIgnoreCase) ||
                   Title.Contains("Telegram", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Updates the last seen timestamp and increments seen count.
        /// </summary>
        public void UpdateLastSeen(bool isActive = false, bool isVisible = true)
        {
            LastSeen = DateTime.Now;
            IsActive = isActive;
            IsVisible = isVisible;
            SeenCount++;

            // Update confidence based on visibility
            if (isActive)
                ConfidenceScore = 1.0; // High confidence - window is active
            else if (isVisible)
                ConfidenceScore = 0.7; // Medium-high confidence - visible but not active
            else
                ConfidenceScore = 0.5; // Medium confidence - in background
        }

        /// <summary>
        /// Gets the age of this window info in seconds.
        /// </summary>
        public double GetAgeInSeconds()
        {
            return (DateTime.Now - LastSeen).TotalSeconds;
        }

        /// <summary>
        /// Checks if this window info has expired (not seen for specified seconds).
        /// </summary>
        public bool IsExpired(int timeoutSeconds = 300)
        {
            return GetAgeInSeconds() > timeoutSeconds;
        }

        public override string ToString()
        {
            string status = IsActive ? "ACTIVE" : (IsVisible ? "VISIBLE" : "BACKGROUND");
            return $"{Title} [{status}] (seen {SeenCount}x, confidence {ConfidenceScore:F2}, age {GetAgeInSeconds():F0}s)";
        }

        public override bool Equals(object? obj)
        {
            if (obj is WindowInfo other)
            {
                return Handle == other.Handle;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }
    }
}
