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
    class Mame64Generator : Generator
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

            string args = null;

            MessSystem messMode = MessSystem.GetMessSystem(system, core);
            if (messMode.Name == "mame")
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
                    commandArray.Add(artPath);
                }

                // Snapshots
                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "mame");
                if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                    catch { }
                if (!string.IsNullOrEmpty(screenshotPath) && Directory.Exists(screenshotPath))
                {
                    commandArray.Add("-snapshot_directory");
                    commandArray.Add(screenshotPath);
                }

                // Cheats
                if (SystemConfig.isOptSet("mame_cheats") && SystemConfig.getOptBoolean("mame_cheats"))
                {
                    string cheatPath = Path.Combine(AppConfig.GetFullPath("bios"), "mame");
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

                // Save States
                string sstatePath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "states");
                if (!Directory.Exists(sstatePath)) try { Directory.CreateDirectory(sstatePath); }
                    catch { }
                if (!string.IsNullOrEmpty(sstatePath) && Directory.Exists(sstatePath))
                {
                    commandArray.Add("-state_directory");
                    commandArray.Add(sstatePath);
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

                // cfg directory
                string ctrlrPath = Path.Combine(AppConfig.GetFullPath("saves"), "mame", "ctrlr");
                if (!Directory.Exists(ctrlrPath)) try { Directory.CreateDirectory(ctrlrPath); }
                    catch { }
                if (!string.IsNullOrEmpty(ctrlrPath) && Directory.Exists(ctrlrPath))
                {
                    commandArray.Add("-ctrlrpath");
                    commandArray.Add(ctrlrPath);
                }

                /// other available paths:
                /// -input_directory
                /// -diff_directory
                /// -comment_directory
                /// -homepath
                /// -crosshairpath
                /// -swpath

                commandArray.Add("-video");
                if (SystemConfig.isOptSet("mame_video_driver") && SystemConfig.isOptSet("mame_video_driver"))
                    commandArray.Add(SystemConfig["mame_video_driver"]);
                else
                    commandArray.Add("d3d");

                if (SystemConfig.isOptSet("triplebuffer") && SystemConfig.getOptBoolean("triplebuffer") && SystemConfig["mame_video_driver"] != "gdi")
                    commandArray.Add("-triplebuffer");

                if (SystemConfig.isOptSet("vsync") && SystemConfig.getOptBoolean("vsync") && SystemConfig["mame_video_driver"] != "gdi")
                    commandArray.Add("-waitvsync");

                if (SystemConfig.isOptSet("mame_rotate") && SystemConfig["mame_rotate"] != "off")
                    commandArray.Add("-" + SystemConfig["mame_rotate"]);

                // Add plugins
                List<string> pluginList = new List<string>();
                if (SystemConfig.isOptSet("mame_cheats") && SystemConfig.getOptBoolean("mame_cheats"))
                    pluginList.Add("cheat");
                if (SystemConfig.isOptSet("mame_hiscore") && SystemConfig.getOptBoolean("mame_hiscore"))
                    pluginList.Add("hiscore");
                
                if (pluginList.Count > 0)
                {
                    string pluginJoin = string.Join<string>(",", pluginList);
                    commandArray.Add("-plugin");
                    commandArray.Add(pluginJoin);
                }

                // Enable inputs
                if (SystemConfig.isOptSet("mame_lightgun") && !string.IsNullOrEmpty(SystemConfig["mame_lightgun"]))
                { 
                    if (SystemConfig["mame_lightgun"] == "none")
                    {
                        commandArray.Add("-lightgun_device");
                        commandArray.Add("none");
                    }
                    else if (SystemConfig["mame_lightgun"] == "lightgun")
                    {
                        commandArray.Add("-lightgun_device");
                        commandArray.Add("lightgun");
                    }
                    else
                    {
                        commandArray.Add("-lightgun_device");
                        commandArray.Add("mouse");
                    }
                }

                if (SystemConfig.isOptSet("mame_offscreen_reload") && SystemConfig.getOptBoolean("mame_offscreen_reload") && SystemConfig["mame_lightgun"] != "none")
                    commandArray.Add("-offscreen_reload");

                // Gamepad driver
                commandArray.Add("-joystickprovider");
                if (SystemConfig.isOptSet("mame_joystick_driver") && !string.IsNullOrEmpty(SystemConfig["mame_joystick_driver"]))
                    commandArray.Add(SystemConfig["mame_joystick_driver"]);
                else
                    commandArray.Add("winhybrid");

                // Unknown system, try to run with rom name only
                commandArray.Add(Path.GetFileName(rom));

                args = string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());
            }
            else
                args = messMode.GetMameCommandLineArguments(system, rom, false);

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
    }
}
