using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
	class RaineGenerator : Generator
	{
		public RaineGenerator()
		{
			DependsOnDesktopResolution = true;
		}

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("raine");

			string exe = Path.Combine(path, "raine.exe");
			if (!File.Exists(exe) || !Environment.Is64BitOperatingSystem)
				exe = Path.Combine(path, "raine32.exe");

			if (!File.Exists(exe))
				return null;

			if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
			{
				rom = Path.GetFileNameWithoutExtension(rom);
			}

				return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
					Arguments = "-n -fs 1 \"" + rom + "\"",
				};
        }
    }
}
