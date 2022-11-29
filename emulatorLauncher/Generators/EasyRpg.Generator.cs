using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class EasyRpgGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("easyrpg");

            string exe = Path.Combine(path, "Player.exe");
            if (!File.Exists(exe))
                return null;

            rom = this.TryUnZipGameIfNeeded(system, rom, true);

            string savePath = "";

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
            {
                string sp = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(sp)) try { Directory.CreateDirectory(sp); } catch { }

                savePath = " --save-path \"" + sp + "\"";
            }

            if (Path.GetExtension(rom) == ".zip")
                rom = rom + "/" + Path.GetFileNameWithoutExtension(rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "--project-path \"" + rom + "\" --fullscreen" + savePath,
            };
        }
    }
}
