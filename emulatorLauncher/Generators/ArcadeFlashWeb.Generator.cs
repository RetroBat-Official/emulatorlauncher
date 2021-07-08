using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
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

            List<string> args = new List<string>();

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig.GetFullPath("saves")))
            {
                string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                    catch { }

                args.Add("-savedataflash:\"" + savePath + "\"");
            }

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                args.Add("-picturesfolder:\"" + AppConfig.GetFullPath("screenshots") + "\"");

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-fullscreen -nodatetime -source:\"" + rom + "\" " + string.Join(" ", args.ToArray()),
            };
        }

    }
}
