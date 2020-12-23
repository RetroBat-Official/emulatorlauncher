using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class AppleWinGenerator : Generator
    {
        private libRetro.LibRetroGenerator.BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("applewin");

            string exe = Path.Combine(path, "applewin.exe");
            if (!File.Exists(exe))
                return null;

            _bezelFileInfo = libRetro.LibRetroGenerator.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-f -d1 \"" + rom + "\"",
            };
        }

        public override void RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();
        }

    }
}
