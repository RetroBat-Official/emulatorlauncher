using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class OricutronGenerator : Generator
	{
		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{
			string path = AppConfig.GetFullPath("oricutron");

            string exe = Path.Combine(path, "oricutron.exe");
			if (!File.Exists(exe))
				return null;

            if (Path.GetExtension(rom).ToLower() == ".dsk")
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "--fullscreen --disk \"" + rom + "\"",
                };
            }

			return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
                    Arguments = "--fullscreen --turbotape on --tape \"" + rom + "\"",
				};
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "simcoupe", InputKey.hotkey | InputKey.start, "(%{KILL})");
        }       
    }
}
