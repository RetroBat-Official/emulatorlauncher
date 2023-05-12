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
                { "ja", "0" },
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

            // Special cases
            if (SystemConfig["Language"] == "zh_TW")
                return "11";
            if (SystemConfig["Language"] == "pt_BR")
                return "17";
            if (SystemConfig["Language"] == "en_GB")
                return "12";
            if (SystemConfig["Language"] == "es_MX")
                return "14";

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }

            return "1";
        }

        private string _gamedirsIniPath;
        private string _gamedirsSize;

        public override void Cleanup()
        {
            base.Cleanup();

            // Restore value for Paths\\gamedirs\\size
            // As it's faster to launch a yuzu game when there's no folder set            

            if (string.IsNullOrEmpty(_gamedirsIniPath) || string.IsNullOrEmpty(_gamedirsSize))
                return;

            using (var ini = new IniFile(_gamedirsIniPath))                
                ini.WriteValue("UI", "Paths\\gamedirs\\size", _gamedirsSize);
        }


        private void SetupConfiguration(string path, string rom)
        {
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");

            using (var ini = new IniFile(conf))
            {
                /* Set up paths
                string switchSavesPath = Path.Combine(AppConfig.GetFullPath("saves"), "switch");
                if (!Directory.Exists(switchSavesPath)) try { Directory.CreateDirectory(switchSavesPath); }
                    catch { }

                string sdmcPath = Path.Combine(switchSavesPath, "sdmc");
                if (!Directory.Exists(sdmcPath)) try { Directory.CreateDirectory(sdmcPath); }
                    catch { }

                if (Directory.Exists(sdmcPath))
                {
                    ini.WriteValue("Data%20Storage", "sdmc_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "sdmc_directory", sdmcPath.Replace("\\", "/"));
                }

                string nandPath = Path.Combine(switchSavesPath, "nand");
                if (!Directory.Exists(nandPath)) try { Directory.CreateDirectory(nandPath); }
                    catch { }

                if (Directory.Exists(nandPath))
                {
                    ini.WriteValue("Data%20Storage", "nand_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "nand_directory", nandPath.Replace("\\", "/"));
                }

                string dumpPath = Path.Combine(switchSavesPath, "dump");
                if (!Directory.Exists(dumpPath)) try { Directory.CreateDirectory(dumpPath); }
                    catch { }

                if (Directory.Exists(dumpPath))
                {
                    ini.WriteValue("Data%20Storage", "dump_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "dump_directory", dumpPath.Replace("\\", "/"));
                }

                string loadPath = Path.Combine(switchSavesPath, "load");
                if (!Directory.Exists(loadPath)) try { Directory.CreateDirectory(loadPath); }
                    catch { }

                if (Directory.Exists(loadPath))
                {
                    ini.WriteValue("Data%20Storage", "load_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "load_directory", loadPath.Replace("\\", "/"));
                }*/

                //language
                ini.WriteValue("System", "language_index\\default", "false");
                if (SystemConfig.isOptSet("yuzu_language") && !string.IsNullOrEmpty(SystemConfig["yuzu_language"]))
                    ini.WriteValue("System", "language_index", SystemConfig["yuzu_language"]);
                else
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

                //Discord
                if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                {
                    ini.WriteValue("UI", "enable_discord_presence\\default", "true");
                    ini.WriteValue("UI", "enable_discord_presence", "true");
                }
                else
                {
                    ini.WriteValue("UI", "enable_discord_presence\\default", "false");
                    ini.WriteValue("UI", "enable_discord_presence", "false");
                }

                //launch in fullscreen
                ini.WriteValue("UI", "fullscreen\\default", "false");
                ini.WriteValue("UI", "fullscreen", "true");

                //Hide mouse when inactive
                ini.WriteValue("UI", "hideInactiveMouse\\default", "true");
                ini.WriteValue("UI", "hideInactiveMouse", "true");

                //docked mode
                if (SystemConfig.isOptSet("yuzu_undock") && SystemConfig.getOptBoolean("yuzu_undock"))
                {
                    ini.WriteValue("UI", "use_docked_mode\\default", "false");
                    ini.WriteValue("UI", "use_docked_mode", "false");
                    ini.WriteValue("Controls", "use_docked_mode\\default", "false");
                    ini.WriteValue("Controls", "use_docked_mode", "false");
                }
                else if (Features.IsSupported("yuzu_undock"))
                {
                    ini.WriteValue("UI", "use_docked_mode\\default", "true");
                    ini.WriteValue("UI", "use_docked_mode", "true");
                    ini.WriteValue("Controls", "use_docked_mode\\default", "true");
                    ini.WriteValue("Controls", "use_docked_mode", "true");
                }


                //disable telemetry
                ini.WriteValue("WebService", "enable_telemetry\\default", "false");
                ini.WriteValue("WebService", "enable_telemetry", "false");

                //remove exit confirmation
                ini.WriteValue("UI", "confirmClose\\default", "false");
                ini.WriteValue("UI", "confirmClose", "false");

                //get path for roms
                string romPath = Path.GetDirectoryName(rom);
                ini.WriteValue("UI", "Paths\\gamedirs\\4\\path", romPath.Replace("\\","/"));
                
                // Set gamedirs count to 4
                var gameDirsSize = ini.GetValue("UI", "Paths\\gamedirs\\size");
                if (gameDirsSize.ToInteger() != 4)
                {
                    _gamedirsIniPath = conf;
                    _gamedirsSize = gameDirsSize;
                    ini.WriteValue("UI", "Paths\\gamedirs\\size", "4");
                }
                
                CreateControllerConfiguration(ini);

                //screenshots path
                string screenshotpath = AppConfig.GetFullPath("screenshots").Replace("\\", "/") + "/yuzu";
                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as", "false");
                    ini.WriteValue("UI", "Screenshots\\screenshot_path", screenshotpath);
                }

                // Audio output
                BindQtIniFeature(ini, "Audio", "output_engine", "audio_backend", "auto");
                BindQtIniFeature(ini, "System", "sound_index", "sound_index", "1");

                // Video drivers                
                BindQtIniFeature(ini, "Renderer", "backend", "backend", "1");

                // resolution_setup
                BindQtIniFeature(ini, "Renderer", "resolution_setup", "resolution_setup", "2");

                // Aspect ratio
                BindQtIniFeature(ini, "Renderer", "aspect_ratio", "yuzu_ratio", "0");

                // Anisotropic filtering
                BindQtIniFeature(ini, "Renderer", "max_anisotropy", "yuzu_anisotropy", "0");

                // Vsync
                BindQtIniFeature(ini, "Renderer", "use_vsync", "use_vsync", "2");

                // anti_aliasing
                BindQtIniFeature(ini, "Renderer", "anti_aliasing", "anti_aliasing", "0");

                // scaling_filter
                BindQtIniFeature(ini, "Renderer", "scaling_filter", "scaling_filter", "1");

                // GPU accuracy
                BindQtIniFeature(ini, "Renderer", "gpu_accuracy", "gpu_accuracy", "1");

                // Asynchronous shaders compilation (hack)
                BindQtIniFeature(ini, "Renderer", "use_asynchronous_shaders", "use_asynchronous_shaders", "true");

                // CPU accuracy (auto except if the user chooses otherwise)
                BindQtIniFeature(ini, "Cpu", "cpu_accuracy", "cpu_accuracy", "0");
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
