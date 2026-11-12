using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Launchers;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {

        class SteamGameLauncher : GameLauncher
        {
            private string _steamID;
            private Uri _uri;
            private bool _isSteamApp;       // false for non-Steam shortcuts : unknown in appinfo.vdf and in Steam registry

            public SteamGameLauncher(Uri uri)
            {
                _uri = uri;
                LauncherExe = GetGameExecutableName();
            }

            private string GetGameExecutableName()
            {
                string steamInternalDBPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "steamexecutables.json");
                string exe = SteamLibrary.GetSteamGameExecutableName(_uri, steamInternalDBPath, out _steamID);

                _isSteamApp = _steamID.ToInteger() > 0;
                return exe;
            }

            public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
            {
                // Check if steam is already running
                bool uiExists = Process.GetProcessesByName("steam").Any();
                if (uiExists)
                    SimpleLogger.Instance.Info("[INFO] Steam found running already.");
                else
                    SimpleLogger.Instance.Info("[INFO] Steam not yet running.");

                if (_isSteamApp && !IsGameInstalled())
                {
                    SimpleLogger.Instance.Info("[INFO] Game is not installed. Triggering installation.");

                    // Start game install
                    Process.Start(path);

                    if (!MonitorUserInstallationValidation())
                    {
                        SimpleLogger.Instance.Info("[INFO] User did not launch installation within the time limit.");
                        KillSteam(uiExists);
                        throw new ApplicationException("Validation of installation not done.");
                    }

                    if (MonitorGameInstallation())
                    {
                        // Re-check for executable now that it's installed
                        LauncherExe = GetGameExecutableName();
                    }
                    else
                    {
                        SimpleLogger.Instance.Info("[INFO] Failed to install the game within the time limit.");
                        KillSteam(uiExists);
                        throw new ApplicationException("Game not installed after 2 hours delay!");
                    }

                    SimpleLogger.Instance.Info("[INFO] Installation complete. Game will now be launched.");
                }

                if (LauncherExe != null)
                {
                    SimpleLogger.Instance.Info("[INFO] Executable name : " + LauncherExe);

                    // Kill game if already running
                    KillExistingLauncherExes();
                }

                // Start game
                Process.Start(path);

                bool monitored = false;

                // First method : wait for the game executable (30 seconds by default)
                var steamGame = LauncherExe != null ? GetLauncherExeProcess() : null;
                if (steamGame != null)
                {
                    AttachGameProcess(steamGame);
                    steamGame.WaitForExit();
                    SimpleLogger.Instance.Info("[INFO] Steam game closed : " + LauncherExe);
                    monitored = true;
                }
                else
                {
                    if (LauncherExe != null)
                        SimpleLogger.Instance.Info("[INFO] Process " + LauncherExe + " not found, checking in Steam registry if the game is running.");

                    // Second method : Steam flags the running games in the registry
                    // When the executable has already been waited for, the game should be flagged already
                    monitored = MonitorGameByRegistry(LauncherExe != null ? 5 : 60);

                    // Third method, only when the executable is unknown : fullscreen window in foreground
                    if (!monitored && LauncherExe == null)
                    {
                        SimpleLogger.Instance.Info("[INFO] Registry monitoring failed. Falling back to window focus detection.");
                        var gameProcess = FindGameProcessByWindowFocus();
                        if (gameProcess != null)
                        {
                            SimpleLogger.Instance.Info("[INFO] Game process '" + gameProcess.ProcessName + "' identified by window focus. Monitoring process.");
                            AttachGameProcess(gameProcess);
                            gameProcess.WaitForExit();
                            SimpleLogger.Instance.Info("[INFO] Game process has exited.");
                            monitored = true;
                        }
                    }

                    if (!monitored)
                        SimpleLogger.Instance.Info("[INFO] Unable to monitor game process.");
                }

                // Kill steam if it was not running previously or if option is set in RetroBat
                if (monitored)
                    KillSteam(uiExists);

                return 0;
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

            private bool IsGameRunningInRegistry()
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Valve\\Steam\\Apps\\" + _steamID))
                    {
                        if (key == null)
                            return false;

                        var running = key.GetValue("Running");
                        return running is int && (int)running == 1;
                    }
                }
                catch { return false; }
            }

            /// <summary>
            /// Wait for the game to start and to exit, based on the 'Running' flag set by Steam in the registry
            /// </summary>
            /// <param name="startTimeout">Seconds to wait for the game to be flagged as running</param>
            private bool MonitorGameByRegistry(int startTimeout)
            {
                if (!_isSteamApp)
                    return false;

                SimpleLogger.Instance.Info("[INFO] Monitoring registry for game start (AppID: " + _steamID + ").");

                // Wait for the game to be marked as running
                bool gameStarted = false;
                for (int i = 0; i <= startTimeout; i++)
                {
                    if (IsGameRunningInRegistry())
                    {
                        gameStarted = true;
                        break;
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                if (!gameStarted)
                {
                    SimpleLogger.Instance.Info("[INFO] Timeout: Game did not appear as 'Running' in the registry.");
                    return false;
                }

                SimpleLogger.Instance.Info("[INFO] Game detected as running. Monitoring registry for exit.");

                string gameFolder = GetGameFolder();
                bool processSeen = false;
                int secondsWithoutProcess = 0;

                // Wait for the game to exit (only Steam resets the flag : stop waiting if Steam is closed)
                while (IsGameRunningInRegistry() && Process.GetProcessesByName("steam").Any())
                {
                    // Safety : Steam may keep the flag while the game is closed (third party launcher left open...)
                    // Stop waiting when the processes of the game folder have been seen and are gone for 10 seconds
                    if (gameFolder != null)
                    {
                        if (IsGameProcessRunning(gameFolder))
                        {
                            processSeen = true;
                            secondsWithoutProcess = 0;
                        }
                        else if (processSeen && ++secondsWithoutProcess >= 10)
                        {
                            SimpleLogger.Instance.Info("[INFO] No more process running from the game folder, stop waiting for Steam.");
                            break;
                        }
                    }

                    System.Threading.Thread.Sleep(1000);
                }

                SimpleLogger.Instance.Info("[INFO] Game has exited.");
                return true;
            }

            /// <summary>
            /// Game folder with a trailing backslash, null if unknown
            /// </summary>
            private string GetGameFolder()
            {
                try
                {
                    string installDir = SteamLibrary.GetGameInstallDirectory(_steamID.ToInteger());
                    if (!string.IsNullOrEmpty(installDir))
                        return Path.GetFullPath(installDir).TrimEnd('\\') + "\\";
                }
                catch { }

                return null;
            }

            private static bool IsGameProcessRunning(string gameFolder)
            {
                return Process.GetProcesses().Any(p =>
                {
                    string exePath = p.GetProcessPath();
                    return exePath != null && exePath.StartsWith(gameFolder, StringComparison.OrdinalIgnoreCase);
                });
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

            private bool HasInstallationStarted()
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

                        if (installed != null && installed is int && (int)installed == 0)
                        {
                            if (updating != null && updating is int && (int)updating == 0)
                                return false;

                            return true;
                        }
                        else if (installed != null && installed is int && (int)installed == 1)
                        {
                            SimpleLogger.Instance.Info("[INFO] Game is already installed.");
                            return true;
                        }
                    }
                }
                catch
                {
                    SimpleLogger.Instance.Warning("[WARNING] Failed to check if Steam game installation has begun.");
                    return true;
                }

                return true;
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

            private bool MonitorUserInstallationValidation()
            {
                if (string.IsNullOrEmpty(_steamID))
                    return false;

                SimpleLogger.Instance.Info("[STEAM] Waiting for game installation to start (AppID: " + _steamID + ").");

                // Wait for user to push the button for installation, with a 30 seconds timeout
                for (int i = 0; i < 30; i++)
                {
                    if (HasInstallationStarted())
                    {
                        SimpleLogger.Instance.Info("[STEAM] Game " + _steamID + " installation in progress or game installed already.");
                        return true;
                    }
                    System.Threading.Thread.Sleep(1000);
                }

                SimpleLogger.Instance.Info("[STEAM] Timeout: User did not validate installation.");
                return false;
            }
        }
    }
}
