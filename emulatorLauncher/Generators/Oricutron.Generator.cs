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
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{
			string path = AppConfig.GetFullPath("oricutron");

            string exe = Path.Combine(path, "oricutron.exe");
			if (!File.Exists(exe))
				return null;

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

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


        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            return ret;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "oricutron", InputKey.hotkey | InputKey.start, "(%{KILL})");
        }       
    }
}
