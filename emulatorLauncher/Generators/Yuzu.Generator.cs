using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class YuzuGenerator : Generator
    {
        public YuzuGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private SdlVersion _sdlVersion = SdlVersion.Unknown;
        
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator.Replace("-", " "));
            if (string.IsNullOrEmpty(path) && emulator.Contains("-"))
                path = AppConfig.GetFullPath(emulator);
            
            string exe = Path.Combine(path, "yuzu.exe");
            if (!File.Exists(exe))
                return null;

            string sdl2 = Path.Combine(path, "SDL2.dll");
            if (File.Exists(sdl2))
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);
            
            SetupConfiguration(path, rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-f -g \"" + rom + "\"",
            };
        }

        private string GetDefaultswitchLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "jp", "0" },
                { "en", "1" },
                { "fr", "2" },
                { "de", "3" },
                { "it", "4" },
                { "es", "5" },
                { "zh", "6" },
                { "ko", "7" },
                { "nl", "8" },
                { "pt", "9" },
                { "ru", "10" },
                { "tw", "11" }
            };

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return "11";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }

            return "1";
        }

        private void SetupConfiguration(string path, string rom)
        {
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");

            using (var ini = new IniFile(conf))
            {
                //language
                if (SystemConfig["Language"] == "en")
                    ini.WriteValue("System", "language_index\\default", "true");
                else
                    ini.WriteValue("System", "language_index\\default", "false");
                
                ini.WriteValue("System", "language_index", GetDefaultswitchLanguage());

                //region
                if (SystemConfig.isOptSet("yuzu_region_value") && !string.IsNullOrEmpty(SystemConfig["yuzu_region_value"]) && SystemConfig["yuzu_region_value"] != "1")
                {
                    ini.WriteValue("System", "region_index\\default", "false");
                    ini.WriteValue("System", "region_index", SystemConfig["yuzu_region_value"]);
                }
                else if (Features.IsSupported("yuzu_region_value"))
                {
                    ini.WriteValue("System", "region_index\\default", "true");
                    ini.WriteValue("System", "region_index", "1");
                }

                //launch in fullscreen
                ini.WriteValue("UI", "fullscreen\\default", "false");
                ini.WriteValue("UI", "fullscreen", "true");

                //docked mode
                ini.WriteValue("UI", "use_docked_mode\\default", "true");
                ini.WriteValue("UI", "use_docked_mode", "true");

                //disable telemetry
                ini.WriteValue("WebService", "enable_telemetry\\default", "false");
                ini.WriteValue("WebService", "enable_telemetry", "false");

                //remove exit confirmation
                ini.WriteValue("UI", "confirmClose\\default", "false");
                ini.WriteValue("UI", "confirmClose", "false");

                //get path for roms
                string romPath = Path.GetDirectoryName(rom);
                ini.WriteValue("UI", "Paths\\gamedirs\\4\\path", romPath.Replace("\\","/"));

                CreateControllerConfiguration(ini);

                //screenshots path
                string screenshotpath = AppConfig.GetFullPath("screenshots").Replace("\\", "/") + "/yuzu";
                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as", "false");
                    ini.WriteValue("UI", "Screenshots\\screenshot_path", screenshotpath);
                }

                // backend
                if (SystemConfig.isOptSet("backend") && !string.IsNullOrEmpty(SystemConfig["backend"]) && SystemConfig["backend"] != "0")
                {
                    ini.WriteValue("Renderer", "backend\\default", "false");
                    ini.WriteValue("Renderer", "backend", SystemConfig["backend"]);
                }
                else if (Features.IsSupported("backend"))
                {
                    ini.WriteValue("Renderer", "backend\\default", "true");
                    ini.WriteValue("Renderer", "backend", "0");
                }

                // resolution_setup
                if (SystemConfig.isOptSet("resolution_setup") && !string.IsNullOrEmpty(SystemConfig["resolution_setup"]) && SystemConfig["resolution_setup"] != "2")
                {
                    ini.WriteValue("Renderer", "resolution_setup\\default", "false");
                    ini.WriteValue("Renderer", "resolution_setup", SystemConfig["resolution_setup"]);
                }
                else if (Features.IsSupported("resolution_setup"))
                {
                    ini.WriteValue("Renderer", "resolution_setup\\default", "true");
                    ini.WriteValue("Renderer", "resolution_setup", "2");
                }

                // anti_aliasing
                if (SystemConfig.isOptSet("anti_aliasing") && SystemConfig.getOptBoolean("anti_aliasing"))
                {
                    ini.WriteValue("Renderer", "anti_aliasing\\default", "false");
                    ini.WriteValue("Renderer", "anti_aliasing", "1");
                }
                else if (Features.IsSupported("anti_aliasing"))
                {
                    ini.WriteValue("Renderer", "anti_aliasing\\default", "true");
                    ini.WriteValue("Renderer", "anti_aliasing", "0");
                }

                // scaling_filter
                if (SystemConfig.isOptSet("scaling_filter") && !string.IsNullOrEmpty(SystemConfig["scaling_filter"]) && SystemConfig["scaling_filter"] != "1")
                {
                    ini.WriteValue("Renderer", "scaling_filter\\default", "false");
                    ini.WriteValue("Renderer", "scaling_filter", SystemConfig["scaling_filter"]);
                }
                else if (Features.IsSupported("scaling_filter"))
                {
                    ini.WriteValue("Renderer", "scaling_filter\\default", "true");
                    ini.WriteValue("Renderer", "scaling_filter", "1");
                }

                //CPU accuracy (auto except if the user chooses otherwise)
                if (SystemConfig.isOptSet("cpu_accuracy") && !string.IsNullOrEmpty(SystemConfig["cpu_accuracy"]) && SystemConfig["cpu_accuracy"] != "0")
                {
                    ini.WriteValue("Cpu", "cpu_accuracy\\default", "false");
                    ini.WriteValue("Cpu", "cpu_accuracy", SystemConfig["cpu_accuracy"]);
                }
                else
                {
                    ini.WriteValue("Cpu", "cpu_accuracy\\default", "true");
                    ini.WriteValue("Cpu", "cpu_accuracy", "0");
                }
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            // Yuzu always returns 0xc0000005 ( null pointer !? )
            if (exitCode == unchecked((int)0xc0000005))
                return 0;
            
            return exitCode;
        }
    }
}
