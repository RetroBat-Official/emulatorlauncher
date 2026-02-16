using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    internal class ScreenTools
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static void MoveWindow(string ProcessName, int targetMonitorIndex = 0, int maxRetries = 20, int retryDelayMs = 2000)
        {
            SimpleLogger.Instance.Info($"[SCREENMOVER] Starting process of moving {ProcessName} to monitor {targetMonitorIndex}");

            try
            {
                Process process = null;
                IntPtr handle = IntPtr.Zero;

                for (int i = 0; i < maxRetries; i++)
                {
                    if (i > 0)
                    {
                        SimpleLogger.Instance.Info($"[SCREENMOVER] Retry {i}/{maxRetries}: Waiting {retryDelayMs}ms...");
                        Thread.Sleep(retryDelayMs);
                    }

                    var candidates = Process.GetProcessesByName(ProcessName).ToArray();

                    if (candidates.Length == 0)
                    {
                        SimpleLogger.Instance.Warning($"[SCREENMOVER] Process '{ProcessName}' not found.");
                        continue;
                    }

                    process = candidates.FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);

                    if (process == null)
                    {
                        SimpleLogger.Instance.Warning("[SCREENMOVER] Process found, but no instance has a main window handle yet.");
                        continue;
                    }

                    handle = process.MainWindowHandle;

                    if (handle != IntPtr.Zero)
                    {
                        SimpleLogger.Instance.Info("[SCREENMOVER] Process and Main Window found!");
                        break;
                    }
                }

                if (handle == IntPtr.Zero)
                {
                    SimpleLogger.Instance.Info($"[SCREENMOVER] Could not find process '{ProcessName}' with a valid window after {maxRetries} retries. Giving up.");

                    return;
                }

                Screen[] screens = Screen.AllScreens;
                if (targetMonitorIndex < 0 || targetMonitorIndex >= screens.Length)
                {
                    SimpleLogger.Instance.Error($"[SCREENMOVER] Target monitor index {targetMonitorIndex} is out of range. Available screens: {screens.Length}");
                    return;
                }

                Screen targetScreen = screens[targetMonitorIndex];
                Screen currentScreen = Screen.FromHandle(handle);

                SimpleLogger.Instance.Info($"[SCREENMOVER] Window is currently on: {currentScreen.DeviceName} (Primary: {currentScreen.Primary})");
                SimpleLogger.Instance.Info($"[SCREENMOVER] Target screen is: {targetScreen.DeviceName} (Primary: {targetScreen.Primary})");

                // Check if we need to move it
                if (!currentScreen.DeviceName.Equals(targetScreen.DeviceName))
                {
                    SimpleLogger.Instance.Info($"[SCREENMOVER] Window is on the wrong screen. Moving...");
                    MoveWindowToScreen(handle, targetScreen);
                }
                else
                {
                    SimpleLogger.Instance.Info($"[SCREENMOVER] Window is already on the correct screen. No action taken.");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error($"[SCREENMOVER] Exception occurred: {ex}");
            }
        }

        static void MoveWindowToScreen(IntPtr hWnd, Screen targetScreen)
        {
            int x = targetScreen.Bounds.Left;
            int y = targetScreen.Bounds.Top;

            SimpleLogger.Instance.Info($"[SCREENMOVER] Moving window to ({x}, {y})");

            bool result = User32.SetWindowPosBool(hWnd, IntPtr.Zero, x, y, 0, 0, SWP.NOSIZE | SWP.NOZORDER | SWP.SHOWWINDOW);

            if (!result)
            {
                int error = Marshal.GetLastWin32Error();
                SimpleLogger.Instance.Error($"[SCREENMOVER] Failed to move window. Error code: {error}");
            }
            else
            {
                SimpleLogger.Instance.Info("[SCREENMOVER] Window moved successfully.");
            }
        }
    }
}
