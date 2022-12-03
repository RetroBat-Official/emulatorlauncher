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
    }
}
