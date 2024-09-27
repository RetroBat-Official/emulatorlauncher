using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private void ConfigurePort(List<string> commandArray, string rom, string exe)
        {
            ConfigureOpenGoal(commandArray, rom);
            ConfigureSonic3air(rom, exe);
            ConfigureSonicMania(rom, exe);
            ConfigureSonicRetro(rom, exe);
        }

        #region ports
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
            BindFeature(settings, "ControllerRumblePlayer1", "sonic3_rumble", "0.0");
            BindFeature(settings, "ControllerRumblePlayer2", "sonic3_rumble", "0.0");

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
            BindFeature(settings, "Scanlines", "sonic3_scanlines", "0");

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
                BindBoolIniFeature(ini, "Video", "vsync", "sonicmania_vsync", "n", "y");
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
                BindBoolIniFeature(ini, "Window", "VSync", "sonicretro_vsync", "true", "false");
                BindIniFeature(ini, "Window", "ScalingMode", "sonicretro_scaling", "0");

                if (sonicCD)
                    ini.WriteValue("Game", "Platform", "0");

                ini.Save();
            }
        }
        #endregion
    }
}
