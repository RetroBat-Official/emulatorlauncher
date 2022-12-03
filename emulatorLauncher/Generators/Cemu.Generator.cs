using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Linq;
using System.Xml;

namespace emulatorLauncher
{
    partial class CemuGenerator : Generator
    {
        public CemuGenerator()
        {
            DependsOnDesktopResolution = true;
        }

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

            string settingsFile = Path.Combine(path, "settings.xml");
            if (File.Exists(settingsFile))
            {
                try
                {
                    XDocument settings = XDocument.Load(settingsFile);

                    var fps = settings.Descendants().FirstOrDefault(d => d.Name == "FPS");
                    if (fps != null)
                    {
                        bool showFPS = SystemConfig.isOptSet("showFPS") && SystemConfig.getOptBoolean("showFPS");
                        if (showFPS.ToString().ToLower() != fps.Value)
                        {
                            fps.SetValue(showFPS);
                            settings.Save(settingsFile);
                        }
                    }
                }
                catch { }
            }

            //settings
            SetupConfiguration(path);

            //controller configuration
            CreateControllerConfiguration(path);

            string romdir = Path.GetDirectoryName(rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-f -g \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }

        /// <summary>
        /// Configure emulator features (settings.xml)
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfiguration(string path)
        {
            string settingsFile = Path.Combine(path, "settings.xml");
            var xdoc = XElement.Load(settingsFile);

            //UI - console language
            /*
             * Japanese = 0
             * English = 1
             * French = 2
             * German = 3
             * Italian = 4
             * Spanish = 5
             * Chinese = 6
             * Korean = 7
             * Dutch = 8
             * Portuguese = 9
             * Russian = 10
             * Taiwanese = 11
            */
            if (SystemConfig.isOptSet("wiiu_language") && !string.IsNullOrEmpty(SystemConfig["wiiu_language"]))
                xdoc.Element("console_language").Value = SystemConfig["wiiu_language"];
            else if (Features.IsSupported("wiiu_language"))
                xdoc.Element("console_language").Value = "1";

            //Graphic part of settings file
            var graphic = xdoc.Element("Graphic");

            //VSYNC (true or false)
            if ((SystemConfig.isOptSet("vsync")) && (SystemConfig.getOptBoolean("vsync")))
                graphic.Element("VSync").Value = "true";
            else if (Features.IsSupported("vsync"))
                graphic.Element("VSync").Value = "false";

            //Graphic driver (0 for OpenGL / 1 for Vulkan)
            if (SystemConfig.isOptSet("video_renderer") && !string.IsNullOrEmpty(SystemConfig["video_renderer"]))
                graphic.Element("api").Value = SystemConfig["video_renderer"];
            else if (Features.IsSupported("video_renderer"))
                graphic.Element("api").Value = "1";

            //Async shader compilation (only if vulkan - true or false)
            if ((SystemConfig.isOptSet("async_shader")) && (SystemConfig.getOptBoolean("async_shader")) && (SystemConfig["video_renderer"] == "1"))
                graphic.Element("AsyncCompile").Value = "true";
            else if (Features.IsSupported("async_shader"))
                graphic.Element("AsyncCompile").Value = "false";

            //Full sync at GX2DrawDone (only if opengl - true or false)
            if ((SystemConfig.isOptSet("accurate_sync")) && (SystemConfig.getOptBoolean("accurate_sync")) && (SystemConfig["video_renderer"] == "0"))
                graphic.Element("GX2DrawdoneSync").Value = "true";
            else if (Features.IsSupported("accurate_sync"))
                graphic.Element("GX2DrawdoneSync").Value = "true";

            //Upscale filter (0 to 3)
            if (SystemConfig.isOptSet("upscaleFilter") && !string.IsNullOrEmpty(SystemConfig["upscaleFilter"]))
                graphic.Element("UpscaleFilter").Value = SystemConfig["upscaleFilter"];
            else if (Features.IsSupported("upscaleFilter"))
                graphic.Element("UpscaleFilter").Value = "1";

            //Downscale filter (0 to 3)
            if (SystemConfig.isOptSet("downscaleFilter") && !string.IsNullOrEmpty(SystemConfig["downscaleFilter"]))
                graphic.Element("DownscaleFilter").Value = SystemConfig["downscaleFilter"];
            else if (Features.IsSupported("downscaleFilter"))
                graphic.Element("DownscaleFilter").Value = "0";

            //Fullscreen scaling (0 = keep aspect ratio / 1 = stretch)
            if (SystemConfig.isOptSet("stretch") && (SystemConfig.getOptBoolean("stretch")))
                graphic.Element("FullscreenScaling").Value = "1";
            else if (Features.IsSupported("stretch"))
                graphic.Element("FullscreenScaling").Value = "0";

            //Audio part of settings file
            var audio = xdoc.Element("Audio");

            //Audio driver (0 for DirectSound / 2 for XAudio2 / 3 for Cubeb)
            if (SystemConfig.isOptSet("audio_renderer") && !string.IsNullOrEmpty(SystemConfig["audio_renderer"]))
                audio.Element("api").Value = SystemConfig["audio_renderer"];
            else if (Features.IsSupported("audio_renderer"))
                audio.Element("api").Value = "0";

            //Audio channels (0 for Mono / 1 for Stereo / 2 for Surround)
            if (SystemConfig.isOptSet("channels") && !string.IsNullOrEmpty(SystemConfig["channels"]))
                audio.Element("TVChannels").Value = SystemConfig["channels"];
            else if (Features.IsSupported("channels"))
                audio.Element("TVChannels").Value = "1";

            //Statistics (3 options : full, fps only or none / full shows FPS, CPU & ram usage)
            var overlay = graphic.Element("Overlay");

            if ((SystemConfig.isOptSet("overlay")) && (SystemConfig["overlay"] == "full"))
            {
                overlay.Element("FPS").Value = "true";
                overlay.Element("CPUUsage").Value = "true";
                overlay.Element("RAMUsage").Value = "true";
                overlay.Element("VRAMUsage").Value = "true";
            }
            else if ((SystemConfig.isOptSet("overlay")) && (SystemConfig["overlay"] == "fps"))
            {
                overlay.Element("FPS").Value = "true";
                overlay.Element("CPUUsage").Value = "false";
                overlay.Element("RAMUsage").Value = "false";
                overlay.Element("VRAMUsage").Value = "false";
            }
            else if (Features.IsSupported("overlay"))
            {
                overlay.Element("FPS").Value = "false";
                overlay.Element("CPUUsage").Value = "false";
                overlay.Element("RAMUsage").Value = "false";
                overlay.Element("VRAMUsage").Value = "false";
            }

            //Notifications (2 options : on or off / on shows controller profiles & shader compilation messages)
            var notification = graphic.Element("Notification");

            if ((SystemConfig.isOptSet("notifications")) && (SystemConfig.getOptBoolean("notifications")))
            {
                notification.Element("ControllerProfiles").Value = "true";
                notification.Element("ShaderCompiling").Value = "true";
            }
            else if (Features.IsSupported("notifications"))
            {
                notification.Element("ControllerProfiles").Value = "false";
                notification.Element("ShaderCompiling").Value = "false";
            }

            //Save xml file
            xdoc.Save(settingsFile);
        }
    }
}
