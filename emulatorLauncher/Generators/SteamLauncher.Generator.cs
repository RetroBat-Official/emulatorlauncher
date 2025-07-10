using System;
using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common.Launchers;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class SteamGameLauncher : GameLauncher
        {
            private Uri _uri;

            public SteamGameLauncher(Uri uri)
            {
                _uri = uri;

                // Call method to get Steam executable
                string steamInternalDBPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "steamexecutables.json");
                LauncherExe = SteamLibrary.GetSteamGameExecutableName(uri, steamInternalDBPath);
            }

            public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
            {
                // Check if steam is already running
                bool uiExists = Process.GetProcessesByName("steam").Any();

                if (string.IsNullOrEmpty(LauncherExe))
                    SimpleLogger.Instance.Info("[INFO] Executable name not found for " + Path.GetFileNameWithoutExtension(path.FileName) + ". Using fallback detection methods.");
                else
                    SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);

                // Kill game if already running
                KillExistingLauncherExes();

                // Start game
                Process.Start(path);

                // If we have an executable name, we can monitor the process
                if (!string.IsNullOrEmpty(LauncherExe))
                {
                    var steamGame = GetLauncherExeProcess();
                    if (steamGame != null)
                    {
                        steamGame.WaitForExit();

                        if (!uiExists || (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam")))
                        {
                            foreach (var ui in Process.GetProcessesByName("steam"))
                            {
                                try { ui.Kill(); }
                                catch { }
                            }
                        }
                    }
                }
                // Otherwise, use fallback methods
                else
                {
                    // Fallback 1: Registry Monitoring
                    bool registrySuccess = MonitorGameByRegistry();

                    // Fallback 2: Window Focus Detection
                    if (!registrySuccess)
                    {
                        SimpleLogger.Instance.Info("[INFO] Registry monitoring failed. Falling back to window focus detection.");
                        var gameProcess = ExeLauncherGenerator.FindGameProcessByWindowFocus();
                        if (gameProcess != null)
                        {
                            SimpleLogger.Instance.Info("[INFO] Game process '" + gameProcess.ProcessName + "' identified by window focus. Monitoring process.");
                            gameProcess.WaitForExit();
                            SimpleLogger.Instance.Info("[INFO] Game process has exited.");
                        }
                        else
                        {
                            SimpleLogger.Instance.Info("[INFO] All fallback methods failed. Unable to monitor game process.");
                        }
                    }
                }

                return 0;
            }

            private bool MonitorGameByRegistry()
            {
                string gameId = _uri.AbsolutePath.Substring(1);
                int endurl = gameId.IndexOf("%");
                if (endurl == -1)
                    endurl = gameId.Length;
                gameId = gameId.Substring(0, endurl);

                int gameID = gameId.ToInteger();
                if (gameID <= 0)
                    return false;

                SimpleLogger.Instance.Info("[INFO] Monitoring registry for game start (AppID: " + gameID + ").");

                // Wait for the game to be marked as running, with a 60-second timeout
                bool gameStarted = false;
                for (int i = 0; i < 60; i++)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + gameID))
                        {
                            if (key != null && key.GetValue("Running") != null && (int)key.GetValue("Running") == 1)
                            {
                                gameStarted = true;
                                break;
                            }
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(1000);
                }

                if (!gameStarted)
                {
                    SimpleLogger.Instance.Info("[INFO] Timeout: Game did not appear as 'Running' in the registry.");
                    return false;
                }

                SimpleLogger.Instance.Info("[INFO] Game detected as running. Monitoring registry for exit.");

                // Wait for the game to exit
                while (true)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + gameID))
                        {
                            if (key == null || (key.GetValue("Running") != null && (int)key.GetValue("Running") == 0))
                                break;
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(1000);
                }

                SimpleLogger.Instance.Info("[INFO] Game has exited.");
                return true;
            }

            private Process FindGameProcessByWindowFocus()
            {
                SimpleLogger.Instance.Info("[INFO] Trying to find game process by window focus.");

                // Wait for initial window to appear (e.g., a launcher)
                System.Threading.Thread.Sleep(10000); // 10 seconds

                IntPtr hWnd = User32.GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return null;

                uint pid;
                User32.GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return null;

                Process candidateProcess = null;
                try { candidateProcess = Process.GetProcessById((int)pid); }
                catch { return null; }

                SimpleLogger.Instance.Info("[INFO] Initial process candidate: " + candidateProcess.ProcessName);

                // Wait longer for the actual game to take over from the launcher
                System.Threading.Thread.Sleep(20000); // 20 more seconds

                hWnd = User32.GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return null;

                User32.GetWindowThreadProcessId(hWnd, out pid);
                if (pid == 0) return null;

                Process finalProcess = null;
                try { finalProcess = Process.GetProcessById((int)pid); }
                catch { return null; }

                SimpleLogger.Instance.Info("[INFO] Final process candidate: " + finalProcess.ProcessName);

                // Check if the final process is fullscreen
                RECT windowRect;
                User32.GetWindowRect(hWnd, out windowRect);

                int screenWidth = User32.GetSystemMetrics(User32.SM_CXSCREEN);
                int screenHeight = User32.GetSystemMetrics(User32.SM_CYSCREEN);

                bool isFullscreen = (windowRect.left == 0 && windowRect.top == 0 &&
                                     windowRect.right == screenWidth && windowRect.bottom == screenHeight);

                if (isFullscreen)
                {
                    SimpleLogger.Instance.Info("[INFO] Final process '" + finalProcess.ProcessName + "' is fullscreen. Selecting it.");
                    return finalProcess;
                }
                
                SimpleLogger.Instance.Info("[INFO] Final process is not fullscreen. Detection by focus failed.");
                return null;
            }
        }
    }
}
