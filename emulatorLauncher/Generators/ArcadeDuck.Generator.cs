using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class ArcadeDuckGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ArcadeDuckDatabase _db;
        private ArcadeDuckDatabase.Game _game;
        private string _path;
        private SaveStatesWatcher _saveStatesWatcher;

        // SETTINGS_VERSION in ArcadeDuck qthost.cpp (3 in 0.5.3)
        private const string ArcadeDuckSettingsVersion = "3";

        public ArcadeDuckGenerator()
        {
            DependsOnDesktopResolution = true;
        }
        protected override bool UseGenericScreenPlacement { get { return true; } }

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "ArcadeDuck.exe"))
                return null;

            _path = path;

            // Disable zinc padtokey
            string padtokeyFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "padtokey", "zinc.keys");
            if (File.Exists(padtokeyFile))
            {
                AddFileForRestoration(padtokeyFile);
                try { File.Delete(padtokeyFile); } catch { }
            }

            // ArcadeDuck boots MAME .zip or .7z sets (non-merged, split or merged), identified by their file name
            string romExtension = Path.GetExtension(rom);
            bool isChd = romExtension.Equals(".chd", StringComparison.InvariantCultureIgnoreCase);
            if (!isChd && !romExtension.Equals(".zip", StringComparison.InvariantCultureIgnoreCase) && !romExtension.Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                throw new ApplicationException("ArcadeDuck only supports MAME .zip or .7z sets, or Taito G-Net .chd files.");

            _db = ArcadeDuckDatabase.Load(path);
            string biosPath = AppConfig.GetFullPath("bios");

            if (_db != null)
            {
                if (isChd)
                {
                    _game = _db.FindGameByChd(rom);

                    // Companion CHD of another game (listed by EmulationStation because of the .chd extension) : launch its set archive instead
                    if (_game == null)
                    {
                        string archive = GetCompanionChdSetArchive(rom);
                        if (archive != null)
                        {
                            SimpleLogger.Instance.Info("[ArcadeDuck] '" + Path.GetFileName(rom) + "' is a companion CHD, launching the set : " + archive);
                            rom = archive;
                            isChd = false;
                            _game = _db.FindGameByArchive(Path.GetFileName(rom));
                        }
                    }
                }
                else
                    _game = _db.FindGameByArchive(Path.GetFileName(rom));

                if (_game == null)
                {
                    if (isChd)
                        throw new ApplicationException("'" + Path.GetFileName(rom) + "' is not a CHD supported by ArcadeDuck (check the MAME CHD version).");

                    throw new ApplicationException("'" + Path.GetFileName(rom) + "' is not a set supported by ArcadeDuck (check the MAME set name).");
                }

                SimpleLogger.Instance.Info("[ArcadeDuck] Game '" + _game.Id + "' (" + _game.System + "), status : " + _game.Status);

                biosPath = GetBiosPath(rom, biosPath, isChd);

                // A G-Net CHD launched directly is the game media itself
                if (!isChd)
                    CheckCompanionMedia(rom);
            }

            bool fullscreen = ShouldRunFullscreen() && !SystemConfig.getOptBoolean("disable_fullscreen");

            string ratio = SystemConfig["arcadeduck_ratio"];
            if (fullscreen && ratio != "Stretch To Fill" && ratio != "16:9")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            SetupSettings(path, rom, system, biosPath, fullscreen);

            var commandArray = new List<string>
            {
                "-nogui"
            };

            if (fullscreen)
                commandArray.Add("-fullscreen");

            if (File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("-statefile");
                commandArray.Add("\"" + Path.GetFullPath(SystemConfig["state_file"]) + "\"");
            }

            commandArray.Add("--");
            commandArray.Add("\"" + rom + "\"");

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
            };
        }

        /// <summary>
        /// Set archive of a companion CHD (MAME layout : '<roms>\<set>\<disk>.chd' and '<roms>\<set>.zip'), null when the CHD belongs to no game.
        /// </summary>
        private string GetCompanionChdSetArchive(string chd)
        {
            var game = _db.FindGameByCompanionChd(chd);
            if (game == null || string.IsNullOrEmpty(game.Archive))
                return null;

            string title = Path.GetFileNameWithoutExtension(game.Archive);
            var romsDir = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(chd)));
            if (romsDir != null)
            {
                foreach (var extension in new[] { ".zip", ".7z" })
                {
                    string archive = Path.Combine(romsDir.FullName, title + extension);
                    if (File.Exists(archive))
                        return archive;
                }
            }

            throw new ApplicationException("'" + Path.GetFileName(chd) + "' belongs to '" + title + "' : '" + title + ".zip' (or .7z) not found" + (romsDir != null ? " in '" + romsDir.FullName + "'." : "."));
        }

        /// <summary>
        /// ArcadeDuck searches the BIOS archive in a single folder ([BIOS] SearchDirectory).
        /// MAME users usually keep BIOS archives next to the games, RetroBat MAME also reads them from the bios root: check both.
        /// </summary>
        private string GetBiosPath(string rom, string biosRoot, bool isChd)
        {
            string biosArchive = _db.GetBiosArchive(_game.System);
            if (string.IsNullOrEmpty(biosArchive))
                return biosRoot;

            string romDir = Path.GetDirectoryName(Path.GetFullPath(rom));
            var candidates = new List<string> { romDir };

            // MAME layout for a CHD only set : <roms>\<set>\<disk>.chd, the BIOS archive is in <roms>
            if (isChd && Directory.GetParent(romDir) != null)
                candidates.Add(Directory.GetParent(romDir).FullName);

            if (!string.IsNullOrEmpty(biosRoot))
                candidates.Add(biosRoot);

            foreach (var folder in candidates)
            {
                if (File.Exists(Path.Combine(folder, biosArchive)))
                {
                    SimpleLogger.Instance.Info("[ArcadeDuck] BIOS '" + biosArchive + "' found in : " + folder);
                    return folder;
                }
            }

            throw new ApplicationException("Missing BIOS '" + biosArchive + "' : place it next to the game or in the RetroBat bios folder.");
        }

        /// <summary>
        /// CHD media are searched in a folder named after the set (or its parent), next to the archive, like MAME.
        /// </summary>
        private void CheckCompanionMedia(string rom)
        {
            string romDir = Path.GetDirectoryName(Path.GetFullPath(rom));

            foreach (var media in _game.Media.Where(m => _game.IsChdMedia(m)))
            {
                string chd = _game.GetChdFileName(media);
                bool found = File.Exists(Path.Combine(romDir, _game.Id, chd)) ||
                             (!string.IsNullOrEmpty(_game.Parent) && File.Exists(Path.Combine(romDir, _game.Parent, chd)));

                if (!found)
                    throw new ApplicationException("Missing CHD : " + Path.Combine(_game.Id, chd) + " (folder next to the game archive).");
            }
        }

        /// <summary>
        /// Save states : with EmulationStation save states support, ArcadeDuck keeps its own 'savestates' folder (files named "<set>_<slot>.sav"
        /// and "<set>_resume.sav", System::GetGameSaveStateFileName) and the watcher copies each new state to the RetroBat saves folder.
        /// A state selected in EmulationStation is loaded with the -statefile command line argument.
        /// </summary>
        private void SetupSaveStates(IniFile ini, string path, string rom, string system)
        {
            bool esSaveStates = Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported("arcadeduck");

            string savesPath = esSaveStates ?
                Program.EsSaveStates.GetSavePath(system, "arcadeduck", "arcadeduck") :
                Path.Combine(AppConfig.GetFullPath("saves"), system, "arcadeduck", "sstates");

            FileTools.TryCreateDirectory(savesPath);

            if (esSaveStates && !string.IsNullOrEmpty(savesPath))
            {
                // Keep the emulator folder, the watcher listens to it and copies the states to the RetroBat folder
                ini.WriteValue("Folders", "SaveStates", "savestates");

                _saveStatesWatcher = new ArcadeDuckSaveStatesMonitor(rom, Path.Combine(path, "savestates"), savesPath);
                _saveStatesWatcher.PrepareEmulatorRepository();
                SimpleLogger.Instance.Info("[INFO] SavesStatesWatcher enabled.");
            }
            else
                ini.WriteValue("Folders", "SaveStates", savesPath);

            // Resume state on exit : ArcadeDuck default is true, only enable it for the RetroBat autosave option or when resuming an autosave
            bool autoSave = SystemConfig.getOptBoolean("autosave") || (_saveStatesWatcher != null && _saveStatesWatcher.IsLaunchingAutoSave());
            ini.WriteValue("Main", "SaveStateOnExit", autoSave ? "true" : "false");

            // Deflate instead of Zstandard (ArcadeDuck default) so that the state screenshot can be read for EmulationStation
            ini.WriteValue("Main", "SaveStateCompression", "DeflateDefault");
        }

        private string GetDefaultLanguage()
        {
            var availableLanguages = new Dictionary<string, string>()
            {
                { "de", "de" },
                { "en", "en" },
                { "es", "es" },
                { "fr", "fr" },
                { "jp", "ja" },
                { "ja", "ja" },
                { "ko", "ko" },
                { "pt", "pt-BR" },
                { "br", "pt-BR" },
                { "ru", "ru" },
                { "zh", "zh-CN" },
            };

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang) && availableLanguages.TryGetValue(lang, out string ret))
                return ret;

            return "en";
        }

        private void SetupSettings(string path, string rom, string system, string biosPath, bool fullscreen)
        {
            string iniFile = Path.Combine(path, "settings.ini");

            // AllowDuplicateValues : ArcadeDuck stores multiple bindings of a same control as repeated keys
            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.AllowDuplicateValues))
            {
                if (string.IsNullOrEmpty(ini.GetValue("Main", "SettingsVersion")))
                    ini.WriteValue("Main", "SettingsVersion", ArcadeDuckSettingsVersion);

                ini.WriteValue("Main", "SetupWizardIncomplete", "false");
                ini.WriteValue("Main", "ConfirmPowerOff", "false");
                ini.WriteValue("Main", "StartFullscreen", fullscreen ? "true" : "false");
                ini.WriteValue("Main", "PauseOnFocusLoss", SystemConfig.getOptBoolean("nopauseonlostfocus") ? "false" : "true");
                ini.WriteValue("Main", "EnableDiscordPresence", SystemConfig.getOptBoolean("discord") ? "true" : "false");
                ini.WriteValue("Main", "Language", GetDefaultLanguage());

                ini.WriteValue("AutoUpdater", "CheckAtStartup", "false");

                // With -nogui, launch errors are only written to stderr and to the log file : the log is read on exit to report them
                ini.WriteValue("Logging", "LogToFile", "true");

                // Folders
                if (!string.IsNullOrEmpty(biosPath))
                    ini.WriteValue("BIOS", "SearchDirectory", biosPath);

                SetupSaveStates(ini, path, rom, system);

                string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "arcadeduck");
                FileTools.TryCreateDirectory(screenshotsPath);
                ini.WriteValue("Folders", "Screenshots", screenshotsPath);

                // No RetroAchievements support for these arcade sets
                ini.WriteValue("Cheevos", "Enabled", "false");
                ini.WriteValue("Cheevos", "ChallengeMode", "false");

                // Bezel : ArcadeDuck draws a full window overlay image (Display.BezelEnabled / BezelPath)
                // The path is normalized with backslashes : ArcadeDuck opens files with an extended length path (\\?\) where '/' is not a separator
                if (_bezelFileInfo != null && !string.IsNullOrEmpty(_bezelFileInfo.PngFile) && File.Exists(_bezelFileInfo.PngFile))
                {
                    ini.WriteValue("Display", "BezelEnabled", "true");
                    ini.WriteValue("Display", "BezelPath", Path.GetFullPath(_bezelFileInfo.PngFile));
                }
                else
                {
                    ini.WriteValue("Display", "BezelEnabled", "false");
                    ini.WriteValue("Display", "BezelPath", "");
                }

                // Video
                BindIniFeature(ini, "GPU", "Renderer", "arcadeduck_renderer", "Automatic");
                BindIniFeature(ini, "GPU", "ResolutionScale", "arcadeduck_resolution", "1");
                BindIniFeature(ini, "GPU", "TextureFilter", "arcadeduck_texturefilter", "Nearest");
                BindBoolIniFeature(ini, "GPU", "TrueColor", "arcadeduck_truecolor", "true", "false");

                if (SystemConfig.getOptBoolean("arcadeduck_pgxp"))
                {
                    ini.WriteValue("GPU", "PGXPEnable", "true");
                    ini.WriteValue("GPU", "PGXPCulling", "true");
                    ini.WriteValue("GPU", "PGXPTextureCorrection", "true");
                    ini.WriteValue("GPU", "PGXPColorCorrection", "true");
                }
                else
                    ini.WriteValue("GPU", "PGXPEnable", "false");

                BindIniFeature(ini, "Display", "AspectRatio", "arcadeduck_ratio", "Auto (Game Native)");
                BindIniFeature(ini, "Display", "Scaling", "arcadeduck_scaling", "BilinearSmooth");
                BindBoolIniFeatureOn(ini, "Display", "VSync", "arcadeduck_vsync", "true", "false");
                BindBoolIniFeatureOn(ini, "Display", "ShowOSDMessages", "arcadeduck_osd", "true", "false");

                // Performance statistics
                string perf = SystemConfig["performance_overlay"];
                ini.WriteValue("Display", "ShowFPS", (perf == "simple" || perf == "detailed") ? "true" : "false");
                ini.WriteValue("Display", "ShowSpeed", perf == "detailed" ? "true" : "false");
                ini.WriteValue("Display", "ShowResolution", perf == "detailed" ? "true" : "false");
                ini.WriteValue("Display", "ShowCPU", perf == "detailed" ? "true" : "false");
                ini.WriteValue("Display", "ShowGPU", perf == "detailed" ? "true" : "false");

                // Audio
                BindIniFeature(ini, "Audio", "Backend", "arcadeduck_audiobackend", "Cubeb");

                // External outputs (lamps, recoil...) for MAMEHooker-like tools
                if (SystemConfig.isOptSet("arcadeduck_outputs") && !string.IsNullOrEmpty(SystemConfig["arcadeduck_outputs"]))
                {
                    ini.WriteValue("ArcadeOutput", "Enabled", "true");
                    ini.WriteValue("ArcadeOutput", "Protocol", SystemConfig["arcadeduck_outputs"]);
                }
                else
                    ini.WriteValue("ArcadeOutput", "Enabled", "false");

                // Controllers, guns, mice
                CreateControllerConfiguration(ini);

                ini.Save();
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int ret = base.RunAndWait(path);

            if (RawExitCode != 0 && ExitCode != ExitCodes.CustomError)
            {
                string error = GetLaunchError();
                if (!string.IsNullOrEmpty(error))
                {
                    SimpleLogger.Instance.Error("[ArcadeDuck] " + error);
                    SetCustomError("ArcadeDuck : " + error);
                }
            }

            return ret;
        }

        /// <summary>
        /// Last boot error written by ArcadeDuck in arcadeduck.log (EmuThread::bootSystem : "Failed to boot system: ..." or the save state message).
        /// Command line errors are written before the log file is opened, the generator checks the set, BIOS and CHD before launching instead.
        /// </summary>
        private string GetLaunchError()
        {
            try
            {
                string logFile = Path.Combine(_path, "arcadeduck.log");
                if (!File.Exists(logFile))
                    return null;

                string[] markers = { "Failed to boot system: ", "Save states are not yet implemented" };

                foreach (var line in File.ReadAllLines(logFile).Reverse())
                {
                    foreach (var marker in markers)
                    {
                        int idx = line.IndexOf(marker, StringComparison.Ordinal);
                        if (idx < 0)
                            continue;

                        // Keep the marker text for the save state message, which has no prefix
                        return marker.EndsWith(": ") ? line.Substring(idx + marker.Length).Trim() : line.Substring(idx).Trim();
                    }
                }
            }
            catch { }

            return null;
        }

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }
    }
}
