using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private void ConfigurePort(List<string> commandArray, string rom, string exe)
        {
            // Add one method per port to configure, you can pass commandArray if the port requires special command line arguments
            // If the port allows controller configuration, create the controller configuration method in PortsLauncher.Controller.cs and call it from the port configuration method
            // Keep alphabetical order

            ConfigureCDogs(commandArray, rom);
            Configurecgenius(commandArray, rom);
            Configurecorsixth(commandArray, rom);
            Configuredhewm3(commandArray, rom);
            ConfigureOpenGoal(commandArray, rom);
            ConfigureOpenJazz(commandArray, rom);
            ConfigurePDark(commandArray, rom);
            ConfigureSOH(rom, exe);
            ConfigureSonic3air(rom, exe);
            ConfigureSonicMania(rom, exe);
            ConfigureSonicRetro(rom, exe);
            ConfigureStarship(rom, exe);
        }

        #region ports
        private void ConfigureCDogs(List<string> commandArray, string rom)
        {
            if (_emulator != "cdogs")
                return;

            // Write environment variable
            string envConfigPath = AppConfig.GetFullPath(_emulator) + "\\";
            Environment.SetEnvironmentVariable("CDOGS_CONFIG_DIR", envConfigPath, EnvironmentVariableTarget.User);

            string configPath = Path.Combine(AppConfig.GetFullPath(_emulator), "C-Dogs SDL");
            if (!Directory.Exists(configPath))
                try { Directory.CreateDirectory(configPath); } catch { }

            // Create settings file if not existing
            string configJSON = Path.Combine(configPath, "options.cnf");
            if (!File.Exists(configJSON))
            {
                try
                {
                    File.WriteAllText(configJSON, cdogs_config);
                    System.Threading.Thread.Sleep(100);
                }
                catch { }
            }

            // Settings file update
            var height = _resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height;
            var width = _resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width;
            
            var settings = DynamicJson.Load(configJSON);
            var graphics = settings.GetOrCreateContainer("Graphics");
            var jsoninterface = settings.GetOrCreateContainer("Interface");
            
            graphics["Fullscreen"] = _fullscreen? "true" : "false";
            graphics["WindowWidth"] = width.ToString();
            graphics["WindowHeight"] = height.ToString();
            BindBoolFeatureOn(graphics, "ShowHUD", "cdogs_hud", "true", "false");
            BindFeature(graphics, "ScaleMode", "cdogs_scalemode", "Nearest neighbor");
            BindBoolFeatureOn(graphics, "Shadows", "cdogs_shadows", "true", "false");

            BindBoolFeature(jsoninterface, "ShowFPS", "cdogs_fps", "true", "false");
            BindBoolFeature(jsoninterface, "ShowTime", "cdogs_time", "true", "false");
            BindBoolFeatureOn(jsoninterface, "ShowHUDMap", "cdogs_hudmap", "true", "false");
            BindFeature(jsoninterface, "Splitscreen", "cdogs_splitscreen", "Never");

            ConfigureCDogsControls(settings);

            settings.Save();
        }
        private void Configurecgenius(List<string> commandArray, string rom)
        {
            if (_emulator != "cgenius")
                return;

            string cfgFile = Path.Combine(_path, "cgenius.cfg");

            using (var ini = IniFile.FromFile(cfgFile, IniOptions.UseSpaces))
            {
                try
                {
                    // Paths
                    ini.WriteValue("FileHandling", "EnableLogfile", "true");
                    ini.WriteValue("FileHandling", "SearchPath1", "${BIN}");
                    ini.WriteValue("FileHandling", "SearchPath2", Path.Combine(AppConfig.GetFullPath("roms"), "cgenius"));

                    //Video
                    /// Screen size
                    if (_resolution != null)
                    {
                        ini.WriteValue("Video", "height", _resolution.Height.ToString());
                        ini.WriteValue("Video", "width", _resolution.Width.ToString());
                    }
                    else
                    {
                        ini.WriteValue("Video", "height", Screen.PrimaryScreen.Bounds.Height.ToString());
                        ini.WriteValue("Video", "width", Screen.PrimaryScreen.Bounds.Width.ToString());
                    }

                    /// Game resolution
                    if (SystemConfig.isOptSet("cgenius_resolution") && !string.IsNullOrEmpty(SystemConfig["cgenius_resolution"]))
                    {
                        string[] gameRes = SystemConfig["cgenius_resolution"].Split('x');
                        string gameWidth = gameRes[0];
                        string gameHeight = gameRes[1];

                        ini.WriteValue("Video", "gameWidth", gameWidth);
                        ini.WriteValue("Video", "gameHeight", gameHeight);
                    }
                    else
                    {
                        ini.WriteValue("Video", "gameWidth", "320");
                        ini.WriteValue("Video", "gameHeight", "200");
                    }

                    ini.WriteValue("Video", "fullscreen", _fullscreen ? "true" : "false");
                    BindIniFeature(ini, "Video", "filter", "cgenius_filter", "1");
                    BindIniFeature(ini, "Video", "aspect", "ratio", "4:3");
                    BindIniFeature(ini, "Video", "OGLfilter", "OGLfilter", "nearest");
                    BindBoolIniFeatureOn(ini, "Video", "vsync", "cgenius_vsync", "true", "false");
                    BindBoolIniFeature(ini, "Video", "integerScaling", "integerscale", "true", "false");
                    BindBoolIniFeature(ini, "Video", "TiltedScreen", "TiltedScreen", "true", "false");
                    BindIniFeatureSlider(ini, "Video", "fps", "cgenius_fps", "60");

                    // Game options
                    BindBoolIniFeatureOn(ini, "Game", "hud", "cgenius_hud", "true", "false");
                    BindBoolIniFeature(ini, "Game", "showfps", "cgenius_fps", "true", "false");

                    ConfigureCGeniusControls(ini);
                }
                catch 
                {
                    SimpleLogger.Instance.Warning("[WARNING] Error opening cgenius.cfg config file.");
                }
            }

            string relativeGamePath = Path.GetDirectoryName(rom);
            string romPath = Path.Combine(AppConfig.GetFullPath("roms"), "cgenius");
            relativeGamePath = relativeGamePath.Replace(romPath, "");
            relativeGamePath = relativeGamePath.Replace('\\', '/');
            int index = relativeGamePath.IndexOf('/');
            if (index > -1 && relativeGamePath.StartsWith("/"))
                relativeGamePath = relativeGamePath.Remove(index, 1);

            commandArray.Add("dir=" + "\"" + relativeGamePath + "\"");
        }

        private void Configurecorsixth(List<string> commandArray, string rom)
        {
            if (_emulator != "corsixth")
                return;

            string cfgFile = Path.Combine(_path, "config.txt");
            if (!File.Exists(cfgFile))
            {
                try { 
                    File.WriteAllText(cfgFile, corsixth_config);
                    System.Threading.Thread.Sleep(100);
                }
                catch { }
            }
            string hotkeyFile = Path.Combine(_path, "hotkeys.txt");
            if (!File.Exists(hotkeyFile))
            {
                try { 
                    File.WriteAllText(hotkeyFile, corsixth_hotkeys);
                    System.Threading.Thread.Sleep(100);
                }
                catch { }
            }

            using (var ini = IniFile.FromFile(cfgFile, IniOptions.UseSpaces | IniOptions.KeepEmptyLines))
            {
                try
                {
                    // Paths
                    ini.WriteValue("", "theme_hospital_install", "[[" + rom + "]]");
                    
                    string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "corsixth");
                    if (!Directory.Exists(savesPath))
                    {
                        try { Directory.CreateDirectory(savesPath); }
                        catch { }
                    }
                    ini.WriteValue("", "savegames", "[[" + savesPath + "]]");

                    string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "corsixth");
                    if (!Directory.Exists(screenshotsPath))
                    {
                        try { Directory.CreateDirectory(screenshotsPath); }
                        catch { }
                    }
                    ini.WriteValue("", "screenshots", "[[" + screenshotsPath + "]]");

                    //Video
                    ini.WriteValue("", "fullscreen", _fullscreen ? "true" : "false");

                    /// Game resolution
                    string gameWidth = Screen.PrimaryScreen.Bounds.Width.ToString();
                    string gameHeight = Screen.PrimaryScreen.Bounds.Height.ToString();

                    if (_resolution != null)
                    {
                        gameWidth = _resolution.Width.ToString();
                        gameHeight = _resolution.Height.ToString();
                    }

                    else if (SystemConfig.isOptSet("th_resolution") && !string.IsNullOrEmpty(SystemConfig["th_resolution"]))
                    {
                        string[] gameRes = SystemConfig["th_resolution"].Split('x');
                        gameWidth = gameRes[0];
                        gameHeight = gameRes[1];
                    }

                    int width = gameWidth.ToInteger();
                    int height = gameHeight.ToInteger();

                    if (height > 0)
                    {
                        float ratio = (float)width / height;

                        if (ratio > 1.4f)
                            _nobezels = true;
                    }

                    ini.WriteValue("", "width", gameWidth);
                    ini.WriteValue("", "height", gameHeight);

                    ini.WriteValue("", "check_for_updates", "false");
                    BindBoolIniFeatureOn(ini, "", "capture_mouse", "th_capture_mouse", "true", "false");
                    if (SystemConfig.isOptSet("th_language") && !string.IsNullOrEmpty(SystemConfig["th_language"]))
                        ini.WriteValue("", "language", "[[" + SystemConfig["th_language"] + "]]");
                    else
                        ini.WriteValue("", "language", "[[English]]");

                    ini.Save();
                }
                catch
                {
                    SimpleLogger.Instance.Warning("[WARNING] Error opening config.txt config file.");
                }
            }

            // Controls
            using (var iniHk = IniFile.FromFile(hotkeyFile, IniOptions.UseSpaces | IniOptions.KeepEmptyLines))
            {
                try
                {
                    iniHk.WriteValue("", "global_exitApp", "{[[alt]],[[f4]]}");
                    iniHk.Save();
                }
                catch
                {
                    SimpleLogger.Instance.Warning("[WARNING] Error opening hotkeys.txt config file.");
                }
            }
            commandArray.Add("--config-file=" + "\"" + cfgFile + "\"");
            commandArray.Add("--hotkeys-file=" + "\"" + hotkeyFile + "\"");
        }

        private void Configuredhewm3(List<string> commandArray, string rom)
        {
            if (_emulator != "dhewm3")
                return;

            bool d3xp = false;
            string cfgPath = _path;
            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "doom3", "dhewm3");
            if (!Directory.Exists(savesPath))
                try { Directory.CreateDirectory(savesPath); } catch { }

            commandArray.Add("+set");
            commandArray.Add("fs_basepath");
            commandArray.Add("\"" + _romPath + "\"");
            commandArray.Add("+set");
            commandArray.Add("fs_configpath");
            commandArray.Add("\"" + cfgPath + "\"");
            commandArray.Add("+set");
            commandArray.Add("fs_savepath");
            commandArray.Add("\"" + savesPath + "\"");

            if (_fullscreen)
            {
                commandArray.Add("+set");
                commandArray.Add("r_fullscreen");
                commandArray.Add("1");
            }
            else
            {
                commandArray.Add("+set");
                commandArray.Add("r_fullscreen");
                commandArray.Add("0");
            }

            if (_resolution != null)
            {
                commandArray.Add("+set");
                commandArray.Add("r_fullscreenDesktop");
                commandArray.Add("0");
            }
            else
            {
                commandArray.Add("+set");
                commandArray.Add("r_fullscreenDesktop");
                commandArray.Add("1");
            }

            string[] pakFile = File.ReadAllLines(rom);
            if (pakFile.Length < 1)
            {
                throw new ApplicationException("Empty game file.");
            }

            string pakSubPath = pakFile[0];
            string game = pakSubPath.Split('\\')[0];

            commandArray.Add("+set");
            commandArray.Add("fs_game");
            commandArray.Add(game);

            if (pakFile.Length > 1)
            {
                for (int i = 1; i < pakFile.Length; i++)
                {
                    commandArray.Add(pakFile[i]);
                }
            }

            var changes = new List<Dhewm3ConfigChange>();
            string cfgFile = Path.Combine(_path, game, "dhewm.cfg");

            if (SystemConfig.isOptSet("dhewm3_resolution") && !string.IsNullOrEmpty(SystemConfig["dhewm3_resolution"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_mode", SystemConfig["dhewm3_resolution"]));

            if (!SystemConfig.isOptSet("dhewm3_vsync") || SystemConfig.getOptBoolean("dhewm3_vsync"))
                changes.Add(new Dhewm3ConfigChange("seta", "r_swapInterval", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_swapInterval", "0"));
            
            if (SystemConfig.isOptSet("dhewm3_quality") && !string.IsNullOrEmpty(SystemConfig["dhewm3_quality"]))
                changes.Add(new Dhewm3ConfigChange("seta", "com_machineSpec", SystemConfig["dhewm3_quality"]));

            if (SystemConfig.isOptSet("dhewm3_antialiasing") && !string.IsNullOrEmpty(SystemConfig["dhewm3_antialiasing"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_multiSamples", SystemConfig["dhewm3_antialiasing"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_multiSamples", "0"));

            if (SystemConfig.isOptSet("dhewm3_sound") && !string.IsNullOrEmpty(SystemConfig["dhewm3_sound"]))
                changes.Add(new Dhewm3ConfigChange("seta", "s_numberOfSpeakers", SystemConfig["dhewm3_sound"]));

            if (SystemConfig.isOptSet("dhewm3_eax") && SystemConfig.getOptBoolean("dhewm3_eax"))
                changes.Add(new Dhewm3ConfigChange("seta", "s_useEAXReverb", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "s_useEAXReverb", "0"));

            if (SystemConfig.isOptSet("dhewm3_playerName") && !string.IsNullOrEmpty(SystemConfig["dhewm3_playerName"]))
                changes.Add(new Dhewm3ConfigChange("seta", "ui_name", SystemConfig["dhewm3_playerName"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "ui_name", "RetroBat"));

            if (SystemConfig.isOptSet("dhewm3_hideHUD") && SystemConfig.getOptBoolean("dhewm3_hideHUD"))
                changes.Add(new Dhewm3ConfigChange("seta", "g_showHud", "0"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "g_showHud", "1"));

            ConfigureDhewm3Controls(changes);

            ConfigEditor.ChangeConfigValues(cfgFile, changes);

            // Cleanup if disabled autoconfigure
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                string[] lines = File.ReadAllLines(cfgFile);
                lines = lines.Where(line => !line.StartsWith("bind \"JOY")).ToArray();
                File.WriteAllLines(cfgFile, lines);
            }
        }

        private void ConfigureOpenGoal(List<string> commandArray, string rom)
        {
            List<string> openGoalGames = new List<string> { "jak1", "jak2" };
            Dictionary<string, string> openGoalDefaultRes = new Dictionary<string, string> 
            {
                { "jak1", "aspect4x3 4 3 #t" },
                { "jak2", "aspect4x3 4 3 #t" }
             };

            Dictionary<string, string> jak1ResList = new Dictionary<string, string>
            {
                { "4_3", "aspect16x9 4 3 #f" },
                { "16_9", "aspect16x9 16 9 #f" },
                { "4_3_ps2", "aspect4x3 4 3 #f" },
                { "16_9_ps2", "aspect16x9 4 3 #f" },
                { "16_10", "aspect4x3 16 10 #f" },
                { "21_9", "aspect4x3 21 9 #f" },
                { "64_27", "aspect4x3 64 27 #f" }
             };

            Dictionary<string, string> jak2ResList = new Dictionary<string, string>
            {
                { "4_3", "aspect4x3 4 3 #t" },
                { "16_9", "aspect4x3 16 9 #f" },
                { "4_3_ps2", "aspect4x3 0 0 " },
                { "16_9_ps2", "aspect16x9 0 0 " },
                { "16_10", "aspect4x3 16 10 #f" },
                { "21_9", "aspect4x3 21 9 #f" },
                { "64_27", "aspect4x3 64 27 #f" }
             };

            if (_emulator != "opengoal")
                return;

            string gameName = Path.GetFileNameWithoutExtension(rom);
            string[] romLines = File.ReadAllLines(rom);
            if (romLines.Length > 0)
                gameName = romLines[0].Trim();

            if (!openGoalGames.Contains(gameName))
                throw new ApplicationException("Game not supported by engine, ensure the game name is correct : " + gameName);

            commandArray.Add("-g");
            commandArray.Add(gameName);

            string configFolder = Path.Combine(_path, "config");
            if (!Directory.Exists(configFolder))
                try { Directory.CreateDirectory(configFolder); } catch { }

            commandArray.Add("--config-path");
            commandArray.Add("\"" + configFolder + "\"");

            // Settings file
            string gameConfigPath = Path.Combine(configFolder, "OpenGOAL", gameName);
            string debugSettingsFile = Path.Combine(gameConfigPath, "misc", "debug-settings.json");

            // Debug Settings file
            var debugSettings = DynamicJson.Load(debugSettingsFile);

            debugSettings["alternate_style"] = "false";
            debugSettings["ignore_hide_imgui"] = "false";
            debugSettings["monospaced_font"] = "true";
            debugSettings["show_imgui"] = "false";
            debugSettings["text_check_range"] = "false";
            debugSettings.Save();

            // Game settings - to check settings for jak 2
            string configFilePath = Path.Combine(gameConfigPath, "settings", "pc-settings.gc");

            if (!File.Exists(configFilePath))
                return;

            string[] configLines = File.ReadAllLines(configFilePath);
            
            Action<string, string> bindFeature = (string feature, string value) =>
            {
                for (int i = 0; i < configLines.Length; i++)
                {
                    if (configLines[i].Contains("(" + feature))
                    {
                        configLines[i] = "  ("+ feature + " " + value + ")";
                        break;
                    }
                }
            };

            if (SystemConfig.isOptSet("opengoal_msaa") && !string.IsNullOrEmpty(SystemConfig["opengoal_msaa"]))
                bindFeature("msaa", SystemConfig["opengoal_msaa"]);
            else
                bindFeature("msaa", "2");

            // ratio values are different per game
            if (SystemConfig.isOptSet("opengoal_ratio") && !string.IsNullOrEmpty(SystemConfig["opengoal_ratio"]))
            {
                string opengalRatio = SystemConfig["opengoal_ratio"];

                switch (gameName)
                {
                    case "jak1":
                        bindFeature("aspect-state", jak1ResList.ContainsKey(opengalRatio) ? jak1ResList[opengalRatio] : "");
                        break;
                    case "jak2":
                        bindFeature("aspect-state", jak2ResList.ContainsKey(opengalRatio) ? jak2ResList[opengalRatio] : "");
                        break;
                    default:
                        bindFeature("aspect-state", openGoalDefaultRes[gameName]);
                        break;
                }

                if (opengalRatio.EndsWith("_ps2"))
                    bindFeature("use-vis?", "#t");
                else
                    bindFeature("use-vis?", "#f");
            }
            else
                bindFeature("aspect-state", openGoalDefaultRes.ContainsKey(gameName) ? openGoalDefaultRes[gameName] : "");

            bindFeature("display-mode", "borderless");

            if (SystemConfig.isOptSet("opengoal_resolution") && !string.IsNullOrEmpty(SystemConfig["opengoal_resolution"]))
                bindFeature("game-size", SystemConfig["opengoal_resolution"]);
            else
            {
                string width = Screen.PrimaryScreen.Bounds.Width.ToString();
                string height = Screen.PrimaryScreen.Bounds.Height.ToString();
                string res = _resolution == null ? width + " " + height : _resolution.Width.ToString() + " " + _resolution.Height.ToString();
                bindFeature("game-size", res);
            }

            if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
            {
                int index = SystemConfig["MonitorIndex"].ToInteger();
                if (index >= 0)
                    bindFeature("monitor", (index - 1).ToString());
            }
            else
                bindFeature("monitor", "0");

            bindFeature("letterbox", "#t");

            if (SystemConfig.isOptSet("opengoal_vsync") && !SystemConfig.getOptBoolean("opengoal_vsync"))
                bindFeature("vsync", "#f");
            else
                bindFeature("vsync", "#t");

            if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                bindFeature("discord-rpc?", "#t");
            else
                bindFeature("discord-rpc?", "#f");

            if (SystemConfig.isOptSet("opengoal_subtitles") && !SystemConfig.getOptBoolean("opengoal_subtitles"))
                bindFeature("subtitles?", "#f");
            else
                bindFeature("subtitles?", "#t");

            if (SystemConfig.isOptSet("opengoal_region") && !string.IsNullOrEmpty(SystemConfig["opengoal_region"]))
                bindFeature("territory", SystemConfig["opengoal_region"]);
            else
                bindFeature("territory", "-1");

            if (SystemConfig.isOptSet("opengoal_language") && !string.IsNullOrEmpty(SystemConfig["opengoal_language"]))
                bindFeature("game-language", SystemConfig["opengoal_language"]);

            if (SystemConfig.isOptSet("opengoal_menulang") && !string.IsNullOrEmpty(SystemConfig["opengoal_menulang"]))
                bindFeature("text-language", SystemConfig["opengoal_menulang"]);

            if (SystemConfig.isOptSet("opengoal_sublang") && !string.IsNullOrEmpty(SystemConfig["opengoal_sublang"]))
                bindFeature("subtitle-language", SystemConfig["opengoal_sublang"]);

            File.WriteAllLines(configFilePath, configLines);
        }

        private void ConfigureOpenJazz(List<string> commandArray, string rom)
        {
            if (_emulator != "openjazz")
                return;

            // Command array
            if (_fullscreen)
                commandArray.Add("-f");
            else
                commandArray.Add("--window");

            commandArray.Add("\"" + _romPath + "\"");

            // Configuration
            string configFile = Path.Combine(_path, "openjazz.cfg");

            if (!File.Exists(configFile))
            {
                string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "openjazz", "openjazz.cfg");
                if (File.Exists(templateFile))
                    try { File.Copy(templateFile, configFile); } catch { }
            }

            if (!File.Exists(configFile))
                return;

            byte[] configBytes = File.ReadAllBytes(configFile);

            // resolution
            string resolution = "320x200";

            if (SystemConfig.isOptSet("openjazz_resolution") && !string.IsNullOrEmpty(SystemConfig["openjazz_resolution"]))
                resolution = SystemConfig["openjazz_resolution"];

            string[] size = resolution.Split('x');
            string width = size[0];
            string height = size[1];

            if (int.TryParse(width, out int widthint) && int.TryParse(height, out int heightint))
            {
                byte[] widthBytes = BitConverter.GetBytes((short)widthint);
                byte[] heightBytes = BitConverter.GetBytes((short)heightint);

                configBytes[1] = widthBytes[0];
                configBytes[2] = widthBytes[1];
                configBytes[3] = heightBytes[0];
                configBytes[4] = heightBytes[1];
            }

            File.WriteAllBytes(configFile, configBytes);
        }

        private void ConfigurePDark(List<string> commandArray, string rom)
        {
            if (_emulator != "pdark")
                return;

            string targetRom = Path.Combine(_path, "data", "pd.ntsc-final.z64");
            if (SystemConfig.isOptSet("pdark_region") && !string.IsNullOrEmpty(SystemConfig["pdark_region"]))
            {
                string pdarkRegion = SystemConfig["pdark_region"];
                switch (pdarkRegion)
                {
                    case "EUR":
                        targetRom = Path.Combine(_path, "data", "pd.pal-final.z64");
                        break;
                    case "JPN":
                        targetRom = Path.Combine(_path, "data", "pd.jpn-final.z64");
                        break;
                    case "USA":
                        targetRom = Path.Combine(_path, "data", "pd.ntsc-final.z64");
                        break;
                }
            }

            if (!File.Exists(targetRom))
            {
                try { File.Copy(rom, targetRom, true); }
                catch { }
            }

            string cfgFile = Path.Combine(_path, "pd.ini");
            using (var ini = IniFile.FromFile(cfgFile))
            {
                try
                {
                    ini.WriteValue("video", "DefaultFullscreen", _fullscreen ? "1" : "0");
                    ini.WriteValue("video", "DefaultMaximize", "1");

                    if (_resolution != null)
                    {
                        ini.WriteValue("video", "ExclusiveFullscreen", "1");
                        ini.WriteValue("video", "DefaultWidth", _resolution.Width.ToString());
                        ini.WriteValue("video", "DefaultHeight", _resolution.Height.ToString());
                    }
                    else
                    {
                        ini.WriteValue("video", "ExclusiveFullscreen", "0");
                        ini.WriteValue("video", "DefaultWidth", ScreenResolution.CurrentResolution.Width.ToString());
                        ini.WriteValue("video", "DefaultHeight", ScreenResolution.CurrentResolution.Height.ToString());
                    }

                    BindBoolIniFeatureOn(ini, "video", "VSync", "pdark_vsync", "1", "0");
                    BindBoolIniFeatureOn(ini, "video", "DetailTextures", "pdark_detailtexture", "1", "0");
                    BindIniFeature(ini, "video", "TextureFilter", "pdark_texturefilter", "1");

                    ConfigurePDarkControls(ini);
                }
                catch { }
            }
        }

        private void ConfigureSOH(string rom, string exe)
        {
            if (_emulator != "soh")
                return;

            var otrFiles = Directory.GetFiles(_path, "*.otr");
            var gameOtrFiles = otrFiles.Where(file => !file.EndsWith("soh.otr", StringComparison.OrdinalIgnoreCase));

            if (!gameOtrFiles.Any())
            {
                string emulatorRom = Path.Combine(_path, Path.GetFileName(rom));
                try { File.Copy(rom, emulatorRom); } catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to copy game file to SOH folder."); }
            }

            // Settings
            JObject jsonObj;
            JObject cvars;
            JObject controllers;
            JObject window;
            JObject fs;
            string settingsFile = Path.Combine(_path, "shipofharkinian.json");
            if (File.Exists(settingsFile))
            {
                string jsonString = File.ReadAllText(settingsFile);
                try { jsonObj = JObject.Parse(jsonString); } catch { jsonObj = new JObject(); }
            }
            else
                jsonObj = new JObject();

            if (jsonObj["CVars"] == null)
            {
                cvars = new JObject();
                jsonObj["CVars"] = cvars;
            }
            else
                cvars = (JObject)jsonObj["CVars"];

            if (jsonObj["Controllers"] == null)
            {
                controllers = new JObject();
                jsonObj["Controllers"] = controllers;
            }
            else
                controllers = (JObject)jsonObj["Controllers"];

            if (jsonObj["Window"] == null)
            {
                window = new JObject();
                jsonObj["Window"] = window;
            }
            else
                window = (JObject)jsonObj["Window"];

            if (window["Fullscreen"] == null)
            {
                fs = new JObject();
                window["Fullscreen"] = fs;
            }
            else
                fs = (JObject)window["Fullscreen"];

            cvars["gOpenMenuBar"] = 0;
            cvars["gTitleScreenTranslation"] = 1;

            // Graphic options
            double res = 1.0;
            if (SystemConfig.isOptSet("soh_resolution") && !string.IsNullOrEmpty(SystemConfig["soh_resolution"]))
                res = (SystemConfig["soh_resolution"].ToDouble() / 100);
            cvars["gInternalResolution"] = res;
            
            BindBoolFeatureOnInt(cvars, "gVsyncEnabled", "vsync", "1", "0");
            BindFeatureSliderInt(cvars, "gMSAAValue", "soh_msaa", "1");
            BindFeatureSliderInt(cvars, "gInterpolationFPS", "soh_fps", "20");
            fs["Enabled"] = _fullscreen ? true : false;

            // Language
            BindFeatureInt(cvars, "gLanguages", "soh_language", "0");

            // Controls
            //BindBoolFeatureInt(cvars, "gControlNav", "soh_menucontrol", "1", "0");

            ConfigureSOHControls(controllers);

            File.WriteAllText(settingsFile, jsonObj.ToString(Formatting.Indented));
        }

        private void ConfigureSonic3air(string rom, string exe)
        {
            if (_emulator != "sonic3air")
                return;

            string configFolder = Path.Combine(_path, "savedata");
            if (!Directory.Exists(configFolder))
                try { Directory.CreateDirectory(configFolder); } catch { }

            // Settings file
            string settingsFile = Path.Combine(configFolder, "settings.json");

            var settings = DynamicJson.Load(settingsFile);

            settings["AutoAssignGamepadPlayerIndex"] = "-1";
            settings["GameExePath"] = exe;
            settings["Fullscreen"] = _fullscreen ? "1" : "0";
            settings["RomPath"] = rom.Replace("\\", "/");

            string rumble = "0.0";
            if (SystemConfig.isOptSet("sonic3_rumble") && !string.IsNullOrEmpty(SystemConfig["sonic3_rumble"]))
            {
                rumble = (SystemConfig["sonic3_rumble"].ToDouble() / 100).ToString(CultureInfo.InvariantCulture);
            }

            settings["ControllerRumblePlayer1"] = rumble;
            settings["ControllerRumblePlayer2"] = rumble;


            if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
            {
                settings["DisplayIndex"] = (SystemConfig["MonitorIndex"].ToInteger() - 1).ToString();
            }
            else
                settings["DisplayIndex"] = "0";

            BindFeature(settings, "RenderMethod", "sonic3_renderer", "opengl-full");
            BindFeature(settings, "FrameSync", "sonic3_vsync", "1");
            BindBoolFeature(settings, "Upscaling", "integerscale", "1", "0");
            BindFeature(settings, "Filtering", "sonic3_shader", "0");
            BindBoolFeature(settings, "PerformanceDisplay", "sonic3_showfps", "1", "0");
            BindFeatureSlider(settings, "Scanlines", "sonic3_scanlines", "0");

            ConfigureSonic3airControls(configFolder, settings);

            settings.Save();
        }

        private void ConfigureSonicMania(string rom, string exe)
        {
            if (_emulator != "sonicmania")
                return;

            string romPath = Path.GetDirectoryName(rom);

            // Put the 2 emulator files in the rom folder (check versions)
            string sourceGameExe = Path.Combine(_path, _exeName);
            string targetGameExe = Path.Combine(romPath, _exeName);
            string sourceDLL = Path.Combine(_path, "game.dll");
            string targetDLL = Path.Combine(romPath, "game.dll");

            if (!File.Exists(targetGameExe) || !File.Exists(targetDLL))
            {
                try
                {
                    File.Copy(sourceGameExe, targetGameExe, true);
                    File.Copy(sourceDLL, targetDLL, true);
                }
                catch { }
            }

            // check versions
            if (File.Exists(targetGameExe))
            {
                var sourceVersionInfo = FileVersionInfo.GetVersionInfo(sourceGameExe);
                var targetVersionInfo = FileVersionInfo.GetVersionInfo(targetGameExe);
                string sourceVersion = sourceVersionInfo.FileMajorPart + "." + sourceVersionInfo.FileMinorPart + "." + sourceVersionInfo.FileBuildPart + "." + sourceVersionInfo.FilePrivatePart;
                string targetVersion = targetVersionInfo.FileMajorPart + "." + targetVersionInfo.FileMinorPart + "." + targetVersionInfo.FileBuildPart + "." + targetVersionInfo.FilePrivatePart;

                if (sourceVersion != targetVersion)
                {
                    try
                    {
                        File.Copy(sourceGameExe, targetGameExe, true);
                        File.Copy(sourceDLL, targetDLL, true);
                    }
                    catch { }
                }
            }
            _path = romPath;
            exe = targetGameExe;

            // Settings
            string settingsFile = Path.Combine(romPath, "Settings.ini");

            using (var ini = IniFile.FromFile(settingsFile))
            {
                ini.WriteValue("Video", "exclusiveFS", "n");
                ini.WriteValue("Video", "border", "n");
                BindBoolIniFeatureOn(ini, "Video", "vsync", "sonicmania_vsync", "y", "n");
                BindBoolIniFeature(ini, "Video", "tripleBuffering", "sonicmania_triple_buffering", "y", "n");
                BindIniFeature(ini, "Game", "language", "sonicmania_lang", "0");

                if (_fullscreen)
                {
                    ini.WriteValue("Video", "windowed", "n");
                    if (_resolution != null)
                        ini.WriteValue("Video", "refreshRate", _resolution.DisplayFrequency.ToString());
                    else
                    {
                        var res = ScreenResolution.CurrentResolution;
                        ini.WriteValue("Video", "refreshRate", res.DisplayFrequency.ToString());
                    }
                }
                else
                {
                    ini.WriteValue("Video", "windowed", "y");

                    if (_resolution != null)
                    {
                        ini.WriteValue("Video", "winWidth", _resolution.Width.ToString());
                        ini.WriteValue("Video", "winHeight", _resolution.Height.ToString());
                        ini.WriteValue("Video", "refreshRate", _resolution.DisplayFrequency.ToString());
                    }
                    else
                    {
                        var res = ScreenResolution.CurrentResolution;
                        ini.WriteValue("Video", "winWidth", res.Width.ToString());
                        ini.WriteValue("Video", "winHeight", res.Height.ToString());
                        ini.WriteValue("Video", "refreshRate", res.DisplayFrequency.ToString());
                    }
                }

                if (SystemConfig.isOptSet("sonicmania_force60hz") && SystemConfig.getOptBoolean("sonicmania_force60hz"))
                    ini.WriteValue("Video", "refreshRate", "60");

                if (SystemConfig.isOptSet("sonicmania_shader") && !string.IsNullOrEmpty(SystemConfig["sonicmania_shader"]) && SystemConfig["sonicmania_shader"] != "none")
                {
                    ini.WriteValue("Video", "shaderSupport", "y");
                    ini.WriteValue("Video", "screenShader", SystemConfig["sonicmania_shader"]);
                }
                else
                {
                    ini.WriteValue("Video", "shaderSupport", "n");
                    ini.WriteValue("Video", "screenShader", "0");
                }

                ini.Save();
            }
        }

        private void ConfigureSonicRetro(string rom, string exe)
        {
            if (_emulator != "sonicretro" && _emulator != "sonicretrocd")
                return;

            string[] rsdkv4Files = new string[] { "glew32.dll", "ogg.dll", "SDL2.dll", "vorbis.dll" };
            string[] rsdkv3Files = new string[] { "glew32.dll", "ogg.dll", "SDL2.dll" };
            bool sonicCD = _emulator == "sonicretrocd";
            string romPath = Path.GetDirectoryName(rom);

            // Put the emulator files in the rom folder (check versions)
            string sourceGameExe = Path.Combine(_path, _exeName);
            string targetGameExe = Path.Combine(romPath, _exeName);

            if (!File.Exists(targetGameExe))
            {
                switch (_emulator)
                {
                    case "sonicretro":
                        try
                        {
                            File.Copy(sourceGameExe, targetGameExe, true);
                            foreach (string file in rsdkv4Files)
                            {
                                string sourceFile = Path.Combine(_path, file);
                                string targetFile = Path.Combine(romPath, file);
                                File.Copy(sourceFile, targetFile, true);
                            }
                        }
                        catch { }
                        break;
                    case "sonicretrocd":
                        try
                        {
                            File.Copy(sourceGameExe, targetGameExe, true);
                            foreach (string file in rsdkv3Files)
                            {
                                string sourceFile = Path.Combine(_path, file);
                                string targetFile = Path.Combine(romPath, file);
                                File.Copy(sourceFile, targetFile, true);
                            }
                        }
                        catch { }
                        break;
                }
            }

            // check versions
            if (File.Exists(targetGameExe))
            {
                var sourceVersionInfo = FileVersionInfo.GetVersionInfo(sourceGameExe);
                var targetVersionInfo = FileVersionInfo.GetVersionInfo(targetGameExe);
                string sourceVersion = sourceVersionInfo.FileMajorPart + "." + sourceVersionInfo.FileMinorPart + "." + sourceVersionInfo.FileBuildPart + "." + sourceVersionInfo.FilePrivatePart;
                string targetVersion = targetVersionInfo.FileMajorPart + "." + targetVersionInfo.FileMinorPart + "." + targetVersionInfo.FileBuildPart + "." + targetVersionInfo.FilePrivatePart;

                if (sourceVersion != targetVersion)
                {
                    switch (_emulator)
                    {
                        case "sonicretro":
                            try
                            {
                                File.Copy(sourceGameExe, targetGameExe, true);
                                foreach (string file in rsdkv4Files)
                                {
                                    string sourceFile = Path.Combine(_path, file);
                                    string targetFile = Path.Combine(romPath, file);
                                    File.Copy(sourceFile, targetFile, true);
                                }
                            }
                            catch { }
                            break;
                        case "sonicretrocd":
                            try
                            {
                                File.Copy(sourceGameExe, targetGameExe, true);
                                foreach (string file in rsdkv3Files)
                                {
                                    string sourceFile = Path.Combine(_path, file);
                                    string targetFile = Path.Combine(romPath, file);
                                    File.Copy(sourceFile, targetFile, true);
                                }
                            }
                            catch { }
                            break;
                    }
                }
            }
            _path = romPath;
            exe = targetGameExe;

            var res = ScreenResolution.CurrentResolution;

            // Settings
            string settingsFile = Path.Combine(romPath, "Settings.ini");

            using (var ini = IniFile.FromFile(settingsFile))
            {
                ini.WriteValue("Dev", "DataFile", Path.GetFileName(rom));
                BindIniFeature(ini, "Game", "Language", "sonicretro_lang", "0");
                if (sonicCD && SystemConfig.isOptSet("sonicretro_lang") && SystemConfig["sonicretro_lang"].ToInteger() > 5)
                    ini.WriteValue("Game", "Language", "0");

                BindIniFeature(ini, "Game", "GameType", "sonicretro_gametype", "0");
                
                if (_fullscreen)
                {
                    ini.WriteValue("Window", "FullScreen", "true");
                    if (_resolution != null)
                        ini.WriteValue("Window", "RefreshRate", _resolution.DisplayFrequency.ToString());
                    else
                        ini.WriteValue("Window", "RefreshRate", res.DisplayFrequency.ToString());
                }
                else
                {
                    ini.WriteValue("Window", "FullScreen", "false");

                    if (_resolution != null)
                        ini.WriteValue("Window", "RefreshRate", _resolution.DisplayFrequency.ToString());
                    else
                        ini.WriteValue("Window", "RefreshRate", res.DisplayFrequency.ToString());
                }

                if (SystemConfig.isOptSet("sonicretro_force60hz") && SystemConfig.getOptBoolean("sonicretro_force60hz"))
                    ini.WriteValue("Window", "RefreshRate", "60");

                ini.WriteValue("Window", "Borderless", "true");
                BindBoolIniFeatureOn(ini, "Window", "VSync", "sonicretro_vsync", "true", "false");
                BindIniFeature(ini, "Window", "ScalingMode", "sonicretro_scaling", "0");

                if (sonicCD)
                    ini.WriteValue("Game", "Platform", "0");

                ini.Save();
            }
        }

        private void ConfigureStarship(string rom, string exe)
        {
            if (_emulator != "starship")
                return;

            var otrFiles = Directory.GetFiles(_path, "*.otr");
            var gameOtrFiles = otrFiles.Where(file => !file.EndsWith("starship.otr", StringComparison.OrdinalIgnoreCase));

            if (!gameOtrFiles.Any())
            {
                string emulatorRom = Path.Combine(_path, Path.GetFileName(rom));
                try { File.Copy(rom, emulatorRom); } catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to copy game file to Starship folder."); }

                string otrExtractExe = Path.Combine(_path, "generate_otr.bat");
                var starshipExtract = new ProcessStartInfo()
                {
                    FileName = otrExtractExe,
                    WorkingDirectory = _path
                };

                using (Process process = new Process())
                {
                    process.StartInfo = starshipExtract;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        SimpleLogger.Instance.Info("[INFO] OTR extracted succesfully.");
                    }
                    else
                    {
                        SimpleLogger.Instance.Error("[INFO] There was a problem extracting OTR data.");
                    }
                }
            }

            // Settings
            JObject jsonObj;
            JObject cvars;
            JObject advancedRes;
            JObject intScale;
            JObject window;
            JObject backend;
            JObject fs;
            
            string settingsFile = Path.Combine(_path, "starship.cfg.json");
            if (File.Exists(settingsFile))
            {
                string jsonString = File.ReadAllText(settingsFile);
                try { jsonObj = JObject.Parse(jsonString); } catch { jsonObj = new JObject(); }
            }
            else
                jsonObj = new JObject();

            if (jsonObj["CVars"] == null)
            {
                cvars = new JObject();
                jsonObj["CVars"] = cvars;
            }
            else
                cvars = (JObject)jsonObj["CVars"];

            if (cvars["gAdvancedResolution"] == null)
            {
                advancedRes = new JObject();
                cvars["gAdvancedResolution"] = advancedRes;
            }
            else
                advancedRes = (JObject)cvars["gAdvancedResolution"];

            if (advancedRes["IntegerScale"] == null)
            {
                intScale = new JObject();
                advancedRes["IntegerScale"] = intScale;
            }
            else
                intScale = (JObject)advancedRes["IntegerScale"];

            if (jsonObj["Window"] == null)
            {
                window = new JObject();
                jsonObj["Window"] = window;
            }
            else
                window = (JObject)jsonObj["Window"];

            if (window["Backend"] == null)
            {
                backend = new JObject();
                window["Backend"] = backend;
            }
            else
                backend = (JObject)window["Backend"];

            if (window["Fullscreen"] == null)
            {
                fs = new JObject();
                window["Fullscreen"] = fs;
            }
            else
                fs = (JObject)window["Fullscreen"];

            // Aspect ratio
            if (SystemConfig.isOptSet("starship_ratio") && !string.IsNullOrEmpty(SystemConfig["starship_ratio"]))
            {
                string starshipRatio = SystemConfig["starship_ratio"];
                switch (starshipRatio)
                {
                    case "off":
                        advancedRes["AspectRatioX"] = 0.0;
                        advancedRes["AspectRatioY"] = 0.0;
                        break;
                    case "native":
                        advancedRes["AspectRatioX"] = 4.0;
                        advancedRes["AspectRatioY"] = 3.0;
                        break;
                    case "widescreen":
                        advancedRes["AspectRatioX"] = 16.0;
                        advancedRes["AspectRatioY"] = 9.0;
                        break;
                    case "3ds":
                        advancedRes["AspectRatioX"] = 5.0;
                        advancedRes["AspectRatioY"] = 3.0;
                        break;
                    case "16:10":
                        advancedRes["AspectRatioX"] = 16.0;
                        advancedRes["AspectRatioY"] = 10.0;
                        break;
                    case "ultrawide":
                        advancedRes["AspectRatioX"] = 21.0;
                        advancedRes["AspectRatioY"] = 9.0;
                        break;
                }
            }
            else
            {
                advancedRes["AspectRatioX"] = 0.0;
                advancedRes["AspectRatioY"] = 0.0;
            }

            advancedRes["Enabled"] = 1;

            if (SystemConfig.isOptSet("integerscale") && SystemConfig.getOptBoolean("integerscale"))
            {
                intScale["Factor"] = 0;
                intScale["FitAutomatically"] = 1;
                intScale["NeverExceedBounds"] = 1;
                advancedRes["PixelPerfectMode"] = 1;
            }
            else
            {
                intScale["Factor"] = 0;
                intScale["FitAutomatically"] = 0;
                intScale["NeverExceedBounds"] = 1;
                advancedRes["PixelPerfectMode"] = 0;
            }

            BindFeatureInt(advancedRes, "VerticalPixelCount", "starship_resolution", "720");

            advancedRes["VerticalResolutionToggle"] = 1;

            cvars["gAdvancedResolutionEditorEnabled"] = 0;
            cvars["gControlNav"] = 1;

            //ConfigureStarshipControls(controllers);

            cvars["gEnableMultiViewports"] = 1;
            cvars["gInternalResolution"] = 1.0;

            if (SystemConfig.isOptSet("starship_fps") && !string.IsNullOrEmpty(SystemConfig["starship_fps"]))
            {
                int starshipFPS = SystemConfig["starship_fps"].ToInteger();
                cvars["gInterpolationFPS"] = starshipFPS;
                cvars["gMatchRefreshRate"] = 0;
            }
            else
            {
                cvars["gInterpolationFPS"] = 60;
                cvars["gMatchRefreshRate"] = 1;
            }

            BindFeatureSliderInt(cvars, "gMSAAValue", "starship_msaa", "1");
            cvars["gOpenMenuBar"] = 1;
            cvars["gSdlWindowedFullscreen"] = 1;
            BindFeatureInt(cvars, "gTextureFilter", "starship_texturefilter", "1");
            BindBoolFeatureOnInt(cvars, "gVsyncEnabled", "starship_vsync", "1", "0");
            BindFeature(window, "AudioBackend", "starship_audioapi", "wasapi");

            if (SystemConfig.isOptSet("starship_renderer") && !string.IsNullOrEmpty(SystemConfig["starship_renderer"]))
            {
                string starshipRenderer = SystemConfig["starship_renderer"];
                switch (starshipRenderer) 
                {
                    case "OpenGL":
                        backend["Id"] = 1;
                        backend["Name"] = "OpenGL";
                        break;
                    case "DirectX":
                        backend["Id"] = 0;
                        backend["Name"] = "DirectX";
                        break;
                }
            }
            else
            {
                backend["Id"] = 1;
                backend["Name"] = "OpenGL";
            }

            fs["Enabled"] = _fullscreen ? true : false;

            File.WriteAllText(settingsFile, jsonObj.ToString(Formatting.Indented));
        }
        #endregion
    }

    #region dhew3 config class
    class Dhewm3ConfigChange
    {
        public string Type { get; set; }
        public string Key { get; set; }
        public string NewValue { get; set; }

        public Dhewm3ConfigChange(string type, string key, string newValue)
        {
            Type = type;
            Key = key;
            NewValue = newValue;
        }
    }

    class ConfigEditor
    {
        public static void ChangeConfigValues(string filePath, List<Dhewm3ConfigChange> changes)
        {
            if (!File.Exists(filePath))
                return;

            // Read all lines from the file
            string[] lines = File.ReadAllLines(filePath);
            bool[] foundFlags = new bool[changes.Count];

            // Make changes in memory
            for (int i = 0; i < lines.Length; i++)
            {
                for (int j = 0; j < changes.Count; j++)
                {
                    string pattern = $@"^{Regex.Escape(changes[j].Type)}\s+{Regex.Escape(changes[j].Key)}\s+""(.*?)""";
                    if (Regex.IsMatch(lines[i], pattern))
                    {
                        lines[i] = $"{changes[j].Type} {changes[j].Key} \"{changes[j].NewValue}\"";
                        foundFlags[j] = true;
                    }
                }
            }

            // Append any missing keys to the file
            using (StreamWriter writer = File.AppendText(filePath))
            {
                for (int j = 0; j < changes.Count; j++)
                {
                    if (!foundFlags[j])
                    {
                        writer.WriteLine($"{changes[j].Type} {changes[j].Key} \"{changes[j].NewValue}\"");
                    }
                }
            }

            // Write all updated lines back to the file
            File.WriteAllLines(filePath, lines);
        }
    }
    #endregion
}
