using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class CemuGenerator : Generator
    {
        public CemuGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private SdlVersion _sdlVersion = SdlVersion.SDL2_0_X;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("cemu");

            string exe = Path.Combine(path, "cemu.exe");
            if (!File.Exists(exe))
                return null;

            rom = TryUnZipGameIfNeeded(system, rom);

            //read m3u if rom is in m3u format
            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                string romPath = Path.GetDirectoryName(rom);
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(romPath, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            // Extract SDL2 version info
            try
            {
                string sdl2 = Path.Combine(path, "SDL2.dll");
                if (FileVersionInfo.GetVersionInfo(exe).ProductMajorPart <= 1 && File.Exists(sdl2))
                    _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);
                else
                    _sdlVersion = SdlJoystickGuidManager.GetSdlVersionFromStaticBinary(exe);
            }
            catch { }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //settings
            SetupConfiguration(path, rom, fullscreen);

            //controller configuration
            CreateControllerConfiguration(path);

            string romdir = Path.GetDirectoryName(rom);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-f");

            commandArray.Add("-g");
            commandArray.Add("\"" + rom +"\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        /// <summary>
        /// UI - console language
        /// Japanese = 0
        /// English = 1
        /// French = 2
        /// German = 3
        /// Italian = 4
        /// Spanish = 5
        /// Chinese = 6
        /// Korean = 7
        /// Dutch = 8
        /// Portuguese = 9
        /// Russian = 10
        /// Taiwanese = 11
        /// </summary>
        /// <returns></returns>
        private string GetDefaultWiiULanguage()
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

        /// <summary>
        /// Configure emulator features (settings.xml)
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfiguration(string path, string rom, bool fullscreen = true)
        {
            string settingsFile = Path.Combine(path, "settings.xml");

            var xdoc = File.Exists(settingsFile) ? XElement.Load(settingsFile) : new XElement("content");

            if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                xdoc.SetElementValue("use_discord_presence", "true");
            else
                xdoc.SetElementValue("use_discord_presence", "false");

            xdoc.SetElementValue("check_update", "false");
            BindFeature(xdoc, "console_language", "wiiu_language", GetDefaultWiiULanguage());

            if (!fullscreen)
                xdoc.SetElementValue("fullscreen", "false");

            // Graphic part of settings file
            var graphic = xdoc.GetOrCreateElement("Graphic");
            BindFeature(graphic, "VSync", "VSync", "1"); // VSYNC (true or false)
            BindFeature(graphic, "api", "video_renderer", "1"); // Graphic driver (0 for OpenGL / 1 for Vulkan)
            BindFeature(graphic, "AsyncCompile", "async_texture", SystemConfig["video_renderer"] != "0" ? "true" : "false"); // Async shader compilation (only if vulkan - true or false)
            BindFeature(graphic, "GX2DrawdoneSync", "accurate_sync", "true"); // Full sync at GX2DrawDone (only if opengl - true or false)
            BindFeature(graphic, "UpscaleFilter", "upscaleFilter", "1"); // Upscale filter (0 to 3)
            BindFeature(graphic, "DownscaleFilter", "downscaleFilter", "0"); // Downscale filter (0 to 3)
            BindFeature(graphic, "FullscreenScaling", "stretch", "0"); // Fullscreen scaling (0 = keep aspect ratio / 1 = stretch)
            
            // Audio part of settings file
            var audio = xdoc.GetOrCreateElement("Audio");
            BindFeature(audio, "api", "audio_renderer", "0"); // Audio driver (0 for DirectSound / 2 for XAudio2 / 3 for Cubeb)
            BindFeature(audio, "TVChannels", "channels", "1"); // Audio channels (0 for Mono / 1 for Stereo / 2 for Surround)

            //Statistics (3 options : full, fps only or none / full shows FPS, CPU & ram usage)
            if (Features.IsSupported("overlay"))
            {
                var overlay = graphic.GetOrCreateElement("Overlay");

                if ((SystemConfig.isOptSet("overlay")) && (SystemConfig["overlay"] == "full"))
                {
                    overlay.SetElementValue("FPS", "true");
                    overlay.SetElementValue("CPUUsage", "true");
                    overlay.SetElementValue("RAMUsage", "true");
                    overlay.SetElementValue("VRAMUsage", "true");
                }
                else if ((SystemConfig.isOptSet("overlay")) && (SystemConfig["overlay"] == "fps"))
                {
                    overlay.SetElementValue("FPS", "true");
                    overlay.SetElementValue("CPUUsage", "false");
                    overlay.SetElementValue("RAMUsage", "false");
                    overlay.SetElementValue("VRAMUsage", "false");
                }
                else
                {
                    overlay.SetElementValue("FPS", "false");
                    overlay.SetElementValue("CPUUsage", "false");
                    overlay.SetElementValue("RAMUsage", "false");
                    overlay.SetElementValue("VRAMUsage", "false");
                }
            }

            // Notifications (2 options : on or off / on shows controller profiles & shader compilation messages)
            if (Features.IsSupported("notifications"))
            {
                var notification = graphic.GetOrCreateElement("Notification");

                if ((SystemConfig.isOptSet("notifications")) && (SystemConfig.getOptBoolean("notifications")))
                {
                    notification.SetElementValue("ControllerProfiles", "true");
                    notification.SetElementValue("ShaderCompiling", "true");
                    notification.SetElementValue("ControllerBattery", "true");
                }
                else
                {
                    notification.SetElementValue("ControllerProfiles", "false");
                    notification.SetElementValue("ShaderCompiling", "false");
                    notification.SetElementValue("ControllerBattery", "false");
                }
            }

            AddPathToGamePaths(Path.GetFullPath(Path.GetDirectoryName(rom)), xdoc);

            // Save xml file
            xdoc.Save(settingsFile);
        }

        private static void AddPathToGamePaths(string romPath, XElement xdoc)
        {
            var gamePaths = xdoc.Element("GamePaths");
            if (gamePaths == null)
                xdoc.Add(gamePaths = new XElement("GamePaths"));

            var paths = gamePaths.Elements("Entry").Select(e => e.Value).Where(e => !string.IsNullOrEmpty(e)).Select(e => Path.GetFullPath(e)).ToList();
            if (!paths.Contains(romPath))
                gamePaths.Add(new XElement("Entry", romPath));
        }
    }
}
