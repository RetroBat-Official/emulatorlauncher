using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class GZDoomGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("gzdoom");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "gzdoom.exe");

            if (!File.Exists(exe))
                return null;
			
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string configFile = Path.Combine(path, "gzdoom_portable.ini");
            string romPath = Path.GetDirectoryName(rom);
            SetupConfiguration(configFile, romPath, fullscreen);

            var commandArray = new List<string>
            {
                "-config",
                "\"" + configFile + "\""
            };

            if (Path.GetExtension(rom).ToLower() == ".gzdoom" && File.Exists(rom))
            {
                var lines = File.ReadAllLines(rom);

                if (lines.Length == 0)
                    throw new ApplicationException("gzdoom file does not contain any launch argument.");

                foreach (var line in lines)
                {
                    string value = line;

                    if (lines.Length == 1)
                    {
                        value = line.Replace("/", "\\");
                        string filePath = Path.Combine(Path.GetDirectoryName(rom), value.TrimStart('\\'));

                        commandArray.Add("-gamegrp");
                        commandArray.Add("\"" + filePath + "\"");
                    }

                    else if (value.StartsWith("\\") || value.StartsWith("/"))
                    {
                        value = line.Replace("/", "\\");
                        string filePath = Path.Combine(Path.GetDirectoryName(rom), value.TrimStart('\\'));

                        commandArray.Add("\"" + filePath + "\"");
                    }
                    else
                        commandArray.Add(line);
                }
            }
            
            else if (Path.GetExtension(rom).ToLower() == ".gzdoom" && Directory.Exists(rom))
            {
                string [] files = Directory.GetFiles(rom);
                string mainIwad = files.FirstOrDefault(file => iwadList.Any(keyword => file.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
                if (mainIwad == null)
                    throw new ApplicationException("There is no iwad file in the folder");
                else
                {
                    commandArray.Add("-iwad");
                    commandArray.Add("\"" + mainIwad + "\"");
                }
                
                var modFiles = files.Where(file => file != mainIwad).ToList();
                if (modFiles.Count > 0)
                {
                    commandArray.Add("-file");
                    commandArray.Add("\"" + string.Join("\" \"", modFiles) + "\"");
                }
            }
            
            else
            {
                commandArray.Add("-iwad");
                commandArray.Add("\"" + rom + "\"");
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        //Setup configuration file
        private void SetupConfiguration(string configFile, string romPath, bool fullscreen)
        {
            if (!File.Exists(configFile))
                File.WriteAllText(configFile, "");

            try
            {
                using (var ini = IniFile.FromFile(configFile, IniOptions.KeepEmptyLines))
                {
                    ini.WriteValue("IWADSearch.Directories", "Path", romPath.Replace("\\", "/"));

                    if (fullscreen)
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "true");
                    else
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "false");

                    BindIniFeature(ini, "GlobalSettings", "vid_preferbackend", "gzdoom_renderer", "0");
                    BindIniFeature(ini, "GlobalSettings", "vid_aspect", "gzdoom_ratio", "0");
                    BindIniFeature(ini, "GlobalSettings", "vid_rendermode", "gzdoom_rendermode", "4");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter", "gzdoom_texture_filter", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter_anisotropic", "gzdoom_anisotropic_filtering", "1");
                    BindIniFeature(ini, "GlobalSettings", "vid_scalemode", "gzdoom_scalemode", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_multisample", "gzdoom_msaa", "1");
                    BindIniFeature(ini, "GlobalSettings", "gl_fxaa", "gzdoom_fxaa", "0");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_vsync", "gzdoom_vsync", "true", "false");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_cropaspect", "gzdoom_cropaspect", "true", "false");

                    BindIniFeature(ini, "GlobalSettings", "snd_mididevice", "gzdoom_mididevice", "-5");

                    // hdr
                    if (SystemConfig["gzdoom_renderer"] == "1" && SystemConfig.getOptBoolean("enable_hdr"))
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
                    
                    // crosshairs
                    if (SystemConfig.isOptSet("gzdoom_crosshair") && SystemConfig["gzdoom_crosshair"] == "false")
                    {
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairon", "false");
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairhealth", "0");
                    }
                    else if (SystemConfig.isOptSet("gzdoom_crosshair") && !string.IsNullOrEmpty(SystemConfig["gzdoom_crosshair"]))
                    {
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairon", "true");
                        ini.WriteValue("Doom.ConsoleVariables", "crosshair", SystemConfig["gzdoom_crosshair"]);
                    }
                    else
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairon", "false");

                    if (SystemConfig.isOptSet("gzdoom_crosshair_color") && SystemConfig["gzdoom_crosshair_color"] == "health")
                    {
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairhealth", "1");
                    }
                    else if (SystemConfig.isOptSet("gzdoom_crosshair_color") && !string.IsNullOrEmpty(SystemConfig["gzdoom_crosshair_color"]))
                    {
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairhealth", "0");
                        ini.WriteValue("Doom.ConsoleVariables", "crosshaircolor", SystemConfig["gzdoom_crosshair_color"].Replace("_", " "));
                    }
                    else
                        ini.WriteValue("Doom.ConsoleVariables", "crosshairhealth", "1");

                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), "gzdoom");
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }
                    ini.WriteValue("GlobalSettings", "save_dir", savePath.Replace("\\", "/"));
                }
            }
            catch { }
        }

        private readonly static List<string> iwadList = new List<string>()
        {
            // Doom
            "doom.wad",
            "doom1.wad",
            "doomu.wad",
            "bfgdoom.wad",
            "doombfg.wad",
            // Doom 2
            "doom2.wad",
            "bfgdoom2.wad",
            "doom2bfg.wad",
            "tnt.wad",
            "plutonia.wad",
            "doom2f.wad",
            // Heretic
            "heretic.wad",
            "blasphem.wad",
            "blasphemer.wad",
            "heretic1.wad",
            "hereticsr.wad",
            // Hexen
            "hexen.wad",
            "hexdemo.wad",
            "hexendemo.wad",
            "hexdd.wad",
            // Strife
            "strife.wad",
            "strife0.wad",
            "strife1.wad",
            "sve.wad",
            // Chex Quest
            "chex.wad",
            "chex3.wad",
            // Freedom
            "freedoom1.wad",
            "freedomu.wad",
            "freedoom2.wad",
            "freedom.wad",
            "freedm.wad",
            // Action Doom 2
            "action2.wad",
            // Harmony
            "harm1.wad",
            // Hacx
            "hacx.wad",
            "hacx2.wad",
            // Square
            "square1.pk3",
            // Delaweare
            "delaweare.wad",
            // Rise of the wool ball
            "rotwb.wad",
        };
    }
}
