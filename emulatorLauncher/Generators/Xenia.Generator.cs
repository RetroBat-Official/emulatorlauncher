using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class XeniaGenerator : Generator
    {
        private bool _canary = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "xenia-canary" || core == "xenia-canary") ? "xenia-canary" : "xenia";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("xenia");

            string exe = Path.Combine(path, "xenia.exe");
            if (!File.Exists(exe))
            {
                _canary = true;
                exe = Path.Combine(path, "xenia-canary.exe");

                if (!File.Exists(exe))
                    exe = Path.Combine(path, "xenia_canary.exe");
            }

            if (!File.Exists(exe))
                return null;
			
			string romdir = Path.GetDirectoryName(rom);
			
			if (Path.GetExtension(rom).ToLower() == ".m3u")
                {
                    rom = File.ReadAllText(rom);
                    rom = Path.Combine(romdir, rom.Substring(1));
                }

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "--fullscreen \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }
    }
}
