using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class YuzuGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("yuzu");

            string exe = Path.Combine(path, "yuzu.exe");
            if (!File.Exists(exe))
                return null;
            
            string conf = Path.Combine(path, "user", "config", "qt-config.ini");
            if (File.Exists(conf))
            {
                var ini = new IniFile(conf);
                ini.WriteValue("UI", "fullscreen\\default", "false");
                ini.WriteValue("UI", "fullscreen", "true");
                ini.Save();
            }
            
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-f -g \"" + rom + "\"",
            };
        }
    }
}
