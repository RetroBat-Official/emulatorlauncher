using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
	class DuckstationGenerator : Generator
	{
		public DuckstationGenerator()
		{
			DependsOnDesktopResolution = true;
		}

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("duckstation");

			string exe = Path.Combine(path, "duckstation-qt.exe");
			if (!File.Exists(exe))
				return null;

			if (core == "duckstation-sdl")
			{
				exe = Path.Combine(path, "duckstation-sdl.exe");
				if (!File.Exists(exe))
				return null;
				
				return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
					Arguments = "\"" + rom + "\"",
				};
			
			}

			return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
					Arguments = "\"" + rom + "\"",
				};
        }
    }
}
