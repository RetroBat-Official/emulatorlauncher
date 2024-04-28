using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class RuffleGenerator : Generator
    {
        public RuffleGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("ruffle");

            string exe = Path.Combine(path, "ruffle.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            var commandArray = new List<string>
            {
                "--graphics"
            };
            if (SystemConfig.isOptSet("ruffle_renderer") && !string.IsNullOrEmpty(SystemConfig["ruffle_renderer"]))
                commandArray.Add(SystemConfig["ruffle_renderer"]);
            else
                commandArray.Add("default");

            commandArray.Add("--force-align");

            if (SystemConfig.isOptSet("ruffle_gui") && SystemConfig.getOptBoolean("ruffle_gui"))
            {
                commandArray.Add("--width");
                commandArray.Add((resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString());
                commandArray.Add("--height");
                commandArray.Add((resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString());
                fullscreen = false;
            }
            else if (fullscreen)
                commandArray.Add("--fullscreen");

            commandArray.Add("-q");

            if (SystemConfig.isOptSet("ruffle_quality") && !string.IsNullOrEmpty(SystemConfig["ruffle_quality"]))
                commandArray.Add(SystemConfig["ruffle_quality"]);
            else
                commandArray.Add("high");

                commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            if (fullscreen)
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = args,
                };
            }
            else
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = args,
                    WindowStyle = ProcessWindowStyle.Maximized,
                };
            }
        }
    }
}
