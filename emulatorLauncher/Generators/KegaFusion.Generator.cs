using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class KegaFusionGenerator : Generator
    {
        public KegaFusionGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        static List<string> _mdSystems = new List<string>() { "genesis", "megadrive", "mega32x", "megacd", "segacd", "sega32x" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("kega-fusion");

            string exe = Path.Combine(path, "Fusion.exe");
            if (!File.Exists(exe))
                return null;

            SetupConfiguration(path, system);

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
			else if (core == "segacd" || core == "megacd")
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

        private void SetupConfiguration(string path, string system)
        {
            string iniFile = Path.Combine(path, "Fusion.ini");

            using (var ini = IniFile.FromFile(iniFile))
            {
                // PATHS
                string screenshotpath = Path.Combine(AppConfig.GetFullPath("screenshots"), "kega-fusion");
                if (!Directory.Exists(screenshotpath)) try { Directory.CreateDirectory(screenshotpath); }
                    catch { }

                ini.WriteValue("", "ScreenshotPath", screenshotpath);

                string savespath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(savespath)) try { Directory.CreateDirectory(savespath); }
                    catch { }

                string statepath = Path.Combine(savespath, "kega-fusion", "sstates");
                if (!Directory.Exists(statepath)) try { Directory.CreateDirectory(statepath); }
                    catch { }

                ini.WriteValue("", "SRMFiles", savespath);
                ini.WriteValue("", "StateFiles", statepath);

                string biosPath = AppConfig.GetFullPath("bios");
                if (!Directory.Exists(biosPath)) try { Directory.CreateDirectory(biosPath); }
                    catch { }

                ini.WriteValue("", "GenesisBIOS", biosPath);

                // BIOS
                string euBios = Path.Combine(biosPath, "bios_CD_E.bin");
                if (File.Exists(euBios))
                    ini.WriteValue("", "SCDEURBIOS", euBios);

                string usBios = Path.Combine(biosPath, "bios_CD_U.bin");
                if (File.Exists(usBios))
                    ini.WriteValue("", "SCDUSABIOS", usBios);

                string jpBios = Path.Combine(biosPath, "bios_CD_J.bin");
                if (File.Exists(jpBios))
                    ini.WriteValue("", "SCDJAPBIOS", jpBios);

                string bios32Xg = Path.Combine(biosPath, "32X_G_BIOS.BIN");
                if (File.Exists(bios32Xg))
                    ini.WriteValue("", "32XM68KBIOS", bios32Xg);

                string bios32Xm = Path.Combine(biosPath, "32X_M_BIOS.BIN");
                if (File.Exists(bios32Xm))
                    ini.WriteValue("", "32XMSH2BIOS", bios32Xm);

                string bios32Xs = Path.Combine(biosPath, "32X_S_BIOS.BIN");
                if (File.Exists(bios32Xs))
                    ini.WriteValue("", "32XSSH2BIOS", bios32Xs);

                // VIDEO
                bool fullscreen = ShouldRunFullscreen();

                if (fullscreen)
                    ini.WriteValue("", "FullScreen", "1");
                else
                    ini.WriteValue("", "FullScreen", "0");

                ini.WriteValue("", "DFixedAspect", "1");
                ini.WriteValue("", "DFixedZoom", "0");

                BindBoolIniFeatureOn(ini, "", "VSyncEnabled", "vsync", "1", "0");
                BindIniFeature(ini, "", "DResolution", "kega_internal_resolution", "224,1,128,2");
                BindIniFeature(ini, "", "DScanlines", "kega_scanlines", "3");
                BindIniFeature(ini, "", "DRenderMode", "kega_rendermode", "0");
                BindBoolIniFeatureOn(ini, "", "DFiltered", "kega_filter", "1", "0");
                BindBoolIniFeatureOn(ini, "", "Brighten", "kega_brighten", "1", "0");
                BindBoolIniFeatureOn(ini, "", "DNTSCAspect", "kega_ntsc_aspect", "1", "0");
                BindBoolIniFeatureOn(ini, "", "DNearestMultiple", "kega_nearest", "1", "0");

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

                BindBoolIniFeature(ini, "", "FPSEnabled", "kega_fps", "1", "0");

                ConfigureControllers(ini, system);
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