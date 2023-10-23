using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
{
    class KegaFusionGenerator : Generator
    {
        public KegaFusionGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("kega-fusion");

            string exe = Path.Combine(path, "Fusion.exe");
            if (!File.Exists(exe))
                return null;

            SetupConfiguration(path);

            List<string> commandArray = new List<string>();

            if (core == "mastersystem" || core == "sms")
            {                
                commandArray.Add("-sms");
            }
            else if (core == "gamegear" || core == "gg")
            {
                commandArray.Add("-gg");
            }
            else if (core == "megadrive" || core == "md")
            {
                commandArray.Add("-md");
            }
            else if (core == "genesis" || core == "gen")
            {                
                commandArray.Add("-gen");
            }
			else if (core == "sega32x")
            {                
                commandArray.Add("-32x");
            }
			else if (core == "megacd")
            {                
                commandArray.Add("-mcd");
            }
			else if (core == "segacd")
            {                
                commandArray.Add("-scd");
            }
			else if (core == "auto")
            {                
                commandArray.Add("-auto");
            }
            
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfiguration(string path)
        {
            string iniFile = Path.Combine(path, "Fusion.ini");

            using (var ini = IniFile.FromFile(iniFile))
            {
                // VIDEO
                bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

                if (fullscreen)
                    ini.WriteValue("", "FullScreen", "1");
                else
                    ini.WriteValue("", "FullScreen", "0");

                ini.WriteValue("", "DFixedAspect", "1");
                ini.WriteValue("", "DFixedZoom", "0");

                BindIniFeature(ini, "", "VSyncEnabled", "vsync", "1");
                BindIniFeature(ini, "", "DResolution", "kega_internal_resolution", "224,1,128,2");
                BindIniFeature(ini, "", "DScanlines", "kega_scanlines", "3");
                BindIniFeature(ini, "", "DRenderMode", "kega_rendermode", "0");
                BindIniFeature(ini, "", "DFiltered", "kega_filter", "1");
                BindIniFeature(ini, "", "Brighten", "kega_brighten", "1");
                BindIniFeature(ini, "", "DNTSCAspect", "kega_ntsc_aspect", "1");
                BindIniFeature(ini, "", "DNearestMultiple", "kega_nearest", "1");

                if (SystemConfig.isOptSet("kega_scaler") && !string.IsNullOrEmpty(SystemConfig["kega_scaler"]))
                {
                    ini.WriteValue("", "DRenderMode", "3");
                    ini.WriteValue("", "CurrentRenderPlugin", SystemConfig["kega_scaler"]);
                }
                else
                    ini.WriteValue("", "DRenderMode", "0");

                // OTHER
                if (SystemConfig.isOptSet("kega_region") && !string.IsNullOrEmpty(SystemConfig["kega_region"]))
                {
                    ini.WriteValue("", "CurrentCountry", SystemConfig["kega_region"]);
                    ini.WriteValue("", "CountryAutoDetect", "0");
                }
                else
                {
                    ini.WriteValue("", "CountryAutoDetect", "1");
                }

                BindIniFeature(ini, "", "FPSEnabled", "kega_fps", "0");

            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            // Fusion always returns 1....
            if (exitCode == 1)
                return 0;

            return exitCode;
        }
    }
}