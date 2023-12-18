using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class CitraGenerator : Generator
    {
        public CitraGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private SdlVersion _sdlVersion = SdlVersion.SDL2_26;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = emulator;
            string path = AppConfig.GetFullPath(folderName);

            string exe = Path.Combine(path, "citra-qt.exe");
            if (!File.Exists(exe))
                return null;

            bool isCitraCanary = folderName == "citra-canary";
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            string sdl2 = Path.Combine(path, "SDL2.dll");
            if (File.Exists(sdl2))
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);

            SetupConfiguration(path, isCitraCanary, fullscreen);

            List<string> commandArray = new List<string>();
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

        private void SetupConfiguration(string path, bool isCitraCanary = false, bool fullscreen = true)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            string userconfigPath = Path.Combine(path, "user", "config");
            if (!Directory.Exists(userconfigPath))
                Directory.CreateDirectory(userconfigPath);

            string conf = Path.Combine(userconfigPath, "qt-config.ini");
            using (var ini = new IniFile(conf))
            {
                ini.WriteValue("UI", "Updater\\check_for_update_on_start\\default", "false");
                ini.WriteValue("UI", "Updater\\check_for_update_on_start", "false");

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

                ini.WriteValue("Data%20Storage", "use_custom_storage\\default", "false");
                ini.WriteValue("Data%20Storage", "use_custom_storage", "true");

                string citraNandPath = Path.Combine(AppConfig.GetFullPath("saves"), "3ds", "Citra", "nand");
                if (!Directory.Exists(citraNandPath)) try { Directory.CreateDirectory(citraNandPath); }
                    catch { }
                ini.WriteValue("Data%20Storage", "nand_directory\\default", "false");
                ini.WriteValue("Data%20Storage", "nand_directory", citraNandPath.Replace("\\", "/"));

                // Write nand settings (language)
                string nandPath = Path.Combine(citraNandPath, "data", "00000000000000000000000000000000", "sysdata", "00010017", "00000000", "config");
                if (File.Exists(nandPath))
                    Write3DSnand(nandPath);

                string sdmcPath = Path.Combine(AppConfig.GetFullPath("saves"), "3ds", "Citra", "sdmc");
                if (!Directory.Exists(sdmcPath)) try { Directory.CreateDirectory(sdmcPath); }
                    catch { }
                ini.WriteValue("Data%20Storage", "sdmc_directory\\default", "false");
                ini.WriteValue("Data%20Storage", "sdmc_directory", sdmcPath.Replace("\\", "/"));

                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "citra");
                if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                    catch { }
                ini.WriteValue("UI", "Paths\\screenshotPath\\default", "false");
                ini.WriteValue("UI", "Paths\\screenshotPath", screenshotPath.Replace("\\", "/"));

                ini.WriteValue("UI", "Updater\\check_for_update_on_start\\default", "false");
                ini.WriteValue("UI", "Updater\\check_for_update_on_start", "false");

                ini.WriteValue("UI", "fullscreen\\default", fullscreen ? "false" : "true");
                ini.WriteValue("UI", "fullscreen", fullscreen ? "true" : "false");

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
                    {
                        ini.WriteValue("Layout", "layout_option\\default", "false");
                        ini.WriteValue("Layout", "layout_option", SystemConfig["citra_layout_option"]);
                    }
                    else
                    {
                        ini.WriteValue("Layout", "layout_option\\default", "true");
                        ini.WriteValue("Layout", "layout_option", "0");
                    }
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

                // Define console region
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

                // Custom textures
                if (SystemConfig.isOptSet("citra_custom_textures") && SystemConfig.getOptBoolean("citra_custom_textures"))
                {
                    ini.WriteValue("Utility", "custom_textures\\default", "false");
                    ini.WriteValue("Utility", "custom_textures", "true");
                }
                else if (Features.IsSupported("citra_custom_textures"))
                {
                    ini.WriteValue("Utility", "custom_textures\\default", "true");
                    ini.WriteValue("Utility", "custom_textures", "false");
                }

                if (SystemConfig.isOptSet("citra_PreloadTextures") && SystemConfig.getOptBoolean("citra_PreloadTextures"))
                {
                    ini.WriteValue("Utility", "preload_textures\\default", "false");
                    ini.WriteValue("Utility", "preload_textures", "true");
                }
                else if (Features.IsSupported("citra_PreloadTextures"))
                {
                    ini.WriteValue("Utility", "preload_textures\\default", "true");
                    ini.WriteValue("Utility", "preload_textures", "false");
                }

                // Renderer
                BindQtIniFeature(ini, "Renderer", "graphics_api", "graphics_api", "1");
            }
        }

        private void Write3DSnand(string path)
        {
            if (!File.Exists(path))
                return;

            int langId = 1;

            if (SystemConfig.isOptSet("n3ds_language") && !string.IsNullOrEmpty(SystemConfig["n3ds_language"]))
                langId = SystemConfig["n3ds_language"].ToInteger();
            else
                langId = get3DSLangFromEnvironment();

            // Read nand file
            byte[] bytes = File.ReadAllBytes(path);

            var toSet = new byte[] { (byte)langId };
            for (int i = 0; i < toSet.Length; i++)
                bytes[104] = toSet[i];

            File.WriteAllBytes(path, bytes);
        }

        private int get3DSLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, int>()  //OA = 10, OB = 11 (traditional chinese)
            {
                { "jp", 0 },
                { "ja", 0 },
                { "en", 1 },
                { "fr", 2 },
                { "de", 3 },
                { "it", 4 },
                { "es", 5 },
                { "zh", 6 },
                { "ko", 7 },
                { "nl", 8 },
                { "pt", 9 },
                { "ru", 10 },
            };

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return 11;

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                int ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }

            return 1;
        }
    }
}