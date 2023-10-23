using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class EasyRpgGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("easyrpg");

            string exe = Path.Combine(path, "Player.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            rom = this.TryUnZipGameIfNeeded(system, rom, true);

            string savePath = "";

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
            {
                string sp = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(sp)) try { Directory.CreateDirectory(sp); } catch { }

                savePath = "--save-path \"" + sp + "\"";
            }

            if (Path.GetExtension(rom) == ".zip")
                rom = rom + "/" + Path.GetFileNameWithoutExtension(rom);

            // Command lines
            var commandArray = new List<string>();

            commandArray.Add("--project-path");
            commandArray.Add("\"" + rom + "\"");

            if (fullscreen)
                commandArray.Add("--fullscreen");

            commandArray.Add(savePath);

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
