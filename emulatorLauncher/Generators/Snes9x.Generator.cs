using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
	class Snes9xGenerator : Generator
	{
		public Snes9xGenerator()
		{
			DependsOnDesktopResolution = true;
		}

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("snes9x");

			string exe = Path.Combine(path, "snes9x-x64.exe");
			if (!File.Exists(exe) || !Environment.Is64BitOperatingSystem)
				exe = Path.Combine(path, "snes9x.exe");

			if (!File.Exists(exe))
				return null;

			return new ProcessStartInfo()
			{
				FileName = exe,
				WorkingDirectory = path,
				Arguments = "-fullscreen \"" + rom + "\"",
			};
			
        }

    }
}
