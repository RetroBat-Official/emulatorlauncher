using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class Mame64Generator : Generator
    {
        public Mame64Generator()
        {
            DependsOnDesktopResolution = true;
        }

        private bool _multigun = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            bool hbmame = system == "hbmame";

            string path = AppConfig.GetFullPath("mame");
            if (string.IsNullOrEmpty(path) && Environment.Is64BitOperatingSystem)
                path = AppConfig.GetFullPath("mame64");

            string exe = Path.Combine(path, "mame.exe");
            if (!File.Exists(exe) && Environment.Is64BitOperatingSystem)
                exe = Path.Combine(path, "mame64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "mame32.exe");

            if (hbmame)
            {
                path = AppConfig.GetFullPath("hbmame");
                if (string.IsNullOrEmpty(path))
                    return null;

                exe = Path.Combine(path, "hbmameui.exe");
            }

            if (!File.Exists(exe))
                return null;

            ConfigureBezels(Path.Combine(AppConfig.GetFullPath("bios"), "mame", "artwork"), system, rom, resolution);
            ConfigureUIini(Path.Combine(AppConfig.GetFullPath("bios"), "mame", "ini"));
            ConfigureMameini(Path.Combine(AppConfig.GetFullPath("bios"), "mame", "ini"));

            string args;

            MessSystem messMode = MessSystem.GetMessSystem(system, core);
            if (messMode == null || messMode.Name == "mame" || messMode.Name == "hbmame")
            {
                List<string> commandArray = new List<string>
                {
                    "-skip_gameinfo",

                    // rompath
                    "-rompath"
                };
                if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig["bios"]))
                    commandArray.Add(AppConfig.GetFullPath("bios") + ";" + Path.GetDirectoryName(rom));
                else
                    commandArray.Add(Path.GetDirectoryName(rom));

                // Sample Path
                string samplePath = hbmame? Path.Combine(AppConfig.GetFullPath("bios"), "hbmame", "samples") : Path.Combine(AppConfig.GetFullPath("bios"), "mame", "samples");
                if (!Directory.Exists(samplePath)) try { Directory.CreateDirectory(samplePath); }
                    catch { }
                if (!string.IsNullOrEmpty(samplePath) && Directory.Exists(samplePath))
                {
                    commandArray.Add("-samplepath");
                    commandArray.Add(samplePath);
                }

                // Artwork Path
                string artPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "artwork");
                string artPath2 = hbmame? Path.Combine(AppConfig.GetFullPath("saves"), "hbmame", "artwork") : Path.Combine(AppConfig.GetFullPath("saves"), "mame", "artwork");
                if (!Directory.Exists(artPath)) try { Directory.CreateDirectory(artPath); }
                    catch { }

                if (!string.IsNullOrEmpty(artPath) && Directory.Exists(artPath))
                {
                    commandArray.Add("-artpath");
                    if (SystemConfig.isOptSet("disable_artwork") && SystemConfig.getOptBoolean("disable_artwork"))
                        commandArray.Add(artPath);
                    else
                        commandArray.Add(artPath + ";" + Path.Combine(path, "artwork") + ";" + artPath2);
                }

                // Snapshots
                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    commandArray.Add("-snapshot_directory");
                    commandArray.Add(AppConfig.GetFullPath("screenshots"));
                }

                // Cheats
                if (SystemConfig.isOptSet("mame_cheats") && SystemConfig.getOptBoolean("mame_cheats"))
                {
                    string cheatPath = hbmame ? Path.Combine(AppConfig.GetFullPath("cheats"), "hbmame") : Path.Combine(AppConfig.GetFullPath("cheats"), "mame");
                    if (!string.IsNullOrEmpty(cheatPath) && Directory.Exists(cheatPath))
                    {
                        commandArray.Add("-cheat");
                        commandArray.Add("-cheatpath");
                        commandArray.Add(cheatPath);
                    }
                }

                // cfg directory
                string cfgPath = hbmame ? Path.Combine(AppConfig.GetFullPath("bios"), "hbmame", "cfg") : Path.Combine(AppConfig.GetFullPath("bios"), "mame", "cfg");
                if (!Directory.Exists(cfgPath)) try { Directory.CreateDirectory(cfgPath); }
                    catch { }
                if (!string.IsNullOrEmpty(cfgPath) && Directory.Exists(cfgPath))
                {
                    commandArray.Add("-cfg_directory");
                    commandArray.Add(cfgPath);
                }

                /* Delete default.cfg files if they exist
                string defaultCfg = Path.Combine(cfgPath, "default.cfg");
                if (File.Exists(defaultCfg))
                    File.Delete(defaultCfg);
                */

                // Ini path
                string iniPath = hbmame ? Path.Combine(AppConfig.GetFullPath("bios"), "hbmame", "ini") : Path.Combine(AppConfig.GetFullPath("bios"), "mame", "ini");
                if (!Directory.Exists(iniPath)) try { Directory.CreateDirectory(iniPath); }
                    catch { }
                if (!string.IsNullOrEmpty(iniPath) && Directory.Exists(iniPath))
                {
                    commandArray.Add("-inipath");
                    commandArray.Add(iniPath);
                }

                // Hash path
                string hashPath = hbmame ? Path.Combine(AppConfig.GetFullPath("bios"), "hbmame", "hash") : Path.Combine(AppConfig.GetFullPath("bios"), "mame", "hash");
                if (!Directory.Exists(hashPath)) try { Directory.CreateDirectory(hashPath); }
                    catch { }
                if (!string.IsNullOrEmpty(hashPath) && Directory.Exists(hashPath))
                {
                    commandArray.Add("-hashpath");
                    commandArray.Add(hashPath);
                }

                /// other available paths:
                /// -input_directory
                /// -diff_directory
                /// -comment_directory
                /// -homepath
                /// -crosshairpath
                /// -swpath

                commandArray.AddRange(GetCommonMame64Arguments(rom, hbmame, resolution));

                // Unknown system, try to run with rom name only
                commandArray.Add(Path.GetFileNameWithoutExtension(rom));

                args = commandArray.JoinArguments();
            }
            else
            {
                var commandArray = messMode.GetMameCommandLineArguments(system, rom, true);
                commandArray.AddRange(GetCommonMame64Arguments(rom, hbmame, resolution));

                args = commandArray.JoinArguments();
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                WindowStyle = ProcessWindowStyle.Minimized,
            };
        }

        private List<string> GetCommonMame64Arguments(string rom, bool hbmame, ScreenResolution resolution = null)
        {
            var retList = new List<string>();

            if (SystemConfig.isOptSet("noread_ini") && SystemConfig.getOptBoolean("noread_ini"))
                retList.Add("-noreadconfig");

            string sstatePath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "states");
            if (!Directory.Exists(sstatePath)) try { Directory.CreateDirectory(sstatePath); }
                catch { }
            if (!string.IsNullOrEmpty(sstatePath) && Directory.Exists(sstatePath))
            {
                retList.Add("-state_directory");
                retList.Add(sstatePath);
            }

            string nvramPath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "nvram");
            if (!Directory.Exists(nvramPath)) try { Directory.CreateDirectory(nvramPath); }
                catch { }
            if (Directory.Exists(nvramPath))
            {
                retList.Add("-nvram_directory");
                retList.Add(nvramPath);
            }

            string homePath = Path.Combine(AppConfig.GetFullPath("bios"), "mame");
            if (!Directory.Exists(homePath)) try { Directory.CreateDirectory(homePath); }
                catch { }
            if (Directory.Exists(homePath))
            {
                retList.Add("-homepath");
                retList.Add(homePath);
            }

            string ctrlrPath = hbmame? Path.Combine(AppConfig.GetFullPath("saves"), "hbmame", "ctrlr") : Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr");
            if (!Directory.Exists(ctrlrPath)) try { Directory.CreateDirectory(ctrlrPath); }
                catch { }
            if (!string.IsNullOrEmpty(ctrlrPath) && Directory.Exists(ctrlrPath))
            {
                retList.Add("-ctrlrpath");
                retList.Add(ctrlrPath);
            }

            if (!SystemConfig.isOptSet("smooth") || !SystemConfig.getOptBoolean("smooth"))
                retList.Add("-nofilter");

            retList.Add("-verbose");

            // Throttle
            if (SystemConfig.isOptSet("mame_throttle") && SystemConfig.getOptBoolean("mame_throttle"))
                retList.Add("-nothrottle");
            else
                retList.Add("-throttle");

            // Autosave and rewind
            if (SystemConfig.isOptSet("autosave") && SystemConfig.getOptBoolean("autosave"))
                retList.Add("-autosave");

            if (SystemConfig.isOptSet("rewind") && SystemConfig.getOptBoolean("rewind"))
                retList.Add("-rewind");

            // Audio driver
            retList.Add("-sound");
            if (SystemConfig.isOptSet("mame_audio_driver") && !string.IsNullOrEmpty(SystemConfig["mame_audio_driver"]))
                retList.Add(SystemConfig["mame_audio_driver"]);
            else
                retList.Add("dsound");

            // Video driver
            retList.Add("-video");
            if (SystemConfig.isOptSet("mame_video_driver") && !string.IsNullOrEmpty(SystemConfig["mame_video_driver"]))
                retList.Add(SystemConfig["mame_video_driver"]);
            else
                retList.Add("d3d");

            // Resolution
            if (resolution != null)
            {
                if (SystemConfig["mame_video_driver"] != "gdi" && SystemConfig["mame_video_driver"] != "bgfx")
                    retList.Add("-switchres");

                retList.Add("-resolution");
                retList.Add(resolution.Width+"x"+resolution.Height+"@"+resolution.DisplayFrequency);
            }
            else 
            {                
                retList.Add("-resolution");
                retList.Add("auto");
            }

            // Aspect ratio
            if (SystemConfig.isOptSet("mame_ratio") && SystemConfig["mame_ratio"] == "stretch")
            {
                    retList.Add("-nokeepaspect");
            }
            else
            {
                retList.Add("-aspect");
                retList.Add("auto");
            }
            
            // Monitor index
            if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
            {
                string mameMonitor = "\\" + "\\" + ".\\" + "DISPLAY" + SystemConfig["MonitorIndex"];
                retList.Add("-screen");
                retList.Add(mameMonitor);
            }

            // Screen rotation
            if (SystemConfig.isOptSet("mame_rotate") && SystemConfig["mame_rotate"] != "off")
                retList.Add("-" + SystemConfig["mame_rotate"]);

            // Other video options
            if (SystemConfig.isOptSet("triplebuffer") && SystemConfig.getOptBoolean("triplebuffer") && SystemConfig["mame_video_driver"] != "gdi")
                retList.Add("-triplebuffer");

            if ((!SystemConfig.isOptSet("vsync") || SystemConfig.getOptBoolean("vsync")) && SystemConfig["mame_video_driver"] != "gdi")
                retList.Add("-waitvsync");

            bool useCoreBrightness = false;
            
            /// Effects and shaders
            /// Currently support: BGFX, OpenGL (GLSL) or simple effects
            
            // BGFX Shaders (only for bgfx driver)
            if (SystemConfig.isOptSet("bgfxshaders") && !string.IsNullOrEmpty(SystemConfig["bgfxshaders"]) && (SystemConfig["mame_video_driver"] == "bgfx"))
            {
                if (SystemConfig.isOptSet("bgfxbackend")  && !string.IsNullOrEmpty(SystemConfig["bgfxbackend"]))
                { 
                    retList.Add("-bgfx_backend");
                    retList.Add(SystemConfig["bgfxbackend"]);
                }

                useCoreBrightness = true;
                retList.Add("-bgfx_screen_chains");
                retList.Add(SystemConfig["bgfxshaders"]);
            }

            else if (SystemConfig.isOptSet("glslshaders") && !string.IsNullOrEmpty(SystemConfig["glslshaders"]) && (SystemConfig["mame_video_driver"] == "opengl"))
            {
                useCoreBrightness = true;
                retList.Add("-gl_glsl");
                retList.AddRange(Getglslshaderchain());
            }

            else if (SystemConfig.isOptSet("effect") && !string.IsNullOrEmpty(SystemConfig["effect"]))
            {
                retList.Add("-effect");
                retList.Add(SystemConfig["effect"]);
            }

            // Adjust gamma, brightness and contrast
            if (SystemConfig["mame_video_driver"] != "gdi")
            {
                if (useCoreBrightness)
                {
                    if (SystemConfig.isOptSet("brightness") && !string.IsNullOrEmpty(SystemConfig["brightness"]))
                    {
                        retList.Add("-brightness");
                        retList.Add(SystemConfig["brightness"]);
                    }

                    if (SystemConfig.isOptSet("gamma") && !string.IsNullOrEmpty(SystemConfig["gamma"]))
                    {
                        retList.Add("-gamma");
                        retList.Add(SystemConfig["gamma"]);
                    }

                    if (SystemConfig.isOptSet("contrast") && !string.IsNullOrEmpty(SystemConfig["contrast"]))
                    {
                        retList.Add("-contrast");
                        retList.Add(SystemConfig["contrast"]);
                    }
                }

                else
                {
                    if (SystemConfig.isOptSet("brightness") && !string.IsNullOrEmpty(SystemConfig["brightness"]))
                    {
                        retList.Add("-full_screen_brightness");
                        retList.Add(SystemConfig["brightness"]);
                    }

                    if (SystemConfig.isOptSet("gamma") && !string.IsNullOrEmpty(SystemConfig["gamma"]))
                    {
                        retList.Add("-full_screen_gamma");
                        retList.Add(SystemConfig["gamma"]);
                    }

                    if (SystemConfig.isOptSet("contrast") && !string.IsNullOrEmpty(SystemConfig["contrast"]))
                    {
                        retList.Add("-full_screen_contrast");
                        retList.Add(SystemConfig["contrast"]);
                    }
                }

            }

            // Add plugins

            List<string> pluginList = new List<string>();
            if (SystemConfig.isOptSet("mame_cheats") && SystemConfig.getOptBoolean("mame_cheats"))
                pluginList.Add("cheat");
            if (SystemConfig.isOptSet("mame_hiscore") && SystemConfig.getOptBoolean("mame_hiscore"))
                pluginList.Add("hiscore");

            if (pluginList.Count > 0)
            {
                string pluginJoin = string.Join<string>(",", pluginList);
                retList.Add("-plugin");
                retList.Add(pluginJoin);
            }

            // DEVICES
            // Mouse
            if (SystemConfig.isOptSet("mame_mouse") && SystemConfig["mame_mouse"] == "none")
                retList.Add("-nomouse");
            else if (SystemConfig.isOptSet("mame_mouse") && !string.IsNullOrEmpty(SystemConfig["mame_mouse"]))
            {
                retList.Add("-mouse_device");
                retList.Add(SystemConfig["mame_mouse"]);
                retList.Add("-ui_mouse");
            }
            else
            {
                retList.Add("-mouse_device");
                retList.Add("mouse");
                retList.Add("-ui_mouse");
            }

            // Lightgun
            if (SystemConfig.isOptSet("mame_lightgun") && SystemConfig["mame_lightgun"] == "none")
                retList.Add("-nolightgun");
            else if (SystemConfig.isOptSet("mame_lightgun") && !string.IsNullOrEmpty(SystemConfig["mame_lightgun"]))
            {
                retList.Add("-lightgun_device");
                retList.Add(SystemConfig["mame_lightgun"]);
            }
            else
            {
                retList.Add("-lightgun_device");
                retList.Add("lightgun");
            }

            // Paddle
            if (SystemConfig.isOptSet("mame_paddle") && !string.IsNullOrEmpty(SystemConfig["mame_paddle"]))
            {
                retList.Add("-paddle_device");
                retList.Add(SystemConfig["mame_paddle"]);
            }
            else
            {
                retList.Add("-paddle_device");
                retList.Add("none");
            }

            // Adstick
            if (SystemConfig.isOptSet("mame_adstick") && !string.IsNullOrEmpty(SystemConfig["mame_adstick"]))
            {
                retList.Add("-adstick_device");
                retList.Add(SystemConfig["mame_adstick"]);
            }
            else
            {
                retList.Add("-adstick_device");
                retList.Add("joystick");
            }

            // Positional Device
            if (SystemConfig.isOptSet("mame_positional") && !string.IsNullOrEmpty(SystemConfig["mame_positional"]))
            {
                retList.Add("-positional_device");
                retList.Add(SystemConfig["mame_positional"]);
            }
            else
            {
                retList.Add("-positional_device");
                retList.Add("none");
            }

            // Trackball
            if (SystemConfig.isOptSet("mame_trackball") && !string.IsNullOrEmpty(SystemConfig["mame_trackball"]))
            {
                retList.Add("-trackball_device");
                retList.Add(SystemConfig["mame_trackball"]);
            }
            else
            {
                retList.Add("-trackball_device");
                retList.Add("none");
            }

            // Dial device
            if (SystemConfig.isOptSet("mame_dial") && !string.IsNullOrEmpty(SystemConfig["mame_dial"]))
            {
                retList.Add("-dial_device");
                retList.Add(SystemConfig["mame_dial"]);
            }
            else
            {
                retList.Add("-dial_device");
                retList.Add("none");
            }

            if (SystemConfig.isOptSet("mame_offscreen_reload") && SystemConfig.getOptBoolean("mame_offscreen_reload") && SystemConfig["mame_lightgun"] != "none")
                retList.Add("-offscreen_reload");

            if (SystemConfig.isOptSet("mame_multimouse") && SystemConfig.getOptBoolean("mame_multimouse"))
            {
                retList.Add("-multimouse");
                _multigun = true;
            }

            // Gamepad driver
            retList.Add("-joystickprovider");
            if (SystemConfig.isOptSet("mame_joystick_driver") && !string.IsNullOrEmpty(SystemConfig["mame_joystick_driver"]))
                retList.Add(SystemConfig["mame_joystick_driver"]);
            else
                retList.Add("winhybrid");

            if (SystemConfig.isOptSet("mame_ctrlr_profile") && SystemConfig["mame_ctrlr_profile"] != "none" && SystemConfig["mame_ctrlr_profile"] != "retrobat_auto")
            {
                string ctrlrProfile = hbmame? Path.Combine(AppConfig.GetFullPath("saves"), "hbmame", "ctrlr", SystemConfig["mame_ctrlr_profile"] + ".cfg") : Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr", SystemConfig["mame_ctrlr_profile"] + ".cfg");

                if (File.Exists(ctrlrProfile) && SystemConfig["mame_ctrlr_profile"] != "per_game")
                {
                    retList.Add("-ctrlr");
                    retList.Add(SystemConfig["mame_ctrlr_profile"]);
                }
                else if (SystemConfig["mame_ctrlr_profile"] == "per_game")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom);
                    ctrlrProfile = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr", romName + ".cfg");
                    string biosctrlrProfile = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "cfg", romName + ".cfg");
                    if (File.Exists(ctrlrProfile))
                    {
                        retList.Add("-ctrlr");
                        retList.Add(romName);
                    }
                    else if (File.Exists(biosctrlrProfile))
                    {
                        try { File.Copy(biosctrlrProfile, ctrlrProfile); }
                        catch { }
                        
                        retList.Add("-ctrlr");
                        retList.Add(romName);
                    }
                }
            }
            
            else if (!SystemConfig.isOptSet("mame_ctrlr_profile") || SystemConfig["mame_ctrlr_profile"] == "retrobat_auto")
            {
                if (ConfigureMameControllers(ctrlrPath, hbmame))
                {
                    retList.Add("-ctrlr");
                    retList.Add("retrobat_auto");
                }
            }

            // Add code here

            return retList;
        }

        private List<string> Getglslshaderchain()
        {
            var shaderlist = new List<string>();
            var ext = new List<string> { "vsh" };

            string path = AppConfig.GetFullPath("mame");
            if (string.IsNullOrEmpty(path) && Environment.Is64BitOperatingSystem)
                path = AppConfig.GetFullPath("mame64");

            string glslPath = Path.Combine(path, "glsl");

            if (Directory.Exists(glslPath))
            {
                string shaderPath = Path.Combine(glslPath, SystemConfig["glslshaders"]);
                if (Directory.Exists(shaderPath))
                {
                    List<string> shaderFiles = Directory.GetFiles(shaderPath, "*.*", SearchOption.AllDirectories)
                      .Where(file => new string[] { ".vsh" }
                      .Contains(Path.GetExtension(file)))
                      .ToList();

                    if (shaderFiles.Count != 0)
                    {
                        int shadernb = 0;
                        foreach (var shader in shaderFiles)
                        {
                            string shaderName = "." + "\\" + "glsl\\" + SystemConfig["glslshaders"] + "\\" + Path.GetFileNameWithoutExtension(shader);
                            shaderlist.Add("-glsl_shader_mame" + shadernb);
                            shaderlist.Add(shaderName);
                            shadernb += 1;
                        }
                    }
                }
            }

            return shaderlist;
        }

        private void ConfigureUIini(string path) 
        {
            var uiIni = MameIniFile.FromFile(Path.Combine(path, "ui.ini"));
            if (uiIni["skip_warnings"] != "1")
            {
                uiIni["skip_warnings"] = "1";
                uiIni.Save();
            }
        }

        private void ConfigureMameini(string path)
        {
            var ini = MameIniFile.FromFile(Path.Combine(path, "mame.ini"));

            if (ini["writeconfig"] != "0")
            {
                ini["writeconfig"] = "0";
            }

            if (SystemConfig.isOptSet("mame_output") && !string.IsNullOrEmpty(SystemConfig["mame_output"]))
                ini["output"] = SystemConfig["mame_output"];
            else
                ini["output"] = "auto";
            
            ini.Save();
        }
    }

    class MameIniFile
    {
        private string _fileName;
        private List<string> _lines;

        public static MameIniFile FromFile(string file)
        {
            var ret = new MameIniFile
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
                    _lines.Add("");
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
