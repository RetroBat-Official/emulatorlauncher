using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;

namespace EmulatorLauncher
{
    partial class AzaharGenerator : Generator
    {
        public AzaharGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private SdlVersion _sdlVersion = SdlVersion.SDL2_30;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);

            string exe = Path.Combine(path, "azahar.exe");
            if (!File.Exists(exe))
                return null;

            // Ensure user folder exists
            string userFolder = Path.Combine(path, "user");
            if (!Directory.Exists(userFolder)) try { Directory.CreateDirectory(userFolder); }
                catch { }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            string sdl2 = Path.Combine(path, "SDL2.dll");
            if (File.Exists(sdl2))
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);

            string[] extensions = new string[] { ".3ds", ".3dsx", ".elf", ".axf", ".cci", ".cxi", ".app" };
            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip" || Path.GetExtension(rom).ToLowerInvariant() == ".7z" || Path.GetExtension(rom).ToLowerInvariant() == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            SetupConfigurationAzahar(path, rom, fullscreen);

            if (fullscreen)
            {
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                _resolution = resolution;
                if (_bezelFileInfo != null && _bezelFileInfo.PngFile != null)
                    SimpleLogger.Instance.Info("[INFO] Bezel file selected : " + _bezelFileInfo.PngFile);
            }

            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
                rom = File.ReadAllText(rom);

            List<string> commandArray = new List<string>();
            if (fullscreen)
                commandArray.Add("-f");
            
            //commandArray.Add("-g");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfigurationAzahar(string path, string rom, bool fullscreen = true)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            string userconfigPath = Path.Combine(path, "user", "config");
            if (!Directory.Exists(userconfigPath))
                try { Directory.CreateDirectory(userconfigPath); } catch {}

            string conf = Path.Combine(userconfigPath, "qt-config.ini");
            using (var ini = new IniFile(conf))
            {
                SimpleLogger.Instance.Info("[INFO] Writing Azahar configuration file: " + conf);

                // Define rom path
                string romPath = Path.GetDirectoryName(rom);

                if (!string.IsNullOrEmpty(romPath))
                {
                    ini.WriteValue("UI", "Paths\\gamedirs\\3\\path", romPath.Replace("\\", "/"));
                    ini.WriteValue("UI", "Paths\\gamedirs\\3\\deep_scan\\default", "false");
                    ini.WriteValue("UI", "Paths\\gamedirs\\3\\deep_scan", "true");
                }
                
                int gameDirsSize = ini.GetValue("UI", "Paths\\gamedirs\\size").ToInteger();
                if (gameDirsSize < 3)
                    ini.WriteValue("UI", "Paths\\gamedirs\\size", "3");
                
                // Write settings
                ini.WriteValue("Miscellaneous", "check_for_update_on_start\\default", "false");
                ini.WriteValue("Miscellaneous", "check_for_update_on_start", "false");

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

                string emuNandPath = Path.Combine(AppConfig.GetFullPath("saves"), "3ds", "azahar", "nand");
                if (!Directory.Exists(emuNandPath)) try { Directory.CreateDirectory(emuNandPath); }
                    catch { }
                ini.WriteValue("Data%20Storage", "nand_directory\\default", "false");
                ini.WriteValue("Data%20Storage", "nand_directory", emuNandPath.Replace("\\", "/"));

                // Write nand settings (language)
                string nandPath = Path.Combine(emuNandPath, "data", "00000000000000000000000000000000", "sysdata", "00010017", "00000000", "config");
                if (File.Exists(nandPath))
                    Write3DSnand(nandPath);

                string sdmcPath = Path.Combine(AppConfig.GetFullPath("saves"), "3ds", "azahar", "sdmc");
                if (!Directory.Exists(sdmcPath)) try { Directory.CreateDirectory(sdmcPath); }
                    catch { }
                ini.WriteValue("Data%20Storage", "sdmc_directory\\default", "false");
                ini.WriteValue("Data%20Storage", "sdmc_directory", sdmcPath.Replace("\\", "/"));

                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "azahar");
                if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                    catch { }
                ini.WriteValue("UI", "Paths\\screenshotPath\\default", "false");
                ini.WriteValue("UI", "Paths\\screenshotPath", screenshotPath.Replace("\\", "/"));

                ini.WriteValue("UI", "fullscreen\\default", fullscreen ? "false" : "true");
                ini.WriteValue("UI", "fullscreen", fullscreen ? "true" : "false");

                ini.WriteValue("UI", "confirmClose\\default", "false");
                ini.WriteValue("UI", "confirmClose", "false");

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

                if (Features.IsSupported("azahar_resolution_factor"))
                {
                    if (SystemConfig.isOptSet("azahar_resolution_factor"))
                    {
                        ini.WriteValue("Renderer", "resolution_factor\\default", SystemConfig["azahar_resolution_factor"] == "1" ? "true" : "false");
                        ini.WriteValue("Renderer", "resolution_factor", SystemConfig["azahar_resolution_factor"]);
                    }
                    else
                    {
                        ini.WriteValue("Renderer", "resolution_factor\\default", "true");
                        ini.WriteValue("Renderer", "resolution_factor", "1");
                    }
                }

                if (Features.IsSupported("azahar_texture_filter"))
                {
                    if (SystemConfig.isOptSet("azahar_texture_filter"))
                    {
                        ini.WriteValue("Renderer", "texture_filter\\default", "false");
                        ini.WriteValue("Renderer", "texture_filter", SystemConfig["azahar_texture_filter"]);
                    }
                    else
                    {
                        ini.WriteValue("Renderer", "texture_filter\\default", "true");
                        ini.WriteValue("Renderer", "texture_filter", "0");
                    }
                }

                if (Features.IsSupported("azahar_layout_option"))
                {
                    if (SystemConfig.isOptSet("azahar_layout_option"))
                    {
                        ini.WriteValue("Layout", "layout_option\\default", "false");
                        ini.WriteValue("Layout", "layout_option", SystemConfig["azahar_layout_option"]);
                        SimpleLogger.Instance.Info("[INFO] Setting layout option to : " + SystemConfig["azahar_layout_option"]);
                    }
                    else
                    {
                        ini.WriteValue("Layout", "layout_option\\default", "true");
                        ini.WriteValue("Layout", "layout_option", "0");
                    }
                }

                if (Features.IsSupported("azahar_swap_screen"))
                {
                    if (SystemConfig.isOptSet("azahar_swap_screen") && SystemConfig.getOptBoolean("azahar_swap_screen"))
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
                if (SystemConfig.isOptSet("azahar_region_value") && !string.IsNullOrEmpty(SystemConfig["azahar_region_value"]) && SystemConfig["azahar_region_value"] != "-1")
                {
                    ini.WriteValue("System", "region_value\\default", "false");
                    ini.WriteValue("System", "region_value", SystemConfig["azahar_region_value"]);
                }
                else if (Features.IsSupported("azahar_region_value"))
                {
                    ini.WriteValue("System", "region_value\\default", "true");
                    ini.WriteValue("System", "region_value", "-1");
                }

                // Custom textures
                if (SystemConfig.isOptSet("azahar_custom_textures") && SystemConfig.getOptBoolean("azahar_custom_textures"))
                {
                    ini.WriteValue("Utility", "custom_textures\\default", "false");
                    ini.WriteValue("Utility", "custom_textures", "true");
                    SimpleLogger.Instance.Info("[INFO] Custom textures enabled.");
                }
                else if (Features.IsSupported("azahar_custom_textures"))
                {
                    ini.WriteValue("Utility", "custom_textures\\default", "true");
                    ini.WriteValue("Utility", "custom_textures", "false");
                }

                if (SystemConfig.isOptSet("azahar_PreloadTextures") && SystemConfig.getOptBoolean("azahar_PreloadTextures"))
                {
                    ini.WriteValue("Utility", "preload_textures\\default", "false");
                    ini.WriteValue("Utility", "preload_textures", "true");
                }
                else if (Features.IsSupported("azahar_PreloadTextures"))
                {
                    ini.WriteValue("Utility", "preload_textures\\default", "true");
                    ini.WriteValue("Utility", "preload_textures", "false");
                }

                // Renderer
                BindQtIniFeature(ini, "Renderer", "graphics_api", "graphics_api", "1");

                // Bezel adjustment for default_nocurve and default_curve
                List<string> bezelAdjust = new List<string>() { "default_curve", "default_curve_night", "default_nocurve", "default_nocurve_night" };
                if (bezelAdjust.Contains(SystemConfig["bezel"]))
                {
                    ini.WriteValue("Layout", "layout_option\\default", "false");
                    ini.WriteValue("Layout", "layout_option", "6");

                    decimal topPosX = 562m / 1920m;
                    decimal newTopPosX = (topPosX * (_resolution == null? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalTopPosx = (int)newTopPosX;

                    decimal topPosY = 66m / 1080m;
                    decimal newTopPosY = (topPosY * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalTopPosY = (int)newTopPosY;

                    decimal topWidth = 796m / 1920m;
                    decimal newTopWidth = (topWidth * (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalTopWidth = (int)newTopWidth;

                    decimal topHeight = 476m / 1080m;
                    decimal newTopHeight = (topHeight * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalTopHeight = (int)newTopHeight;

                    decimal bottomPosX = 648m / 1920m;
                    decimal newBottomPosX = (bottomPosX * (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalBottomPosx = (int)newBottomPosX;

                    decimal bottomPosY = 554m / 1080m;
                    decimal newBottomPosY = (bottomPosY * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalBottomPosY = (int)newBottomPosY;

                    decimal bottomWidth = 624m / 1920m;
                    decimal newBottomWidth = (bottomWidth * (_resolution == null ? ScreenResolution.CurrentResolution.Width : _resolution.Width));
                    int finalBottomWidth = (int)newBottomWidth;

                    decimal bottomHeight = 466m / 1080m;
                    decimal newBottomHeight = (bottomHeight * (_resolution == null ? ScreenResolution.CurrentResolution.Height : _resolution.Height));
                    int finalBottomHeight = (int)newBottomHeight;

                    ini.WriteValue("Layout", "custom_top_x\\default", "false");
                    ini.WriteValue("Layout", "custom_top_x", finalTopPosx.ToString());

                    ini.WriteValue("Layout", "custom_top_y\\default", "false");
                    ini.WriteValue("Layout", "custom_top_y", finalTopPosY.ToString());

                    ini.WriteValue("Layout", "custom_top_width\\default", "false");
                    ini.WriteValue("Layout", "custom_top_width", finalTopWidth.ToString());

                    ini.WriteValue("Layout", "custom_top_height\\default", "false");
                    ini.WriteValue("Layout", "custom_top_height", finalTopHeight.ToString());

                    ini.WriteValue("Layout", "custom_bottom_x\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_x", finalBottomPosx.ToString());

                    ini.WriteValue("Layout", "custom_bottom_y\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_y", finalBottomPosY.ToString());

                    ini.WriteValue("Layout", "custom_bottom_width\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_width", finalBottomWidth.ToString());

                    ini.WriteValue("Layout", "custom_bottom_height\\default", "false");
                    ini.WriteValue("Layout", "custom_bottom_height", finalBottomHeight.ToString());
                }
            }
        }

        private void Write3DSnand(string path)
        {
            if (!File.Exists(path))
                return;

            SimpleLogger.Instance.Info("[Generator] Writing to 3DS nand file.");

            int langId;

            if (SystemConfig.isOptSet("n3ds_language") && !string.IsNullOrEmpty(SystemConfig["n3ds_language"]))
                langId = SystemConfig["n3ds_language"].ToInteger();
            else
                langId = Get3DSLangFromEnvironment();

            // Read nand file
            byte[] bytes = File.ReadAllBytes(path);

            var toSet = new byte[] { (byte)langId };
            for (int i = 0; i < toSet.Length; i++)
            {
                bytes[128] = toSet[i];
                bytes[272] = toSet[i];
            }

            File.WriteAllBytes(path, bytes);
        }

        private int Get3DSLangFromEnvironment()
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

            SimpleLogger.Instance.Info("[Generator] Getting language from RetroBat language.");

            // Special case for Taiwanese which is zh_TW
            if (SystemConfig["Language"] == "zh_TW")
                return 11;

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out int ret))
                    return ret;
            }

            return 1;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}