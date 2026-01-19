using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using TelegramOrganizer.Core.Models;

namespace TelegramOrganizer.Infra.Interop
{
    /// <summary>
    /// Win32 API wrapper for enumerating windows.
    /// Provides access to EnumWindows, GetWindowText, and related functions.
    /// </summary>
    public static class Win32WindowEnumerator
    {
        // ========================================
        // Win32 API Declarations
        // ========================================

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);

        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_VM_READ = 0x0010;

        // ========================================
        // Public Methods
        // ========================================

        /// <summary>
        /// Enumerates all top-level windows and returns their information.
        /// </summary>
        public static List<WindowInfo> EnumerateAllWindows()
        {
            var windows = new List<WindowInfo>();
            IntPtr foregroundWindow = GetForegroundWindow();

            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    // Only process visible windows
                    if (!IsWindowVisible(hWnd))
                        return true;

                    // Get window title
                    int length = GetWindowTextLength(hWnd);
                    if (length == 0)
                        return true;

                    var builder = new StringBuilder(length + 1);
                    GetWindowText(hWnd, builder, builder.Capacity);
                    string title = builder.ToString();

                    if (string.IsNullOrWhiteSpace(title))
                        return true;

                    // Get process information
                    GetWindowThreadProcessId(hWnd, out uint processId);
                    string processName = GetProcessName(processId);

                    // Create window info
                    var windowInfo = new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        ProcessName = processName,
                        ProcessId = (int)processId,
                        IsActive = hWnd == foregroundWindow,
                        IsVisible = true,
                        LastSeen = DateTime.Now,
                        FirstSeen = DateTime.Now
                    };

                    // Set confidence based on active state
                    windowInfo.ConfidenceScore = windowInfo.IsActive ? 1.0 : 0.7;

                    windows.Add(windowInfo);
                }
                catch
                {
                    // Ignore errors for individual windows
                }

                return true; // Continue enumeration
            }, IntPtr.Zero);

            return windows;
        }

        /// <summary>
        /// Enumerates only Telegram windows.
        /// </summary>
        public static List<WindowInfo> EnumerateTelegramWindows()
        {
            var allWindows = EnumerateAllWindows();
            var telegramWindows = new List<WindowInfo>();

            foreach (var window in allWindows)
            {
                if (window.IsTelegramWindow())
                {
                    telegramWindows.Add(window);
                }
            }

            return telegramWindows;
        }

        /// <summary>
        /// Gets the currently active (foreground) window.
        /// </summary>
        public static WindowInfo? GetForegroundWindowInfo()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero)
                return null;

            return GetWindowInfo(hWnd);
        }

        /// <summary>
        /// Gets information about a specific window by handle.
        /// </summary>
        public static WindowInfo? GetWindowInfo(IntPtr hWnd)
        {
            try
            {
                if (!IsWindowVisible(hWnd))
                    return null;

                // Get window title
                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return null;

                var builder = new StringBuilder(length + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                string title = builder.ToString();

                if (string.IsNullOrWhiteSpace(title))
                    return null;

                // Get process information
                GetWindowThreadProcessId(hWnd, out uint processId);
                string processName = GetProcessName(processId);

                // Check if active
                IntPtr foregroundWindow = GetForegroundWindow();
                bool isActive = hWnd == foregroundWindow;

                return new WindowInfo
                {
                    Handle = hWnd,
                    Title = title,
                    ProcessName = processName,
                    ProcessId = (int)processId,
                    IsActive = isActive,
                    IsVisible = true,
                    LastSeen = DateTime.Now,
                    FirstSeen = DateTime.Now,
                    ConfidenceScore = isActive ? 1.0 : 0.7
                };
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // Private Helper Methods
        // ========================================

        private static string GetProcessName(uint processId)
        {
            try
            {
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, processId);
                if (hProcess == IntPtr.Zero)
                    return "Unknown";

                try
                {
                    var builder = new StringBuilder(1024);
                    if (GetModuleFileNameEx(hProcess, IntPtr.Zero, builder, (uint)builder.Capacity) > 0)
                    {
                        string fullPath = builder.ToString();
                        return System.IO.Path.GetFileNameWithoutExtension(fullPath);
                    }
                }
                finally
                {
                    CloseHandle(hProcess);
                }
            }
            catch
            {
                // Ignore
            }

            return "Unknown";
        }
    }
}
