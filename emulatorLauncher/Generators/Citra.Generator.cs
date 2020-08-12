using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
	class CitraGenerator : Generator
	{
		public CitraGenerator()
		{
			DependsOnDesktopResolution = true;
		}

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("citra");

			string exe = Path.Combine(path, "citra-qt.exe");
			if (!File.Exists(exe))
				return null;

			if (core == "citra-sdl")
			{
				exe = Path.Combine(path, "citra.exe");
				if (!File.Exists(exe))
				return null;
				
				return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
					Arguments = "--fullscreen + \"" + rom + "\"",
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
