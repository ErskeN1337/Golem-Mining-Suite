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
            if (hwnd == IntPtr.Zero) return null;

            if (GetWindowRect(hwnd, out RECT rect))
            {
                return (
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top
                );
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
    }
}
