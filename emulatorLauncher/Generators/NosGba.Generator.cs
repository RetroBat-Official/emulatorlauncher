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

            string exe = Path.Combine(path, "no$gba.exe");
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
