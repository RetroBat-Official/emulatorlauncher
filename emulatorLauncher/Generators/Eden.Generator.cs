using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace EmulatorLauncher
{
    partial class EdenGenerator : Generator
    {
        public EdenGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _gamedirsIniPath;
        private string _gamedirsSize;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);

            string exe = Path.Combine(path, "eden.exe");
            if (!File.Exists(exe))
                return null;

            // Ensure user folder exists
            string userFolder = Path.Combine(path, "user");
            if (!Directory.Exists(userFolder)) try { Directory.CreateDirectory(userFolder); }
                catch { }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfigurationEden(path, rom, fullscreen);

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

        private void SetupConfigurationEden(string path, string rom, bool fullscreen)
        {
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");

            using (var ini = new IniFile(conf, IniOptions.KeepEmptyValues))
            {
                ini.WriteValue("UI", "check_for_updates\\default", "false");
                ini.WriteValue("UI", "check_for_updates", "false");

                // Set up paths
                bool mutualize = SystemConfig.getOptBoolean("yuzu_mutualize");
                if (mutualize)
                {
                    var sharedSavesPath = Path.Combine(AppConfig.GetFullPath("saves"), "switch");
                    FileTools.TryCreateDirectory(sharedSavesPath);

                    ini.WriteValue("Data%20Storage", "save_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "save_directory", sharedSavesPath.Replace("\\", "/") + "/");
                }
                else
                {
                    ini.WriteValue("Data%20Storage", "save_directory\\default", "true");
                    ini.WriteValue("Data%20Storage", "save_directory", "");
                }

                string sdmcPath = Path.Combine(path, "user", "sdmc");
                if (FileTools.TryCreateDirectory(sdmcPath))
                {
                    ini.WriteValue("Data%20Storage", "sdmc_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "sdmc_directory", sdmcPath.Replace("\\", "/"));
                }

                string nandPath = Path.Combine(path, "user", "nand");
                if (FileTools.TryCreateDirectory(nandPath))
                {
                    ini.WriteValue("Data%20Storage", "nand_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "nand_directory", nandPath.Replace("\\", "/"));
                }

                string dumpPath = Path.Combine(path, "user", "dump");
                if (FileTools.TryCreateDirectory(dumpPath))
                {
                    ini.WriteValue("Data%20Storage", "dump_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "dump_directory", dumpPath.Replace("\\", "/"));
                }

                string loadPath = Path.Combine(path, "user", "load");
                FileTools.TryCreateDirectory(loadPath);
                if (Directory.Exists(loadPath))
                {
                    ini.WriteValue("Data%20Storage", "load_directory\\default", "false");
                    ini.WriteValue("Data%20Storage", "load_directory", loadPath.Replace("\\", "/"));
                }

                ini.WriteValue("System", "language_index\\default", "false");
                if (SystemConfig.isOptSet("eden_language") && !string.IsNullOrEmpty(SystemConfig["eden_language"]))
                    ini.WriteValue("System", "language_index", SystemConfig["eden_language"]);
                else
                    ini.WriteValue("System", "language_index", GetDefaultswitchLanguage());

                if (SystemConfig.isOptSet("eden_region_value") && !string.IsNullOrEmpty(SystemConfig["eden_region_value"]) && SystemConfig["eden_region_value"] != "1")
                {
                    ini.WriteValue("System", "region_index\\default", "false");
                    ini.WriteValue("System", "region_index", SystemConfig["eden_region_value"]);
                }
                else if (Features.IsSupported("eden_region_value"))
                {
                    ini.WriteValue("System", "region_index\\default", "true");
                    ini.WriteValue("System", "region_index", "1");
                }

                if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                {
                    ini.WriteValue("UI", "enable_discord_presence\\default", "false");
                    ini.WriteValue("UI", "enable_discord_presence", "true");
                }
                else
                {
                    ini.WriteValue("UI", "enable_discord_presence\\default", "true");
                    ini.WriteValue("UI", "enable_discord_presence", "false");
                }

                ini.WriteValue("UI", "fullscreen\\default", fullscreen ? "false" : "true");
                ini.WriteValue("UI", "fullscreen", fullscreen ? "true" : "false");
                ini.WriteValue("Renderer", "fullscreen_mode\\default", SystemConfig.getOptBoolean("exclusivefs") ? "false" : "true");
                ini.WriteValue("Renderer", "fullscreen_mode", SystemConfig.getOptBoolean("exclusivefs") ? "1" : "0");

                ini.WriteValue("UI", "hideInactiveMouse\\default", "true");
                ini.WriteValue("UI", "hideInactiveMouse", "true");

                ini.WriteValue("UI", "pauseWhenInBackground\\default", "false");
                ini.WriteValue("UI", "pauseWhenInBackground", "true");

                if (SystemConfig.isOptSet("eden_controller_applet") && !SystemConfig.getOptBoolean("eden_controller_applet"))
                {
                    ini.WriteValue("UI", "disableControllerApplet\\default", "true");
                    ini.WriteValue("UI", "disableControllerApplet", "false");
                }
                else if (Features.IsSupported("eden_controller_applet"))
                {
                    ini.WriteValue("UI", "disableControllerApplet\\default", "false");
                    ini.WriteValue("UI", "disableControllerApplet", "true");
                }

                if (SystemConfig.isOptSet("eden_undock") && SystemConfig.getOptBoolean("eden_undock"))
                {
                    ini.WriteValue("System", "use_docked_mode\\default", "false");
                    ini.WriteValue("System", "use_docked_mode", "0");
                }
                else if (Features.IsSupported("eden_undock"))
                {
                    ini.WriteValue("System", "use_docked_mode\\default", "true");
                    ini.WriteValue("System", "use_docked_mode", "1");
                }

                ini.WriteValue("System", "hide_nca_verification_popup\\default", "false");
                ini.WriteValue("System", "hide_nca_verification_popup", "true");

                ini.WriteValue("UI", "confirmStop\\default", "false");
                ini.WriteValue("UI", "confirmStop", "2");

                string romPath = Path.GetDirectoryName(rom);
                ini.WriteValue("UI", "Paths\\gamedirs\\4\\path", romPath.Replace("\\", "/"));

                // Set gamedirs count to 4
                var gameDirsSize = ini.GetValue("UI", "Paths\\gamedirs\\size");
                if (gameDirsSize.ToInteger() != 4)
                {
                    _gamedirsIniPath = conf;
                    _gamedirsSize = gameDirsSize;
                    ini.WriteValue("UI", "Paths\\gamedirs\\size", "4");
                }

                string screenshotpath = AppConfig.GetFullPath("screenshots").Replace("\\", "/") + "/eden";
                if (!Directory.Exists(screenshotpath)) try { Directory.CreateDirectory(screenshotpath); }
                    catch { }
                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as\\default", "false");
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as", "false");
                    ini.WriteValue("UI", "Screenshots\\screenshot_path", screenshotpath);
                }

                BindQtIniFeature(ini, "Audio", "output_engine", "eden_audio_backend", "0");
                BindQtIniFeature(ini, "System", "sound_index", "eden_sound_index", "1");            
                BindQtIniFeature(ini, "Renderer", "backend", "eden_backend", "1");
                BindQtIniFeature(ini, "Renderer", "resolution_setup", "eden_resolution_setup", "3");
                BindQtIniFeature(ini, "Renderer", "aspect_ratio", "eden_ratio", "0");
                BindQtIniFeature(ini, "Renderer", "max_anisotropy", "eden_anisotropy", "0");
                BindQtIniFeature(ini, "Renderer", "use_vsync", "eden_use_vsync", "2");
                BindQtIniFeature(ini, "Renderer", "anti_aliasing", "eden_anti_aliasing", "0");
                BindQtIniFeature(ini, "Renderer", "scaling_filter", "eden_scaling_filter", "1");
                BindQtIniFeature(ini, "Renderer", "gpu_accuracy", "eden_gpu_accuracy", "1");
                
                if (SystemConfig.isOptSet("eden_use_asynchronous_shaders") && !SystemConfig.getOptBoolean("eden_use_asynchronous_shaders"))
                {
                    ini.WriteValue("Renderer", "use_asynchronous_shaders\\default", "true");
                    ini.WriteValue("Renderer", "use_asynchronous_shaders", "false");
                }
                else
                {
                    ini.WriteValue("Renderer", "use_asynchronous_shaders\\default", "false");
                    ini.WriteValue("Renderer", "use_asynchronous_shaders", "true");
                }

                BindQtIniFeature(ini, "Renderer", "astc_recompression", "eden_astc_recompression", "0");
                BindQtBoolIniFeature(ini, "Core", "use_multi_core", "eden_multicore", "true", "false", "true");
                BindQtIniFeature(ini, "Core", "memory_layout_mode", "eden_memory", "0");
                BindQtIniFeature(ini, "Cpu", "cpu_accuracy", "eden_cpu_accuracy", "0");

                CreateControllerConfiguration(ini);
            }
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

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            // Eden always returns 0xc0000005 ( null pointer !? )
            if (exitCode == unchecked((int)0xc0000005))
                return 0;
            
            return exitCode;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            // Restore value for Paths\\gamedirs\\size
            // As it's faster to launch a Eden game when there's no folder set            

            if (!_norawinput)
            {
                try { Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", null, EnvironmentVariableTarget.User); } catch { }
            }

            if (string.IsNullOrEmpty(_gamedirsIniPath) || string.IsNullOrEmpty(_gamedirsSize))
                return;

            using (var ini = new IniFile(_gamedirsIniPath))
                ini.WriteValue("UI", "Paths\\gamedirs\\size", _gamedirsSize);
        }
    }
}
