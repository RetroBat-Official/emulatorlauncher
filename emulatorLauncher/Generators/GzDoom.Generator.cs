using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Windows.Forms;

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
            SetupConfiguration(configFile, resolution, fullscreen);

            var commandArray = new List<string>();

            commandArray.Add("-config");
            commandArray.Add("\"" + configFile + "\"");

            if (Path.GetExtension(rom).ToLower() == ".gzdoom")
            {
                var lines = File.ReadAllLines(rom);

                if (lines.Length == 0)
                    throw new ApplicationException("gzdoom file does not contain any launch argument.");

                foreach (var line in lines)
                    commandArray.Add(line);
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
        private void SetupConfiguration(string configFile, ScreenResolution resolution, bool fullscreen)
        {
            if (!File.Exists(configFile))
                File.WriteAllText(configFile, "");

            try
            {
                using (var ini = IniFile.FromFile(configFile, IniOptions.KeepEmptyLines))
                {
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
                    BindBoolIniFeature(ini, "GlobalSettings", "vid_vsync", "gzdoom_vsync", "false", "true");
                    BindBoolIniFeature(ini, "GlobalSettings", "vid_cropaspect", "gzdoom_cropaspect", "false", "true");

                    BindIniFeature(ini, "GlobalSettings", "snd_mididevice", "gzdoom_mididevice", "-5");
                }
            }
            catch { }
         }
    }
}
