using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
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
        private void ConfigurePort(List<string> commandArray, string rom, string exe)
        {
            // Add one method per port to configure, you can pass commandArray if the port requires special command line arguments
            // If the port allows controller configuration, create the controller configuration method in PortsLauncher.Controller.cs and call it from the port configuration method
            // Keep alphabetical order

            ConfigureBStone(commandArray, rom);
            ConfigureBSyndrome(commandArray, rom);
            ConfigureCDogs(commandArray, rom);
            Configurecgenius(commandArray, rom);
            Configurecorsixth(commandArray, rom);
            Configuredhewm3(commandArray, rom);
            Configuredusklight(commandArray, rom);
            ConfigureGhostship(commandArray, rom, exe);
            ConfigureOpenGoal(commandArray, rom);
            ConfigureOpenJazz(commandArray, rom);
            ConfigurePDark(commandArray, rom);
            ConfigurePowerBomberman(rom, exe);
            ConfigureRTCW(commandArray, rom);
            ConfigureShip2(commandArray, rom, exe);
            ConfigureSOH(commandArray, rom, exe);
            ConfigureSonic3air(rom, exe);
            ConfigureSonicMania(rom, exe);
            ConfigureSonicRetro(rom, exe);
            ConfigureStarship(rom, exe);
            ConfigurevkQuake(commandArray, rom);
            ConfigurevkQuake2(commandArray, rom);
            ConfigureXash3d(commandArray, rom);
        }

        #region ports

        #region bstone
        private void ConfigureBStone(List<string> commandArray, string rom)
        {
            if (_emulator != "bstone")
                return;

            // Get romPath
            string dataDir = Path.GetDirectoryName(rom);

            // Settings file update
            string configFile = Path.Combine(_path, "bstone_config.txt");

            commandArray.Add("--data_dir");
            commandArray.Add("\"" + dataDir + "\"");
            commandArray.Add("--profile_dir");
            commandArray.Add("\"" + _path + "\"");

            // Force the game found in the rom folder
            // Otherwise BStone also scans GOG/Steam installs and shows a mouse-only selection dialog when several games are found
            string game = GetBStoneGame(dataDir, rom);
            if (game != null)
                commandArray.Add("--" + game);

            // BStone fullscreen always uses the primary display
            // On another screen: windowed at the target screen size, made borderless and placed after launch (see ApplyScreenPlacement)
            var targetScreen = Program.IsMonitorIndexSet ? Program.TargetScreen : null;
            _borderlessOnScreen = _fullscreen && targetScreen != null && !targetScreen.Primary;

            var cfg = new QuakeConfig(configFile);
            cfg.AppendIfMissing = true;

            if (_borderlessOnScreen)
            {
                cfg["vid_window_mode"] = "windowed";
                cfg["vid_width"] = targetScreen.Bounds.Width.ToString();
                cfg["vid_height"] = targetScreen.Bounds.Height.ToString();

                // BStone clamps negative coordinates to 0: in that case the window is only moved after launch
                bool canPosition = targetScreen.Bounds.Left >= 0 && targetScreen.Bounds.Top >= 0;
                cfg["vid_is_positioned"] = canPosition ? "1" : "0";
                if (canPosition)
                {
                    cfg["vid_x"] = targetScreen.Bounds.Left.ToString();
                    cfg["vid_y"] = targetScreen.Bounds.Top.ToString();
                }
            }
            else
            {
                cfg["vid_is_positioned"] = "0";

                if (_fullscreen)
                {
                    cfg["vid_window_mode"] = _exclusivefs ? "fullscreen" : "fake_fullscreen";

                    // Display mode only used by exclusive fullscreen, borderless fullscreen always uses the desktop resolution
                    if (_exclusivefs)
                    {
                        cfg["vid_width"] = (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width).ToString();
                        cfg["vid_height"] = (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height).ToString();
                    }
                }
                else
                {
                    cfg["vid_window_mode"] = "windowed";

                    // Keep the window size saved by the game unless a video mode is set
                    if (_resolution != null)
                    {
                        cfg["vid_width"] = _resolution.Width.ToString();
                        cfg["vid_height"] = _resolution.Height.ToString();
                    }
                }
            }

            // Renderer: auto-detect tries Vulkan, OpenGL 3.2, OpenGL 2.0, OpenGL ES 2.0, then falls back to software
            if (SystemConfig.isOptSet("bstone_renderer") && !string.IsNullOrEmpty(SystemConfig["bstone_renderer"]))
                cfg["vid_renderer"] = SystemConfig["bstone_renderer"];
            else
                cfg["vid_renderer"] = "auto-detect";

            cfg["vid_is_vsync"] = (!SystemConfig.isOptSet("bstone_vsync") || SystemConfig.getOptBoolean("bstone_vsync")) ? "1" : "0";
            cfg["vid_is_widescreen"] = (!SystemConfig.isOptSet("bstone_widescreen") || SystemConfig.getOptBoolean("bstone_widescreen")) ? "1" : "0";
            cfg["vid_is_ui_stretched"] = SystemConfig.getOptBoolean("bstone_stretchui") ? "1" : "0";

            // MSAA requires both the type and the degree
            int aaDegree;
            if (int.TryParse(SystemConfig["bstone_antialiasing"], out aaDegree) && aaDegree >= 2)
            {
                cfg["vid_aa_type"] = "msaa";
                cfg["vid_aa_degree"] = aaDegree.ToString();
            }
            else
            {
                cfg["vid_aa_type"] = "none";
                cfg["vid_aa_degree"] = "1";
            }

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

            cfg["gp_no_intro_outro"] = SystemConfig.getOptBoolean("bstone_skipintro") ? "1" : "0";
            cfg["snd_opl3_type"] = SystemConfig.isOptSet("bstone_opl3") && SystemConfig["bstone_opl3"] == "nuked" ? "nuked" : "dbopl";

            cfg.Save();

            ConfigureBStoneBindings(configFile);
        }

        // Game forced on the command line from the data files found in the rom folder (null = let BStone search)
        private static string GetBStoneGame(string dataDir, string rom)
        {
            bool ps = File.Exists(Path.Combine(dataDir, "VSWAP.VSI"));
            bool aog = File.Exists(Path.Combine(dataDir, "VSWAP.BS6"));
            bool aogSw = File.Exists(Path.Combine(dataDir, "VSWAP.BS1"));

            // Several games in the same folder: use the rom name (aog.bstone, ps.bstone)
            string romName = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
            if ((aog || aogSw) && (romName.Contains("aog") || romName.Contains("alien")))
                return aog ? "aog" : "aog_sw";
            if (ps && (romName.Contains("ps") || romName.Contains("planet")))
                return "ps";

            if (aog)
                return "aog";
            if (aogSw)
                return "aog_sw";
            if (ps)
                return "ps";

            return null;
        }

        // Key bindings appended at the end of the config: in_bind is executed after the saved bindings and removes the key from any other action
        // BStone rewrites the whole file on exit, so these lines do not accumulate
        private void ConfigureBStoneBindings(string configFile)
        {
            var bindings = new List<string>
            {
                // RetroBat default hotkeys
                "in_bind \"f2\" \"quick_save\" \"0\"",
                "in_bind \"f4\" \"quick_load\" \"0\"",
                "in_bind \"f8\" \"screenshot\" \"0\""
            };

            // Layout independent keys used by the gamepad mapping (system\padtokey\bstone.keys), BStone reads layout dependent key codes
            if (!SystemConfig.getOptBoolean("disableautocontrollers"))
            {
                bindings.Add("in_bind \"up_arrow\" \"forward\" \"1\"");
                bindings.Add("in_bind \"down_arrow\" \"backward\" \"1\"");
                bindings.Add("in_bind \"delete\" \"strafe_left\" \"1\"");
                bindings.Add("in_bind \"end\" \"strafe_right\" \"1\"");
                bindings.Add("in_bind \"page_up\" \"previous_weapon\" \"0\"");
                bindings.Add("in_bind \"page_down\" \"next_weapon\" \"0\"");
            }

            try
            {
                var lines = File.Exists(configFile) ? File.ReadAllLines(configFile).ToList() : new List<string>();
                lines.RemoveAll(l => bindings.Contains(l.Trim()));
                lines.AddRange(bindings);
                File.WriteAllLines(configFile, lines, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[WARNING] BStone: unable to write key bindings : " + ex.Message);
            }
        }
        #endregion

        #region bsyndrome
        private void ConfigureBSyndrome(List<string> commandArray, string rom)
        {
            if (_emulator != "bsyndrome")
                return;

            // Get romPath
            string dataDir = Path.Combine(Path.GetDirectoryName(rom), "DATA");
            if (!Directory.Exists(dataDir))
                throw new ApplicationException("DATA folder not found in roms\\bsyndrome\\DATA.");

            string arg1 = "--datapath=\"" + dataDir + "\"";
            commandArray.Add(arg1);

            // SAVESPATH
            string savesDir = Path.Combine(AppConfig.GetFullPath("saves"), "bsyndrome");
            if (!Directory.Exists(savesDir))
                try { Directory.CreateDirectory(savesDir); } catch { }

            string arg2 = "--savepath=\"" + savesDir + "\"";
            commandArray.Add(arg2);

            // MUSICPATH
            string musicDir = Path.Combine(AppConfig.GetFullPath("bios"), "bsyndrome", "MUSIC");
            if (!Directory.Exists(musicDir))
                try { Directory.CreateDirectory(musicDir); } catch { }

            string arg3 = "--musicpath=\"" + musicDir + "\"";
            commandArray.Add(arg3);

            // Other command lines
            if (_fullscreen)
                commandArray.Add("--fullscreen");

            if (SystemConfig.isOptSet("bsyndrome_aspect") && !string.IsNullOrEmpty(SystemConfig["bsyndrome_aspect"]))
                commandArray.Add("--widescreen=" + SystemConfig["bsyndrome_aspect"]);
        }
        #endregion

        #region cdogs
        private void ConfigureCDogs(List<string> commandArray, string rom)
        {
            if (_emulator != "cdogs")
                return;

            string cdogsRoot = AppConfig.GetFullPath(_emulator).TrimEnd('\\', '/');

            // Portable config: C-Dogs appends "C-Dogs SDL\" to CDOGS_CONFIG_DIR (falls back to %APPDATA% otherwise)
            // Passed to the game process only: a User-level variable is not visible to a process started in the same session
            _environmentVariables["CDOGS_CONFIG_DIR"] = cdogsRoot;

            // XDG_CONFIG_HOME has priority over CDOGS_CONFIG_DIR in C-Dogs, remove it in case a third-party tool set it
            _environmentVariables["XDG_CONFIG_HOME"] = null;

            // Remove the User-level variable written by previous RetroBat versions
            try
            {
                if (Environment.GetEnvironmentVariable("CDOGS_CONFIG_DIR", EnvironmentVariableTarget.User) != null)
                    Environment.SetEnvironmentVariable("CDOGS_CONFIG_DIR", null, EnvironmentVariableTarget.User);
            }
            catch { }

            string configPath = Path.Combine(cdogsRoot, "C-Dogs SDL");
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
            var settings = DynamicJson.Load(configJSON);
            var graphics = settings.GetOrCreateContainer("Graphics");
            var jsoninterface = settings.GetOrCreateContainer("Interface");

            // C-Dogs fullscreen always uses the primary display (display 0)
            // On another screen: windowed at the target screen size, made borderless and placed after launch (see ApplyScreenPlacement)
            var targetScreen = Program.IsMonitorIndexSet ? Program.TargetScreen : null;
            _borderlessOnScreen = _fullscreen && targetScreen != null && !targetScreen.Primary;

            graphics["Fullscreen"] = (_fullscreen && !_borderlessOnScreen) ? "true" : "false";

            if (_borderlessOnScreen)
            {
                graphics["WindowWidth"] = targetScreen.Bounds.Width.ToString();
                graphics["WindowHeight"] = targetScreen.Bounds.Height.ToString();

                // Physical pixel coordinates when the target screen uses Windows scaling (SDL 2.24+, ignored by older SDL)
                _environmentVariables["SDL_WINDOWS_DPI_AWARENESS"] = "permonitorv2";
            }
            // In fullscreen C-Dogs always uses the desktop resolution, the window size only matters in windowed mode
            // Keep the window size saved by the game unless a video mode is set
            else if (_resolution != null)
            {
                graphics["WindowWidth"] = _resolution.Width.ToString();
                graphics["WindowHeight"] = _resolution.Height.ToString();
            }

            BindBoolFeatureOn(graphics, "ShowHUD", "cdogs_hud", "true", "false");
            BindFeature(graphics, "ScaleMode", "cdogs_scalemode", "Nearest neighbor");
            BindBoolFeatureOn(graphics, "Shadows", "cdogs_shadows", "true", "false");
            BindFeature(graphics, "ScaleFactor", "cdogs_scalefactor", "2");
            BindBoolFeatureOn(graphics, "DOSPAR", "cdogs_dospar", "true", "false");
            BindFeature(graphics, "Gore", "cdogs_gore", "Trickle");

            BindBoolFeature(jsoninterface, "ShowFPS", "cdogs_fps", "true", "false");
            BindBoolFeature(jsoninterface, "ShowTime", "cdogs_time", "true", "false");
            BindBoolFeatureOn(jsoninterface, "ShowHUDMap", "cdogs_hudmap", "true", "false");
            BindFeature(jsoninterface, "Splitscreen", "cdogs_splitscreen", "Never");

            ConfigureCDogsControls(settings);

            settings.Save();
        }
        #endregion

        #region cgenius
        private void Configurecgenius(List<string> commandArray, string rom)
        {
            if (_emulator != "cgenius")
                return;

            // Commander Genius looks for cgenius.cfg in %USERPROFILE%\Documents\Commander Genius first, then in the working directory (emulator folder)
            // The file is fully written below before each launch, no template is needed
            string cfgFile = Path.Combine(_path, "cgenius.cfg");

            // A standalone installation config would be used instead of the RetroBat one : rename it during the game, restored in Cleanup()
            string userCfg = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE") ?? "", "Documents", "Commander Genius", "cgenius.cfg");
            string userCfgBackup = userCfg + ".retrobat";

            if (File.Exists(userCfg))
            {
                if (File.Exists(userCfgBackup))
                    SimpleLogger.Instance.Warning("[WARNING] Commander Genius: " + userCfg + " and " + userCfgBackup + " both exist, the user configuration is used instead of the RetroBat one.");
                else
                {
                    try
                    {
                        File.Move(userCfg, userCfgBackup);
                        _cgeniusUserCfg = userCfg;
                        SimpleLogger.Instance.Info("[INFO] Commander Genius: " + userCfg + " renamed during the game.");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Commander Genius: unable to rename " + userCfg + " : " + ex.Message);
                    }
                }
            }
            else if (File.Exists(userCfgBackup))
            {
                // Backup left by a previous session that was not restored : restore it after this game
                _cgeniusUserCfg = userCfg;
            }

            // Games are only detected in the "games" subfolder of a search path : find the root folder that contains "games"
            string gameFolder = Path.GetDirectoryName(rom);
            string searchRoot = null;
            string relativeGamePath = null;

            var dir = new DirectoryInfo(gameFolder);
            while (dir != null && dir.Parent != null)
            {
                if (dir.Name.Equals("games", StringComparison.OrdinalIgnoreCase))
                {
                    searchRoot = dir.Parent.FullName;
                    break;
                }
                dir = dir.Parent;
            }

            if (searchRoot == null)
            {
                SimpleLogger.Instance.Warning("[WARNING] Commander Genius: the game must be in a \"games\" subfolder (roms\\cgenius\\games\\<game>).");
                searchRoot = Path.Combine(AppConfig.GetFullPath("roms"), "cgenius");
            }

            relativeGamePath = gameFolder.Substring(Math.Min(gameFolder.Length, searchRoot.Length)).Replace('\\', '/').TrimStart('/');

            // Game resolution (320x200 = original)
            string gameWidth = "320";
            string gameHeight = "200";
            if (SystemConfig.isOptSet("cgenius_resolution") && !string.IsNullOrEmpty(SystemConfig["cgenius_resolution"]))
            {
                string[] gameRes = SystemConfig["cgenius_resolution"].Split('x');
                if (gameRes.Length == 2)
                {
                    gameWidth = gameRes[0];
                    gameHeight = gameRes[1];
                }
            }

            // Bezels need the 4:3 picture
            string aspect = SystemConfig.GetValueOrDefault("ratio", "4:3");
            if (aspect != "4:3")
                _nobezels = true;

            var screen = Program.TargetScreen ?? Screen.PrimaryScreen;

            using (var ini = IniFile.FromFile(cfgFile, IniOptions.UseSpaces))
            {
                try
                {
                    // Paths : ${BIN} first, configuration and saves (save\<game>) are written to the first search path
                    ini.WriteValue("FileHandling", "EnableLogfile", "true");
                    ini.WriteValue("FileHandling", "SearchPath1", "${BIN}");
                    ini.WriteValue("FileHandling", "SearchPath2", searchRoot);

                    // Window size (fullscreen always uses the desktop resolution)
                    if (_resolution != null)
                    {
                        ini.WriteValue("Video", "width", _resolution.Width.ToString());
                        ini.WriteValue("Video", "height", _resolution.Height.ToString());
                    }
                    else
                    {
                        ini.WriteValue("Video", "width", screen.Bounds.Width.ToString());
                        ini.WriteValue("Video", "height", screen.Bounds.Height.ToString());
                    }

                    // Game resolution : since 3.4.0 it is stored per engine section, [Video] gameWidth / gameHeight are no longer read
                    foreach (string engine in new[] { "Launcher", "Vorticon", "Galaxy" })
                    {
                        ini.WriteValue(engine, "gameWidth", gameWidth);
                        ini.WriteValue(engine, "gameHeight", gameHeight);
                    }
                    ini.Remove("Video", "gameWidth");
                    ini.Remove("Video", "gameHeight");

                    ini.WriteValue("Video", "fullscreen", _fullscreen ? "true" : "false");
                    ini.WriteValue("Video", "aspect", aspect);
                    BindIniFeature(ini, "Video", "filter", "cgenius_filter", "1");
                    BindIniFeature(ini, "Video", "OGLfilter", "OGLfilter", "nearest");
                    BindBoolIniFeatureOn(ini, "Video", "vsync", "cgenius_vsync", "true", "false");
                    BindBoolIniFeature(ini, "Video", "integerScaling", "integerscale", "true", "false");
                    BindBoolIniFeature(ini, "Video", "TiltedScreen", "TiltedScreen", "true", "false");
                    BindIniFeatureSlider(ini, "Video", "fps", "cgenius_fps", "60");

                    // Game options
                    BindBoolIniFeatureOn(ini, "Game", "hud", "cgenius_hud", "true", "false");
                    BindBoolIniFeature(ini, "Game", "showfps", "cgenius_showfps", "true", "false");
                    BindBoolIniFeatureOn(ini, "Game", "showachv", "cgenius_achievements", "true", "false");

                    ConfigureCGeniusControls(ini);
                }
                catch
                {
                    SimpleLogger.Instance.Warning("[WARNING] Error opening cgenius.cfg config file.");
                }
            }

            // Start the game directly (path relative to the search path, as listed by the game launcher)
            commandArray.Add("dir=" + "\"" + relativeGamePath + "\"");
        }
        #endregion

        #region corsixth
        private void Configurecorsixth(List<string> commandArray, string rom)
        {
            if (_emulator != "corsixth")
                return;

            // Theme Hospital data: the game is normally a ".game" folder, if a file is used its folder is taken
            string thInstall = Directory.Exists(rom) ? rom : Path.GetDirectoryName(rom);

            // Portable mode: without config.path.txt, CorsixTH creates %APPDATA%\CorsixTH at startup even with --config-file
            // "." = working directory (emulator folder), avoids path encoding issues in Lua
            string configPathFile = Path.Combine(_path, "config.path.txt");
            try
            {
                if (!File.Exists(configPathFile) || File.ReadAllText(configPathFile).Trim() != ".")
                    File.WriteAllText(configPathFile, ".");
            }
            catch { SimpleLogger.Instance.Warning("[WARNING] Unable to write CorsixTH config.path.txt."); }

            string cfgFile = Path.Combine(_path, "config.txt");
            if (!File.Exists(cfgFile))
            {
                try
                {
                    File.WriteAllText(cfgFile, corsixth_config);
                    System.Threading.Thread.Sleep(100);
                }
                catch { }
            }
            string hotkeyFile = Path.Combine(_path, "hotkeys.txt");
            if (!File.Exists(hotkeyFile))
            {
                try
                {
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
                    ini.WriteValue("", "theme_hospital_install", "[[" + thInstall + "]]");

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

                    // Internal resolution = logical size, letterboxed by CorsixTH in desktop fullscreen
                    // CorsixTH always opens on the primary display
                    int width = _resolution != null ? _resolution.Width : Screen.PrimaryScreen.Bounds.Width;
                    int height = _resolution != null ? _resolution.Height : Screen.PrimaryScreen.Bounds.Height;

                    // The game option has priority over the desktop video mode
                    if (SystemConfig.isOptSet("th_resolution") && !string.IsNullOrEmpty(SystemConfig["th_resolution"]))
                    {
                        string[] gameRes = SystemConfig["th_resolution"].Split('x');
                        if (gameRes.Length == 2 && gameRes[0].ToInteger() > 0 && gameRes[1].ToInteger() > 0)
                        {
                            width = gameRes[0].ToInteger();
                            height = gameRes[1].ToInteger();
                        }
                    }

                    if (height > 0 && (float)width / height > 1.4f)
                        _nobezels = true;

                    ini.WriteValue("", "width", width.ToString());
                    ini.WriteValue("", "height", height.ToString());

                    // UI scale is capped by CorsixTH to the largest scale fitting the resolution
                    // Removed when not set, so that the engine default applies
                    if (SystemConfig.isOptSet("th_ui_scale") && !string.IsNullOrEmpty(SystemConfig["th_ui_scale"]))
                        ini.WriteValue("", "ui_scale", SystemConfig["th_ui_scale"]);
                    else
                        ini.Remove("", "ui_scale");

                    ini.WriteValue("", "check_for_updates", "false");
                    BindBoolIniFeatureOn(ini, "", "capture_mouse", "th_capture_mouse", "true", "false");
                    ini.WriteValue("", "language", "[[" + GetCorsixTHLanguage() + "]]");

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

        // CorsixTH language names (Lua/languages/*.lua), by EmulationStation language code
        private static readonly Dictionary<string, string> corsixthLanguages = new Dictionary<string, string>
        {
            { "cs", "Czech" },
            { "da", "Danish" },
            { "de", "German" },
            { "el", "Greek" },
            { "en", "English" },
            { "es", "Spanish" },
            { "fi", "Finnish" },
            { "fr", "French" },
            { "hu", "Hungarian" },
            { "it", "Italian" },
            { "ja", "Japanese" },
            { "ko", "Korean" },
            { "nb", "Norwegian" },
            { "nl", "Dutch" },
            { "no", "Norwegian" },
            { "pl", "Polish" },
            { "pt", "Portuguese" },
            { "ru", "Russian" },
            { "sv", "Swedish" },
            { "uk", "Ukrainian" },
            { "zh", "Chinese (simplified)" }
        };

        private string GetCorsixTHLanguage()
        {
            if (SystemConfig.isOptSet("th_language") && !string.IsNullOrEmpty(SystemConfig["th_language"]))
                return SystemConfig["th_language"];

            // Default: EmulationStation language, regional variants first
            string esLanguage = SystemConfig["Language"] ?? string.Empty;
            if (esLanguage.Equals("pt_BR", StringComparison.OrdinalIgnoreCase))
                return "Brazilian Portuguese";
            if (esLanguage.Equals("zh_TW", StringComparison.OrdinalIgnoreCase))
                return "Chinese (traditional)";

            string lang;
            if (corsixthLanguages.TryGetValue(GetCurrentLanguage(), out lang))
                return lang;

            return "English";
        }
        #endregion

        #region dhewm3
        private void Configuredhewm3(List<string> commandArray, string rom)
        {
            if (_emulator != "dhewm3")
                return;

            string cfgPath = _path;

            string[] pakFile = File.ReadAllLines(rom);
            if (pakFile.Length < 1)
            {
                throw new ApplicationException("Empty game file.");
            }

            string pakSubPath = pakFile[0];
            string game = pakSubPath.Split('\\')[0];

            // Resurrection of Evil and d3xp based mods (fs_game_base d3xp) use a different weapon slot layout
            bool isD3xp = pakFile.Any(l => l.IndexOf("d3xp", StringComparison.OrdinalIgnoreCase) >= 0);

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

            commandArray.Add("+set");
            commandArray.Add("r_fullscreen");
            commandArray.Add(_fullscreen ? "1" : "0");

            // Desktop fullscreen (borderless, desktop resolution) unless a resolution is forced in RetroBat
            bool forcedMode = SystemConfig.isOptSet("dhewm3_resolution") && !string.IsNullOrEmpty(SystemConfig["dhewm3_resolution"]);
            bool desktopFullscreen = !forcedMode && _resolution == null;

            commandArray.Add("+set");
            commandArray.Add("r_fullscreenDesktop");
            commandArray.Add(desktopFullscreen ? "1" : "0");

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

            string cfgFile = Path.Combine(_path, game, "dhewm.cfg");

            // Without config.spec, dhewm3 runs its hardware detection on first launch and overrides resolution, anti-aliasing, quality and speakers
            string specFile = Path.Combine(_path, game, "config.spec");
            if (!File.Exists(specFile))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(specFile));
                    File.WriteAllText(specFile, string.Empty);
                }
                catch { }
            }

            var changes = new List<Dhewm3ConfigChange>();

            // d3xp re-executes default.cfg after dhewm.cfg on first launch when g_xp_bind_run_once is 0, which would reset the RetroBat bindings
            // default.cfg of d3xp is already executed at startup (fs_game d3xp), so the xp bindings are not lost
            if (isD3xp)
                changes.Add(new Dhewm3ConfigChange("seta", "g_xp_bind_run_once", "1"));

            if (forcedMode)
                changes.Add(new Dhewm3ConfigChange("seta", "r_mode", SystemConfig["dhewm3_resolution"]));
            else if (_resolution != null)
            {
                changes.Add(new Dhewm3ConfigChange("seta", "r_mode", "-1"));
                changes.Add(new Dhewm3ConfigChange("seta", "r_customWidth", _resolution.Width.ToString()));
                changes.Add(new Dhewm3ConfigChange("seta", "r_customHeight", _resolution.Height.ToString()));
            }

            if (!SystemConfig.isOptSet("dhewm3_vsync") || SystemConfig.getOptBoolean("dhewm3_vsync"))
                changes.Add(new Dhewm3ConfigChange("seta", "r_swapInterval", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_swapInterval", "0"));

            // com_machineSpec alone is only a label: write the preset values like dhewm3 does when a preset is loaded (r_mode and r_multiSamples excluded)
            int quality;
            if (SystemConfig.isOptSet("dhewm3_quality") && int.TryParse(SystemConfig["dhewm3_quality"], out quality) && quality >= 0 && quality <= 3)
            {
                changes.Add(new Dhewm3ConfigChange("seta", "com_machineSpec", quality.ToString()));
                foreach (var preset in dhewm3QualityPresets)
                    changes.Add(new Dhewm3ConfigChange("seta", preset.Key, preset.Value[quality]));
            }

            if (SystemConfig.isOptSet("dhewm3_antialiasing") && !string.IsNullOrEmpty(SystemConfig["dhewm3_antialiasing"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_multiSamples", SystemConfig["dhewm3_antialiasing"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_multiSamples", "0"));

            if (SystemConfig.isOptSet("dhewm3_brightness") && !string.IsNullOrEmpty(SystemConfig["dhewm3_brightness"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_brightness", SystemConfig["dhewm3_brightness"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_brightness", "1"));

            if (SystemConfig.isOptSet("dhewm3_sound") && !string.IsNullOrEmpty(SystemConfig["dhewm3_sound"]))
                changes.Add(new Dhewm3ConfigChange("seta", "s_numberOfSpeakers", SystemConfig["dhewm3_sound"]));

            if (!SystemConfig.isOptSet("dhewm3_eax") || SystemConfig.getOptBoolean("dhewm3_eax"))
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

            if (SystemConfig.isOptSet("dhewm3_showfps") && SystemConfig.getOptBoolean("dhewm3_showfps"))
                changes.Add(new Dhewm3ConfigChange("seta", "com_showFPS", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "com_showFPS", "0"));

            // Always run is ignored in single player unless in_allowAlwaysRunInSP is set
            if (SystemConfig.isOptSet("dhewm3_alwaysrun") && SystemConfig.getOptBoolean("dhewm3_alwaysrun"))
            {
                changes.Add(new Dhewm3ConfigChange("seta", "in_alwaysRun", "1"));
                changes.Add(new Dhewm3ConfigChange("seta", "in_allowAlwaysRunInSP", "1"));
            }
            else
            {
                changes.Add(new Dhewm3ConfigChange("seta", "in_alwaysRun", "0"));
                changes.Add(new Dhewm3ConfigChange("seta", "in_allowAlwaysRunInSP", "0"));
            }

            if (SystemConfig.isOptSet("dhewm3_invertlook") && SystemConfig.getOptBoolean("dhewm3_invertlook"))
            {
                changes.Add(new Dhewm3ConfigChange("seta", "joy_invertLook", "1"));
                changes.Add(new Dhewm3ConfigChange("seta", "m_invertLook", "1"));
            }
            else
            {
                changes.Add(new Dhewm3ConfigChange("seta", "joy_invertLook", "0"));
                changes.Add(new Dhewm3ConfigChange("seta", "m_invertLook", "0"));
            }

            // Stick dead zone (0 to 1, dhewm3 default 0.25)
            changes.Add(new Dhewm3ConfigChange("seta", "joy_deadZone", SystemConfig.GetValueOrDefault("dhewm3_deadzone", "0.25")));

            // Keyboard keys used by RetroBat hotkeys (es_padtokey.cfg): F2 quicksave, F4 quickload, F8 screenshot
            changes.Add(new Dhewm3ConfigChange("bind", "F2", "savegame quick"));
            changes.Add(new Dhewm3ConfigChange("bind", "F4", "loadgame quick"));
            changes.Add(new Dhewm3ConfigChange("bind", "F8", "screenshot"));

            // Multiplayer "vote no" moved from F2 to F11
            changes.Add(new Dhewm3ConfigChange("bind", "F11", "_impulse29"));

            ConfigureDhewm3Controls(changes, isD3xp);

            ConfigEditorDhewm3.ChangeConfigValues(cfgFile, changes, true);
        }

        // Same values as dhewm3 Com_ExecMachineSpec_f, index = com_machineSpec (0 = low, 1 = medium, 2 = high, 3 = ultra)
        private static readonly Dictionary<string, string[]> dhewm3QualityPresets = new Dictionary<string, string[]>()
        {
            { "image_filter", new[] { "GL_LINEAR_MIPMAP_LINEAR", "GL_LINEAR_MIPMAP_LINEAR", "GL_LINEAR_MIPMAP_LINEAR", "GL_LINEAR_MIPMAP_LINEAR" } },
            { "image_anisotropy", new[] { "0", "1", "8", "8" } },
            { "image_lodbias", new[] { "0", "0", "0", "0" } },
            { "image_downSize", new[] { "1", "0", "0", "0" } },
            { "image_forceDownSize", new[] { "0", "0", "0", "0" } },
            { "image_roundDown", new[] { "1", "1", "1", "1" } },
            { "image_preload", new[] { "1", "1", "1", "1" } },
            { "image_useAllFormats", new[] { "1", "1", "1", "1" } },
            { "image_useCompression", new[] { "1", "1", "1", "0" } },
            { "image_usePrecompressedTextures", new[] { "1", "1", "1", "0" } },
            { "image_useNormalCompression", new[] { "2", "2", "0", "0" } },
            { "image_ignoreHighQuality", new[] { "1", "0", "0", "0" } },
            { "image_downSizeSpecular", new[] { "1", "0", "0", "0" } },
            { "image_downSizeBump", new[] { "1", "0", "0", "0" } },
            { "image_downSizeSpecularLimit", new[] { "64", "64", "64", "64" } },
            { "image_downSizeBumpLimit", new[] { "256", "256", "256", "256" } },
            { "s_maxSoundsPerShader", new[] { "1", "0", "0", "0" } },
            { "r_useSoftParticles", new[] { "0", "0", "0", "1" } }
        };
        #endregion

        #region dusklight
        private void Configuredusklight(List<string> commandArray, string rom)
        {
            if (_emulator != "dusklight")
                return;

            // Commandarray and romPath
            if (Path.GetExtension(rom).Equals(".m3u", StringComparison.OrdinalIgnoreCase))
            {
                string newRom = FileTools.ReadFirstValidLine(rom);
                if (File.Exists(newRom))
                {
                    commandArray.Add("\"" + newRom + "\"");
                    rom = newRom;
                    _romPath = Path.GetDirectoryName(newRom);
                }
                else
                {
                    throw new ApplicationException("Unable to find path to iso in m3u file.");
                }
            }
            else
            {
                commandArray.Add("\"" + rom + "\"");
            }

            // Ensure portable mode: Dusklight 2.x ignores the descriptor without "version": 1 (also accepted by 1.x)
            string dataJson = Path.Combine(_path, "data_location.json");
            try { File.WriteAllText(dataJson, "{\r\n    \"version\": 1,\r\n    \"mode\": \"portable\"\r\n}"); }
            catch { SimpleLogger.Instance.Warning("[WARNING] Unable to write Dusklight data_location.json."); }

            var datalocation = DynamicJson.Load(dataJson);
            datalocation["mode"] = "portable";
            datalocation.Save();

            // Config json file
            string dataPath = Path.Combine(_path, "data");
            if (!Directory.Exists(dataPath))
                try { Directory.CreateDirectory(dataPath); } catch { }

            string configJson = Path.Combine(dataPath, "config.json");
            if (!File.Exists(configJson))
                try { File.WriteAllText(configJson, "{}"); } catch { }

            var config = DynamicJson.Load(configJson);
            config["backend.checkForUpdates"] = "false";
            config["backend.isoPath"] = rom;
            config["backend.wasPresetChosen"] = "true";
            config["video.enableFullscreen"] = _fullscreen ? "true" : "false";

            BindFeature(config, "backend.graphicsBackend", "dusklight_renderer", "auto");
            BindFeature(config, "game.internalResolutionScale", "dusklight_resolution", "0");
            BindFeatureSlider(config, "game.shadowResolutionMultiplier", "dusklight_shadowres", "1");
            BindFeature(config, "game.language", "dusklight_language", "0");
            config["game.pauseOnFocusLost"] = SystemConfig.getOptBoolean("nopauseonlostfocus") ? "false" : "true";
            BindBoolFeatureOn(config, "video.enableVsync", "dusklight_vsync", "true", "false");
            BindBoolFeature(config, "video.lockAspectRatio", "dusklight_force43", "true", "false");
            BindBoolFeature(config, "game.enableFpsOverlay", "dusklight_fps", "true", "false");
            BindBoolFeature(config, "game.enableDiscordPresence", "discord", "true", "false");
            BindFeature(config, "game.bloomMode", "dusklight_bloom", "2");
            BindFeature(config, "game.enableFrameInterpolation", "dusklight_interpolation", "0");
            BindFeature(config, "game.depthOfFieldMode", "dusklight_dof", "2");
            BindFeature(config, "game.disableLetterboxing", "dusklight_letterbox", "0");
            BindBoolFeature(config, "game.disableCutscenePillarboxing", "dusklight_pillarbox", "true", "false");
            BindBoolFeature(config, "game.enableMirrorMode", "dusklight_mirror", "true", "false");
            BindBoolFeature(config, "game.freeCamera", "dusklight_freecam", "true", "false");
            BindBoolFeature(config, "game.enableGyroAim", "dusklight_gyroaim", "true", "false");
            BindBoolFeature(config, "game.enableControllerToasts", "dusklight_ctrltoasts", "true", "false");
            BindBoolFeature(config, "backend.showPipelineCompilation", "dusklight_showshadercompile", "true", "false");

            // Quality of life settings from Dusklight's own presets, only written when the option is set
            // so that settings changed in the Dusklight menu are kept otherwise
            if (SystemConfig.isOptSet("dusklight_enhancements") && !string.IsNullOrEmpty(SystemConfig["dusklight_enhancements"]))
            {
                bool duskPreset = SystemConfig["dusklight_enhancements"] == "dusk";

                foreach (string key in dusklightEnhancements)
                    config[key] = duskPreset ? "true" : "false";

                // The original TV settings screen is only shown by the Classic preset
                config["game.hideTvSettingsScreen"] = duskPreset ? "true" : "false";
            }

            if (config["game.fpsOverlayCorner"] == null)
                config["game.fpsOverlayCorner"] = "0";

            config.Save();

            ConfigureDusklightControls(dataPath);
        }

        // Quality of life settings enabled by Dusklight's "Dusklight" preset (ui/preset.cpp), disabled by its "Classic" preset
        private static readonly string[] dusklightEnhancements = new string[]
        {
            "game.enableQuickTransform",
            "game.biggerWallets",
            "game.noReturnRupees",
            "game.disableRupeeCutscenes",
            "game.noSwordRecoil",
            "game.fastClimbing",
            "game.noMissClimbing",
            "game.fastTears",
            "game.no2ndFishForCat",
            "game.buttonFishing",
            "game.instantSaves",
            "game.midnasLamentNonStop",
            "game.sunsSong",
            "game.autoSave",
            "game.enhancedMapMenus"
        };
        #endregion

        #region opengoal
        // PC_KERNEL_VERSION, as written by the game in pc-settings.gc
        private static readonly Dictionary<string, string> _openGoalSettingsHeader = new Dictionary<string, string>
        {
            { "jak1", "#x1000a00040000" },  // 1.10.4.0
            { "jak2", "#x200000000" },      // 0.2.0.0
            { "jak3", "#x300000000" }       // 0.3.0.0
        };

        // Language values are game specific
        private static readonly Dictionary<string, Dictionary<string, int>> _openGoalTextLanguages = new Dictionary<string, Dictionary<string, int>>
        {
            { "jak1", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=5,en_uk=6,pt=7,fi=8,sv=9,da=10,no=11,nl=12,pt_br=13,hu=14,pl=19,lt=20,cs=21,hr=22,gl=23,bs=24") },
            { "jak2", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=5,ko=6,en_uk=7,pt=8,fi=9,sv=10,da=11,no=12,nl=13,pt_br=14,hu=15,ca=16,is=17,pl=18,lt=19,cs=20,hr=21,gl=22") },
            { "jak3", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=6,ko=7,ru=8,pt=9,nl=10,en_uk=11,fi=12,sv=13,da=14,no=15,pt_br=16,hu=17,ca=18,is=19,pl=20,lt=21,cs=22,hr=23,gl=24") }
        };

        private static readonly Dictionary<string, Dictionary<string, int>> _openGoalSubtitleLanguages = new Dictionary<string, Dictionary<string, int>>
        {
            { "jak1", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,en_uk=6,fi=8,sv=9,da=10,nl=12,pt_br=13,pl=19,lt=20,cs=21,hr=22,gl=23,bs=24") },
            { "jak2", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=5,ko=6,en_uk=7,pt=8,fi=9,sv=10,da=11,no=12,nl=13,pt_br=14,hu=15,ca=16,is=17,pl=18,lt=19,cs=20,hr=21,gl=22") },
            { "jak3", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=6,ko=7,ru=8,pt=9,nl=10,en_uk=11,fi=12,sv=13,da=14,no=15,pt_br=16,hu=17,ca=18,is=19,pl=20,lt=21,cs=22,hr=23,gl=24") }
        };

        private static readonly Dictionary<string, Dictionary<string, int>> _openGoalAudioLanguages = new Dictionary<string, Dictionary<string, int>>
        {
            { "jak1", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=5") },
            { "jak2", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=5,ko=6") },
            { "jak3", OpenGoalLanguageTable("en=0,fr=1,de=2,es=3,it=4,ja=6,ko=7,ru=8,pt=9,nl=10,en_uk=11") }
        };

        private static Dictionary<string, int> OpenGoalLanguageTable(string table)
        {
            return table.Split(',')
                .Select(s => s.Split('='))
                .ToDictionary(p => p[0], p => int.Parse(p[1], CultureInfo.InvariantCulture));
        }
        private void ConfigureOpenGoal(List<string> commandArray, string rom)
        {
            if (_emulator != "opengoal")
                return;

            List<string> openGoalGames = new List<string> { "jak1", "jak2", "jak3" };

            // "aspect-state" = PS2 game aspect, custom ratio X, custom ratio Y, auto ratio (follows the window)
            // A fixed ratio (auto = #f) resets "use-vis?", so "use-vis?" must always be written after "aspect-state"
            Dictionary<string, string> ratioList = new Dictionary<string, string>
            {
                { "4_3", "aspect4x3 4 3 #f" },
                { "16_9", "aspect4x3 16 9 #f" },
                { "4_3_ps2", "aspect4x3 4 3 #f" },
                { "16_9_ps2", "aspect16x9 4 3 #f" },
                { "16_10", "aspect4x3 16 10 #f" },
                { "21_9", "aspect4x3 21 9 #f" },
                { "64_27", "aspect4x3 64 27 #f" }
            };

            string gameName = Path.GetFileNameWithoutExtension(rom);
            string[] romLines = File.ReadAllLines(rom);
            if (romLines.Length > 0)
                gameName = romLines[0].Trim();

            if (!openGoalGames.Contains(gameName))
                throw new ApplicationException("Game not supported by engine, ensure the game name is correct : " + gameName);

            // Check if game has been extracted already, if not, user can set path to iso in ES
            string outDataPath = Path.Combine(_path, "data", "out", gameName, "iso");
            if (!Directory.Exists(outDataPath))
                ExtractOpenGoalGame(gameName, outDataPath);

            commandArray.Add("-g");
            commandArray.Add(gameName);

            string configFolder = Path.Combine(_path, "config");
            commandArray.Add("--config-path");
            commandArray.Add("\"" + configFolder + "\"");

            string gameConfigPath = Path.Combine(configFolder, "OpenGOAL", gameName);
            string settingsFolder = Path.Combine(gameConfigPath, "settings");
            string miscFolder = Path.Combine(gameConfigPath, "misc");

            // Create the folders on first launch, so that settings are applied right away (no need to start the game once)
            foreach (string folder in new[] { settingsFolder, miscFolder })
            {
                if (!Directory.Exists(folder))
                    try { Directory.CreateDirectory(folder); } catch { }
            }

            // Debug Settings file
            string debugSettingsFile = Path.Combine(miscFolder, "debug-settings.json");

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
            string displaySettingsFile = Path.Combine(settingsFolder, "display-settings.json");

            if (!File.Exists(displaySettingsFile))
            {
                try { File.WriteAllText(displaySettingsFile, "{}"); }
                catch { SimpleLogger.Instance.Warning("[WARNING] Error opening display-settings.json config file."); }
            }

            if (File.Exists(displaySettingsFile))
            {
                var displayConf = DynamicJson.Load(displaySettingsFile);

                if (Program.IsMonitorIndexSet)
                    displayConf["display_id"] = Displays.IndexOf(Program.TargetScreen, MonitorOrder.SdlLike).ToString();

                if (_fullscreen && _exclusivefs)
                    displayConf["display_mode"] = "1";
                else if (_fullscreen)
                    displayConf["display_mode"] = "2";
                else
                    displayConf["display_mode"] = "0";

                displayConf.Save();
            }

            // Controllers (input-settings.json)
            ConfigureOpenGoalControls(settingsFolder);

            // Game settings
            string configFilePath = Path.Combine(settingsFolder, "pc-settings.gc");

            // First launch: create a minimal file, the game fills in the missing settings with its own defaults
            if (!File.Exists(configFilePath) && _openGoalSettingsHeader.ContainsKey(gameName))
            {
                try { File.WriteAllLines(configFilePath, new[] { "(settings " + _openGoalSettingsHeader[gameName], "  )" }); }
                catch { SimpleLogger.Instance.Warning("[WARNING] Could not create file: " + configFilePath); }
            }

            if (!File.Exists(configFilePath))
            {
                SimpleLogger.Instance.Warning("[WARNING] Could not find file: " + configFilePath);
                return;
            }

            List<string> configLines = File.ReadAllLines(configFilePath).ToList();

            // Replace a top-level setting, or add it before the closing parenthesis when the game has not written it yet
            // Only keys known by the game must be written : the game rejects the whole file on an unknown key
            Action<string, string> bindFeature = (string feature, string value) =>
            {
                string newLine = "  (" + feature + " " + value + ")";

                int index = configLines.FindIndex(l => l.TrimStart().StartsWith("(" + feature + " "));
                if (index >= 0)
                {
                    configLines[index] = newLine;
                    return;
                }

                int closeIndex = configLines.FindLastIndex(l => l.Trim() == ")");
                if (closeIndex >= 0)
                    configLines.Insert(closeIndex, newLine);
            };

            // Remove a top-level setting : the game then uses its own default (pc-settings "reset" runs before the file is read)
            Action<string> removeFeature = (string feature) =>
            {
                configLines.RemoveAll(l => l.TrimStart().StartsWith("(" + feature + " "));
            };

            // Language values differ for each game, ES stores a language code
            Action<string, string, Dictionary<string, Dictionary<string, int>>> bindLanguage = (string feature, string option, Dictionary<string, Dictionary<string, int>> table) =>
            {
                string lang = SystemConfig.isOptSet(option) ? SystemConfig[option] : null;

                int value;
                if (!string.IsNullOrEmpty(lang) && table.ContainsKey(gameName) && table[gameName].TryGetValue(lang, out value))
                {
                    bindFeature(feature, value.ToString(CultureInfo.InvariantCulture));
                    return;
                }

                if (!string.IsNullOrEmpty(lang))
                    SimpleLogger.Instance.Info("[INFO] OpenGOAL: language '" + lang + "' is not available in " + gameName + " (" + feature + "), using the game default.");

                removeFeature(feature);
            };


            if (SystemConfig.isOptSet("opengoal_msaa") && !string.IsNullOrEmpty(SystemConfig["opengoal_msaa"]))
                bindFeature("msaa", SystemConfig["opengoal_msaa"]);
            else
                bindFeature("msaa", "2");

            // Default = engine default for the 3 games (pc-settings "reset-gfx"): automatic ratio, no PS2 visibility trees
            string openGoalRatio = SystemConfig["opengoal_ratio"];

            if (!string.IsNullOrEmpty(openGoalRatio) && ratioList.ContainsKey(openGoalRatio))
            {
                bindFeature("aspect-state", ratioList[openGoalRatio]);
                bindFeature("use-vis?", openGoalRatio.EndsWith("_ps2") ? "#t" : "#f");
            }
            else
            {
                bindFeature("aspect-state", "aspect4x3 4 3 #t");
                bindFeature("use-vis?", "#f");
            }

            if (SystemConfig.isOptSet("opengoal_resolution") && !string.IsNullOrEmpty(SystemConfig["opengoal_resolution"]))
                bindFeature("game-size", SystemConfig["opengoal_resolution"]);
            else
            {
                var bounds = Program.TargetScreen.Bounds;
                string res = _resolution == null ? bounds.Width + " " + bounds.Height : _resolution.Width + " " + _resolution.Height;
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
                bindFeature(gameName == "jak1" ? "subtitles?" : "memcard-subtitles?", "#f");
            else
                bindFeature(gameName == "jak1" ? "subtitles?" : "memcard-subtitles?", "#t");

            if (SystemConfig.isOptSet("opengoal_region") && !string.IsNullOrEmpty(SystemConfig["opengoal_region"]))
                bindFeature("territory", SystemConfig["opengoal_region"]);
            else
                bindFeature("territory", "-1");

            bindLanguage("game-language", "opengoal_audio_lang", _openGoalAudioLanguages);
            bindLanguage("text-language", "opengoal_text_lang", _openGoalTextLanguages);
            bindLanguage("subtitle-language", "opengoal_subtitle_lang", _openGoalSubtitleLanguages);

            File.WriteAllLines(configFilePath, configLines);
        }

        private void ExtractOpenGoalGame(string gameName, string outDataPath)
        {
            SimpleLogger.Instance.Warning("[WARNING] OpenGOAL data folder not found, checking if a path to ISO is specified.");

            string isoOption = "opengoal_isopath_" + gameName;
            string isoPath = SystemConfig.isOptSet(isoOption) ? SystemConfig[isoOption] : null;

            if (string.IsNullOrEmpty(isoPath))
                throw new ApplicationException("OpenGOAL: " + gameName + " is not extracted yet. Set '" + gameName.ToUpperInvariant() + " ISO PATH' in the advanced settings of the system to extract it automatically.");

            if (!File.Exists(isoPath))
                throw new ApplicationException("OpenGOAL: ISO file not found: " + isoPath);

            string extractor = Path.Combine(_path, "extractor.exe");
            if (!File.Exists(extractor))
                throw new ApplicationException("OpenGOAL: extractor.exe not found in " + _path);

            SimpleLogger.Instance.Info("[INFO] Extracting " + gameName + " from ISO: " + isoPath);

            // Steps are explicit (extract, decompile, compile): without step flags, the extractor also runs "play",
            // which starts gk.exe without --config-path (settings and saves would be written to %APPDATA%)
            var extractorInfo = new ProcessStartInfo()
            {
                FileName = extractor,
                WorkingDirectory = _path,
                Arguments = "-g " + gameName + " -e -d -c \"" + isoPath + "\"",
            };

            int exitCode;
            try
            {
                using (var process = Process.Start(extractorInfo))
                {
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("OpenGOAL: failed to start the extractor - " + ex.Message);
            }

            if (exitCode != 0 || !Directory.Exists(outDataPath))
                throw new ApplicationException("OpenGOAL: extraction of " + gameName + " failed (code " + exitCode + "). Check that the ISO is a supported release of the game.");

            SimpleLogger.Instance.Info("[INFO] OpenGOAL: " + gameName + " extracted successfully, starting the game.");
        }
        #endregion

        #region openjazz
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

            // Configuration (stored in the working directory, i.e. the emulator folder)
            string configFile = Path.Combine(_path, "openjazz.cfg");

            byte[] configBytes = null;
            if (File.Exists(configFile))
                try { configBytes = File.ReadAllBytes(configFile); } catch { }

            // No config yet (or empty file): start from the OpenJazz default settings so RetroBat options apply on first launch
            if (configBytes == null || configBytes.Length == 0)
                configBytes = CreateOpenJazzDefaultConfig();

            // Config written by OpenJazz 20240919 or older (v6): convert it to v7 to keep the user settings
            // v6 header: version, width, height, video options (fullscreen | scale << 1) / v7 header: version, width, height, fullscreen, scale, scale method
            if (configBytes.Length > 6 && configBytes[0] == 6)
            {
                byte videoOptions = configBytes[5];
                var converted = new List<byte>();
                converted.Add(7);
                converted.AddRange(configBytes.Skip(1).Take(4));
                converted.Add((byte)(videoOptions & 1));
                converted.Add((byte)Math.Max(1, videoOptions >> 1));
                converted.Add(0);
                converted.AddRange(configBytes.Skip(6));
                configBytes = converted.ToArray();
                SimpleLogger.Instance.Info("[INFO] OpenJazz: openjazz.cfg converted to version 7.");
            }

            if (configBytes.Length < 8 || configBytes[0] != 7)
            {
                SimpleLogger.Instance.Warning("[WARNING] OpenJazz: unknown openjazz.cfg version, file left unchanged.");
                return;
            }

            // Internal resolution
            string resolution = "320x200";

            if (SystemConfig.isOptSet("openjazz_resolution") && !string.IsNullOrEmpty(SystemConfig["openjazz_resolution"]))
                resolution = SystemConfig["openjazz_resolution"];

            string[] size = resolution.Split('x');

            if (size.Length == 2 && int.TryParse(size[0], out int widthint) && int.TryParse(size[1], out int heightint))
            {
                byte[] widthBytes = BitConverter.GetBytes((short)widthint);
                byte[] heightBytes = BitConverter.GetBytes((short)heightint);

                configBytes[1] = widthBytes[0];
                configBytes[2] = widthBytes[1];
                configBytes[3] = heightBytes[0];
                configBytes[4] = heightBytes[1];
            }

            // Fullscreen (also forced by the command line)
            configBytes[5] = (byte)(_fullscreen ? 1 : 0);

            // Scale factor: the internal resolution is divided by this factor (only applied if it stays at least 320x200)
            int scale = 1;
            if (SystemConfig.isOptSet("openjazz_scale") && int.TryParse(SystemConfig["openjazz_scale"], out int s))
                scale = Math.Max(1, Math.Min(4, s));
            configBytes[6] = (byte)scale;

            // Scale method: 0 = none, 1 = bilinear, 2 = scale2x (3 = hqx is not implemented in 20260301)
            int filter = 0;
            if (SystemConfig.isOptSet("openjazz_filter") && int.TryParse(SystemConfig["openjazz_filter"], out int f))
                filter = Math.Max(0, Math.Min(2, f));
            configBytes[7] = (byte)filter;

            ConfigureOpenJazzControls(configBytes);

            try { File.WriteAllBytes(configFile, configBytes); }
            catch { SimpleLogger.Instance.Warning("[WARNING] OpenJazz: unable to write " + configFile); }
        }

        // Default openjazz.cfg (version 7, OpenJazz 20260301) with the engine default settings for Windows
        private static byte[] CreateOpenJazzDefaultConfig()
        {
            var data = new List<byte>();
            Action<int> addInt = v => data.AddRange(BitConverter.GetBytes(v));

            // Header: version, width, height, fullscreen, scale, scale method
            data.Add(7);
            data.AddRange(BitConverter.GetBytes((short)320));
            data.AddRange(BitConverter.GetBytes((short)200));
            data.Add(0);
            data.Add(1);
            data.Add(0);

            // Keys (SDL2 key codes): up, down, left, right, jump, swim, fire, change weapon, enter, escape, blaster, toaster, missile, bouncer, TNT
            int[] keys = { 1073741906, 1073741905, 1073741904, 1073741903, 1073742054, 1073742054, 32, 1073742052, 13, 27, 49, 50, 51, 52, 53 };
            foreach (int k in keys)
                addInt(k);

            // Joystick buttons (19 controls)
            int[] buttons = { -1, -1, -1, -1, 1, 1, 0, 3, 0, 5, -1, -1, -1, -1, -1, -1, 4, -1, -1 };
            foreach (int b in buttons)
                addInt(b);

            // Joystick axes (axis, positive direction): up/down on axis 1, left/right on axis 0
            for (int i = 0; i < 19; i++)
            {
                switch (i)
                {
                    case 0: addInt(1); addInt(0); break;
                    case 1: addInt(1); addInt(1); break;
                    case 2: addInt(0); addInt(0); break;
                    case 3: addInt(0); addInt(1); break;
                    default: addInt(-1); addInt(0); break;
                }
            }

            // Joystick hats (hat, direction): hat 0 up = 1, down = 4, left = 8, right = 2
            for (int i = 0; i < 19; i++)
            {
                switch (i)
                {
                    case 0: addInt(0); addInt(1); break;
                    case 1: addInt(0); addInt(4); break;
                    case 2: addInt(0); addInt(8); break;
                    case 3: addInt(0); addInt(2); break;
                    default: addInt(-1); addInt(0); break;
                }
            }

            // Player name (32 chars)
            byte[] name = new byte[32];
            Encoding.ASCII.GetBytes("jazz").CopyTo(name, 0);
            data.AddRange(name);

            // Player colours, music and sound volume, gameplay options (leave unneeded)
            data.AddRange(new byte[] { 4, 3, 2, 8 });
            data.Add(50);
            data.Add(25);
            data.Add(2);

            return data.ToArray();
        }
        #endregion

        #region pdark
        private void ConfigurePDark(List<string> commandArray, string rom)
        {
            if (_emulator != "pdark")
                return;

            // Rom file: big endian roms are loaded in place, byte-swapped roms are converted in the data folder
            string romFile = rom;
            if (!_pdarkRomIsZ64)
            {
                string dataPath = Path.Combine(_path, "data");
                romFile = Path.Combine(dataPath, "pd." + _pdarkRomId + ".z64");

                try
                {
                    if (!PDarkRomHeaderMatches(rom, romFile))
                    {
                        if (!Directory.Exists(dataPath))
                            Directory.CreateDirectory(dataPath);

                        byte[] data = File.ReadAllBytes(rom);
                        SwapN64ToBigEndian(data);
                        File.WriteAllBytes(romFile, data);
                        SimpleLogger.Instance.Info("[INFO] Perfect Dark: rom converted to " + romFile);
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[WARNING] Perfect Dark: unable to convert the rom : " + ex.Message);
                }
            }

            commandArray.Add("--rom-file");
            commandArray.Add("\"" + romFile + "\"");

            string cfgFile = Path.Combine(_path, "pd.ini");
            using (var ini = IniFile.FromFile(cfgFile))
            {
                try
                {
                    // Exclusive fullscreen always stays on the primary display: use borderless fullscreen on another screen so the window can be moved
                    bool otherScreen = Program.IsMonitorIndexSet && Program.TargetScreen != null && !Program.TargetScreen.Primary;
                    bool exclusive = _exclusivefs && !otherScreen;

                    ini.WriteValue("Video", "DefaultFullscreen", _fullscreen ? "1" : "0");
                    ini.WriteValue("Video", "ExclusiveFullscreen", exclusive ? "1" : "0");

                    // Maximized window unless a video mode is chosen
                    ini.WriteValue("Video", "DefaultMaximize", _resolution == null ? "1" : "0");

                    if (_resolution != null)
                    {
                        ini.WriteValue("Video", "DefaultWidth", _resolution.Width.ToString());
                        ini.WriteValue("Video", "DefaultHeight", _resolution.Height.ToString());
                    }
                    else if (exclusive)
                    {
                        ini.WriteValue("Video", "DefaultWidth", ScreenResolution.CurrentResolution.Width.ToString());
                        ini.WriteValue("Video", "DefaultHeight", ScreenResolution.CurrentResolution.Height.ToString());
                    }

                    BindBoolIniFeatureOn(ini, "Video", "VSync", "pdark_vsync", "1", "0");
                    BindBoolIniFeatureOn(ini, "Video", "DetailTextures", "pdark_detailtexture", "1", "0");
                    BindIniFeature(ini, "Video", "TextureFilter", "pdark_texturefilter", "1");
                    BindIniFeature(ini, "Video", "MSAA", "pdark_msaa", "1");
                    BindBoolIniFeature(ini, "Game", "SkipIntro", "pdark_skipintro", "1", "0");

                    ConfigurePDarkControls(ini);
                }
                catch { }
            }
        }

        // Reads the rom id of a Perfect Dark rom from its header, whatever its byte order
        // Returns "ntsc-final", "pal-final", "jpn-final" or null when the file is not a supported Perfect Dark rom
        private static string GetPDarkRomId(string rom, out bool isBigEndian)
        {
            isBigEndian = false;

            try
            {
                byte[] header = new byte[0x40];
                using (var fs = File.OpenRead(rom))
                {
                    if (fs.Length != 33554432 || fs.Read(header, 0, header.Length) != header.Length)
                        return null;
                }

                isBigEndian = header[0] == 0x80 && header[1] == 0x37;
                if (!SwapN64ToBigEndian(header))
                    return null;

                // Game code at 0x3B: N PD + region
                if (header[0x3B] != 'N' || header[0x3C] != 'P' || header[0x3D] != 'D')
                    return null;

                switch ((char)header[0x3E])
                {
                    case 'E': return "ntsc-final";
                    case 'P': return "pal-final";
                    case 'J': return "jpn-final";
                }
            }
            catch { }

            return null;
        }

        // True when the converted rom already exists and comes from the same dump (header includes the rom checksums)
        private static bool PDarkRomHeaderMatches(string sourceRom, string targetRom)
        {
            if (!File.Exists(targetRom))
                return false;

            byte[] src = new byte[0x40];
            byte[] dst = new byte[0x40];

            using (var fs = File.OpenRead(sourceRom))
                fs.Read(src, 0, src.Length);

            using (var fs = File.OpenRead(targetRom))
            {
                if (fs.Length != 33554432)
                    return false;
                fs.Read(dst, 0, dst.Length);
            }

            SwapN64ToBigEndian(src);
            return src.SequenceEqual(dst);
        }

        // Converts a .v64 (byte-swapped) or .n64 (little endian) N64 image to .z64 (big endian), in place
        private static bool SwapN64ToBigEndian(byte[] data)
        {
            if (data == null || data.Length < 4)
                return false;

            if (data[0] == 0x80 && data[1] == 0x37)
                return true;

            if (data[0] == 0x37 && data[1] == 0x80)
            {
                for (int i = 0; i + 1 < data.Length; i += 2)
                {
                    byte t = data[i];
                    data[i] = data[i + 1];
                    data[i + 1] = t;
                }
                return true;
            }

            if (data[0] == 0x40 && data[1] == 0x12)
            {
                for (int i = 0; i + 3 < data.Length; i += 4)
                {
                    byte t0 = data[i];
                    byte t1 = data[i + 1];
                    data[i] = data[i + 3];
                    data[i + 1] = data[i + 2];
                    data[i + 2] = t1;
                    data[i + 3] = t0;
                }
                return true;
            }

            return false;
        }
        #endregion

        #region powerbomberman
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
        #endregion

        #region rtcw
        private void ConfigureRTCW(List<string> commandArray, string rom)
        {
            if (_emulator != "rtcw")
                return;

            // Check engine files are available in roms folder
            string engineMainPath = Path.Combine(AppConfig.GetFullPath("roms"), "rtcw", "Main");
            string checkFile = Path.Combine(engineMainPath, "z_zrealrtcw_ui.pk3");
            if (!File.Exists(checkFile))
            {
                throw new ApplicationException("RealRTCW engine files not found in roms/rtcw/Main folder.");
            }

            List<string> pakFiles = new List<string>() { "pak0.pk3", "sp_pak1.pk3", "sp_pak2.pk3", "sp_pak3.pk3", "sp_pak4.pk3" };

            foreach (string pakFile in pakFiles)
            {
                string fileCheck = Path.Combine(engineMainPath, pakFile);

                if (!File.Exists(fileCheck))
                    throw new ApplicationException("Missing file: " + fileCheck);
            }

            string game = "Main";
            string homePath = Path.Combine(AppConfig.GetFullPath("roms"), "rtcw");
            string gameFolderName = Path.GetFileName(_romPath);

            if (!string.IsNullOrEmpty(gameFolderName))
                game = gameFolderName;

            commandArray.Add("+set");
            commandArray.Add("fs_homepath");
            commandArray.Add(homePath.QuoteString(true));

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
                commandArray.Add("r_noborder");
                commandArray.Add("0");
            }
            else
            {
                commandArray.Add("+set");
                commandArray.Add("r_noborder");
                commandArray.Add("1");
            }

            commandArray.Add("+set");
            commandArray.Add("fs_game");
            commandArray.Add(game);

            var lines = File.ReadAllLines(rom);
            if (lines.Length > 0)
            {
                foreach (var line in lines)
                {
                    if (!string.IsNullOrEmpty(line.Trim()))
                        commandArray.Add(line);
                }
            }

            string cfgFile = Path.Combine(_romPath, "realrtcwconfig.cfg");
            if (!File.Exists(cfgFile))
            {
                string templateFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "templates", "rtcw", "realrtcwconfig.cfg");
                if (File.Exists(templateFile))
                    try { File.Copy(templateFile, cfgFile); } catch { }
            }

            var changes = new List<Dhewm3ConfigChange>();

            if (SystemConfig.isOptSet("rtcw_resolution") && !string.IsNullOrEmpty(SystemConfig["rtcw_resolution"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_mode", SystemConfig["rtcw_resolution"]));
            else
            {
                changes.Add(new Dhewm3ConfigChange("seta", "r_mode", "-1"));
                changes.Add(new Dhewm3ConfigChange("seta", "r_customwidth", ScreenResolution.CurrentResolution.Width.ToString()));
                changes.Add(new Dhewm3ConfigChange("seta", "r_customheight", ScreenResolution.CurrentResolution.Height.ToString()));
            }

            if (_resolution != null)
            {
                changes.Add(new Dhewm3ConfigChange("seta", "r_mode", "-1"));
                changes.Add(new Dhewm3ConfigChange("seta", "r_customwidth", _resolution.Width.ToString()));
                changes.Add(new Dhewm3ConfigChange("seta", "r_customheight", _resolution.Height.ToString()));
            }

            if (!SystemConfig.isOptSet("rtcw_vsync") || SystemConfig.getOptBoolean("rtcw_vsync"))
                changes.Add(new Dhewm3ConfigChange("seta", "r_swapInterval", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_swapInterval", "0"));

            if (SystemConfig.isOptSet("rtcw_aspect") && !string.IsNullOrEmpty(SystemConfig["rtcw_aspect"]))
                changes.Add(new Dhewm3ConfigChange("seta", "cg_fixedAspect", SystemConfig["rtcw_aspect"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "cg_fixedAspect", "0"));

            if (SystemConfig.isOptSet("rtcw_showfps") && SystemConfig.getOptBoolean("rtcw_showfps"))
                changes.Add(new Dhewm3ConfigChange("seta", "cg_drawFPS", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "cg_drawFPS", "0"));

            if (!SystemConfig.isOptSet("rtcw_anisotropic") || SystemConfig.getOptBoolean("rtcw_anisotropic"))
                changes.Add(new Dhewm3ConfigChange("seta", "r_ext_texture_filter_anisotropic", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_ext_texture_filter_anisotropic", "0"));

            if (SystemConfig.isOptSet("rtcw_texturefilter") && !string.IsNullOrEmpty(SystemConfig["rtcw_texturefilter"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_textureMode", SystemConfig["rtcw_texturefilter"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_textureMode", "GL_LINEAR_MIPMAP_LINEAR"));

            if (SystemConfig.isOptSet("rtcw_texturequality") && !string.IsNullOrEmpty(SystemConfig["rtcw_texturequality"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_picmip", SystemConfig["rtcw_texturequality"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_picmip", "0"));

            if (SystemConfig.isOptSet("rtcw_subtitles") && SystemConfig.getOptBoolean("rtcw_subtitles"))
                changes.Add(new Dhewm3ConfigChange("seta", "cg_drawSubtitles", "1"));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "cg_drawSubtitles", "0"));

            if (SystemConfig.isOptSet("rtcw_lighting") && !string.IsNullOrEmpty(SystemConfig["rtcw_lighting"]))
                changes.Add(new Dhewm3ConfigChange("seta", "r_vertexLight", SystemConfig["rtcw_lighting"]));
            else
                changes.Add(new Dhewm3ConfigChange("seta", "r_vertexLight", "0"));

            ConfigureRTCWControls(changes);

            ConfigEditorDhewm3.ChangeConfigValues(cfgFile, changes);
        }
        #endregion

        #region ultraship
        // Extracts one entry of a Ship o2r archive (zip) and returns its bytes. Returns null if unavailable.
        private static byte[] ReadShipArchiveEntry(string archivePath, string entryName)
        {
            if (!File.Exists(archivePath))
                return null;

            string tmpPath = Path.Combine(Path.GetTempPath(), "rb_o2r_" + Guid.NewGuid().ToString("N"));
            try
            {
                using (var archive = Zip.Open(archivePath))
                {
                    if (archive == null)
                        return null;

                    archive.Extract(tmpPath, entryName);
                }

                string entryFile = Path.Combine(tmpPath, entryName);
                return File.Exists(entryFile) ? File.ReadAllBytes(entryFile) : null;
            }
            catch { return null; }
            finally
            {
                try { if (Directory.Exists(tmpPath)) Directory.Delete(tmpPath, true); } catch { }
            }
        }

        // Ghostship "portVersion" = 3 x uint16 big-endian (major, minor, patch), no endianness byte. Returns null if unknown.
        private static int[] GetGhostshipArchiveVersion(string archivePath)
        {
            byte[] data = ReadShipArchiveEntry(archivePath, "portVersion");
            if (data == null || data.Length < 4)
                return null;

            return new int[] { (data[0] << 8) | data[1], (data[2] << 8) | data[3] };
        }

        private void ConfigureGhostship(List<string> commandArray, string rom, string exe)
        {
            if (_emulator != "ghostship")
                return;

            // Game archive is sm64.o2r, generated in the emulator folder (portable mode)
            string gameArchive = Path.Combine(_path, "sm64.o2r");

            // Ghostship rejects an archive whose major.minor differs from the exe version: delete it so it is regenerated silently
            if (File.Exists(gameArchive))
            {
                try
                {
                    int[] archiveVersion = GetGhostshipArchiveVersion(gameArchive);
                    var exeVersion = FileVersionInfo.GetVersionInfo(exe);

                    // 0.0.x archives (dev builds) are accepted by Ghostship, keep the same rule
                    bool devArchive = archiveVersion != null && archiveVersion[0] == 0 && archiveVersion[1] == 0;

                    if (archiveVersion != null && !devArchive && exeVersion.FileMajorPart > 0 &&
                        (archiveVersion[0] != exeVersion.FileMajorPart || archiveVersion[1] != exeVersion.FileMinorPart))
                    {
                        SimpleLogger.Instance.Info("[INFO] Ghostship: sm64.o2r generated by version " + archiveVersion[0] + "." + archiveVersion[1] + ", regenerating for version " + exeVersion.FileMajorPart + "." + exeVersion.FileMinorPart);
                        File.Delete(gameArchive);
                    }
                }
                catch { }
            }

            if (!File.Exists(gameArchive))
            {
                // Ghostship extracts the ROM without any prompt when its path is passed as argument (only .z64 is supported)
                if (Path.GetExtension(rom).Equals(".z64", StringComparison.OrdinalIgnoreCase))
                    commandArray.Add("\"" + rom + "\"");
                else
                {
                    // Fallback: copy the ROM next to the exe so Ghostship's own ROM search can find it
                    string emulatorRom = Path.Combine(_path, Path.GetFileName(rom));
                    if (!File.Exists(emulatorRom))
                        try { File.Copy(rom, emulatorRom); } catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to copy game file to Ghostship folder."); }
                }
            }

            // Settings
            string settingsFile = Path.Combine(_path, "ghostship.cfg.json");

            JObject jsonObj = null;
            if (File.Exists(settingsFile))
            {
                try { jsonObj = JObject.Parse(File.ReadAllText(settingsFile)); } catch { }
            }
            if (jsonObj == null)
                jsonObj = new JObject();

            // LUS saves empty objects as null, so containers must be type-checked, not only null-checked
            JObject cvars = GetOrCreateJObject(jsonObj, "CVars");
            JObject advancedRes = GetOrCreateJObject(cvars, "gAdvancedResolution");
            JObject intScale = GetOrCreateJObject(advancedRes, "IntegerScale");
            JObject controllers = GetOrCreateJObject(cvars, "gControllers");
            JObject settings = GetOrCreateJObject(cvars, "gSettings");
            JObject window = GetOrCreateJObject(jsonObj, "Window");
            JObject backend = GetOrCreateJObject(window, "Backend");
            JObject fs = GetOrCreateJObject(window, "Fullscreen");

            // Ensure config windows are not opened
            settings.Remove("Menu");
            cvars.Remove("gOpenWindows");

            // Aspect ratio
            if (SystemConfig.isOptSet("ghostship_ratio") && !string.IsNullOrEmpty(SystemConfig["ghostship_ratio"]))
            {
                string ghostshipRatio = SystemConfig["ghostship_ratio"];
                switch (ghostshipRatio)
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

            BindFeatureInt(advancedRes, "VerticalPixelCount", "ghostship_resolution", "720");

            advancedRes["VerticalResolutionToggle"] = 1;
            cvars["gControlNav"] = 1;

            ConfigureShipControls(controllers);

            cvars["gEnableMultiViewports"] = 1;
            cvars["gInternalResolution"] = 1.0;

            if (SystemConfig.isOptSet("ghostship_fps") && !string.IsNullOrEmpty(SystemConfig["ghostship_fps"]))
            {
                int ghostshipFPS = SystemConfig["ghostship_fps"].ToIntegerString().ToInteger();
                settings["InterpolationFPS"] = ghostshipFPS;
                settings["MatchRefreshRate"] = 0;
            }
            else
            {
                settings["InterpolationFPS"] = 60;
                settings["MatchRefreshRate"] = 1;
            }

            BindFeatureSliderInt(cvars, "gMSAAValue", "ghostship_msaa", "1");

            cvars["gSdlWindowedFullscreen"] = _exclusivefs ? 0 : 1;
            BindFeatureInt(cvars, "gTextureFilter", "ghostship_texturefilter", "1");
            BindBoolFeatureOnInt(cvars, "gVsyncEnabled", "ghostship_vsync", "1", "0");
            BindFeature(window, "AudioBackend", "ghostship_audioapi", "wasapi");

            // LUS window backend IDs (Ghostship 3.x): 1 = DirectX 11, 2 = OpenGL, 4 = Vulkan (0 is no longer valid)
            string ghostshipRenderer = SystemConfig["ghostship_renderer"] ?? "";
            if (ghostshipRenderer.IndexOf("directx", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                backend["Id"] = 1;
                backend["Name"] = "DirectX 11";
            }
            else if (ghostshipRenderer.IndexOf("vulkan", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                backend["Id"] = 4;
                backend["Name"] = "Vulkan";
            }
            else
            {
                backend["Id"] = 2;
                backend["Name"] = "OpenGL";
            }

            fs["Enabled"] = _fullscreen ? true : false;

            File.WriteAllText(settingsFile, jsonObj.ToString(Formatting.Indented));
        }

        // 2Ship "portVersion" = endianness byte (1 = big, 0 = little) followed by uint16 major. Returns -1 if unknown.
        private static int GetShipArchiveMajorVersion(string archivePath)
        {
            byte[] data = ReadShipArchiveEntry(archivePath, "portVersion");
            if (data == null || data.Length < 3)
                return -1;

            return data[0] == 1 ? (data[1] << 8) | data[2] : (data[2] << 8) | data[1];
        }

        private void ConfigureShip2(List<string> commandArray, string rom, string exe)
        {
            if (_emulator != "2ship")
                return;

            _nobezels = true;

            // Game archive is mm.o2r, generated in the emulator folder (portable mode)
            string gameArchive = Path.Combine(_path, "mm.o2r");

            // An archive generated by another major version is rejected by 2Ship: delete it so it is regenerated silently
            if (File.Exists(gameArchive))
            {
                int gameMajor = GetShipArchiveMajorVersion(gameArchive);
                int portMajor = GetShipArchiveMajorVersion(Path.Combine(_path, "2ship.o2r"));

                if (gameMajor >= 0 && portMajor >= 0 && gameMajor != portMajor)
                {
                    SimpleLogger.Instance.Info("[INFO] 2Ship: mm.o2r generated by version " + gameMajor + ".x, regenerating for version " + portMajor + ".x");
                    try { File.Delete(gameArchive); } catch { }
                }
            }

            if (!File.Exists(gameArchive))
            {
                // 2Ship extracts the ROM without any prompt when its path is passed as argument (extension check is case-sensitive)
                string romExt = Path.GetExtension(rom);
                if (romExt == ".z64" || romExt == ".n64" || romExt == ".v64")
                    commandArray.Add("\"" + rom + "\"");
                else
                {
                    // Fallback: copy the ROM next to the exe so 2Ship's own ROM search can find it
                    string emulatorRom = Path.Combine(_path, Path.GetFileName(rom));
                    if (!File.Exists(emulatorRom))
                        try { File.Copy(rom, emulatorRom); } catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to copy game file to 2ship folder."); }
                }
            }

            // Settings
            string settingsFile = Path.Combine(_path, "2ship2harkinian.json");

            // Initialize JSON structure
            JObject jsonObj = null;
            if (File.Exists(settingsFile))
            {
                try { jsonObj = JObject.Parse(File.ReadAllText(settingsFile)); } catch { }
            }
            if (jsonObj == null)
                jsonObj = new JObject();

            // LUS saves empty objects as null, so containers must be type-checked, not only null-checked
            JObject cvars = GetOrCreateJObject(jsonObj, "CVars");
            JObject gsettings = GetOrCreateJObject(cvars, "gSettings");
            JObject advres = GetOrCreateJObject(gsettings, "AdvancedResolution");

            advres.Remove("IntegerScale");
            JObject intscale = new JObject();
            advres["IntegerScale"] = intscale;

            JObject controllers = GetOrCreateJObject(gsettings, "Controllers");
            JObject gwindows = GetOrCreateJObject(cvars, "gWindows");
            JObject window = GetOrCreateJObject(jsonObj, "Window");
            JObject wbackend = GetOrCreateJObject(window, "Backend");
            JObject fs = GetOrCreateJObject(window, "Fullscreen");

            // Settings
            advres["Enabled"] = 1;
            if (SystemConfig.getOptBoolean("integerscale"))
            {
                advres["PixelPerfectMode"] = 1;
                intscale["FitAutomatically"] = 1;
            }
            else
                advres["PixelPerfectMode"] = 0;
            
            if (SystemConfig.isOptSet("ship2_resolution") && !string.IsNullOrEmpty(SystemConfig["ship2_resolution"]))
            {
                string resString = SystemConfig["ship2_resolution"];
                if (int.TryParse(resString, out int res))
                    advres["VerticalPixelCount"] = res;
            }
            else
                advres["VerticalPixelCount"] = 480;

            // Aspect ratio
            if (SystemConfig.isOptSet("ship2_ratio") && !string.IsNullOrEmpty(SystemConfig["ship2_ratio"]))
            {
                string ship2Ratio = SystemConfig["ship2_ratio"];
                switch (ship2Ratio)
                {
                    case "off":
                        advres["AspectRatioX"] = 0.0;
                        advres["AspectRatioY"] = 0.0;
                        break;
                    case "native":
                        advres["AspectRatioX"] = 4.0;
                        advres["AspectRatioY"] = 3.0;
                        _nobezels = false;
                        break;
                    case "widescreen":
                        advres["AspectRatioX"] = 16.0;
                        advres["AspectRatioY"] = 9.0;
                        break;
                    case "3ds":
                        advres["AspectRatioX"] = 5.0;
                        advres["AspectRatioY"] = 3.0;
                        break;
                    case "16:10":
                        advres["AspectRatioX"] = 16.0;
                        advres["AspectRatioY"] = 10.0;
                        break;
                    case "ultrawide":
                        advres["AspectRatioX"] = 21.0;
                        advres["AspectRatioY"] = 9.0;
                        break;
                }
            }
            else
            {
                advres["AspectRatioX"] = 0.0;
                advres["AspectRatioY"] = 0.0;
            }

            advres["VerticalResolutionToggle"] = 1;
            gsettings["SdlWindowedFullscreen"] = _exclusivefs ? 0 : 1;

            BindFeatureInt(gsettings, "TextureFilter", "ship2_texturefilter", "0");
            BindBoolFeatureOnInt(gsettings, "VsyncEnabled", "vsync", "1", "0");
            BindFeatureSliderInt(gsettings, "MSAAValue", "ship2_msaa", "1");
            BindFeatureSliderInt(cvars, "gInterpolationFPS", "ship2_fps", "20");
            BindBoolFeatureInt(cvars, "gMatchRefreshRate", "ship2_matchfps", "1", "0");

            gsettings["OpenMenuBar"] = 0;
            gwindows["Menu"] = 0;

            BindFeature(window, "AudioBackend", "ship2_audiobackend", "wasapi");

            string ship2Backend = SystemConfig["ship2_backend"] ?? "";
            if (ship2Backend.IndexOf("opengl", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                wbackend["Id"] = 2;
                wbackend["Name"] = "OpenGL";
            }
            else
            {
                wbackend["Id"] = 1;
                wbackend["Name"] = "DirectX 11";
            }

            fs["Enabled"] = _fullscreen ? true : false;

            // Controllers
            ConfigureShipControls(controllers);

            File.WriteAllText(settingsFile, jsonObj.ToString(Formatting.Indented));
        }

        private void ConfigureSOH(List<string> commandArray, string rom, string exe)
        {
            if (_emulator != "soh")
                return;

            _nobezels = true;

            // Game archives are oot.o2r / oot-mq.o2r, generated in the emulator folder (portable mode)
            string vanillaArchive = Path.Combine(_path, "oot.o2r");
            string mqArchive = Path.Combine(_path, "oot-mq.o2r");

            // Archives generated by another major version are rejected by SoH: delete them so they are regenerated silently
            int portMajor = GetShipArchiveMajorVersion(Path.Combine(_path, "soh.o2r"));
            if (portMajor >= 0)
            {
                foreach (string archive in new[] { vanillaArchive, mqArchive })
                {
                    int gameMajor = GetShipArchiveMajorVersion(archive);
                    if (gameMajor >= 0 && gameMajor != portMajor)
                    {
                        SimpleLogger.Instance.Info("[INFO] SoH: " + Path.GetFileName(archive) + " generated by version " + gameMajor + ".x, regenerating for version " + portMajor + ".x");
                        try { File.Delete(archive); } catch { }
                    }
                }
            }

            if (!File.Exists(vanillaArchive) && !File.Exists(mqArchive))
            {
                // SoH extracts the ROM when its path is passed as argument (extension check is case-sensitive)
                string romExt = Path.GetExtension(rom);
                if (romExt == ".z64" || romExt == ".n64" || romExt == ".v64")
                    commandArray.Add("\"" + rom + "\"");
                else
                {
                    // Fallback: copy the ROM next to the exe so SoH's own ROM search can find it
                    string emulatorRom = Path.Combine(_path, Path.GetFileName(rom));
                    if (!File.Exists(emulatorRom))
                        try { File.Copy(rom, emulatorRom); } catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to copy game file to SOH folder."); }
                }
            }

            // Settings
            string settingsFile = Path.Combine(_path, "shipofharkinian.json");

            JObject jsonObj = null;
            if (File.Exists(settingsFile))
            {
                try { jsonObj = JObject.Parse(File.ReadAllText(settingsFile)); } catch { }
            }
            if (jsonObj == null)
                jsonObj = new JObject();

            // LUS saves empty objects as null, so containers must be type-checked, not only null-checked
            JObject cvars = GetOrCreateJObject(jsonObj, "CVars");
            JObject gsettings = GetOrCreateJObject(cvars, "gSettings");
            JObject gwindows = GetOrCreateJObject(cvars, "gOpenWindows");
            JObject advres = GetOrCreateJObject(gsettings, "AdvancedResolution");

            advres.Remove("IntegerScale");
            JObject intscale = new JObject();
            advres["IntegerScale"] = intscale;

            JObject controllers = GetOrCreateJObject(gsettings, "Controllers");
            JObject window = GetOrCreateJObject(jsonObj, "Window");
            JObject wbackend = GetOrCreateJObject(window, "Backend");
            JObject fs = GetOrCreateJObject(window, "Fullscreen");

            gwindows["Menu"] = 0;
            gsettings["ControlNav"] = 1;

            // Settings
            advres["Enabled"] = 1;
            if (SystemConfig.getOptBoolean("integerscale"))
            {
                advres["PixelPerfectMode"] = 1;
                intscale["FitAutomatically"] = 1;
            }
            else
                advres["PixelPerfectMode"] = 0;

            if (SystemConfig.isOptSet("soh_resolution") && !string.IsNullOrEmpty(SystemConfig["soh_resolution"]))
            {
                string resString = SystemConfig["soh_resolution"];
                if (int.TryParse(resString, out int res))
                    advres["VerticalPixelCount"] = res;
            }
            else
                advres["VerticalPixelCount"] = 480;

            // Aspect ratio
            if (advres["UIComboItem"] == null)
                advres["UIComboItem"] = new JObject();

            // Aspect ratio
            GetOrCreateJObject(advres, "UIComboItem")["AspectRatio"] = 1;

            if (SystemConfig.isOptSet("soh_Ratio") && !string.IsNullOrEmpty(SystemConfig["soh_Ratio"]))
            {
                string sohRatio = SystemConfig["soh_Ratio"];
                switch (sohRatio)
                {
                    case "off":
                        advres["AspectRatioX"] = 0.0;
                        advres["AspectRatioY"] = 0.0;
                        break;
                    case "native":
                        advres["AspectRatioX"] = 4.0;
                        advres["AspectRatioY"] = 3.0;
                        _nobezels = false;
                        break;
                    case "widescreen":
                        advres["AspectRatioX"] = 16.0;
                        advres["AspectRatioY"] = 9.0;
                        break;
                    case "3ds":
                        advres["AspectRatioX"] = 5.0;
                        advres["AspectRatioY"] = 3.0;
                        break;
                    case "16:10":
                        advres["AspectRatioX"] = 16.0;
                        advres["AspectRatioY"] = 10.0;
                        break;
                    case "ultrawide":
                        advres["AspectRatioX"] = 21.0;
                        advres["AspectRatioY"] = 9.0;
                        break;
                }
            }
            else
            {
                advres["AspectRatioX"] = 0.0;
                advres["AspectRatioY"] = 0.0;
            }

            advres["VerticalResolutionToggle"] = 1;
            gsettings["SdlWindowedFullscreen"] = _exclusivefs ? 0 : 1;

            BindFeatureInt(gsettings, "TextureFilter", "soh_texturefilter", "0");
            BindBoolFeatureOnInt(gsettings, "VsyncEnabled", "vsync", "1", "0");
            BindFeatureSliderInt(gsettings, "MSAAValue", "soh_msaa", "1");
            BindFeatureSliderInt(gsettings, "InterpolationFPS", "soh_fps", "20");
            BindBoolFeatureInt(gsettings, "MatchRefreshRate", "soh_matchfps", "1", "0");

            BindFeature(window, "AudioBackend", "soh_audiobackend", "wasapi");

            if (SystemConfig.isOptSet("soh_backend") && !string.IsNullOrEmpty(SystemConfig["soh_backend"]))
            {
                string[] backends = SystemConfig["soh_backend"].Split('_');
                if (backends.Length > 1)
                {
                    int backendID = backends[0].ToInteger();
                    string backendName = backends[1];

                    wbackend["Id"] = backendID;
                    wbackend["Name"] = backendName;
                }

                else
                {
                    wbackend["Id"] = 0;
                    wbackend["Name"] = "DirectX";
                }
            }
            else
            {
                wbackend["Id"] = 0;
                wbackend["Name"] = "DirectX";
            }

            fs["Enabled"] = _fullscreen ? true : false;

            // Language
            BindFeatureInt(gsettings, "Languages", "soh_language", "0");

            ConfigureShipControls(controllers);

            File.WriteAllText(settingsFile, jsonObj.ToString(Formatting.Indented));
        }

        private void ConfigureStarship(string rom, string exe)
        {
            if (_emulator != "starship")
                return;

            var otrFiles = Directory.GetFiles(_path, "*.o2r");
            var gameOtrFiles = otrFiles.Where(file => !file.EndsWith("starship.o2r", StringComparison.OrdinalIgnoreCase));

            if (!gameOtrFiles.Any())
            {
                string emulatorRom = Path.Combine(_path, Path.GetFileName(rom));
                try { File.Copy(rom, emulatorRom); } catch { SimpleLogger.Instance.Warning("[WARNING] Impossible to copy game file to Starship folder."); }
            }

            // Settings
            JObject jsonObj;
            JObject cvars;
            JObject advancedRes;
            JObject intScale;
            JObject controllers;
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

            if (cvars["gControllers"] == null)
            {
                controllers = new JObject();
                cvars["gControllers"] = controllers;
            }
            else
                controllers = (JObject)cvars["gControllers"];

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

            // Ensure config windows are not opened
            cvars["gAdvancedResolutionEditorEnabled"] = 0;
            cvars["gAudioChannelsSetting"] = 0;
            cvars["gConsoleEnabled"] = 0;
            cvars["gControllerConfigurationEnabled"] = 0;

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
                int starshipFPS = SystemConfig["starship_fps"].ToIntegerString().ToInteger();
                cvars["gInterpolationFPS"] = starshipFPS;
                cvars["gMatchRefreshRate"] = 0;
            }
            else
            {
                cvars["gInterpolationFPS"] = 60;
                cvars["gMatchRefreshRate"] = 1;
            }

            BindFeatureSliderInt(cvars, "gMSAAValue", "starship_msaa", "1");
            cvars["gOpenMenuBar"] = 0;
            cvars["gSdlWindowedFullscreen"] = _exclusivefs ? 0 : 1;
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

        #region sonic
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

            if (Program.IsMonitorIndexSet)
                settings["DisplayIndex"] = Displays.IndexOf(Program.TargetScreen, MonitorOrder.SdlLike).ToString();

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
                    ini.WriteValue("Video", "exclusiveFS", _exclusivefs ? "y" : "n");
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
        #endregion

        #region quake
        private void ConfigurevkQuake(List<string> commandArray, string rom)
        {
            if (_emulator != "vkquake")
                return;

            // Base folder = folder holding id1, game folder = first folder below it in the rom path (id1, hipnotic, rogue, dopa, mods...)
            string rompath = QuakeGameFolders.GetBaseDir(rom, "id1", "quake");
            string game = QuakeGameFolders.GetGameDir(rom, rompath, "id1");

            // An explicit -basedir disables the Steam/GOG/Epic detection and the user folder: everything is written in the roms folder
            commandArray.Add("-basedir");
            commandArray.Add("\"" + rompath + "\"");

            if (game != null)
            {
                string lowerGame = game.ToLowerInvariant();

                // Mission packs have their own switch (status bar), any other folder is loaded as a mod
                if (lowerGame == "hipnotic" || lowerGame == "rogue" || lowerGame == "quoth")
                    commandArray.Add("-" + lowerGame);
                else
                {
                    commandArray.Add("-game");
                    commandArray.Add("\"" + game + "\"");
                }
            }

            // Default: borderless desktop fullscreen (no display mode change)
            // Exclusive fullscreen: Vulkan exclusive mode (vid_fullscreen 2) with a real display mode
            // Forced video mode: real display mode at the chosen resolution
            bool desktopFullscreen = _fullscreen && !_exclusivefs && _resolution == null;
            commandArray.Add(_fullscreen ? "-fullscreen" : "-window");

            // Only "+" commands from here: stuffcmds stops at the next "-" switch, and a "+" value cannot be negative.
            // Settings not needed at video startup are in retrobat.cfg, executed after vkQuake.cfg (quake.rc runs stuffcmds after the config files)
            commandArray.Add("+exec");
            commandArray.Add("retrobat.cfg");

            // Video settings are read on the command line before the window is created (CFG_ReadCvarOverrides), whatever the config layout
            // (single vkQuake.cfg up to 1.35, global <basedir>\vkQuake.cfg + per game vkQuake.cfg since 1.36)
            AddQuakeCvar(commandArray, "vid_fullscreen", !_fullscreen ? "0" : (_exclusivefs ? "2" : "1"));
            AddQuakeCvar(commandArray, "vid_desktopfullscreen", desktopFullscreen ? "1" : "0");
            AddQuakeCvar(commandArray, "vid_borderless", "0");

            ScreenResolution res = _resolution;
            if (res == null && _fullscreen && !desktopFullscreen)
                res = ScreenResolution.CurrentResolution;

            if (res != null)
            {
                AddQuakeCvar(commandArray, "vid_width", res.Width.ToString());
                AddQuakeCvar(commandArray, "vid_height", res.Height.ToString());

                if (res.DisplayFrequency > 1)
                    AddQuakeCvar(commandArray, "vid_refreshrate", res.DisplayFrequency.ToString());
            }

            if (SystemConfig.isOptSet("vkquake_vsync") && !string.IsNullOrEmpty(SystemConfig["vkquake_vsync"]))
                AddQuakeCvar(commandArray, "vid_vsync", SystemConfig["vkquake_vsync"]);
            else
                AddQuakeCvar(commandArray, "vid_vsync", "1");

            if (SystemConfig.isOptSet("vkquake_msaa") && !string.IsNullOrEmpty(SystemConfig["vkquake_msaa"]))
                AddQuakeCvar(commandArray, "vid_fsaa", SystemConfig["vkquake_msaa"]);
            else
                AddQuakeCvar(commandArray, "vid_fsaa", "0");

            // vkQuake rebuilds its command line from all the arguments and cuts it at 255 characters (CMDLINE_LENGTH)
            int cmdLength = string.Join(" ", commandArray).Replace("\"", "").Length;
            if (cmdLength > 255)
                SimpleLogger.Instance.Warning("[WARNING] vkQuake: command line is " + cmdLength + " characters long (limit 255), the last settings may be ignored. Use a shorter roms path.");

            // Settings applied after the config files
            string filter = SystemConfig.isOptSet("vkquake_filter") && !string.IsNullOrEmpty(SystemConfig["vkquake_filter"]) ? SystemConfig["vkquake_filter"] : "0";

            var retrobatCfg = new List<string>
            {
                "// Generated by RetroBat at each launch, changes are overwritten",
                "vid_filter \"" + filter + "\"",
                "scr_showfps \"" + (SystemConfig.getOptBoolean("vkquake_showfps") ? "1" : "0") + "\"",
                "joy_invert \"" + (SystemConfig.getOptBoolean("vkquake_invertlook") ? "1" : "0") + "\"",

                // Keyboard keys sent by RetroBat hotkeys (es_padtokey.cfg): F2 quicksave, F4 quickload, F8 screenshot
                "bind \"F2\" \"save quick\"",
                "bind \"F4\" \"load quick\"",
                "bind \"F8\" \"screenshot\""
            };

            // id1 is always in the search path, mods included
            string retrobatCfgFile = Path.Combine(rompath, "id1", "retrobat.cfg");
            try { File.WriteAllLines(retrobatCfgFile, retrobatCfg); }
            catch (Exception ex) { SimpleLogger.Instance.Warning("[WARNING] vkQuake: unable to write " + retrobatCfgFile + " : " + ex.Message); }

            ConfigurevkQuakeControls();
        }

        private void ConfigurevkQuake2(List<string> commandArray, string rom)
        {
            if (_emulator != "vkquake2")
                return;

            // Base folder = folder holding baseq2, game folder = first folder below it in the rom path (baseq2, rogue, xatrix, zaero, smd, ctf, mods...)
            string rompath = QuakeGameFolders.GetBaseDir(rom, "baseq2", "quake2");
            string game = QuakeGameFolders.GetGameDir(rom, rompath, "baseq2");

            // Quake 2 splits its command line on spaces and ignores quotes (ParseCommandLine in win32/sys_win.c): "+set basedir" breaks when the path contains a space.
            // basedir defaults to "." (working directory), ref_vk.dll is still loaded from the executable folder.
            _workingPath = rompath;

            // The game library is only searched in <basedir>\<game> (Sys_GetGameAPI)
            CopyQuake2GameLibrary("baseq2", rompath);

            if (game != null)
            {
                CopyQuake2GameLibrary(game, rompath);
                commandArray.Add("+set game " + game);
            }

            // Quake 2 executes the first config.cfg found in <game> then baseq2 (and writes it in <game> on exit):
            // do not create an almost empty <game>\config.cfg that would hide the baseq2 bindings
            string vkquake2cfg = Path.Combine(rompath, "baseq2", "config.cfg");
            if (game != null && File.Exists(Path.Combine(rompath, game, "config.cfg")))
                vkquake2cfg = Path.Combine(rompath, game, "config.cfg");

            var cfg = new QuakeConfig(vkquake2cfg);

            // Fullscreen always changes the display mode (ChangeDisplaySettingsEx) of the monitor holding the window:
            // use the target screen resolution unless a video mode is forced. Windowed mode keeps its size unless a video mode is forced.
            ScreenResolution res = _resolution;
            if (res == null && _fullscreen)
            {
                if (Program.IsMonitorIndexSet && Program.TargetScreen != null)
                    res = ScreenResolution.GetCurrentResolution(Program.TargetScreen.DeviceName);
                if (res == null)
                    res = ScreenResolution.CurrentResolution;
            }

            if (res != null)
            {
                cfg["set vk_mode"] = "-1";
                cfg["set r_customwidth"] = res.Width.ToString();
                cfg["set r_customheight"] = res.Height.ToString();
            }

            // The window position selects the monitor used in fullscreen (MonitorFromWindow), upstream default position otherwise
            if (Program.IsMonitorIndexSet && Program.TargetScreen != null)
            {
                cfg["set vid_xpos"] = Program.TargetScreen.Bounds.X.ToString();
                cfg["set vid_ypos"] = Program.TargetScreen.Bounds.Y.ToString();
            }
            else
            {
                cfg["set vid_xpos"] = "3";
                cfg["set vid_ypos"] = "22";
            }

            if (SystemConfig.isOptSet("vkquake2_vsync") && SystemConfig.getOptBoolean("vkquake2_vsync"))
                cfg["set vk_vsync"] = "1";
            else
                cfg["set vk_vsync"] = "0";

            if (_fullscreen)
                cfg["set vid_fullscreen"] = "1";
            else
                cfg["set vid_fullscreen"] = "0";

            if (_exclusivefs)
                cfg["set vk_fullscreen_exclusive"] = "1";
            else
                cfg["set vk_fullscreen_exclusive"] = "0";

            // s_khz 44 is not usable (snd_win.c falls back to 11 kHz), only 11 and 22
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

            // Keyboard keys sent by RetroBat hotkeys (es_padtokey.cfg): F2 quicksave, F4 quickload, F8 screenshot
            cfg["bind F2"] = "save quick";
            cfg["bind F4"] = "load quick";
            cfg["bind F8"] = "screenshot";

            cfg.Save();
        }

        // Quake 2 loads the game library from <basedir>\<game> only: copy the one shipped with the emulator when it is missing in the roms folder
        private void CopyQuake2GameLibrary(string game, string basePath)
        {
            foreach (string dll in new[] { "gamex64.dll", "gamex86.dll" })
            {
                string source = Path.Combine(_path, game, dll);
                string target = Path.Combine(basePath, game, dll);

                if (!File.Exists(source) || File.Exists(target))
                    continue;

                try
                {
                    Directory.CreateDirectory(Path.Combine(basePath, game));
                    File.Copy(source, target);
                    SimpleLogger.Instance.Info("[INFO] vkQuake2: " + game + "\\" + dll + " copied to the roms folder.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[WARNING] vkQuake2: unable to copy " + source + " : " + ex.Message);
                }
            }
        }

        // Quake engines: the base folder is the one holding the base game folder (id1, baseq2), searched from the rom path
        private static string GetQuakeBaseDir(string rom, string baseGame, string systemFolder)
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(rom));
                while (!string.IsNullOrEmpty(dir))
                {
                    if (Directory.Exists(Path.Combine(dir, baseGame)))
                        return dir;

                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }

            return Path.Combine(Program.AppConfig.GetFullPath("roms"), systemFolder);
        }

        // Quake engines: the game folder is the first folder of the rom path below the base folder.
        // A game file placed in the base folder (.quake, .quake2) gives the folder on its first line, otherwise its file name is used.
        // Returns null for the base game.
        private static string GetQuakeGameDir(string rom, string baseDir, string baseGame)
        {
            string game = null;

            try
            {
                string romDir = Path.GetDirectoryName(Path.GetFullPath(rom)).TrimEnd('\\') + "\\";
                string root = Path.GetFullPath(baseDir).TrimEnd('\\') + "\\";

                if (romDir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    if (romDir.Length > root.Length)
                        game = romDir.Substring(root.Length).Split('\\')[0];
                    else if (!Path.GetExtension(rom).Equals(".pak", StringComparison.OrdinalIgnoreCase))
                    {
                        string line = null;
                        if (new FileInfo(rom).Length < 4096)
                            line = File.ReadAllLines(rom).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

                        string fileName = Path.GetFileNameWithoutExtension(rom);

                        if (!string.IsNullOrEmpty(line) && line.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Directory.Exists(Path.Combine(root, line)))
                            game = line;
                        else if (Directory.Exists(Path.Combine(root, fileName)))
                            game = fileName;
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(game) || game.Equals(baseGame, StringComparison.OrdinalIgnoreCase))
                return null;

            return game;
        }

        // Quake engines: "+cvar value" on the command line
        private static void AddQuakeCvar(List<string> commandArray, string cvar, string value)
        {
            commandArray.Add("+" + cvar);
            commandArray.Add(value);
        }
        #endregion

        #region xash3d
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
            
            if (_fullscreen && _exclusivefs)
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

        #endregion

        #region tools
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
        private static JObject GetOrCreateJObject(JObject parent, string key)
        {
            JObject obj = parent[key] as JObject;
            if (obj == null)
            {
                obj = new JObject();
                parent[key] = obj;
            }
            return obj;
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

    class ConfigEditorDhewm3
    {
        public static void ChangeConfigValues(string filePath, List<Dhewm3ConfigChange> changes, bool createIfMissing = false)
        {
            if (string.IsNullOrEmpty(filePath) || changes == null || changes.Count == 0)
                return;

            if (!File.Exists(filePath))
            {
                if (!createIfMissing)
                    return;

                try
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllText(filePath, string.Empty);
                }
                catch { return; }
            }

            // Read all lines from the file
            List<string> lines = File.ReadAllLines(filePath).ToList();
            bool[] foundFlags = new bool[changes.Count];

            // Make changes in memory
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = 0; j < changes.Count; j++)
                {
                    // Key can be quoted or not (dhewm3 writes: bind "KEY" "value"), cvar and key names are case insensitive
                    string pattern = $@"^\s*{Regex.Escape(changes[j].Type)}\s+""?{Regex.Escape(changes[j].Key)}""?\s+";
                    if (Regex.IsMatch(lines[i], pattern, RegexOptions.IgnoreCase))
                    {
                        lines[i] = FormatLine(changes[j]);
                        foundFlags[j] = true;
                    }
                }
            }

            // Append missing keys
            for (int j = 0; j < changes.Count; j++)
            {
                if (!foundFlags[j])
                    lines.Add(FormatLine(changes[j]));
            }

            // Write all updated lines back to the file
            File.WriteAllLines(filePath, lines);
        }

        private static string FormatLine(Dhewm3ConfigChange change)
        {
            if (change.Type == "bind")
                return $"bind \"{change.Key}\" \"{change.NewValue}\"";

            return $"{change.Type} {change.Key} \"{change.NewValue}\"";
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
            // The value is the last quoted string of the line (bind lines may also quote the key: bind "F2" "value")
            int quoteEnd = raw.LastIndexOf('"');
            if (quoteEnd <= 0)
                return raw; // cannot parse, leave unchanged

            int quoteStart = raw.LastIndexOf('"', quoteEnd - 1);
            if (quoteStart < 0)
                return raw;

            return raw.Substring(0, quoteStart + 1) + newValue + raw.Substring(quoteEnd);
        }

        #endregion
    }
    #endregion

    #region Quake game folders
    /// <summary>
    /// Game folders shared by vkQuake / vkQuake2 and the libretro cores tyrquake / vitaquake2:
    /// roms\quake\[game]\ and roms\quake2\[game]\, the base game being id1 / baseq2.
    /// </summary>
    static class QuakeGameFolders
    {
        /// <summary>Base folder = folder holding the base game folder (id1, baseq2), searched from the rom path.</summary>
        public static string GetBaseDir(string rom, string baseGame, string systemFolder)
        {
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(rom));
                while (!string.IsNullOrEmpty(dir))
                {
                    if (Directory.Exists(Path.Combine(dir, baseGame)))
                        return dir;

                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { }

            return Path.Combine(Program.AppConfig.GetFullPath("roms"), systemFolder);
        }

        /// <summary>
        /// Game folder of a rom, null for the base game.
        /// Game data file: first folder below the base folder.
        /// Launch file (.quake / .quake2): folder name on its first line, otherwise its file name, otherwise the historic keywords of its file name.
        /// </summary>
        public static string GetGameDir(string rom, string baseDir, string baseGame)
        {
            string game = null;

            try
            {
                string ext = Path.GetExtension(rom).ToLowerInvariant();
                string root = Path.GetFullPath(baseDir).TrimEnd('\\') + "\\";

                if (ext == ".quake" || ext == ".quake2")
                {
                    string line = null;
                    if (new FileInfo(rom).Length < 4096)
                        line = File.ReadAllLines(rom).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

                    string fileName = Path.GetFileNameWithoutExtension(rom);

                    if (!string.IsNullOrEmpty(line) && line.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && Directory.Exists(Path.Combine(root, line)))
                        game = line;
                    else if (Directory.Exists(Path.Combine(root, fileName)))
                        game = fileName;
                    else
                        game = GetLegacyGameDir(fileName, baseGame);
                }
                else
                {
                    string romDir = Path.GetDirectoryName(Path.GetFullPath(rom)).TrimEnd('\\') + "\\";
                    if (romDir.StartsWith(root, StringComparison.OrdinalIgnoreCase) && romDir.Length > root.Length)
                        game = romDir.Substring(root.Length).Split('\\')[0];
                }
            }
            catch { }

            if (string.IsNullOrEmpty(game) || game.Equals(baseGame, StringComparison.OrdinalIgnoreCase))
                return null;

            return game;
        }

        /// <summary>
        /// File given to the libretro cores, which only use its folder: pak0.pak, otherwise the first pak, otherwise any file (mod without pak).
        /// </summary>
        public static string GetContentFile(string baseDir, string game, string baseGame)
        {
            string dir = Path.Combine(baseDir, game ?? baseGame);
            string pak0 = Path.Combine(dir, "pak0.pak");

            try
            {
                if (!File.Exists(pak0) && Directory.Exists(dir))
                {
                    string file = Directory.GetFiles(dir, "*.pak").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                        ?? Directory.GetFiles(dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

                    if (file != null)
                        return file;
                }
            }
            catch { }

            return pak0;
        }

        // Keywords of the launch files used by previous RetroBat versions
        private static string GetLegacyGameDir(string fileName, string baseGame)
        {
            string name = fileName.ToLowerInvariant();

            if (baseGame == "id1")
            {
                if (name.Contains("scourge") || name.Contains("hipnotic"))
                    return "hipnotic";
                if (name.Contains("dissolution") || name.Contains("rogue"))
                    return "rogue";
            }
            else if (baseGame == "baseq2")
            {
                if (name.Contains("rogue"))
                    return "rogue";
                if (name.Contains("xatrix"))
                    return "xatrix";
                if (name.Contains("zaero"))
                    return "zaero";
            }

            return null;
        }
    }
    #endregion
}
