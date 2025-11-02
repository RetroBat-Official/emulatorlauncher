using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private bool _finishProcess = false;

        private void ConfigurePort(List<string> commandArray, string rom, string exe)
        {
            // Add one method per port to configure, you can pass commandArray if the port requires special command line arguments
            // If the port allows controller configuration, create the controller configuration method in PortsLauncher.Controller.cs and call it from the port configuration method
            // Keep alphabetical order

            ConfigureBStone(commandArray, rom);
            ConfigureCDogs(commandArray, rom);
            Configurecgenius(commandArray, rom);
            Configurecorsixth(commandArray, rom);
            Configuredhewm3(commandArray, rom);
            ConfigureOpenGoal(commandArray, rom);
            ConfigureOpenJazz(commandArray, rom);
            ConfigurePDark(commandArray, rom);
            ConfigurePowerBomberman(rom, exe);
            ConfigureSOH(rom, exe);
            ConfigureSonic3air(rom, exe);
            ConfigureSonicMania(rom, exe);
            ConfigureSonicRetro(rom, exe);
            ConfigureStarship(rom, exe);
            ConfigurevkQuake(commandArray, rom);
            ConfigurevkQuake2(commandArray, rom);
            ConfigureXash3d(commandArray, rom);
        }

        #region ports
        private void ConfigureBStone(List<string> commandArray, string rom)
        {
            if (_emulator != "bstone")
                return;

            // Get romPath
            string dataDir = Path.GetDirectoryName(rom);

            // Settings file update
            string configFile = Path.Combine(_path, "bstone_config.txt");

            var height = _resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height;
            var width = _resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width;

            commandArray.Add("--data_dir");
            commandArray.Add("\"" + dataDir + "\"");
            commandArray.Add("--profile_dir");
            commandArray.Add("\"" + _path + "\"");

            bool exclusivefs = SystemConfig.getOptBoolean("exclusivefs");

            commandArray.Add("--vid_window_mode value");
            if (_fullscreen)
            {
                commandArray.Add(exclusivefs ? "fullscreen" : "fake_fullscreen");
            }
            else
                commandArray.Add("windowed");

            var cfg = new QuakeConfig(configFile);
            cfg.AppendIfMissing = true;
            
            if (SystemConfig.isOptSet("bstone_renderer") && !string.IsNullOrEmpty(SystemConfig["bstone_renderer"]))
                cfg["vid_renderer"] = SystemConfig["bstone_renderer"];
            else
                cfg["vid_renderer"] = "gles_2_0";

            cfg["vid_is_vsync"] = SystemConfig.getOptBoolean("bstone_vsync") ? "1" : "0";
            cfg["vid_is_widescreen"] = SystemConfig.getOptBoolean("bstone_widescreen") ? "1" : "0";
            cfg["vid_width"] = width.ToString();
            cfg["vid_height"] = height.ToString();

            if (SystemConfig.isOptSet("bstone_antialiasing") && !string.IsNullOrEmpty(SystemConfig["bstone_antialiasing"]))
                cfg["vid_aa_degree"] = SystemConfig["bstone_antialiasing"];
            else
                cfg["vid_aa_degree"] = "2";

            if (SystemConfig.isOptSet("bstone_upscale_filter") && !string.IsNullOrEmpty(SystemConfig["bstone_upscale_filter"]))
            {
                string x = SystemConfig["bstone_upscale_filter"];
                cfg["vid_texture_upscale_filter"] = x == "none" ? "none" : "xbrz";
                cfg["vid_texture_upscale_xbrz_degree"] = x == "none" ? "2" : x;
            }
            else
            {
                cfg["vid_texture_upscale_filter"] = "none";
                cfg["vid_texture_upscale_xbrz_degree"] = "2";
            }

            if (SystemConfig.isOptSet("bstone_anisotropy") && !string.IsNullOrEmpty(SystemConfig["bstone_anisotropy"]))
                cfg["vid_3d_texture_anisotropy"] = SystemConfig["bstone_anisotropy"];
            else
                cfg["vid_3d_texture_anisotropy"] = "1";

            if (SystemConfig.isOptSet("bstone_texture_filter") && !string.IsNullOrEmpty(SystemConfig["bstone_texture_filter"]))
            {
                string x = SystemConfig["bstone_texture_filter"];
                cfg["vid_2d_texture_filter"] = x;
                cfg["vid_3d_texture_image_filter"] = x;
                cfg["vid_3d_texture_mipmap_filter"] = x;
            }
            else
            {
                cfg["vid_2d_texture_filter"] = "nearest";
                cfg["vid_3d_texture_image_filter"] = "nearest";
                cfg["vid_3d_texture_mipmap_filter"] = "nearest";
            }

            cfg.Save();
        }

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

            ConfigEditorDhewm3.ChangeConfigValues(cfgFile, changes);

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
            List<string> openGoalGames = new List<string> { "jak1", "jak2", "jak3" };
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

            // Check if game has been extracted already, if not, user can set path to iso in ES
            string outDataPath = Path.Combine(_path, "data", "out", gameName);
            {
                if (!Directory.Exists(outDataPath))
                {
                    SimpleLogger.Instance.Warning("[WARNING] OpenGOAL data folder not found, checking if a path to ISO is specified.");
                    
                    string isoSearch = "opengoal_isopath_" + gameName;

                    if (SystemConfig.isOptSet(isoSearch) && !string.IsNullOrEmpty(SystemConfig[isoSearch]))
                    {
                        string isoPath = SystemConfig[isoSearch];
                        if (!File.Exists(isoPath))
                            SimpleLogger.Instance.Error("[ERROR] Failed to extract game from ISO: " + isoPath);
                        else
                        {
                            SimpleLogger.Instance.Error("[INFO] Trying to extract game file with provided path: " + isoPath);
                            
                            var openGoalExtract = new ProcessStartInfo()
                            {
                                FileName = Path.Combine(_path, "extractor.exe"),
                                WorkingDirectory = _path,
                                Arguments = "\"" + isoPath + "\"",
                            };

                            try
                            {
                                using (var process = new Process())
                                {
                                    process.StartInfo = openGoalExtract;
                                    process.Start();
                                    process.WaitForExit();
                                    _finishProcess = true;
                                    return;
                                }
                            }
                            catch (Exception ex) { SimpleLogger.Instance.Error("[ERROR] Failed to extract game from ISO: " + isoPath + " - " + ex.Message); }
                        }
                    }
                    else
                        throw new ApplicationException("File needs to be extracted first.");
                }
            }

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

            if (!File.Exists(debugSettingsFile))
            {
                try { File.WriteAllText(debugSettingsFile, "{}"); }
                catch { SimpleLogger.Instance.Warning("[WARNING] Error opening debug-settings.json config file."); }
            }

            if (File.Exists(debugSettingsFile))
            {
                var debugSettings = DynamicJson.Load(debugSettingsFile);

                debugSettings["alternate_style"] = "false";
                debugSettings["ignore_hide_imgui"] = "false";
                debugSettings["monospaced_font"] = "true";
                debugSettings["show_imgui"] = "false";
                debugSettings["text_check_range"] = "false";
                debugSettings.Save();
            }

            // Display settings
            string displaySettingsFile = Path.Combine(gameConfigPath, "settings", "display-settings.json");

            if (!File.Exists(displaySettingsFile))
            {
                try { File.WriteAllText(displaySettingsFile, "{}"); }
                catch { SimpleLogger.Instance.Warning("[WARNING] Error opening display-settings.json config file."); }
            }

            if (File.Exists(displaySettingsFile))
            {
                var displayConf = DynamicJson.Load(displaySettingsFile);

                BindFeature(displayConf, "display_id", "MonitorIndex", "0");
                if (_fullscreen && SystemConfig.getOptBoolean("exclusivefs"))
                    displayConf["display_mode"] = "2";
                else if (_fullscreen)
                    displayConf["display_mode"] = "1";
                else
                    displayConf["display_mode"] = "0";

                displayConf.Save();
            }

            // Game settings - to check settings for jak 2
            string configFilePath = Path.Combine(gameConfigPath, "settings", "pc-settings.gc");

            if (!File.Exists(configFilePath))
            {
                SimpleLogger.Instance.Warning("[WARNING] Could not find file: " + configFilePath);
                return;
            }

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

            if (SystemConfig.isOptSet("opengoal_resolution") && !string.IsNullOrEmpty(SystemConfig["opengoal_resolution"]))
                bindFeature("game-size", SystemConfig["opengoal_resolution"]);
            else
            {
                string width = Screen.PrimaryScreen.Bounds.Width.ToString();
                string height = Screen.PrimaryScreen.Bounds.Height.ToString();
                string res = _resolution == null ? width + " " + height : _resolution.Width.ToString() + " " + _resolution.Height.ToString();
                bindFeature("game-size", res);
            }

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
                bindFeature(gameName == "jak2" ? "memcard-subtitles?" : "subtitles ?", "#f");
            else
                bindFeature(gameName == "jak2" ? "memcard-subtitles?" : "subtitles?", "#t");

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
            else
            {
                string romName = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
                if (rom.Contains("eur") || rom.Contains("pal") || rom.Contains("fr"))
                    targetRom = Path.Combine(_path, "data", "pd.pal-final.z64");
                else if (rom.Contains("jap") || rom.Contains("jp") || rom.Contains("japan"))
                    targetRom = Path.Combine(_path, "data", "pd.jpn-final.z64");
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

        private void ConfigurePowerBomberman(string rom, string exe)
        {
            if (_emulator != "powerbomberman")
                return;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pbPath = Path.Combine(localAppData, "pb");
            if (!Directory.Exists(pbPath))
                try { Directory.CreateDirectory(pbPath); } catch { }
            string config = Path.Combine(pbPath, "config.ini");
            if (!File.Exists(config))
            {
                try
                {
                    string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "powerbomberman", "config.ini");

                    if (File.Exists(templateFile))
                    {
                        try { File.Copy(templateFile, config); } catch { }
                    }
                }
                catch { }
            }

            using (var ini = new IniFile(config))
            {
                BindBoolIniFeatureOn(ini, "VIDEO", "vsync", "pb_vsync", "1", "0");
                if (_fullscreen)
                    ini.WriteValue("VIDEO", "fullscreen", "1");
                else
                    ini.WriteValue("VIDEO", "fullscreen", "0");

                ConfigurePowerBombermanControls(ini);

                ini.Save();
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
                res = SystemConfig["soh_resolution"].ToDouble();
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

        private void ConfigurevkQuake(List<string> commandArray, string rom)
        {
            if (_emulator != "vkquake")
                return;

            bool hipnotic = false;
            bool rogue = false;
            bool exclusivefs = SystemConfig.getOptBoolean("exclusivefs");

            commandArray.Add("-basedir");
            string rompath =Path.Combine(AppConfig.GetFullPath("roms"), "quake");

            commandArray.Add("\"" + rompath + "\"");

            if (rom.ToLowerInvariant().Contains("scourge") || rom.ToLowerInvariant().Contains("hipnotic"))
            {
                commandArray.Add("-hipnotic");
                hipnotic = true;
            }
            else if (rom.ToLowerInvariant().Contains("dissolution") || rom.ToLowerInvariant().Contains("rogue"))
            {
                commandArray.Add("-rogue");
                rogue = true;
            }

            if (_fullscreen)
                commandArray.Add("-fullscreen");

            // Config file
            string vkquakecfg = Path.Combine(rompath, "id1", "vkQuake.cfg");

            if (hipnotic)
                vkquakecfg = Path.Combine(rompath, "hipnotic", "vkQuake.cfg");
            else if (rogue)
                vkquakecfg = Path.Combine(rompath, "rogue", "vkQuake.cfg");

            var cfg = new QuakeConfig(vkquakecfg);
            cfg.AppendIfMissing = true;
            var height = _resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height;
            var width = _resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width;

            cfg["vid_width"] = width.ToString();
            cfg["vid_height"] = height.ToString();

            if (SystemConfig.isOptSet("vkquake_vsync") && !string.IsNullOrEmpty(SystemConfig["vkquake_vsync"]))
                cfg["vid_vsync"] = SystemConfig["vkquake_vsync"];
            else
                cfg["vid_vsync"] = "0";

            if (_fullscreen)
                cfg["vid_fullscreen"] = "1";
            else
                cfg["vid_fullscreen"] = "0";

            if (exclusivefs)
                cfg["vid_borderless"] = "0";
            else
                cfg["vid_borderless"] = "1";

            cfg.Save();
        }

        private void ConfigurevkQuake2(List<string> commandArray, string rom)
        {
            if (_emulator != "vkquake2")
                return;

            bool zaero = false;
            bool xatrix = false;
            bool smd = false;
            bool rogue = false;
            bool exclusivefs = SystemConfig.getOptBoolean("exclusivefs");
            string rompath = Path.Combine(AppConfig.GetFullPath("roms"), "quake2");

            commandArray.Add("+set basedir");
            commandArray.Add("\"" + rompath + "\"");

            if (rom.ToLowerInvariant().Contains("zaero"))
            {
                commandArray.Add("+set game zaero");
                zaero = true;
            }
            else if (rom.ToLowerInvariant().Contains("xatrix"))
            {
                commandArray.Add("+set game xatrix");
                xatrix = true;
            }
            else if (rom.ToLowerInvariant().Contains("rogue"))
            {
                commandArray.Add("+set game rogue");
                rogue = true;
            }
            else if (rom.ToLowerInvariant().Contains("smd"))
            {
                commandArray.Add("+set game smd");
                smd = true;
            }

            if (_fullscreen)
                commandArray.Add("-fullscreen");

            // Config file
            string vkquake2cfg = Path.Combine(rompath, "baseq2", "config.cfg");

            if (zaero)
                vkquake2cfg = Path.Combine(rompath, "zaero", "config.cfg");
            else if (xatrix)
                vkquake2cfg = Path.Combine(rompath, "xatrix", "config.cfg");
            else if (smd)
                vkquake2cfg = Path.Combine(rompath, "smd", "config.cfg");
            else if (rogue)
                vkquake2cfg = Path.Combine(rompath, "rogue", "config.cfg");

            var cfg = new QuakeConfig(vkquake2cfg);
            var height = _resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height;
            var width = _resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width;

            cfg["set vk_mode"] = "-1";
            cfg["set r_customwidth"] = width.ToString();
            cfg["set r_customheight"] = height.ToString();

            if (SystemConfig.isOptSet("vkquake2_vsync") && SystemConfig.getOptBoolean("vkquake2_vsync"))
                cfg["set vk_vsync"] = "1";
            else
                cfg["set vk_vsync"] = "0";

            if (_fullscreen)
                cfg["set vid_fullscreen"] = "1";
            else
                cfg["set vid_fullscreen"] = "0";

            if (exclusivefs)
                cfg["set vk_fullscreen_exclusive"] = "1";
            else
                cfg["set vk_fullscreen_exclusive"] = "0";

            if (SystemConfig.isOptSet("vkquake2_soundhighquality") && !SystemConfig.getOptBoolean("vkquake2_soundhighquality"))
                cfg["set s_khz"] = "11";
            else
                cfg["set s_khz"] = "22";

            if (SystemConfig.getOptBoolean("vkquake2_usejoy"))
                cfg["set in_joystick"] = "1";
            else
                cfg["set in_joystick"] = "0";

            if (SystemConfig.getOptBoolean("vkquake2_invmouse"))
                cfg["set m_pitch"] = "-0.022000";
            else
                cfg["set m_pitch"] = "0.022000";

            if (SystemConfig.isOptSet("vkquake2_antialiasing") && !string.IsNullOrEmpty(SystemConfig["vkquake2_antialiasing"]))
                cfg["set vk_msaa"] = SystemConfig["vkquake2_antialiasing"];
            else
                cfg["set vk_msaa"] = "0";

            cfg.Save();
        }

        private void ConfigureXash3d(List<string> commandArray, string rom)
        {
            List<string> args = new List<string>();

            if (_emulator != "xash3d")
                return;

            string rompath = Path.Combine(AppConfig.GetFullPath("roms"), "halflife");

            // Copy emulator to roms folder
            CopyFolderContent(_path, rompath);

            // Set environment variable for xash3d base directory
            Environment.SetEnvironmentVariable("XASH3D_BASEDIR", rompath, EnvironmentVariableTarget.User);

            bool exclusivefs = SystemConfig.getOptBoolean("exclusivefs");
            
            if (_fullscreen && exclusivefs)
                commandArray.Add("-fullscreen");
            else if (_fullscreen)
                commandArray.Add("-borderless");
            else
                commandArray.Add("-windowed");

            string game = Path.GetFileNameWithoutExtension(rom);
            commandArray.Add("-game");
            commandArray.Add(game);

            var height = _resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height;
            var width = _resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width;

            // Config file
            string cfgSubPath = Path.Combine(rompath, "valve");

            if (!game.ToLowerInvariant().Contains("life") || !game.ToLowerInvariant().Contains("half"))
                cfgSubPath = Path.Combine(rompath, game);

            string videoConf = Path.Combine(cfgSubPath, "video.cfg");
            var vcfg = new QuakeConfig(videoConf);
            vcfg["height"] = _fullscreen ? height.ToString() : ScreenResolution.CurrentResolution.Height.ToString();
            vcfg["width"] = _fullscreen ? width.ToString() : ScreenResolution.CurrentResolution.Width.ToString();
            vcfg.Save();

            string gameConf = Path.Combine(cfgSubPath, "config.cfg");
            var gcfg = new QuakeConfig(gameConf);
            
            if (SystemConfig.getOptBoolean("xash3d_showfps"))
                gcfg["cl_showfps"] = "1";
            else
                gcfg["cl_showfps"] = "0";

            if (SystemConfig.getOptBoolean("xash3d_vsync"))
                gcfg["gl_vsync"] = "1";
            else
                gcfg["gl_vsync"] = "0";

            if (SystemConfig.getOptBoolean("xash3d_invmouse"))
                gcfg["m_pitch"] = "-0.022000";
            else
                gcfg["m_pitch"] = "0.022000";

            if (SystemConfig.isOptSet("xash3d_crosshair") && !SystemConfig.getOptBoolean("xash3d_crosshair"))
                gcfg["crosshair"] = "0";
            else
                gcfg["crosshair"] = "1";

            if (SystemConfig.getOptBoolean("xash3d_autoaim"))
                gcfg["sv_aim"] = "1";
            else
                gcfg["sv_aim"] = "0";

            if (SystemConfig.getOptBoolean("xash3d_disabledsp"))
                gcfg["room_off"] = "1";
            else
                gcfg["room_off"] = "0";

            if (SystemConfig.isOptSet("xash3d_sensitivity") && !string.IsNullOrEmpty(SystemConfig["xash3d_sensitivity"]))
                gcfg["sensitivity"] = SystemConfig["xash3d_sensitivity"];
            else
                gcfg["sensitivity"] = "3";

            gcfg.Save();

            string oglConf = Path.Combine(cfgSubPath, "opengl.cfg");
            var glcfg = new QuakeConfig(oglConf);

            if (SystemConfig.getOptBoolean("smooth"))
                glcfg["gl_texture_nearest"] = "1";
            else
                glcfg["gl_texture_nearest"] = "0";

            glcfg.Save();
        }
        #endregion

        private static void CopyFolderContent(string sourceDir, string destDir)
        {
            try { Directory.CreateDirectory(destDir); } catch { }

            foreach (var sourceFile in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(sourceFile);
                string destFile = Path.Combine(destDir, fileName);

                bool shouldCopy = false;

                if (!File.Exists(destFile))
                {
                    shouldCopy = true; // doesn't exist — copy it
                }
                else
                {
                    DateTime srcTime = File.GetLastWriteTimeUtc(sourceFile);
                    DateTime dstTime = File.GetLastWriteTimeUtc(destFile);

                    // Compare by time (within 2-second tolerance to avoid FAT/NTFS rounding)
                    if (Math.Abs((srcTime - dstTime).TotalSeconds) > 2)
                        shouldCopy = true;
                }

                if (shouldCopy)
                {
                    File.Copy(sourceFile, destFile, true);
                    SimpleLogger.Instance.Info($"[INFO] Copied file: {destFile}");
                }
            }

            SimpleLogger.Instance.Info($"[INFO] Copy Complete");
        }
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

    class ConfigEditorDhewm3
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

    #region vkQuake config class
    public class QuakeConfig
    {
        private class Line
        {
            public string Raw;
            public string Key;
            public string Value;
            public bool IsSet;     // set lines
            public bool IsBind;    // bind lines
        }

        private readonly List<Line> _lines = new List<Line>();
        private readonly string _filePath;

        public bool AppendIfMissing = true;

        public QuakeConfig(string filePath)
        {
            _filePath = filePath;

            if (File.Exists(filePath))
            {
                string[] all = File.ReadAllLines(filePath, Encoding.UTF8);
                for (int i = 0; i < all.Length; i++)
                {
                    _lines.Add(ParseLine(all[i]));
                }
            }
            else
            {
                // file does not exist — create empty config
                _lines = new List<Line>();
            }
        }

        // indexer: "set key", "bind KEY", "key"
        public string this[string requestedKey]
        {
            get
            {
                string type, key;
                SplitKey(requestedKey, out type, out key);
                Line line = FindLine(type, key);
                return line != null ? line.Value : null;
            }
            set
            {
                string type, key;
                SplitKey(requestedKey, out type, out key);
                Line line = FindLine(type, key);
                if (line != null)
                {
                    // Only update the value, keep original spacing, comments, etc.
                    line.Raw = UpdateRawValue(line.Raw, value);
                    line.Value = value;
                }
                else if (AppendIfMissing)
                {
                    // append at end
                    Line newLine = new Line();
                    if (type == "set")
                        newLine.Raw = "set " + key + " \"" + value + "\"";
                    else if (type == "bind")
                        newLine.Raw = "bind \"" + key + "\" \"" + value + "\"";
                    else
                        newLine.Raw = key + " \"" + value + "\"";
                    newLine.Key = key;
                    newLine.Value = value;
                    newLine.IsSet = type == "set";
                    newLine.IsBind = type == "bind";
                    _lines.Add(newLine);
                }
            }
        }

        public void Save()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _lines.Count; i++)
            {
                if (i > 0)
                    sb.Append(Environment.NewLine);
                sb.Append(_lines[i].Raw);
            }
            File.WriteAllText(_filePath, sb.ToString(), new UTF8Encoding(false));
        }

        #region Helpers

        private Line FindLine(string type, string key)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                Line ln = _lines[i];
                if (type == "set" && ln.IsSet && string.Equals(ln.Key, key, StringComparison.OrdinalIgnoreCase))
                    return ln;
                if (type == "bind" && ln.IsBind && string.Equals(ln.Key, key, StringComparison.OrdinalIgnoreCase))
                    return ln;
                if (type == "var" && !ln.IsBind && !ln.IsSet && string.Equals(ln.Key, key, StringComparison.OrdinalIgnoreCase))
                    return ln;
            }
            return null;
        }

        private void SplitKey(string requestedKey, out string type, out string key)
        {
            type = "var";
            key = requestedKey.Trim();
            if (key.StartsWith("set "))
            {
                type = "set";
                key = key.Substring(4).Trim();
            }
            else if (key.StartsWith("bind "))
            {
                type = "bind";
                key = key.Substring(5).Trim().Trim('"');
            }
        }

        private Line ParseLine(string raw)
        {
            Line line = new Line();
            line.Raw = raw;

            if (string.IsNullOrWhiteSpace(raw))
            {
                line.Key = null;  // mark as non-editable
                return line;
            }

            string trimmed = raw.TrimStart();
            if (trimmed.StartsWith("set "))
            {
                line.IsSet = true;
                string rest = trimmed.Substring(4);
                int space = rest.IndexOf(' ');
                if (space > 0)
                {
                    line.Key = rest.Substring(0, space);
                    line.Value = ExtractValue(rest.Substring(space + 1));
                }
            }
            else if (trimmed.StartsWith("bind "))
            {
                line.IsBind = true;
                string rest = trimmed.Substring(5);
                int space = rest.IndexOf(' ');
                if (space > 0)
                {
                    line.Key = Unquote(rest.Substring(0, space));
                    line.Value = ExtractValue(rest.Substring(space + 1));
                }
            }
            else
            {
                // treat as plain variable, e.g., "vid_fullscreen "0""
                int space = trimmed.IndexOf(' ');
                if (space > 0)
                {
                    line.Key = trimmed.Substring(0, space);
                    line.Value = ExtractValue(trimmed.Substring(space + 1));
                }
            }

            return line;
        }

        private string ExtractValue(string raw)
        {
            string val = raw.Trim();
            if (val.StartsWith("\"") && val.EndsWith("\"") && val.Length >= 2)
                val = val.Substring(1, val.Length - 2);
            return val;
        }

        private string Unquote(string raw)
        {
            string s = raw.Trim();
            if (s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2)
                return s.Substring(1, s.Length - 2);
            return s;
        }

        private string UpdateRawValue(string raw, string newValue)
        {
            // find first " after key
            int quoteStart = raw.IndexOf('"');
            if (quoteStart < 0)
                return raw; // cannot parse, leave unchanged

            int quoteEnd = raw.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0)
                return raw;

            return raw.Substring(0, quoteStart + 1) + newValue + raw.Substring(quoteEnd);
        }

        #endregion
    }
    #endregion
}
