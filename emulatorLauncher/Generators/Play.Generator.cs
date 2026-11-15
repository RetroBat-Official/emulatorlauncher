using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
{
    partial class PlayGenerator : Generator
    {
        public PlayGenerator()
        {
            DependsOnDesktopResolution = false;
        }
        protected override bool UseGenericScreenPlacement { get { return true; } }
        private bool _isArcade = false;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _romName;
        private SaveStatesWatcher _saveStatesWatcher;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("play");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "Play.exe");
            if (!File.Exists(exe))
                return null;

            // Create portable file if it does not exist
            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            _isArcade = system == "namco2x6";
            bool fullscreen = ShouldRunFullscreen();

            // Archives: extract the game (formats read by Play!)
            string romExt = Path.GetExtension(rom).ToLowerInvariant();
            if (!_isArcade && (romExt == ".zip" || romExt == ".7z" || romExt == ".squashfs"))
            {
                string[] extensions = new string[] { ".m3u", ".chd", ".cue", ".mds", ".iso", ".isz", ".cso", ".bin" };

                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            // Play! cannot read m3u files: boot the first disc
            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
            {
                string firstDisc = FileTools.ReadFirstValidLine(rom);
                if (!string.IsNullOrEmpty(firstDisc))
                    rom = Path.Combine(Path.GetDirectoryName(rom), firstDisc.TrimStart('\\', '/'));
            }

            _romName = Path.GetFileNameWithoutExtension(rom);

            // Save states
            if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
                string emulatorPath = Path.Combine(path, "Play Data Files", "states");

                _saveStatesWatcher = new PlaySaveStatesMonitor(rom, emulatorPath, localPath);
                _saveStatesWatcher.PrepareEmulatorRepository();
            }

            //settings
            SetupConfiguration(path, rom, system);
            ConfigureControllers(path);

            //Applying bezels
            if (!fullscreen)
            {
                SystemConfig["forceNoBezel"] = "1"; // Force no bezel in windowed mode
            }

            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            var commandArray = new List<string>();

            if (_isArcade)
            {
                commandArray.Add("--arcade");
                commandArray.Add(Path.GetFileNameWithoutExtension(rom));
            }

            else
            {
                commandArray.Add("--disc");
                commandArray.Add("\"" + rom + "\"");
            }

            if (fullscreen)
                commandArray.Add("--fullscreen");

            // Load the state selected in EmulationStation
            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(SystemConfig["state_file"]) && File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("--state");
                commandArray.Add(_saveStatesWatcher.Slot.ToString());
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        /// <summary>
        /// Configure emulator features (config.xml)
        /// </summary>
        private void SetupConfiguration(string path, string rom, string system)
        {
            string dataPath = Path.Combine(path, "Play Data Files");
            FileTools.TryCreateDirectory(dataPath);

            string settingsFile = Path.Combine(dataPath, "config.xml");
            string templateConfigFile = Path.Combine(AppConfig.GetFullPath("templates"), "play", "Play Data Files", "config.xml");

            if (!File.Exists(settingsFile) && File.Exists(templateConfigFile))
                FileTools.TryCopyFile(templateConfigFile, settingsFile);

            try
            {
                XDocument configfile = File.Exists(settingsFile) ? XDocument.Load(settingsFile) : new XDocument(new XElement("Config"));

                SetPreference(configfile, "ui.showexitconfirmation", "boolean", "false");
                SetPreference(configfile, "ui.pausewhenfocuslost", "boolean", SystemConfig.getOptBoolean("nopauseonlostfocus") ? "false" : "true");

                // Write path to arcade roms
                if (_isArcade)
                    SetPreference(configfile, "ps2.arcaderoms.directory", "path", Path.GetDirectoryName(rom).Replace("\\", "/"));

                // Memory cards in the RetroBat saves folder
                SetPreference(configfile, "ps2.mc0.directory.v2", "path", GetMemoryCardPath(dataPath, system, "mc0"));
                SetPreference(configfile, "ps2.mc1.directory.v2", "path", GetMemoryCardPath(dataPath, system, "mc1"));

                // Language
                string language = GetPS2LanguageFromEnvironment();
                if (SystemConfig.isOptSet("play_language") && !string.IsNullOrEmpty(SystemConfig["play_language"]))
                    language = SystemConfig["play_language"];
                SetPreference(configfile, "system.language", "integer", language);

                // Limit framerate
                SetPreference(configfile, "ps2.limitframerate", "boolean", SystemConfig.isOptSet("play_limitframerate") && !SystemConfig.getOptBoolean("play_limitframerate") ? "false" : "true");

                // Resolution
                SetPreference(configfile, "renderer.opengl.resfactor", "integer", SystemConfig.isOptSet("play_resolution") && !string.IsNullOrEmpty(SystemConfig["play_resolution"]) ? SystemConfig["play_resolution"] : "1");

                // Aspect ratio
                SetPreference(configfile, "renderer.presentationmode", "integer", SystemConfig.isOptSet("play_presentation") && !string.IsNullOrEmpty(SystemConfig["play_presentation"]) ? SystemConfig["play_presentation"] : "1");

                // Bilinear filtering
                SetPreference(configfile, "renderer.opengl.forcebilineartextures", "boolean", SystemConfig.getOptBoolean("smooth") ? "true" : "false");

                // GS RAM reads (on by default in Play!)
                SetPreference(configfile, "renderer.ramreads.enabled", "boolean", SystemConfig.isOptSet("play_ramreads") && !SystemConfig.getOptBoolean("play_ramreads") ? "false" : "true");

                // Video driver
                SetPreference(configfile, "video.gshandler", "integer", SystemConfig.isOptSet("play_renderer") && !string.IsNullOrEmpty(SystemConfig["play_renderer"]) ? SystemConfig["play_renderer"] : "0");

                // Widescreen
                SetPreference(configfile, "renderer.widescreen", "boolean", SystemConfig.getOptBoolean("play_widescreen") ? "true" : "false");

                // Manage Input profiles
                string inputProfilePath = Path.Combine(dataPath, "inputprofiles");
                FileTools.TryCreateDirectory(inputProfilePath);

                string padProfile = "Retrobat";
                if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
                    padProfile = File.Exists(Path.Combine(inputProfilePath, _romName + ".xml")) ? _romName : "default";

                SetPreference(configfile, "input.pad1.profile", "string", padProfile);

                //save file
                configfile.Save(settingsFile);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Play] Unable to write config.xml : " + ex.Message);
            }
        }

        /// <summary>
        /// Updates a preference of config.xml, the preference is created when Play! has not written it yet
        /// </summary>
        private static void SetPreference(XDocument cfg, string name, string type, string value)
        {
            var pref = cfg.Descendants("Preference").FirstOrDefault(x => (string)x.Attribute("Name") == name);
            if (pref == null)
            {
                pref = new XElement("Preference", new XAttribute("Name", name), new XAttribute("Type", type));
                cfg.Root.Add(pref);
            }

            pref.SetAttributeValue("Value", value);
        }

        /// <summary>
        /// Memory card folder in saves\[system]\play, existing cards of the emulator folder are copied the first time
        /// </summary>
        private string GetMemoryCardPath(string dataPath, string system, string card)
        {
            string target = Path.Combine(AppConfig.GetFullPath("saves"), system == "namco2x6" ? "ps2" : system, "play", card);
            string oldPath = Path.Combine(dataPath, "vfs", card);

            try
            {
                if (!Directory.Exists(target) && Directory.Exists(oldPath) && Directory.EnumerateFileSystemEntries(oldPath).Any())
                    FileTools.CopyDirectory(oldPath, target, true);

                FileTools.TryCreateDirectory(target);
            }
            catch { }

            return target;
        }

        private string GetPS2LanguageFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, string>()
            {
                { "ja", "0" }, { "jp", "0" }, { "en", "1" }, { "fr", "2" }, { "es", "3" }, { "de", "4" },
                { "it", "5" }, { "nl", "6" }, { "pt", "7" }, { "ru", "8" }, { "ko", "9" }, { "zh", "11" }
            };

            if (SystemConfig["Language"] == "zh_TW")
                return "10";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang) && availableLanguages.TryGetValue(lang, out string ret))
                return ret;

            return "1";
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_saveStatesWatcher == null)
                return mapping;

            // Play! state shortcuts are fixed: Ctrl + Shift + slot (save), Ctrl + slot (load)
            string slot = _saveStatesWatcher.Slot.ToString();
            mapping = PadToKey.AddOrUpdateKeyMapping(mapping, "Play", InputKey.hotkey | InputKey.y, "(^+{" + slot + "})");
            mapping = PadToKey.AddOrUpdateKeyMapping(mapping, "Play", InputKey.hotkey | InputKey.x, "(^{" + slot + "})");

            return mapping;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
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