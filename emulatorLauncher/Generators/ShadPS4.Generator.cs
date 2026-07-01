using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmulatorLauncher
{
    partial class ShadPS4Generator : Generator
    {
        private bool _useLauncher = false;
        private bool _default = false;
        private string _versionselected = null;
        private bool _showGUI = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (!Directory.Exists(path))
                return null;

            string launcherFolder = Path.Combine(path, "launcher");
            if (!Directory.Exists(launcherFolder))
                try { Directory.CreateDirectory(launcherFolder); } catch { }

            string launcherExe = Path.Combine(path, "shadPS4QtLauncher.exe");
            string exe = null;

            if (File.Exists(launcherExe))
            {
                exe = GetShadPS4Executable(path);
                _useLauncher = true;
            }
            else
                exe = Path.Combine(path, "shadPS4.exe");
            
            if (exe == null || !File.Exists(exe))
                return null;

            string targetRom = "";
            string exePath = Path.GetDirectoryName(exe);

            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "eboot.bin", SearchOption.AllDirectories).FirstOrDefault();

                if (!File.Exists(rom))
                    throw new ApplicationException("Unable to find any game in the provided folder");
            }

            else if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                string romPath = Path.GetDirectoryName(rom);
                string romSubPath = File.ReadAllText(rom);
                rom = Path.Combine(romPath, romSubPath);
            }
            
            else if (Path.GetExtension(rom).ToLower() == ".lnk")
            {
                targetRom = FileTools.GetShortcutArgswsh(rom);
            }

            bool fullscreen = ShouldRunFullscreen();

            //settings
            SetupConfigurationJSON(path, rom, fullscreen, resolution);
            SetupUI(path);

            var commandArray = new List<string>();

            if (!_useLauncher)
            {
                if (SystemConfig.getOptBoolean("shadps4_gui"))
                {
                    commandArray.Add("-s");
                    _showGUI = true;
                }

                if (Path.GetExtension(rom).ToLower() == ".lnk")
                {
                    commandArray.Add(targetRom.Replace("/", "\\"));
                }
                else
                {
                    //commandArray.Add("-g");
                    commandArray.Add("\"" + rom + "\"");
                }
            }
            else
            {
                if (Path.GetExtension(rom).ToLower() == ".lnk")
                {
                    commandArray.Add(targetRom.Replace("/", "\\"));
                }
                else
                {
                    commandArray.Add("-g");
                    commandArray.Add("\"" + rom + "\"");
                }

                if (_default)
                    commandArray.Add("-d");
                else
                {
                    commandArray.Add("-e");
                    commandArray.Add("\"" + exe + "\"");
                }

                if (SystemConfig.getOptBoolean("shadps4_gui"))
                    commandArray.Add("-s");
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = _useLauncher ? launcherExe : exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private void SetupConfigurationJSON(string path, string rom, bool fullscreen, ScreenResolution resolution)
        {
            string userFolder = Path.Combine(path, "user");
            if (!Directory.Exists(userFolder))
                try { Directory.CreateDirectory(userFolder); } catch { }

            string configFile = Path.Combine(userFolder, "config.json");

            JObject root;

            if (File.Exists(configFile))
            {
                string jsonText = File.ReadAllText(configFile);
                root = JObject.Parse(jsonText);
            }
            else
            {
                root = new JObject { };
            }

            var audio = GetOrCreateObject(root, "Audio");
            var debug = GetOrCreateObject(root, "Debug");
            var gpu = GetOrCreateObject(root, "GPU");
            var general = GetOrCreateObject(root, "General");
            var input = GetOrCreateObject(root, "Input");
            var log = GetOrCreateObject(root, "Log");
            var vulkan = GetOrCreateObject(root, "Vulkan");

            // Roms folder
            string romPath = Path.GetDirectoryName(rom);
            if (Path.GetExtension(romPath).ToLowerInvariant() == ".ps4")
                romPath = Directory.GetParent(romPath).FullName;

            if (!(general["install_dirs"] is JArray installDirs))
            {
                installDirs = new JArray();
                general["install_dirs"] = installDirs;
            }

            if (!installDirs.Any(d => string.Equals(d["path"]?.ToString(), romPath, StringComparison.OrdinalIgnoreCase)))
                installDirs.Add(new JObject { ["enabled"] = true, ["path"] = romPath });

            BindBoolFeature(general, "discord_rpc_enabled", "discord");
            BindBoolFeatureOn(input, "motion_controls_enabled", "shadps4_motion");
            general["show_splash"] = false;

            string homeDir = Path.Combine(AppConfig.GetFullPath("saves"), "ps4", "shadps4", "home");
            if (!Directory.Exists(homeDir))
                try { Directory.CreateDirectory(homeDir); } catch { }
            general["home_dir"] = homeDir;

            // GPU section
            if (!fullscreen)
            {
                gpu["window_height"] = resolution == null ? ScreenResolution.CurrentResolution.Height : resolution.Height;
                gpu["window_width"] = resolution == null ? ScreenResolution.CurrentResolution.Width : resolution.Width;
            }
            BindBoolFeature(gpu, "hdr_allowed", "enable_hdr");

            if (fullscreen)
                gpu["full_screen"] = true;
            else
                gpu["full_screen"] = false;

            if (fullscreen && SystemConfig.getOptBoolean("exclusivefs"))
                gpu["full_screen_mode"] = "Fullscreen";
            else if (fullscreen)
                gpu["full_screen_mode"] = "Fullscreen (Borderless)";
            else
                gpu["full_screen_mode"] = "Windowed";

            // Settings section
            int ps4Lang = Getps4LangFromEnvironment();
            if (SystemConfig.isOptSet("shadps4_lang") && !string.IsNullOrEmpty(SystemConfig["shadps4_lang"]))
                try { ps4Lang = SystemConfig["shadps4_lang"].ToInteger(); } catch { }
            
            general["console_language"] = ps4Lang;

            string dlcPath = Path.Combine(romPath, "ps4", "DLC");
            if (!Directory.Exists(dlcPath))
                try { Directory.CreateDirectory(dlcPath); } catch { }
            
            general["addonInstallDir"] = dlcPath;

            //SetupController(input);

            string jsonString = root.ToString(Formatting.Indented);
            try { File.WriteAllText(configFile, jsonString); } catch { }
        }

        private void SetupUI(string path)
        {
            string uiFile = Path.Combine(path, "launcher", "qt_ui.ini");

            if (!_useLauncher)
                uiFile = Path.Combine(path, "user", "qt_ui.ini");

            using (var ini = new IniFile(uiFile))
            {
                string versionPath = Path.Combine(path, "launcher", "versions");
                string escaped = Regex.Replace(versionPath, @"(?<!\\)\\(?!\\)", @"\\");
                ini.WriteValue("version_manager", "versionPath", escaped);

                string selectedVersion = ini.GetValue("version_manager", "selectedVersion");

                if (string.IsNullOrEmpty(selectedVersion) && _versionselected != null)
                    ini.WriteValue("version_manager", "versionSelected", _versionselected);

                ini.WriteValue("general_settings", "checkForUpdates", "false");
                ini.WriteValue("general_settings", "showChangeLog", "false");

                ini.Save();
            }
        }

        private void UpdateSdlControllersWithHints()
        {
            if (Program.Controllers.Count(c => !c.IsKeyboard) == 0)
                return;

            var hints = new List<string>
            {
                "SDL_JOYSTICK_RAWINPUT = 1",
            };

            if (SystemConfig.getOptBoolean("ps_controller_enhanced"))
            {
                hints.Add("SDL_JOYSTICK_HIDAPI_PS4_RUMBLE = 1");
                hints.Add("SDL_JOYSTICK_HIDAPI_PS5_RUMBLE = 1");
            }

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private void SetupController(JObject input)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            UpdateSdlControllersWithHints();

            var ctrl = this.Controllers.Where(c => c.PlayerIndex == 1).FirstOrDefault();

            if (ctrl?.Config == null)
                return;

            if (ctrl.Sdl3Controller == null || string.IsNullOrEmpty(ctrl.Sdl3Controller.GuidString))
                return;

            try
            {
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "1", EnvironmentVariableTarget.Process);
            }
            catch { }

            input["use_unified_input_config"] = true;
            input["default_controller_id"] = ctrl.Sdl3Controller.GuidString;
        }

        private int Getps4LangFromEnvironment()
        {
            SimpleLogger.Instance.Info("[Generator] Getting Language from RetroBat language.");

            var availableLanguages = new Dictionary<string, int>()
            {
                { "ja", 0 },
                { "jp", 0 },
                { "en", 1 },
                { "fr", 2 },
                { "es", 3 },
                { "de", 4 },
                { "it", 5 },
                { "nl", 6 },
                { "pt", 7 },
                { "ru", 8 },
                { "ko", 9 },
                { "zh", 11 },
                { "fi", 12 },
                { "sv", 13 },
                { "nn", 15 },
                { "nb", 15 },
                { "pl", 16 },
                { "tr", 19 },
            };

            // Special case for some variances
            if (SystemConfig["Language"] == "zh_TW")
                return 10;
            else if (SystemConfig["Language"] == "pt_BR")
                return 17;
            else if (SystemConfig["Language"] == "en_GB")
                return 18;
            else if (SystemConfig["Language"] == "cs_CZ")
                return 23;
            else if (SystemConfig["Language"] == "ja_JP")
                return 0;

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out int ret))
                    return ret;
            }

            return 1;
        }

        private string GetShadPS4Executable(string path)
        {
            string requestedVersion = null;

            if (SystemConfig.isOptSet("shadps4_version") && !string.IsNullOrEmpty(SystemConfig["shadps4_version"]))
                requestedVersion = SystemConfig["shadps4_version"];

            if (requestedVersion == "default")
                _default = true;

            string jsonFile = Path.Combine(path, "launcher", "versions.json");
            if (!File.Exists(jsonFile))
            {
                SimpleLogger.Instance.Warning("versions.json not found!");
                string exeFile = Directory.GetFiles(path, "*", SearchOption.AllDirectories).FirstOrDefault(f => Path.GetFileName(f).Equals("shadPS4.exe", StringComparison.OrdinalIgnoreCase) && (requestedVersion == null || Path.GetDirectoryName(f).Contains(requestedVersion)));

                if (exeFile == null)
                {
                    SimpleLogger.Instance.Warning("Specific version not found in launcher path, searching first available executable.");
                    exeFile = Directory.GetFiles(path, "*", SearchOption.AllDirectories).FirstOrDefault(f => Path.GetFileName(f).Equals("shadPS4.exe", StringComparison.OrdinalIgnoreCase));
                }

                if (exeFile != null)
                    return exeFile;
                else
                    throw new ApplicationException("Unable to find requested version of shadPS4 executable.");
            }
            else
            {
                List<ShadPS4Version> versions = null;
                try
                {
                    var json = File.ReadAllText(jsonFile);
                    versions = JsonConvert.DeserializeObject<List<ShadPS4Version>>(json);

                    var versionRegex = new Regex(@"v\.?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)", RegexOptions.IgnoreCase);

                    var parsed = versions.Select(v =>
                    {
                        Version ver = null;
                        var match = versionRegex.Match(v.name ?? "");
                        if (match.Success)
                        {
                            ver = new Version(
                                int.Parse(match.Groups["major"].Value),
                                int.Parse(match.Groups["minor"].Value),
                                int.Parse(match.Groups["patch"].Value));
                        }

                        DateTime.TryParse(v.date, out var parsedDate);
                        return new
                        {
                            Entry = v,
                            Version = ver,
                            Date = parsedDate
                        };
                    }).ToList();

                    dynamic target = null;

                    // If user requested something specific
                    if (!string.IsNullOrEmpty(requestedVersion))
                    {
                        if (requestedVersion.Equals("pre-release", StringComparison.OrdinalIgnoreCase))
                        {
                            target = parsed
                                .Where(x => x.Entry.type == 1)
                                .OrderByDescending(x => x.Date)
                                .FirstOrDefault();
                        }
                        else
                        {
                            target = parsed.FirstOrDefault(x =>
                                x.Version != null && x.Version.ToString() == requestedVersion);
                        }
                    }

                    if (target == null)
                    {
                        target = parsed
                            .Where(x => x.Version != null)
                            .OrderByDescending(x => x.Version)
                            .FirstOrDefault();
                    }

                    if (target == null)
                    {
                        string exeFile = Directory.GetFiles(path, "*", SearchOption.AllDirectories).FirstOrDefault(f => Path.GetFileName(f).Equals("shadPS4.exe", StringComparison.OrdinalIgnoreCase) && (requestedVersion == null || Path.GetDirectoryName(f).Contains(requestedVersion)));

                        if (exeFile == null)
                        {
                            SimpleLogger.Instance.Warning("Specific version not found in versions json, searching first available executable.");
                            exeFile = Directory.GetFiles(path, "*", SearchOption.AllDirectories).FirstOrDefault(f => Path.GetFileName(f).Equals("shadPS4.exe", StringComparison.OrdinalIgnoreCase));
                        }

                        if (exeFile != null)
                            return exeFile;
                        else
                            throw new ApplicationException("Unable to find requested version of shadPS4 executable.");
                    }

                    else
                    {
                        _versionselected = target.Entry.path;

                        if ((target.Entry.path).StartsWith("./"))
                            return Path.Combine(path, target.Entry.path.Replace("./", "").Replace("/", "\\"));
                        else
                            return target.Entry.path;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing versions.json: {ex.Message}");
                }
            }
            
            return null;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_showGUI)
            {
                return PadToKey.AddOrUpdateKeyMapping(mapping, "shadPS4", InputKey.hotkey | InputKey.start,
                    action: () =>
                    {
                        var shadProcesses = Process.GetProcesses()
                        .Where(p => p.ProcessName.StartsWith("shadps4", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                        if (shadProcesses == null)
                            return;

                        foreach (var p in shadProcesses)
                        {
                            try
                            {
                                if (!p.HasExited)
                                {
                                    p.CloseMainWindow();
                                    p.WaitForExit(3000);
                                }
                            }
                            catch { }

                            try
                            {
                                if (!p.HasExited)
                                    p.Kill();
                            }
                            catch { }
                        }
                    });
            }
            else
                return mapping;
        }

        private static JObject GetOrCreateObject(JObject parent, string key)
        {
            if (!(parent[key] is JObject obj))
            {
                obj = new JObject();
                parent[key] = obj;
            }
            return obj;
        }
    }

    public class ShadPS4Version
    {
        public string codename { get; set; }
        public string date { get; set; }
        public string name { get; set; }
        public string path { get; set; }
        public int type { get; set; } // 1 = Pre-release, 0 = Stable
    }
}
