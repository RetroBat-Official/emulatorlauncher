using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class Mame64Generator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mame64");
            if (!string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("mame");

            string exe = Path.Combine(path, "mame64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "mame32.exe");

            if (!File.Exists(exe))
                return null;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }
    }
}
