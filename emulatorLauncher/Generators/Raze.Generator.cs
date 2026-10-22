using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    class RazeGenerator : Generator
    {
        public RazeGenerator()
        {
            // Raze fullscreen is a borderless window covering the monitor: the videomode option has to change the desktop resolution
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;


        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("raze");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "raze.exe");

            if (!File.Exists(exe))
                return null;

            bool fullscreen = ShouldRunFullscreen();
            _resolution = resolution;

            // Bezels need a 4:3 picture: only when the game aspect ratio is automatic or 4:3
            bool ratioAllowsBezel = !SystemConfig.isOptSet("raze_ratio") || string.IsNullOrEmpty(SystemConfig["raze_ratio"]) || SystemConfig["raze_ratio"] == "3";
            if (fullscreen && ratioAllowsBezel)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            string configFile = Path.Combine(path, "raze_portable.ini");
            string romPath = Path.GetDirectoryName(rom);

            // Folders where Raze looks for the game files (GameSearch.Directories)
            var gameDirs = new List<string>() { romPath };

            var commandArray = new List<string>
            {
                "-config",
                "\"" + configFile + "\""
            };

            if (Path.GetExtension(rom).ToLower() == ".eduke32" && File.Exists(rom))
            {
                // EDuke32 launch file : Raze accepts the same parameters (-gamegrp, -g, -j, -x, -mx, -h, -mh, -map)
                var edukeArgs = Eduke32Arg.ParseConfig(rom, romPath, out string gamegrp, out _);

                foreach (var arg in edukeArgs)
                {
                    if (!arg.StartsWith("-") && arg.Contains(" "))
                        commandArray.Add("\"" + arg + "\"");
                    else
                        commandArray.Add(arg);
                }

                // Raze only starts a game file found in GameSearch.Directories (-gamegrp is matched against them)
                if (!string.IsNullOrEmpty(gamegrp) && Path.IsPathRooted(gamegrp))
                {
                    string grpDir = Path.GetDirectoryName(gamegrp);
                    if (!string.IsNullOrEmpty(grpDir) && !gameDirs.Contains(grpDir, StringComparer.OrdinalIgnoreCase))
                        gameDirs.Add(grpDir);
                }
            }
            else if (Path.GetExtension(rom).ToLower() == ".raze" && File.Exists(rom))
            {
                var lines = File.ReadAllLines(rom);

                if (lines.Length == 0)
                    throw new ApplicationException("raze file does not contain any launch argument.");

                foreach (var line in lines)
                {
                    string value = line.Trim();
                    if (string.IsNullOrEmpty(value))
                        continue;

                    if (lines.Length == 1)
                    {
                        value = value.Replace("/", "\\");
                        string filePath = Path.Combine(Path.GetDirectoryName(rom), value.TrimStart('\\'));

                        commandArray.Add("-iwad");
                        commandArray.Add("\"" + filePath + "\"");
                    }

                    else if (value.StartsWith("\\") || value.StartsWith("/"))
                    {
                        value = value.Replace("/", "\\");
                        string filePath = Path.Combine(Path.GetDirectoryName(rom), value.TrimStart('\\'));

                        commandArray.Add("\"" + filePath + "\"");
                    }
                    // Absolute path (C:\...) must be quoted
                    else if (value.Length > 2 && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))
                        commandArray.Add("\"" + value.Replace("/", "\\") + "\"");
                    else
                        commandArray.Add(value);
                }
            }

            // Console commands, executed by Raze after the configuration file is loaded (must stay after all the '-' parameters)
            if (!SystemConfig.getOptBoolean("disableautocontrollers"))
            {
                // Back/Select is the RetroBat hotkey button, by default it opens the main menu in Raze: move the menu to Start
                commandArray.Add("+unbind pad_back");
                commandArray.Add("+bind pad_start menu_main");
            }

            // Keyboard keys used by RetroBat hotkeys (es_padtokey.cfg): F2 quicksave, F4 quickload, F8 screenshot
            // Raze defaults of these keys are swapped with F6, F9 and F12
            commandArray.Add("+bind F2 quicksave");
            commandArray.Add("+bind F4 quickload");
            commandArray.Add("+bind F8 screenshot");
            commandArray.Add("+bind F6 opensavemenu");
            commandArray.Add("+bind F9 \"openmenu SoundOptions\"");
            commandArray.Add("+bind F12 togglemessages");

            if (SystemConfig.getOptBoolean("raze_showfps"))
                commandArray.Add("+vid_fps 1");

            SetupConfiguration(configFile, gameDirs, fullscreen, _bezelFileInfo != null);

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        //Setup configuration file
        private void SetupConfiguration(string configFile, List<string> gameDirs, bool fullscreen, bool useBezel)
        {
            if (!File.Exists(configFile))
                File.WriteAllText(configFile, "");

            try
            {
                // Raze uses duplicate keys (Path= in search directories, Name=/Command= in aliases), they must be kept
                using (var ini = IniFile.FromFile(configFile, IniOptions.KeepEmptyLines | IniOptions.AllowDuplicateValues))
                {
                    // "folder/*" only adds the subfolders, the folder itself must be added too
                    ini.Remove("GameSearch.Directories", "Path");
                    foreach (string dir in gameDirs)
                    {
                        string gamePath = dir.Replace("\\", "/").TrimEnd('/');
                        ini.AppendValue("GameSearch.Directories", "Path", gamePath);
                        ini.AppendValue("GameSearch.Directories", "Path", gamePath + "/*");
                    }

                    if (fullscreen)
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "true");
                    else
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "false");

                    // Screen : vid_adapter (0 = primary screen, N = Nth monitor in EnumDisplayMonitors order, 1-based)
                    // Only when the user chose a screen, otherwise Raze keeps its own choice
                    if (Program.IsMonitorIndexSet)
                    {
                        var screen = Program.TargetScreen;
                        int adapter = screen.Primary ? 0 : Displays.IndexOf(screen, MonitorOrder.EnumDisplayMonitors) + 1;
                        ini.WriteValue("GlobalSettings", "vid_adapter", Math.Max(0, adapter).ToString());
                    }

                    BindIniFeature(ini, "GlobalSettings", "vid_preferbackend", "raze_renderer", "0");
                    // Bezel : force a 4:3 picture with black borders, the bezel covers the borders
                    if (useBezel)
                        ini.WriteValue("GlobalSettings", "vid_aspect", "3");
                    else
                        BindIniFeature(ini, "GlobalSettings", "vid_aspect", "raze_ratio", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter", "raze_texture_filter", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter_anisotropic", "raze_anisotropic_filtering", "1");
                    BindIniFeature(ini, "GlobalSettings", "vid_scalemode", "raze_scalemode", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_multisample", "raze_msaa", "1");
                    BindIniFeature(ini, "GlobalSettings", "gl_fxaa", "raze_fxaa", "0");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_vsync", "raze_vsync", "true", "false");

                    // vid_cropaspect : false = stretch, true = letterbox
                    if (useBezel)
                        ini.WriteValue("GlobalSettings", "vid_cropaspect", "true");
                    else
                        BindBoolIniFeature(ini, "GlobalSettings", "vid_cropaspect", "raze_cropaspect", "false", "true");

                    BindIniFeature(ini, "GlobalSettings", "snd_mididevice", "raze_mididevice", "-5");

                    // Quicksave / quickload hotkeys must act immediately, without a confirmation box
                    ini.WriteValue("GlobalSettings", "saveloadconfirmation", "false");

                    // Raze language follows RetroBat language
                    ini.WriteValue("GlobalSettings", "language", GetRazeLanguage());

                    // Controllers : joystick input is disabled by default in Raze
                    if (!SystemConfig.getOptBoolean("disableautocontrollers"))
                    {
                        ini.WriteValue("GlobalSettings", "use_joystick", "true");

                        // Stick dead zone : one config section per device
                        //  - XInput : "Joy:XI:<slot>", axes 0 to 3 = left X/Y, right X/Y
                        //  - DirectInput : "Joy:DI:{instance guid}", axis order depends on the device, so the first 8 axes are set (unmapped axes are not affected)
                        string deadZone = SystemConfig["raze_deadzone"];

                        var sections = new List<KeyValuePair<string, int>>();
                        for (int slot = 0; slot < 4; slot++)
                            sections.Add(new KeyValuePair<string, int>("Joy:XI:" + slot, 4));

                        foreach (var ctrl in this.Controllers.Where(c => !c.IsKeyboard && c.DirectInput != null && c.DirectInput.InstanceGuid != Guid.Empty))
                            sections.Add(new KeyValuePair<string, int>("Joy:DI:" + ctrl.DirectInput.InstanceGuid.ToString("B"), 8));

                        foreach (var section in sections)
                        {
                            for (int axis = 0; axis < section.Value; axis++)
                            {
                                if (string.IsNullOrEmpty(deadZone))
                                    ini.Remove(section.Key, "Axis" + axis + "deadzone");
                                else
                                    ini.WriteValue(section.Key, "Axis" + axis + "deadzone", deadZone);
                            }
                        }
                    }

                    // hdr
                    if (SystemConfig["raze_renderer"] == "1" && SystemConfig.getOptBoolean("enable_hdr"))
                    {
                        ini.WriteValue("GlobalSettings", "vid_hdr", "false");
                        ini.WriteValue("GlobalSettings", "vk_hdr", "true");
                    }
                    else if (SystemConfig.getOptBoolean("enable_hdr"))
                    {
                        ini.WriteValue("GlobalSettings", "vid_hdr", "true");
                        ini.WriteValue("GlobalSettings", "vk_hdr", "false");
                    }
                    else
                    {
                        ini.WriteValue("GlobalSettings", "vid_hdr", "false");
                        ini.WriteValue("GlobalSettings", "vk_hdr", "false");
                    }

                    // Per game cvars (<Game>.ConsoleVariables)
                    // crosshairs
                    if (SystemConfig.isOptSet("raze_crosshair") && !string.IsNullOrEmpty(SystemConfig["raze_crosshair"]) && SystemConfig["raze_crosshair"] != "off")
                    {
                        WriteGameValue(ini, "crosshair", SystemConfig["raze_crosshair"]);
                        WriteGameValue(ini, "cl_crosshair", "true");
                    }
                    else if (SystemConfig["raze_crosshair"] == "off")
                    {
                        WriteGameValue(ini, "crosshair", "0");
                        WriteGameValue(ini, "cl_crosshair", "false");
                    }
                    else
                    {
                        WriteGameValue(ini, "crosshair", "0");
                        WriteGameValue(ini, "cl_crosshair", "true");
                    }

                    if (SystemConfig.isOptSet("raze_alwaysrun") && !SystemConfig.getOptBoolean("raze_alwaysrun"))
                        WriteGameValue(ini, "cl_autorun", "false");
                    else
                        WriteGameValue(ini, "cl_autorun", "true");

                    string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "raze");
                    if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                        catch { }
                    WriteGameValue(ini, "screenshot_dir", screenshotPath.Replace("\\", "/"));

                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), "raze");
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }
                    ini.WriteValue("GlobalSettings", "save_dir", savePath.Replace("\\", "/"));
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[RAZE] Unable to write raze_portable.ini : " + ex.Message);
            }
        }

        // Write a per game cvar in every game section
        private static void WriteGameValue(IniFile ini, string key, string value)
        {
            foreach (string game in gameNames)
                ini.WriteValue(game + ".ConsoleVariables", key, value);
        }

        private static string GetRazeLanguage()
        {
            string lang = GetCurrentLanguage();
            if (string.IsNullOrEmpty(lang))
                return "auto";

            if (lang == "en")
                return "default";

            if (razeLanguages.Contains(lang))
                return lang;

            return "auto";
        }

        // Languages available in the Raze menu (language codes from wadsrc/static/menudef.txt)
        private static readonly List<string> razeLanguages = new List<string>() { "cs", "da", "de", "es", "fr", "hu", "it", "nl", "no", "pl", "pt", "ro", "ru", "tr" };

        // Section prefix used by Raze : group gamefilter truncated at the first dot (wadsrc/static/engine/grpinfo.txt)
        private readonly static List<string> gameNames = new List<string>()
        {
            "Blood",
            "Duke",
            "Exhumed",
            "Nam",
            "Redneck",
            "ShadowWarrior",
            "WW2GI"
        };

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            return ret;
        }
    }
}