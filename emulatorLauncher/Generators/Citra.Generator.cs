using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class CitraGenerator : Generator
    {
        public CitraGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private SdlVersion _sdlVersion = SdlVersion.Unknown;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("citra");

            string exe = Path.Combine(path, "citra-qt.exe");
            if (!File.Exists(exe))
                return null;

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            string sdl2 = Path.Combine(path, "SDL2.dll");
            if (File.Exists(sdl2))
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);

            if (core == "citra-sdl")
            {
                exe = Path.Combine(path, "citra.exe");
                if (!File.Exists(exe))
                    return null;

                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "\"" + rom + "\"",
                };
            }

            SetupConfiguration(path);

            return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "\"" + rom + "\" -f",
                };
        }

        private void SetupConfiguration(string path)
        {
            string userconfigPath = Path.Combine(path, "user", "config");
            if (!Directory.Exists(userconfigPath))
                Directory.CreateDirectory(userconfigPath);

            string conf = Path.Combine(userconfigPath, "qt-config.ini");
            using (var ini = new IniFile(conf))
            {
                ini.WriteValue("UI", "fullscreen\\default", "false");
                ini.WriteValue("UI", "fullscreen", "true");

                ini.WriteValue("UI", "confirmClose\\default", "false");
                ini.WriteValue("UI", "confirmClose", "false");

                ini.WriteValue("WebService", "enable_telemetry\\default", "false");
                ini.WriteValue("WebService", "enable_telemetry", "false");

                ini.WriteValue("UI", "firstStart\\default", "false");
                ini.WriteValue("UI", "firstStart", "false");

                ini.WriteValue("UI", "calloutFlags\\default", "false");
                ini.WriteValue("UI", "calloutFlags", "1");

                CreateControllerConfiguration(ini);

                if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                {
                    ini.WriteValue("Layout", "filter_mode\\default", "true");
                    ini.WriteValue("Layout", "filter_mode", "true");
                }
                else
                {
                    ini.WriteValue("Layout", "filter_mode\\default", "false");
                    ini.WriteValue("Layout", "filter_mode", "false");
                }

                if (Features.IsSupported("citra_resolution_factor"))
                {
                    if (SystemConfig.isOptSet("citra_resolution_factor"))
                    {
                        ini.WriteValue("Renderer", "resolution_factor\\default", SystemConfig["citra_resolution_factor"] == "1" ? "true" : "false");
                        ini.WriteValue("Renderer", "resolution_factor", SystemConfig["citra_resolution_factor"]);
                    }
                    else
                    {
                        ini.WriteValue("Renderer", "resolution_factor\\default", "true");
                        ini.WriteValue("Renderer", "resolution_factor", "1");
                    }
                }

                if (Features.IsSupported("citra_layout_option"))
                {
                    if (SystemConfig.isOptSet("citra_layout_option"))
                        ini.WriteValue("Layout", "layout_option", SystemConfig["citra_layout_option"]);
                    else
                        ini.WriteValue("Layout", "layout_option", "0");
                }

                if (Features.IsSupported("citra_swap_screen"))
                {
                    if (SystemConfig.isOptSet("citra_swap_screen") && SystemConfig.getOptBoolean("citra_swap_screen"))
                    {
                        ini.WriteValue("Layout", "swap_screen\\default", "false");
                        ini.WriteValue("Layout", "swap_screen", "true");
                    }
                    else
                    {
                        ini.WriteValue("Layout", "swap_screen\\default", "true");
                        ini.WriteValue("Layout", "swap_screen", "false");
                    }
                }

                //Define console region
                if (SystemConfig.isOptSet("citra_region_value") && !string.IsNullOrEmpty(SystemConfig["citra_region_value"]) && SystemConfig["citra_region_value"] != "-1")
                {
                    ini.WriteValue("System", "region_value\\default", "false");
                    ini.WriteValue("System", "region_value", SystemConfig["citra_region_value"]);
                }
                else if (Features.IsSupported("citra_region_value"))
                {
                    ini.WriteValue("System", "region_value\\default", "true");
                    ini.WriteValue("System", "region_value", "-1");
                }
            }
        }
    }
}