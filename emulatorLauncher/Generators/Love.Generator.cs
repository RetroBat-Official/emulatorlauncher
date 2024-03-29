﻿using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class LoveGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("love");

            string exe = Path.Combine(path, "love.exe");
            if (!File.Exists(exe))
                return null;
				
				
			if (Path.GetExtension(rom).ToLower() == ".exe")
			{
				path = Path.GetDirectoryName(rom);
				
				if (!File.Exists(rom))
                return null;

                return new ProcessStartInfo()
                {
					FileName = rom,
					WorkingDirectory = path
                };

			}
			
			return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "\"" + rom + "\"",
                WorkingDirectory = path,
            };
        }
    }
}
