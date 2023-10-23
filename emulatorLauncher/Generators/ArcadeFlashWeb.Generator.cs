using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class ArcadeFlashWebGenerator : Generator
    {
        public ArcadeFlashWebGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {            
            string path = AppConfig.GetFullPath("ArcadeFlashWeb");

            string exe = Path.Combine(path, "ArcadeFlashWeb.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            List<string> commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-fullscreen");

            commandArray.Add("-nodatetime");
            commandArray.Add("-source:\"" + rom + "\"");

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig.GetFullPath("saves")))
            {
                string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                    catch { }

                commandArray.Add("-savedataflash:\"" + savePath + "\"");
            }

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                commandArray.Add("-picturesfolder:\"" + AppConfig.GetFullPath("screenshots") + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

    }
}
