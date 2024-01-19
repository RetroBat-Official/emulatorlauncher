using System;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class AmigaForeverGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("amigaforever");

            string exe = Path.Combine(path, "AmigaForever.exe");
            if (!File.Exists(exe))
            {
                path = Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%"), "Cloanto", "Amiga Forever");
                exe = Path.Combine(path, "AmigaForever.exe");
            }
            if (!File.Exists(exe))
                return null;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "/f \"" + rom + "\"",
            };
        }
    }
}
