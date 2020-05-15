using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class CxbxGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = null;
            
            if ((core != null && core == "chihiro") || (emulator != null && emulator == "chihiro"))
                path = AppConfig.GetFullPath("chihiro");
            
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-r");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-reloaded");

            string exe = Path.Combine(path, "cxbx.exe");
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
