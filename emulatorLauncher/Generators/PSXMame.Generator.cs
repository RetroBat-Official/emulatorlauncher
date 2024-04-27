using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class PSXMameGenerator : Generator
    {
        public PSXMameGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private bool _fullscreen;
        private string _gameName;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("psxmame");

            string exe = Path.Combine(path, "mame.exe");
            if (!File.Exists(exe))
                return null;

            _gameName = Path.GetFileNameWithoutExtension(rom);
            _resolution = resolution;
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string args;

            List<string> commandArray = new List<string>
            {
                "-skip_gameinfo",                
                "-rompath"
            };

            // rompath
            string rompath = Path.Combine(AppConfig.GetFullPath("bios"), "psxmame", "bios");
            if (!Directory.Exists(rompath))
            { try { Directory.CreateDirectory(rompath); }
                catch { }
            }
            string combinedRomPath = rompath + ";" + Path.GetDirectoryName(rom);
            commandArray.Add(combinedRomPath);

            // Artwork Path
            string artPath = Path.Combine(AppConfig.GetFullPath("saves"), "psxmame", "artwork");
            if (!Directory.Exists(artPath)) try { Directory.CreateDirectory(artPath); }
                catch { }

            if (!string.IsNullOrEmpty(artPath) && Directory.Exists(artPath))
            {
                commandArray.Add("-artpath");
                commandArray.Add(Path.Combine(path, "artwork") + ";" + artPath);
            }

            // Snapshots
            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
            {
                commandArray.Add("-snapshot_directory");
                commandArray.Add(AppConfig.GetFullPath("screenshots"));
            }

            // Cheats
            if (SystemConfig.isOptSet("psxmame_cheats") && SystemConfig.getOptBoolean("psxmame_cheats"))
            {
                string cheatPath = Path.Combine(AppConfig.GetFullPath("cheats"), "psxmame");
                if (!string.IsNullOrEmpty(cheatPath) && Directory.Exists(cheatPath))
                {
                    commandArray.Add("-cheat");
                    commandArray.Add("-cheatpath");
                    commandArray.Add(cheatPath);
                }
            }

            // NVRAM directory
            string nvramPath = Path.Combine(AppConfig.GetFullPath("saves"), "psxmame", "nvram");
            if (!Directory.Exists(nvramPath)) try { Directory.CreateDirectory(nvramPath); }
                catch { }
            if (!string.IsNullOrEmpty(nvramPath) && Directory.Exists(nvramPath))
            {
                commandArray.Add("-nvram_directory");
                commandArray.Add(nvramPath);
            }

            // Savestate path
            string sstatePath = Path.Combine(AppConfig.GetFullPath("saves"), "psxmame", "states");
            if (!Directory.Exists(sstatePath)) try { Directory.CreateDirectory(sstatePath); }
                catch { }
            if (!string.IsNullOrEmpty(sstatePath) && Directory.Exists(sstatePath))
            {
                commandArray.Add("-state_directory");
                commandArray.Add(sstatePath);
            }

            string ctrlrPath = Path.Combine(path, "ctrlr");
            if (!Directory.Exists(ctrlrPath)) try { Directory.CreateDirectory(ctrlrPath); }
                catch { }

            if (SystemConfig["psxmame_controller_configmode"] == "per_game" && File.Exists(Path.Combine(path, "ctrlr", Path.GetFileNameWithoutExtension(rom) + ".cfg")))
            {
                commandArray.Add("-ctrlr");
                commandArray.Add(Path.GetFileNameWithoutExtension(rom));
            }

            else if (ConfigureMameControllers(path))
            {
                commandArray.Add("-ctrlr");
                commandArray.Add("retrobat");
            }

            commandArray.Add("-verbose");

            commandArray.Add(Path.GetFileNameWithoutExtension(rom));

            args = commandArray.JoinArguments();

            ConfigureUIini(Path.Combine(path));
            ConfigureMameini(Path.Combine(path), combinedRomPath);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void ConfigureUIini(string path) 
        {
            if (!Directory.Exists(path)) try { Directory.CreateDirectory(path); }
                catch { }

            var uiIni = PSXMameIniFile.FromFile(Path.Combine(path, "ui.ini"));
            if (uiIni["skip_warnings"] != "1")
            {
                uiIni["skip_warnings"] = "1";
                uiIni.Save();
            }
        }

        private void ConfigureMameini(string path, string romPath)
        {
            string filetoDelete = Path.Combine(path, "ini", "mame.ini");
            if (File.Exists(filetoDelete))
                try { File.Delete(filetoDelete); }
                catch { }

            List<string> iniFiles = new List<string>();
            string gameIniFile = Path.Combine(path, "ini", _gameName + ".ini");
            if (File.Exists(gameIniFile))
                iniFiles.Add(gameIniFile);

            string iniFile = Path.Combine(path, "mame.ini");
            iniFiles.Add(iniFile);

            string nvramPath = Path.Combine(AppConfig.GetFullPath("saves"), "psxmame", "nvram");

            foreach (string file in iniFiles)
            {
                var ini = PSXMameIniFile.FromFile(file);

                // Paths
                ini["rompath"] = romPath;
                ini["ctrlrpath"] = "ctrlr";
                ini["inipath"] = ".;ini";
                ini["cfg_directory"] = "cfg";
                ini["nvram_directory"] = nvramPath;

                // Core state options
                ini["snapname"] = "%g/%i";
                ini["snapsize"] = "auto";
                ini["snapview"] = "internal";

                // Core performance options
                if (SystemConfig.isOptSet("psxmame_frameskipping") && SystemConfig.getOptBoolean("psxmame_frameskipping"))
                    ini["autoframeskip"] = "1";
                else
                    ini["autoframeskip"] = "0";

                ini["frameskip"] = "0";
                ini["seconds_to_run"] = "0";

                if (SystemConfig.isOptSet("psxmame_throttle") && SystemConfig.getOptBoolean("psxmame_throttle"))
                    ini["throttle"] = "0";
                else
                    ini["throttle"] = "1";

                ini["sleep"] = "1";
                ini["speed"] = "1.0";
                ini["refreshspeed"] = "0";

                // Rotation
                ini["rotate"] = "1";

                // Artwork
                ini["artwork_crop"] = "0";
                ini["use_backdrops"] = "1";
                ini["use_overlays"] = "1";
                ini["use_bezels"] = "1";

                // Screen options
                ini["brightness"] = "1.0";
                ini["contrast"] = "1.0";
                ini["gamma"] = "1.0";
                ini["pause_brightness"] = "0.65";

                // Vector options
                if (SystemConfig.isOptSet("psxmame_antialiasing") && SystemConfig.getOptBoolean("psxmame_antialiasing"))
                    ini["antialias"] = "1";
                else
                    ini["antialias"] = "0";

                ini["beam"] = "1.0";
                ini["flicker"] = "0";

                // Sound options
                ini["sound"] = "1";

                if (SystemConfig.isOptSet("psxmame_samplerate") && !string.IsNullOrEmpty(SystemConfig["psxmame_samplerate"]))
                    ini["samplerate"] = SystemConfig["psxmame_samplerate"];
                else
                    ini["samplerate"] = "48000";

                ini["samples"] = "1";

                // Input options
                ini["coin_lockout"] = "1";
                ini["mouse"] = "0";
                ini["joystick"] = "1";
                ini["lightgun"] = "0";
                ini["multikeyboard"] = "0";
                ini["multimouse"] = "0";
                ini["steadykey"] = "0";
                ini["offscreen_reload"] = "0";
                ini["joystick_map"] = "auto";
                ini["joystick_deadzone"] = "0.3";
                ini["joystick_saturation"] = "0.85";

                // MISC options
                ini["cheat"] = SystemConfig.getOptBoolean("psxmame_cheats") ? "1" : "0";
                ini["skip_gameinfo"] = "1";

                // PSX options
                ini["use_gpu_plugin"] = "1";

                string gpuPlugin = "ogl_renderer.znc";
                if (SystemConfig.isOptSet("psxmame_gpu_plugin") && !string.IsNullOrEmpty(SystemConfig["psxmame_gpu_plugin"]))
                    gpuPlugin = SystemConfig["psxmame_gpu_plugin"];

                ini["gpu_plugin_name"] = gpuPlugin;
                ini["gpu_screen_size"] = "0";
                ini["gpu_screen_std"] = "1";
                ini["gpu_screen_ctm"] = "1";
                ini["gpu_screen_x"] = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width).ToString();
                ini["gpu_screen_y"] = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height).ToString();
                ini["gpu_fullscreen"] = _fullscreen ? "1" : "0";

                if (SystemConfig.isOptSet("psxmame_fps") && SystemConfig.getOptBoolean("psxmame_fps"))
                    ini["gpu_showfps"] = "1";
                else
                    ini["gpu_showfps"] = "0";

                if (SystemConfig.isOptSet("psxmame_scanlines") && !string.IsNullOrEmpty(SystemConfig["psxmame_scanlines"]))
                    ini["gpu_scanline"] = SystemConfig["psxmame_scanlines"];
                else
                    ini["gpu_scanline"] = "0";

                ini["gpu_blending"] = "3";

                if (!SystemConfig.isOptSet("psxmame_depth32") || SystemConfig.getOptBoolean("psxmame_depth32"))
                    ini["gpu_32bit"] = "1";
                else
                    ini["gpu_32bit"] = "0";

                ini["gpu_dithering"] = "1";
                ini["gpu_frame_skip"] = "0";
                ini["gpu_detection"] = "0";
                ini["gpu_frame_limit"] = "0";
                ini["gpu_frame_rate"] = "60";

                if (SystemConfig.isOptSet("psxmame_filtering") && !string.IsNullOrEmpty(SystemConfig["psxmame_filtering"]))
                    ini["gpu_filtering"] = SystemConfig["psxmame_filtering"];
                else
                    ini["gpu_filtering"] = "0";

                ini["gpu_quality"] = "3";
                ini["gpu_caching"] = "0";
                ini["make_gpu_gamewin"] = "0";

                // Window performance options
                ini["priority"] = "0";
                ini["multithreading"] = "0";

                // Windows video options
                ini["video"] = "gdi";
                ini["numscreens"] = "1";
                ini["window"] = "0";
                ini["maximize"] = "1";
                ini["keepaspect"] = "1";
                ini["prescale"] = "1";
                ini["effect"] = "none";
                ini["waitvsync"] = "0";
                ini["syncrefresh"] = "0";

                // Directdraw specific options
                ini["hwstretch"] = "1";

                // Direct3D specific options
                ini["d3dversion"] = "9";
                ini["filter"] = "1";

                // Fullscreen specific options
                ini["triplebuffer"] = "0";
                ini["switchres"] = "0";
                ini["full_screen_brightness"] = "1.0";
                ini["full_screen_contrast"] = "1.0";
                ini["full_screen_gamma"] = "1.0";

                // Windows sound options
                ini["audio_latency"] = "2";

                // Input device options
                ini["dual_lightgun"] = "0";

                ini.Save();
            }
        }
    }

    class PSXMameIniFile
    {
        private string _fileName;
        private List<string> _lines;

        public static PSXMameIniFile FromFile(string file)
        {
            var ret = new PSXMameIniFile
            {
                _fileName = file
            };

            try
            {
                if (File.Exists(file))
                    ret._lines = File.ReadAllLines(file).ToList();
            }
            catch { }

            if (ret._lines == null)
                ret._lines = new List<string>();

            return ret;
        }

        public string this[string key]
        {
            get
            {
                int spaceLength = 26 - key.Length;
                string space = new string(' ', spaceLength);
                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != '#' && l.StartsWith(key + space));
                if (idx >= 0)
                {
                    int split = _lines[idx].IndexOf(" ");
                    if (split >= 0)
                        return _lines[idx].Substring(split + spaceLength).Trim();
                }

                return string.Empty;
            }
            set
            {
                if (this[key] == value)
                    return;

                int spaceLength = 26 - key.Length;
                string space = new string(' ', spaceLength);

                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != '#' && l.StartsWith(key + space));
                if (idx >= 0)
                {
                    _lines.RemoveAt(idx);

                    if (!string.IsNullOrEmpty(value))
                        _lines.Insert(idx, key + space + value);
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    _lines.Add(key + space + value);
                }

                IsDirty = true;
            }
        }

        public bool IsDirty { get; private set; }

        public void Save()
        {
            if (!IsDirty)
                return;

            File.WriteAllLines(_fileName, _lines);
            IsDirty = false;
        }
    }
}
