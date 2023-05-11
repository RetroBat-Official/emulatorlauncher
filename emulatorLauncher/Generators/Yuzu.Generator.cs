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
                ini.WriteValue("System", "sound_index\\default", "false");
                if (SystemConfig.isOptSet("sound_index") && !string.IsNullOrEmpty(SystemConfig["sound_index"]))
                    ini.WriteValue("System", "sound_index", SystemConfig["sound_index"]);
                else if (Features.IsSupported("sound_index"))
                    ini.WriteValue("System", "sound_index", "1");

                // Video and Audio drivers
                ini.WriteValue("Renderer", "backend\\default", "false");
                if (SystemConfig.isOptSet("backend") && !string.IsNullOrEmpty(SystemConfig["backend"]))
                    ini.WriteValue("Renderer", "backend", SystemConfig["backend"]);
                else if (Features.IsSupported("backend"))
                    ini.WriteValue("Renderer", "backend", "1");

                ini.WriteValue("Audio", "output_engine\\default", "false");
                if (SystemConfig.isOptSet("audio_backend") && !string.IsNullOrEmpty(SystemConfig["audio_backend"]))
                    ini.WriteValue("Audio", "output_engine", SystemConfig["audio_backend"]);
                else if (Features.IsSupported("audio_backend"))
                    ini.WriteValue("Audio", "output_engine", "auto");

                // resolution_setup
                ini.WriteValue("Renderer", "resolution_setup\\default", "false");
                if (SystemConfig.isOptSet("resolution_setup") && !string.IsNullOrEmpty(SystemConfig["resolution_setup"]))
                    ini.WriteValue("Renderer", "resolution_setup", SystemConfig["resolution_setup"]);
                else if (Features.IsSupported("resolution_setup"))
                    ini.WriteValue("Renderer", "resolution_setup", "2");

                // Aspect ratio
                ini.WriteValue("Renderer", "aspect_ratio\\default", "false");
                if (SystemConfig.isOptSet("yuzu_ratio") && !string.IsNullOrEmpty(SystemConfig["yuzu_ratio"]))
                    ini.WriteValue("Renderer", "aspect_ratio", SystemConfig["yuzu_ratio"]);
                else if (Features.IsSupported("yuzu_ratio"))
                    ini.WriteValue("Renderer", "aspect_ratio", "0");

                // Anisotropic filtering
                ini.WriteValue("Renderer", "max_anisotropy\\default", "false");
                if (SystemConfig.isOptSet("yuzu_anisotropy") && !string.IsNullOrEmpty(SystemConfig["yuzu_anisotropy"]))
                    ini.WriteValue("Renderer", "max_anisotropy", SystemConfig["yuzu_anisotropy"]);
                else if (Features.IsSupported("yuzu_anisotropy"))
                    ini.WriteValue("Renderer", "max_anisotropy", "0");

                // Vsync
                ini.WriteValue("Renderer", "use_vsync\\default", "false");
                if (SystemConfig.isOptSet("use_vsync") && !string.IsNullOrEmpty(SystemConfig["use_vsync"]))
                    ini.WriteValue("Renderer", "use_vsync", SystemConfig["use_vsync"]);
                else if (Features.IsSupported("use_vsync"))
                    ini.WriteValue("Renderer", "use_vsync", "true");

                // anti_aliasing
                ini.WriteValue("Renderer", "anti_aliasing\\default", "false");
                if (SystemConfig.isOptSet("anti_aliasing") && !string.IsNullOrEmpty(SystemConfig["anti_aliasing"]))
                    ini.WriteValue("Renderer", "anti_aliasing", SystemConfig["anti_aliasing"]);
                else if (Features.IsSupported("anti_aliasing"))
                    ini.WriteValue("Renderer", "anti_aliasing", "0");

                // scaling_filter
                ini.WriteValue("Renderer", "scaling_filter\\default", "false");
                if (SystemConfig.isOptSet("scaling_filter") && !string.IsNullOrEmpty(SystemConfig["scaling_filter"]))
                    ini.WriteValue("Renderer", "scaling_filter", SystemConfig["scaling_filter"]);
                else if (Features.IsSupported("scaling_filter"))
                    ini.WriteValue("Renderer", "scaling_filter", "1");

                // GPU accuracy
                ini.WriteValue("Renderer", "gpu_accuracy\\default", "false");
                if (SystemConfig.isOptSet("gpu_accuracy") && !string.IsNullOrEmpty(SystemConfig["gpu_accuracy"]))
                    ini.WriteValue("Renderer", "gpu_accuracy", SystemConfig["gpu_accuracy"]);
                else if (Features.IsSupported("gpu_accuracy"))
                    ini.WriteValue("Renderer", "gpu_accuracy", "1");

                // Asynchronous shaders compilation (hack)
                ini.WriteValue("Renderer", "use_asynchronous_shaders\\default", "false");
                if (SystemConfig.isOptSet("use_asynchronous_shaders") && SystemConfig.getOptBoolean("use_asynchronous_shaders"))
                    ini.WriteValue("Renderer", "use_asynchronous_shaders", "true");
                else if (Features.IsSupported("use_asynchronous_shaders"))
                    ini.WriteValue("Renderer", "use_asynchronous_shaders", "false");

                //CPU accuracy (auto except if the user chooses otherwise)
                ini.WriteValue("Cpu", "cpu_accuracy\\default", "false");
                if (SystemConfig.isOptSet("cpu_accuracy") && !string.IsNullOrEmpty(SystemConfig["cpu_accuracy"]))
                    ini.WriteValue("Cpu", "cpu_accuracy", SystemConfig["cpu_accuracy"]);
                else
                    ini.WriteValue("Cpu", "cpu_accuracy", "0");
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
