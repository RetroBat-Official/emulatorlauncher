using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EmulatorLauncher
{
    partial class SudachiGenerator : Generator
    {
        public SudachiGenerator()
        {
            DependsOnDesktopResolution = true;
        }
        
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "sudachi.exe");
            if (!File.Exists(exe))
                return null;

            // Ensure user folder exists
            string userFolder = Path.Combine(path, "user");
            if (!Directory.Exists(userFolder)) try { Directory.CreateDirectory(userFolder); }
                catch { }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfigurationSudachi(path, rom, fullscreen);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-f");

            commandArray.Add("-g");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
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
                if (availableLanguages.TryGetValue(lang, out string ret))
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
            // As it's faster to launch a sudachi game when there's no folder set            

            if (!_norawinput)
            {
                try { Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", null, EnvironmentVariableTarget.User); } catch { }
            }

            if (string.IsNullOrEmpty(_gamedirsIniPath) || string.IsNullOrEmpty(_gamedirsSize))
                return;

            using (var ini = new IniFile(_gamedirsIniPath))                
                ini.WriteValue("UI", "Paths\\gamedirs\\size", _gamedirsSize);
        }

        private void SetupConfigurationSudachi(string path, string rom, bool fullscreen)
        {
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");

            using (var ini = new IniFile(conf, IniOptions.KeepEmptyValues))
            {
                /* Set up paths
                string switchSavesPath = Path.Combine(path, "user");

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
                if (SystemConfig.isOptSet("sudachi_language") && !string.IsNullOrEmpty(SystemConfig["sudachi_language"]))
                    ini.WriteValue("System", "language_index", SystemConfig["sudachi_language"]);
                else
                    ini.WriteValue("System", "language_index", GetDefaultswitchLanguage());

                //region
                if (SystemConfig.isOptSet("sudachi_region_value") && !string.IsNullOrEmpty(SystemConfig["sudachi_region_value"]) && SystemConfig["sudachi_region_value"] != "1")
                {
                    ini.WriteValue("System", "region_index\\default", "false");
                    ini.WriteValue("System", "region_index", SystemConfig["sudachi_region_value"]);
                }
                else if (Features.IsSupported("sudachi_region_value"))
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
                ini.WriteValue("UI", "fullscreen\\default", fullscreen ? "false" : "true");
                ini.WriteValue("UI", "fullscreen", fullscreen ? "true" : "false");

                ini.WriteValue("Renderer", "fullscreen_mode\\default", SystemConfig.getOptBoolean("exclusivefs") ? "false" : "true");
                ini.WriteValue("Renderer", "fullscreen_mode", SystemConfig.getOptBoolean("exclusivefs") ? "1" : "0");

                //Hide mouse when inactive
                ini.WriteValue("UI", "hideInactiveMouse\\default", "true");
                ini.WriteValue("UI", "hideInactiveMouse", "true");

                ini.WriteValue("UI", "pauseWhenInBackground\\default", "false");
                ini.WriteValue("UI", "pauseWhenInBackground", "true");

                // Controller applet
                if (SystemConfig.isOptSet("sudachi_controller_applet") && !SystemConfig.getOptBoolean("sudachi_controller_applet"))
                {
                    ini.WriteValue("UI", "disableControllerApplet\\default", "true");
                    ini.WriteValue("UI", "disableControllerApplet", "false");
                }
                else if (Features.IsSupported("sudachi_controller_applet"))
                {
                    ini.WriteValue("UI", "disableControllerApplet\\default", "false");
                    ini.WriteValue("UI", "disableControllerApplet", "true");
                }

                //docked mode
                if (SystemConfig.isOptSet("sudachi_undock") && SystemConfig.getOptBoolean("sudachi_undock"))
                {
                    ini.WriteValue("System", "use_docked_mode\\default", "false");
                    ini.WriteValue("System", "use_docked_mode", "0");
                }
                else if (Features.IsSupported("sudachi_undock"))
                {
                    ini.WriteValue("System", "use_docked_mode\\default", "true");
                    ini.WriteValue("System", "use_docked_mode", "1");
                }

                //disable telemetry
                ini.WriteValue("WebService", "enable_telemetry\\default", "false");
                ini.WriteValue("WebService", "enable_telemetry", "false");

                //remove exit confirmation
                ini.WriteValue("UI", "confirmStop\\default", "false");
                ini.WriteValue("UI", "confirmStop", "2");

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

                //screenshots path
                string screenshotpath = AppConfig.GetFullPath("screenshots").Replace("\\", "/") + "/sudachi";
                if (!Directory.Exists(screenshotpath)) try { Directory.CreateDirectory(screenshotpath); }
                    catch { }
                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as\\default", "false");
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as", "false");
                    ini.WriteValue("UI", "Screenshots\\screenshot_path", screenshotpath);
                }

                // Audio output
                BindQtIniFeature(ini, "Audio", "output_engine", "sudachi_audio_backend", "auto");
                BindQtIniFeature(ini, "System", "sound_index", "sudachi_sound_index", "1");

                // Video drivers                
                BindQtIniFeature(ini, "Renderer", "backend", "sudachi_backend", "1");

                // resolution_setup
                BindQtIniFeature(ini, "Renderer", "resolution_setup", "sudachi_resolution_setup", "2");

                // Aspect ratio
                BindQtIniFeature(ini, "Renderer", "aspect_ratio", "sudachi_ratio", "0");

                // Anisotropic filtering
                BindQtIniFeature(ini, "Renderer", "max_anisotropy", "sudachi_anisotropy", "0");

                // Vsync
                BindQtIniFeature(ini, "Renderer", "use_vsync", "sudachi_use_vsync", "2");

                // anti_aliasing
                BindQtIniFeature(ini, "Renderer", "anti_aliasing", "sudachi_anti_aliasing", "0");

                // scaling_filter
                BindQtIniFeature(ini, "Renderer", "scaling_filter", "sudachi_scaling_filter", "1");

                // GPU accuracy
                BindQtIniFeature(ini, "Renderer", "gpu_accuracy", "sudachi_gpu_accuracy", "1");

                // Asynchronous shaders compilation (hack)
                if (SystemConfig.isOptSet("sudachi_use_asynchronous_shaders") && !SystemConfig.getOptBoolean("sudachi_use_asynchronous_shaders"))
                {
                    ini.WriteValue("Renderer", "use_asynchronous_shaders\\default", "true");
                    ini.WriteValue("Renderer", "use_asynchronous_shaders", "false");
                }
                else
                {
                    ini.WriteValue("Renderer", "use_asynchronous_shaders\\default", "false");
                    ini.WriteValue("Renderer", "use_asynchronous_shaders", "true");
                }

                // ASTC Compression (non compressed by default, use medium for videocards with 6GB of VRAM and low for 2-4GB VRAM)
                BindQtIniFeature(ini, "Renderer", "astc_recompression", "sudachi_astc_recompression", "0");

                //Core options
                BindQtBoolIniFeature(ini, "Core", "use_multi_core", "sudachi_multicore", "true", "false", "true");
                BindQtIniFeature(ini, "Core", "memory_layout_mode", "sudachi_memory", "0");

                // CPU accuracy (auto except if the user chooses otherwise)
                BindQtIniFeature(ini, "Cpu", "cpu_accuracy", "sudachi_cpu_accuracy", "0");

                CreateControllerConfiguration(ini);
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            // Sudachi always returns 0xc0000005 ( null pointer !? )
            if (exitCode == unchecked((int)0xc0000005))
                return 0;
            
            return exitCode;
        }
    }
}
