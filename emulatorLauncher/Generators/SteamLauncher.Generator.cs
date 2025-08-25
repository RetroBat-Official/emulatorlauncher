using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common.Launchers;
using EmulatorLauncher.Common;
using Microsoft.Win32;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        
        class SteamGameLauncher : GameLauncher
        {
            private string _steamID;
            private Uri _uri;

            public SteamGameLauncher(Uri uri)
            {
                // Call method to get Steam executable
                _uri = uri;
                string steamInternalDBPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "steamexecutables.json");
                LauncherExe = SteamLibrary.GetSteamGameExecutableName(uri, steamInternalDBPath, out _steamID);
            }

            public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
            {
                // Check if steam is already running
                bool uiExists = Process.GetProcessesByName("steam").Any();
                if (uiExists)
                    SimpleLogger.Instance.Info("[INFO] Steam found running already.");
                else
                    SimpleLogger.Instance.Info("[INFO] Steam not yet running.");

                if (!IsGameInstalled())
                {
                    SimpleLogger.Instance.Info("[INFO] Game is not installed. Triggering installation.");

                    // Start game install
                    Process.Start(path);

                    if (MonitorGameInstallation())
                    {
                        // Re-check for executable now that it's installed
                        string steamInternalDBPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "steamexecutables.json");
                        LauncherExe = SteamLibrary.GetSteamGameExecutableName(_uri, steamInternalDBPath, out _steamID);
                    }
                    else
                    {
                        SimpleLogger.Instance.Info("[INFO] Failed to install the game within the time limit.");
                        KillSteam(uiExists);
                        return -1;
                    }

                    SimpleLogger.Instance.Info("[INFO] Installation complete. Game will now be launched.");
                }

                if (LauncherExe != null)
                {
                    SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);

                    // Kill game if already running
                    KillExistingLauncherExes();

                    // Start game
                    Process.Start(path);

                    // Get running game process (30 seconds delay 30x1000)
                    var steamGame = GetLauncherExeProcess();

                    if (steamGame != null)
                    {
                        steamGame.WaitForExit();
                        SimpleLogger.Instance.Info("[INFO] Steam game closed : " + LauncherExe);

                        // Kill steam if it was not running previously or if option is set in RetroBat
                        KillSteam(uiExists);
                    }
                    return 0;
                }
                else
                {
                    // Start game
                    Process.Start(path);

                    if (MonitorGameByRegistry(uiExists))
                        return 0;

                    SimpleLogger.Instance.Info("[INFO] Registry monitoring failed. Falling back to window focus detection.");
                    var gameProcess = FindGameProcessByWindowFocus();
                    if (gameProcess != null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Game process '" + gameProcess.ProcessName + "' identified by window focus. Monitoring process.");
                        gameProcess.WaitForExit();
                        SimpleLogger.Instance.Info("[INFO] Game process has exited.");

                        // Kill steam if it was not running previously or if option is set in RetroBat
                        KillSteam(uiExists);
                    }
                    else
                        SimpleLogger.Instance.Info("[INFO] All fallback methods failed. Unable to monitor game process.");

                    return 0;
                }
            }

            private void KillSteam(bool uiExists)
            {
                if (Program.SystemConfig.getOptBoolean("killsteam"))
                    SimpleLogger.Instance.Info("[INFO] Option set to always kill Steam.");
                else if (Program.SystemConfig.isOptSet("killsteam"))
                    SimpleLogger.Instance.Info("[INFO] Option set to never kill Steam.");
                else
                    SimpleLogger.Instance.Info("[INFO] Steam will be killed if not running before.");

                // Kill steam if it was not running previously or if option is set in RetroBat
                if (Program.SystemConfig.getOptBoolean("killsteam"))
                {
                    foreach (var ui in Process.GetProcessesByName("steam"))
                    {
                        try { ui.Kill(); }
                        catch { }
                        SimpleLogger.Instance.Info("[INFO] Killing Steam.");
                    }
                }
                else if (!Program.SystemConfig.isOptSet("killsteam"))
                {
                    if (!uiExists)
                    {
                        foreach (var ui in Process.GetProcessesByName("steam"))
                        {
                            try { ui.Kill(); }
                            catch { }
                            SimpleLogger.Instance.Info("[INFO] Killing Steam.");
                        }
                    }
                }
            }

            private bool MonitorGameByRegistry(bool uiExists = false)
            {
                if (string.IsNullOrEmpty(_steamID))
                    return false;

                SimpleLogger.Instance.Info("[INFO] Monitoring registry for game start (AppID: " + _steamID + ").");

                // Wait for the game to be marked as running, with a 60-second timeout
                bool gameStarted = false;
                for (int i = 0; i < 60; i++)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + _steamID))
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
                    // Kill steam if it was not running previously or if option is set in RetroBat
                    KillSteam(uiExists);
                    return false;
                }

                SimpleLogger.Instance.Info("[INFO] Game detected as running. Monitoring registry for exit.");

                // Wait for the game to exit
                while (true)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + _steamID))
                        {
                            if (key == null || (key.GetValue("Running") != null && (int)key.GetValue("Running") == 0))
                                break;
                        }
                    }
                    catch { }
                    System.Threading.Thread.Sleep(1000);
                }

                SimpleLogger.Instance.Info("[INFO] Game has exited.");

                // Kill steam if it was not running previously or if option is set in RetroBat
                KillSteam(uiExists);

                return true;
            }

            private bool IsGameInstalled()
            {
                if (string.IsNullOrEmpty(_steamID))
                    return false;

                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + _steamID))
                    {
                        if (key == null)
                            return false;

                        var installed = key.GetValue("Installed");
                        var updating = key.GetValue("Updating");

                        if (installed != null && installed is int && (int)installed == 1)
                        {
                            if (updating != null && updating is int && (int)updating == 1)
                                return false;

                            return true;
                        }
                    }
                }
                catch 
                {
                    SimpleLogger.Instance.Warning("[WARNING] Failed to check if Steam game is installed");
                    return false;
                }

                return false;
            }

            private bool MonitorGameInstallation()
            {
                if (string.IsNullOrEmpty(_steamID))
                    return false;

                SimpleLogger.Instance.Info("[STEAM] Waiting for game installation to complete (AppID: " + _steamID + ").");

                // Wait for installation, with a long timeout (e.g., 2 hours = 7200 seconds)
                for (int i = 0; i < 7200; i++)
                {
                    if (IsGameInstalled())
                    {
                        SimpleLogger.Instance.Info("[STEAM] Game " + _steamID + " installation complete.");
                        return true;
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                SimpleLogger.Instance.Info("[STEAM] Timeout: Game installation did not complete.");
                return false;
            }
        }
    }
}
