using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmulatorLauncher
{
    class QuiverLauncherGenerator : Generator
    {
        private const string LauncherExe = "QuiverLauncher.exe";
        private const string LauncherProcessName = "QuiverLauncher";
        private const string OldLauncherExe = "N64RecompLauncher.exe";

        private string _exename = LauncherProcessName;
        private bool _launcherMode = false;
        private bool _batFile = false;

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("quiverlauncher");
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return null;

            string launcherExe = Path.Combine(path, LauncherExe);
            if (!File.Exists(launcherExe))
                return null;

            bool useLauncher = SystemConfig.getOptBoolean("quiver_useLauncher") || !FileTools.IsExtension(rom, ".lnk");

            if (!useLauncher)
            {
                string gameExe = ResolveShortcutExecutable(rom, path);

                if (string.IsNullOrEmpty(gameExe))
                {
                    // Show the launcher so the user can fix the installation instead of failing silently
                    SimpleLogger.Instance.Warning("[QuiverLauncher] Unable to resolve game executable from shortcut, starting launcher instead.");
                }
                else
                {
                    gameExe = ApplyGameSpecificExecutable(gameExe);
                    string gameFolder = Path.GetDirectoryName(gameExe);

                    SetupGame(gameFolder, gameExe);

                    _batFile = FileTools.IsExtension(gameExe, ".bat") || FileTools.IsExtension(gameExe, ".cmd");
                    _exename = _batFile ? "" : Path.GetFileNameWithoutExtension(gameExe);

                    SimpleLogger.Instance.Info("[QuiverLauncher] Running " + gameExe);

                    var psi = new ProcessStartInfo()
                    {
                        FileName = gameExe,
                        WorkingDirectory = gameFolder,
                    };

                    if (_batFile)
                        psi.UseShellExecute = true;

                    return psi;
                }
            }

            _launcherMode = true;
            _exename = LauncherProcessName;
            SetupLauncher(Path.Combine(path, "settings.json"));

            // Root QuiverLauncher.exe exits right after starting, use current\QuiverLauncher.exe
            string appExe = Path.Combine(path, "current", LauncherExe);
            if (File.Exists(appExe))
                launcherExe = appExe;

            return new ProcessStartInfo()
            {
                FileName = launcherExe,
                WorkingDirectory = Path.GetDirectoryName(launcherExe)
            };
        }

        #region configuration
        private static void SetupLauncher(string conf)
        {
            JObject jsonObj = LoadJson(conf);

            jsonObj["StartFullscreen"] = ShouldRunFullscreen();
            jsonObj["EnableGamepadInput"] = true;
            jsonObj["FirstStartup"] = false;

            var asked = jsonObj["UsageDataAsked"];
            if (asked == null || asked.Type != JTokenType.Boolean || !(bool)asked)
            {
                jsonObj["UsageDataEnabled"] = false;
                jsonObj["UsageDataAsked"] = true;
            }

            SaveJson(conf, jsonObj);
        }

        private static void SetupGame(string gameFolder, string gameExe)
        {
            // Quiver checks "portable.txt" in the working directory, otherwise it stores config and saves in %LOCALAPPDATA%
            string portableFile = Path.Combine(gameFolder, "portable.txt");
            if (!File.Exists(portableFile))
            {
                try { File.WriteAllText(portableFile, ""); }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[QuiverLauncher] Unable to create portable.txt: " + ex.Message); }
            }

            // Only touch graphics.json for recomp runtime games (or if it already exists), missing keys fall back to runtime defaults
            string graphicsConf = Path.Combine(gameFolder, "graphics.json");
            bool isRecomp = Path.GetFileNameWithoutExtension(gameExe).IndexOf("recomp", StringComparison.InvariantCultureIgnoreCase) >= 0;

            if (isRecomp || File.Exists(graphicsConf))
                SetupGraphics(graphicsConf);
        }

        private static void SetupGraphics(string conf)
        {
            JObject jsonObj = LoadJson(conf);

            jsonObj["wm_option"] = ShouldRunFullscreen() ? "Fullscreen" : "Windowed";

            SaveJson(conf, jsonObj);
        }

        private string ApplyGameSpecificExecutable(string gameExe)
        {
            string gameFolder = Path.GetDirectoryName(gameExe);
            string exeName = Path.GetFileName(gameExe);

            // Perfect Dark port ships one executable per region
            if (exeName.StartsWith("pd.", StringComparison.InvariantCultureIgnoreCase) && exeName.EndsWith(".x86_64.exe", StringComparison.InvariantCultureIgnoreCase) && SystemConfig.isOptSet("pdark_region"))
            {
                string regionExe;
                switch (SystemConfig["pdark_region"])
                {
                    case "EUR": regionExe = "pd.pal.x86_64.exe"; break;
                    case "JPN": regionExe = "pd.jpn.x86_64.exe"; break;
                    default: regionExe = "pd.x86_64.exe"; break;
                }

                string regionPath = Path.Combine(gameFolder, regionExe);
                if (File.Exists(regionPath))
                    return regionPath;

                SimpleLogger.Instance.Warning("[QuiverLauncher] Perfect Dark region executable not found: " + regionExe);
            }

            return gameExe;
        }
        #endregion

        #region process
        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (string.IsNullOrEmpty(_exename))
                return mapping;

            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{CLOSE})");
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_launcherMode)
            {
                KillLauncher();
                Process.Start(path);

                // Velopack stub hands off to current\QuiverLauncher.exe : wait for every launcher instance
                WaitForProcessName(LauncherProcessName, 10000);
                return 0;
            }

            Process process = Process.Start(path);
            if (process == null)
                return 0;

            process.WaitForExit();

            // Some ports restart themselves (e.g. after first setup) : wait for any remaining instance
            if (!string.IsNullOrEmpty(_exename))
            {
                Thread.Sleep(500);
                WaitForProcessName(_exename, 0);
            }

            return 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            if (_launcherMode)
                KillLauncher();
        }

        private static void WaitForProcessName(string name, int appearTimeoutMs)
        {
            int elapsed = 0;
            while (Process.GetProcessesByName(name).Length == 0 && elapsed < appearTimeoutMs)
            {
                Thread.Sleep(200);
                elapsed += 200;
            }

            while (true)
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length == 0)
                    break;

                foreach (var px in processes)
                {
                    try { px.WaitForExit(); }
                    catch { }
                    finally { px.Dispose(); }
                }

                Thread.Sleep(500);
            }
        }

        private static void KillLauncher()
        {
            foreach (var px in Process.GetProcessesByName(LauncherProcessName))
            {
                try
                {
                    if (!px.HasExited)
                    {
                        px.Kill();
                        px.WaitForExit(3000);
                    }
                }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[QuiverLauncher] Unable to kill launcher process: " + ex.Message); }
                finally { px.Dispose(); }
            }
        }
        #endregion

        #region library
        /// <summary>
        /// Resolves the game executable targeted by a shortcut.
        /// Handles direct shortcuts created by Quiver, moved installations and old launcher shortcuts ("--run name").
        /// </summary>
        private static string ResolveShortcutExecutable(string shortcut, string launcherPath)
        {
            string target = null;
            try { target = FileTools.GetShortcutTargetwsh(shortcut); }
            catch { }

            if (string.IsNullOrEmpty(target))
                target = FileTools.GetShortcutTarget(shortcut);

            if (string.IsNullOrEmpty(target))
                return null;

            string appsFolder = GetAppsFolder(launcherPath);
            string targetName = Path.GetFileName(target);

            // Shortcut to the launcher itself (Quiver or old N64RecompLauncher) : resolve game by name
            if (targetName.Equals(LauncherExe, StringComparison.InvariantCultureIgnoreCase) || targetName.Equals(OldLauncherExe, StringComparison.InvariantCultureIgnoreCase))
            {
                string gameName = GetRunArgument(FileTools.GetShortcutArgswsh(shortcut));
                if (string.IsNullOrEmpty(gameName))
                    return null;

                var app = LoadApps(launcherPath).FirstOrDefault(a => a.Matches(gameName));
                if (app == null)
                {
                    SimpleLogger.Instance.Warning("[QuiverLauncher] Game not found in Quiver library: " + gameName);
                    return null;
                }

                return GetAppExecutable(app, appsFolder);
            }

            if (File.Exists(target))
                return target;

            // RetroBat folder moved : rebase the stored absolute path on the current apps folder
            string rebased = RebaseOnFolder(target, appsFolder);
            if (rebased != null)
                SimpleLogger.Instance.Info("[QuiverLauncher] Shortcut target not found, using " + rebased);

            return rebased;
        }

        private static string GetRunArgument(string args)
        {
            if (string.IsNullOrEmpty(args))
                return null;

            args = args.Trim();
            foreach (var prefix in new string[] { "--run ", "-r " })
            {
                if (args.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                    return args.Substring(prefix.Length).Trim().Trim('"', '\'');
            }

            return null;
        }

        /// <summary>
        /// Finds the longest trailing part of an absolute path that exists under the given folder.
        /// </summary>
        private static string RebaseOnFolder(string file, string folder)
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;

            string[] parts = file.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Keep at least "<appfolder>\<file>"
            for (int i = 1; i <= parts.Length - 2; i++)
            {
                string candidate = Path.Combine(folder, string.Join("\\", parts, i, parts.Length - i));
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static string GetAppsFolder(string launcherPath)
        {
            try
            {
                JObject settings = LoadJson(Path.Combine(launcherPath, "settings.json"));
                string appsPath = (string)settings.GetValue("AppsPath", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(appsPath))
                {
                    if (!Path.IsPathRooted(appsPath))
                        appsPath = Path.Combine(launcherPath, appsPath);

                    if (Directory.Exists(appsPath))
                        return appsPath;
                }
            }
            catch { }

            return Path.Combine(launcherPath, "Apps");
        }

        private static List<QuiverApp> LoadApps(string launcherPath)
        {
            var ret = new List<QuiverApp>();

            string appsJson = Path.Combine(launcherPath, "apps.json");
            if (!File.Exists(appsJson))
            {
                SimpleLogger.Instance.Warning("[QuiverLauncher] apps.json not found.");
                return ret;
            }

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(appsJson));

                // "apps" is the current format, "standard/experimental/custom" the legacy one (still accepted by Quiver)
                foreach (var key in new string[] { "apps", "standard", "experimental", "custom" })
                {
                    var array = root[key] as JArray;
                    if (array == null)
                        continue;

                    foreach (var item in array.OfType<JObject>())
                    {
                        var app = new QuiverApp()
                        {
                            Name = (string)item["name"],
                            CustomDisplayName = (string)item["customDisplayName"],
                            FolderName = (string)item["folderName"],
                            InstallPath = (string)item["installPath"]
                        };

                        if (!string.IsNullOrEmpty(app.Name) && (!string.IsNullOrEmpty(app.FolderName) || !string.IsNullOrEmpty(app.InstallPath)))
                            ret.Add(app);
                    }
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Error("[QuiverLauncher] Unable to read apps.json: " + ex.Message); }

            return ret;
        }

        private static string GetAppExecutable(QuiverApp app, string appsFolder)
        {
            string gamePath = app.GetInstallFolder(appsFolder);
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return null;

            if (File.Exists(Path.Combine(gamePath, "install-incomplete.txt")))
            {
                SimpleLogger.Instance.Warning("[QuiverLauncher] Incomplete installation: " + app.Name);
                return null;
            }

            // Windows installer apps : executable stored in the receipt
            string receipt = Path.Combine(gamePath, "windows-installer.json");
            if (File.Exists(receipt))
            {
                try
                {
                    string exe = (string)JObject.Parse(File.ReadAllText(receipt)).GetValue("ExecutablePath", StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                        return exe;
                }
                catch { }

                return null;
            }

            // Executable chosen by the user in Quiver (absolute path)
            string selectedFile = Path.Combine(gamePath, "selected_executable.txt");
            if (File.Exists(selectedFile))
            {
                try
                {
                    string selected = File.ReadAllText(selectedFile).Trim();
                    if (File.Exists(selected))
                        return selected;

                    string rebased = RebaseOnFolder(selected, appsFolder);
                    if (rebased != null)
                        return rebased;
                }
                catch { }
            }

            // Same detection as Quiver : top directory first, then subfolders, only if there is no ambiguity
            var candidates = FindExecutableCandidates(gamePath, SearchOption.TopDirectoryOnly);
            if (candidates.Count == 0)
                candidates = FindExecutableCandidates(gamePath, SearchOption.AllDirectories);

            if (candidates.Count == 1)
                return candidates[0];

            if (candidates.Count > 1)
                SimpleLogger.Instance.Warning("[QuiverLauncher] Several executables found for " + app.Name + ", select one in Quiver Launcher first.");

            return null;
        }

        private static List<string> FindExecutableCandidates(string path, SearchOption option)
        {
            try
            {
                return new string[] { "*.exe", "*.bat", "*.cmd" }
                    .SelectMany(p => Directory.GetFiles(path, p, option))
                    .Distinct(StringComparer.InvariantCultureIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void UpdateQuiverGames()
        {
            try
            {
                string launcherPath = Program.AppConfig.GetFullPath("quiverlauncher");
                if (string.IsNullOrEmpty(launcherPath) || !Directory.Exists(launcherPath))
                {
                    SimpleLogger.Instance.Error("[QuiverLauncher] Invalid path.");
                    return;
                }

                var apps = LoadApps(launcherPath);
                if (apps.Count == 0)
                {
                    SimpleLogger.Instance.Error("[QuiverLauncher] No game in library.");
                    return;
                }

                string appsFolder = GetAppsFolder(launcherPath);

                string romPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "roms", "recomp");
                if (!Directory.Exists(romPath))
                    try { Directory.CreateDirectory(romPath); } catch { return; }

                foreach (var app in apps)
                {
                    string exe = GetAppExecutable(app, appsFolder);
                    if (string.IsNullOrEmpty(exe))
                        continue;

                    SimpleLogger.Instance.Info("[QuiverLauncher] Found: " + app.DisplayName);
                    CreateShortcut(app, exe, romPath);
                }
            }
            catch (Exception ex) { SimpleLogger.Instance.Error("[QuiverLauncher] Library scan failed: " + ex.Message); }
        }

        private static void CreateShortcut(QuiverApp app, string exe, string romPath)
        {
            string shortcutPath = Path.Combine(romPath, FileTools.CleanupName(app.DisplayName) + ".lnk");

            // Keep existing valid shortcuts, replace the ones pointing to the old launcher or to a missing file
            if (File.Exists(shortcutPath))
            {
                string existingTarget = null;
                try { existingTarget = FileTools.GetShortcutTargetwsh(shortcutPath); }
                catch { }

                bool isOldLauncher = !string.IsNullOrEmpty(existingTarget) && Path.GetFileName(existingTarget).Equals(OldLauncherExe, StringComparison.InvariantCultureIgnoreCase);
                if (!isOldLauncher && !string.IsNullOrEmpty(existingTarget) && File.Exists(existingTarget))
                    return;
            }

            dynamic shell = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exe;
                shortcut.Arguments = "";
                shortcut.WorkingDirectory = Path.GetDirectoryName(exe);
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            }
            catch (Exception ex) { SimpleLogger.Instance.Warning("[QuiverLauncher] Unable to create shortcut for " + app.DisplayName + ": " + ex.Message); }
            finally
            {
                if (shell != null)
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
        }

        private class QuiverApp
        {
            public string Name { get; set; }
            public string CustomDisplayName { get; set; }
            public string FolderName { get; set; }
            public string InstallPath { get; set; }

            public string DisplayName { get { return string.IsNullOrWhiteSpace(CustomDisplayName) ? Name : CustomDisplayName.Trim(); } }

            public string GetInstallFolder(string appsFolder)
            {
                if (!string.IsNullOrWhiteSpace(InstallPath))
                    return InstallPath;

                return string.IsNullOrWhiteSpace(FolderName) ? null : Path.Combine(appsFolder, FolderName);
            }

            public bool Matches(string name)
            {
                return string.Equals(Name, name, StringComparison.InvariantCultureIgnoreCase) ||
                       string.Equals(CustomDisplayName, name, StringComparison.InvariantCultureIgnoreCase) ||
                       string.Equals(FolderName, name, StringComparison.InvariantCultureIgnoreCase);
            }
        }
        #endregion

        #region json
        private static JObject LoadJson(string file)
        {
            if (File.Exists(file))
            {
                try { return JObject.Parse(File.ReadAllText(file)); }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[QuiverLauncher] Invalid json file " + file + ": " + ex.Message); }
            }

            return new JObject();
        }

        private static void SaveJson(string file, JObject jsonObj)
        {
            try { File.WriteAllText(file, jsonObj.ToString(Formatting.Indented)); }
            catch (Exception ex) { SimpleLogger.Instance.Warning("[QuiverLauncher] Unable to write " + file + ": " + ex.Message); }
        }
        #endregion
    }
}
