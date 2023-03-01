using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class NosGbaGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("nosgba");
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("no$gba");

            string exe = Path.Combine(path, "no$gba.exe");
            if (!File.Exists(exe))
                return null;

            SetupConfiguration(path, system);

            var commandArray = new List<string>();
            commandArray.Add("/f");

            if (system == "gba2players")
                commandArray.Add("/2");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args + " " + "\"" + rom + "\"",
            };
        }

        private void SetupConfiguration(string path, string system)
        {
            string conf = Path.Combine(path, "NO$GBA.INI");
            if (!File.Exists(conf))
                return;

            using (var ini = IniFile.FromFile(conf, IniOptions.KeepEmptyLines | IniOptions.KeepEmptyValues | IniOptions.UseSpaceLeft))
            {
                // Link cable setup for gba2players
                if (system == "gba2players")
                    ini.WriteValue("", "Link Cable Type", "= -Automatic");
                else
                    ini.WriteValue("", "Link Cable Type", "= None");

                if (SystemConfig.isOptSet("gba_colors") && !string.IsNullOrEmpty(SystemConfig["gba_colors"]))
                    ini.WriteValue("", "GBA Mode/Colors", SystemConfig["gba_colors"]);
                else if (Features.IsSupported("gba_colors"))
                    ini.WriteValue("", "GBA Mode/Colors", "= GBA SP(backlight)");

                if (SystemConfig.isOptSet("gba_video_output") && !string.IsNullOrEmpty(SystemConfig["gba_video_output"]))
                    ini.WriteValue("", "Video Output", SystemConfig["gba_video_output"]);
                else if (Features.IsSupported("gba_video_output"))
                    ini.WriteValue("", "Video Output", "= 24bit True Color");

                if (SystemConfig.isOptSet("gba_video_renderer") && !string.IsNullOrEmpty(SystemConfig["gba_video_renderer"]))
                    ini.WriteValue("", "3D Renderer", SystemConfig["gba_video_renderer"]);
                else if (Features.IsSupported("gba_video_renderer"))
                    ini.WriteValue("", "3D Renderer", "= nocash");
            }
        }
    }
}
