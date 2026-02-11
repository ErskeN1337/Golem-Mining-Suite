using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Golem_Mining_Suite.Services
{
    /// <summary>
    /// Service to detect if Star Citizen is running and get window information
    /// </summary>
    public class GameDetectionService
    {
        // Windows API imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private const string STAR_CITIZEN_PROCESS = "StarCitizen";

        /// <summary>
        /// Check if Star Citizen is currently running
        /// </summary>
        public bool IsStarCitizenRunning()
        {
            try
            {
                var processes = Process.GetProcessesByName(STAR_CITIZEN_PROCESS);
                return processes.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the Star Citizen window handle by finding the main window of the StarCitizen.exe process
        /// </summary>
        public IntPtr GetStarCitizenWindow()
        {
            try
            {
                var processes = Process.GetProcessesByName(STAR_CITIZEN_PROCESS);
                if (processes.Length == 0) return IntPtr.Zero;

                // Get the first Star Citizen process
                var process = processes[0];
                
                // Return the main window handle
                return process.MainWindowHandle;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Check if the Star Citizen window is visible and active
        /// </summary>
        public bool IsGameWindowVisible()
        {
            IntPtr hwnd = GetStarCitizenWindow();
            if (hwnd == IntPtr.Zero) return false;
            
            return IsWindowVisible(hwnd);
        }

        /// <summary>
        /// Get the window bounds for screen capture
        /// </summary>
        public (int X, int Y, int Width, int Height)? GetWindowBounds()
        {
            IntPtr hwnd = GetStarCitizenWindow();
            
            // Try to get window rect
            if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out RECT rect))
            {
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                
                // Check if we got valid dimensions
                if (width > 0 && height > 0)
                {
                    return (rect.Left, rect.Top, width, height);
                }
            }

            // Fallback: If Star Citizen is running but we can't get window bounds,
            // assume it's in fullscreen mode and use primary screen dimensions
            if (IsStarCitizenRunning())
            {
                try
                {
                    // Get primary screen dimensions using WPF
                    int width = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    int height = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
                    
                    if (width > 0 && height > 0)
                    {
                        return (0, 0, width, height);
                    }
                }
                catch
                {
                    // If screen detection fails, return null
                }
            }

            return null;
        }

        /// <summary>
        /// Estimate if a terminal might be open based on window state
        /// Note: This is a heuristic, not perfect detection
        /// </summary>
        public bool IsPossiblyAtTerminal()
        {
            // For now, just check if game is running and visible
            // In Phase 1, we'll improve this with OCR detection
            return IsStarCitizenRunning() && IsGameWindowVisible();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Check if Star Citizen is the currently active window
        /// </summary>
        public bool IsGameForeground()
        {
            var gameWnd = GetStarCitizenWindow();
            if (gameWnd == IntPtr.Zero) return false;
            
            var foreground = GetForegroundWindow();
            return gameWnd == foreground;
        }
    }
}
