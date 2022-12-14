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

        private SdlVersion _sdlVersion = SdlVersion.Current;
        
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
            
            SetupConfiguration(path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-f -g \"" + rom + "\"",
            };
        }

        private void SetupConfiguration(string path)
        {
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");

            using (var ini = new IniFile(conf))
            {
                ini.WriteValue("UI", "fullscreen\\default", "false");
                ini.WriteValue("UI", "fullscreen", "true");

                ini.WriteValue("UI", "use_docked_mode\\default", "true");
                ini.WriteValue("UI", "use_docked_mode", "true");

                CreateControllerConfiguration(ini);

                if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                {
                    ini.WriteValue("UI", "Screenshots\\enable_screenshot_save_as", "false");
                    ini.WriteValue("UI", "Screenshots\\screenshot_path", AppConfig.GetFullPath("screenshots"));
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
