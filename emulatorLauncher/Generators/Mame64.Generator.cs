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
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("mame");

            string exe = Path.Combine(path, "mame64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "mame32.exe");

            if (!File.Exists(exe))
                return null;
         
            SetupRomPaths(path, rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }

        private static void SetupRomPaths(string path, string rom)
        {
            try
            {
                string iniFile = Path.Combine(path, "mame.ini");
                if (!File.Exists(iniFile))
                    File.WriteAllText(iniFile, Properties.Resources.mame);

                var lines = File.ReadAllLines(iniFile).ToList();
                int idx = lines.FindIndex(l => l.StartsWith("rompath"));
                if (idx >= 0)
                {
                    string romPath = Path.GetDirectoryName(rom);

                    var line = lines[idx];
                    var name = line.Substring(0, 26);
                    var paths = line.Substring(26).Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (!paths.Contains(romPath))
                    {
                        paths.Add(romPath);
                        lines[idx] = name + string.Join(";", paths.ToArray());
                        File.WriteAllLines(iniFile, lines);
                    }
                }
            }
            catch { }
        }
    }
}
