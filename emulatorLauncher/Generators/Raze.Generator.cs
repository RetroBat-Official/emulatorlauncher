using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class RazeGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("raze");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "raze.exe");

            if (!File.Exists(exe))
                return null;
			
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string configFile = Path.Combine(path, "raze_portable.ini");
            string romPath = Path.GetDirectoryName(rom);
            SetupConfiguration(configFile, romPath, fullscreen);

            var commandArray = new List<string>
            {
                "-config",
                "\"" + configFile + "\""
            };

            if (Path.GetExtension(rom).ToLower() == ".raze" && File.Exists(rom))
            {
                var lines = File.ReadAllLines(rom);

                if (lines.Length == 0)
                    throw new ApplicationException("raze file does not contain any launch argument.");

                foreach (var line in lines)
                {
                    string value = line;

                    if (lines.Length == 1)
                    {
                        value = line.Replace("/", "\\");
                        string filePath = Path.Combine(Path.GetDirectoryName(rom), value.TrimStart('\\'));

                        commandArray.Add("-iwad");
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

            Dictionary<string, string> gameSettings = new Dictionary<string, string>();

            try
            {
                using (var ini = IniFile.FromFile(configFile, IniOptions.KeepEmptyLines))
                {
                    ini.WriteValue("GameSearch.Directories", "Path", romPath.Replace("\\", "/") + "/*");

                    if (fullscreen)
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "true");
                    else
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "false");

                    BindIniFeature(ini, "GlobalSettings", "vid_preferbackend", "raze_renderer", "0");
                    BindIniFeature(ini, "GlobalSettings", "vid_aspect", "raze_ratio", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter", "raze_texture_filter", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter_anisotropic", "raze_anisotropic_filtering", "1");
                    BindIniFeature(ini, "GlobalSettings", "vid_scalemode", "raze_scalemode", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_multisample", "raze_msaa", "1");
                    BindIniFeature(ini, "GlobalSettings", "gl_fxaa", "raze_fxaa", "0");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_vsync", "raze_vsync", "true", "false");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_cropaspect", "raze_cropaspect", "true", "false");

                    BindIniFeature(ini, "GlobalSettings", "snd_mididevice", "raze_mididevice", "-5");

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
                    if (SystemConfig.isOptSet("raze_crosshair") && !string.IsNullOrEmpty(SystemConfig["raze_crosshair"]) && SystemConfig["raze_crosshair"] != "off")
                    {
                        gameSettings.Add("crosshair", SystemConfig["raze_crosshair"]);
                        gameSettings.Add("cl_crosshair", "true");
                    }
                    else if (SystemConfig["raze_crosshair"] == "off")
                    {
                        gameSettings.Add("crosshair", "0");
                        gameSettings.Add("cl_crosshair", "false");
                    }
                    else
                    {
                        gameSettings.Add("crosshair", "0");
                        gameSettings.Add("cl_crosshair", "true");
                    }

                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), "raze");
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }
                    ini.WriteValue("GlobalSettings", "save_dir", savePath.Replace("\\", "/"));

                    // Set configuration to the individual game
                    foreach (string game in gameNames)
                    {
                        string iniSection = game + ".ConsoleVariables";

                        if (gameSettings.Count > 0)
                        {
                            foreach (var setting in gameSettings)
                            {
                                ini.WriteValue(iniSection, setting.Key, setting.Value);
                            }
                        }
                    }
                }
            }
            catch { }
        }

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
    }
}
