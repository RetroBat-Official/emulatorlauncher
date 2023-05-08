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

            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            if (File.Exists(Path.Combine(path, "oricutron.cfg")))
            {
                var cfg = ConfigFile.FromFile(Path.Combine(path, "oricutron.cfg"));
                cfg["machine"] = "atmos";
                cfg["debug"] = "no";
                cfg["palghosting"] = "no";
                cfg["fullscreen"] = "yes";
                cfg["aratio"] = "no";
                cfg["rendermode"] = "opengl";
                cfg.Save(Path.Combine(path, "oricutron.cfg"), true);
            }
            
            if (Path.GetExtension(rom).ToLower() == ".dsk")
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "--fullscreen --rendermode opengl --disk \"" + rom + "\"",
                };
            }

			return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
                    Arguments = "--fullscreen --rendermode opengl --turbotape on --tape \"" + rom + "\"",
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
