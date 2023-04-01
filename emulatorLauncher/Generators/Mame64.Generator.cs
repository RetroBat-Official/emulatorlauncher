using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class Mame64Generator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mame");
            if (string.IsNullOrEmpty(path) && Environment.Is64BitOperatingSystem)
                path = AppConfig.GetFullPath("mame64");

            string exe = Path.Combine(path, "mame.exe");
            if (!File.Exists(exe) && Environment.Is64BitOperatingSystem)
                exe = Path.Combine(path, "mame64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "mame32.exe");

            if (!File.Exists(exe))
                return null;

            _exeName = Path.GetFileNameWithoutExtension(exe);

            ConfigureBezels(Path.Combine(AppConfig.GetFullPath("bios"), "mame", "artwork"), system, rom, resolution);

            string args = null;

            MessSystem messMode = MessSystem.GetMessSystem(system, core);
            if (messMode == null || messMode.Name == "mame")
            {
                List<string> commandArray = new List<string>();

                commandArray.Add("-skip_gameinfo");

                // rompath
                commandArray.Add("-rompath");
                if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig["bios"]))
                    commandArray.Add(AppConfig.GetFullPath("bios") + ";" + Path.GetDirectoryName(rom));
                else
                    commandArray.Add(Path.GetDirectoryName(rom));

                // Sample Path
                string samplePath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "samples");
                if (!Directory.Exists(samplePath)) try { Directory.CreateDirectory(samplePath); }
                    catch { }
                if (!string.IsNullOrEmpty(samplePath) && Directory.Exists(samplePath))
                {
                    commandArray.Add("-samplepath");
                    commandArray.Add(samplePath);
                }

                // Artwork Path
                string artPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "artwork");
                if (!Directory.Exists(artPath)) try { Directory.CreateDirectory(artPath); }
                    catch { }

                if (!string.IsNullOrEmpty(artPath) && Directory.Exists(artPath))
                {
                    commandArray.Add("-artpath");
                    commandArray.Add(artPath + ";" + Path.Combine(path, "artwork") + ";" + Path.Combine(AppConfig.GetFullPath("saves"), "mame", "artwork"));
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
                    string cheatPath = Path.Combine(AppConfig.GetFullPath("cheats"), "mame");
                    if (!string.IsNullOrEmpty(cheatPath) && Directory.Exists(cheatPath))
                    {
                        commandArray.Add("-cheat");
                        commandArray.Add("-cheatpath");
                        commandArray.Add(cheatPath);
                    }
                }

                // NVRAM directory
                string nvramPath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "nvram");
                if (!Directory.Exists(nvramPath)) try { Directory.CreateDirectory(nvramPath); }
                    catch { }
                if (!string.IsNullOrEmpty(nvramPath) && Directory.Exists(nvramPath))
                {
                    commandArray.Add("-nvram_directory");
                    commandArray.Add(nvramPath);
                }

                // cfg directory
                string cfgPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "cfg");
                if (!Directory.Exists(cfgPath)) try { Directory.CreateDirectory(cfgPath); }
                    catch { }
                if (!string.IsNullOrEmpty(cfgPath) && Directory.Exists(cfgPath))
                {
                    commandArray.Add("-cfg_directory");
                    commandArray.Add(cfgPath);
                }

                // Ini path
                string iniPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "ini");
                if (!Directory.Exists(iniPath)) try { Directory.CreateDirectory(iniPath); }
                    catch { }
                if (!string.IsNullOrEmpty(iniPath) && Directory.Exists(iniPath))
                {
                    commandArray.Add("-inipath");
                    commandArray.Add(iniPath);
                }

                // Hash path
                string hashPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame", "hash");
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

                commandArray.AddRange(GetCommonMame64Arguments(rom, resolution));

                // Unknown system, try to run with rom name only
                commandArray.Add(Path.GetFileName(rom));

                args = commandArray.JoinArguments();
            }
            else
            {
                var commandArray = messMode.GetMameCommandLineArguments(system, rom, true);
                commandArray.AddRange(GetCommonMame64Arguments(rom, resolution));

                args = commandArray.JoinArguments();
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private string _exeName;

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _exeName, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

        private List<string> GetCommonMame64Arguments(string rom, ScreenResolution resolution = null)
        {
            var retList = new List<string>();

            string sstatePath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "states");
            if (!Directory.Exists(sstatePath)) try { Directory.CreateDirectory(sstatePath); }
                catch { }
            if (!string.IsNullOrEmpty(sstatePath) && Directory.Exists(sstatePath))
            {
                retList.Add("-state_directory");
                retList.Add(sstatePath);
            }

            string ctrlrPath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr");
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
            retList.Add("-aspect");
            if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                retList.Add(SystemConfig["ratio"]);
            else
                retList.Add("auto");
            
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
            // Shaders (only for bgfx driver)
            if (SystemConfig.isOptSet("bgfxbackend") && (SystemConfig["mame_video_driver"] == "bgfx") && !string.IsNullOrEmpty(SystemConfig["bgfxbackend"]))
            {
                retList.Add("-bgfx_backend");
                retList.Add(SystemConfig["bgfxbackend"]);

                if (SystemConfig.isOptSet("bgfxshaders") && !string.IsNullOrEmpty(SystemConfig["bgfxshaders"]))
                {
                    useCoreBrightness = true;
                    retList.Add("-bgfx_screen_chains");
                    retList.Add(SystemConfig["bgfxshaders"]);
                }
                else if (Features.IsSupported("bgfxshaders"))
                {
                    retList.Add("-bgfx_screen_chains");
                    retList.Add("default");
                }
            }
            
            if (SystemConfig.isOptSet("effect") && !string.IsNullOrEmpty(SystemConfig["effect"]))
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

            // Enable inputs
            if (SystemConfig.isOptSet("mame_lightgun") && !string.IsNullOrEmpty(SystemConfig["mame_lightgun"]))
            {
                if (SystemConfig["mame_lightgun"] == "mouse")
                {
                    retList.Add("-lightgun_device");
                    retList.Add("mouse");
                    retList.Add("-adstick_device");
                    retList.Add("mouse");
                }
                else if (SystemConfig["mame_lightgun"] == "lightgun")
                {
                    retList.Add("-lightgun_device");
                    retList.Add("lightgun");
                    retList.Add("-adstick_device");
                    retList.Add("lightgun");
                }
                else if (SystemConfig["mame_lightgun"] == "none")
                {
                    retList.Add("-lightgun_device");
                    retList.Add("none");
                    retList.Add("-adstick_device");
                    retList.Add("none");
                }
                else
                {
                    retList.Add("-lightgun_device");
                    retList.Add("joystick");
                    retList.Add("-adstick_device");
                    retList.Add("joystick");
                }
            }

            // Other devices
            if (SystemConfig.isOptSet("mame_mouse") && SystemConfig.getOptBoolean("mame_mouse"))
            {
                retList.Add("-dial_device");
                retList.Add("mouse");
                retList.Add("-trackball_device");
                retList.Add("mouse");
                retList.Add("-paddle_device");
                retList.Add("mouse");
                retList.Add("-positional_device");
                retList.Add("mouse");
                retList.Add("-mouse_device");
                retList.Add("mouse");
                retList.Add("-ui_mouse");
            }
            else
            {
                retList.Add("-dial_device");
                retList.Add("joystick");
                retList.Add("-trackball_device");
                retList.Add("joystick");
                retList.Add("-paddle_device");
                retList.Add("joystick");
                retList.Add("-positional_device");
                retList.Add("joystick");
                retList.Add("-mouse_device");
                retList.Add("joystick");
            }

            if (SystemConfig.isOptSet("mame_offscreen_reload") && SystemConfig.getOptBoolean("mame_offscreen_reload") && SystemConfig["mame_lightgun"] != "none")
                retList.Add("-offscreen_reload");

            // Gamepad driver
            retList.Add("-joystickprovider");
            if (SystemConfig.isOptSet("mame_joystick_driver") && !string.IsNullOrEmpty(SystemConfig["mame_joystick_driver"]))
                retList.Add(SystemConfig["mame_joystick_driver"]);
            else
                retList.Add("winhybrid");

            if (SystemConfig.isOptSet("mame_ctrlr_profile") && SystemConfig["mame_ctrlr_profile"] != "none")
            {
                string ctrlrProfile = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr", SystemConfig["mame_ctrlr_profile"] + ".cfg");
                if (File.Exists(ctrlrProfile) && SystemConfig["mame_ctrlr_profile"] != "per_game")
                {
                    retList.Add("-ctrlr");
                    retList.Add(SystemConfig["mame_ctrlr_profile"]);
                }
                else if (SystemConfig["mame_ctrlr_profile"] == "per_game")
                {
                    string romName = Path.GetFileNameWithoutExtension(rom);
                    ctrlrProfile = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr", romName + ".cfg");
                    if (File.Exists(ctrlrProfile))
                    {
                        retList.Add("-ctrlr");
                        retList.Add(romName);
                    }
                }
            }

            return retList;
        }

    }
}
