using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Xml.Linq;
using System.Windows.Forms;
using emulatorLauncher.PadToKeyboard;
using System.Security.Cryptography;
using emulatorLauncher;

namespace emulatorLauncher
{
    class Xm6proGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public Xm6proGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("xm6pro");

			string exe = Path.Combine(path, "XM6.exe");

			if (!File.Exists(exe))
				return null;

			//Applying bezels
			if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
				_bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

			_resolution = resolution;

			//SetupConfiguration(path, rom, system);

			return new ProcessStartInfo()
			{
				FileName = exe,
				WorkingDirectory = path,
				Arguments = "\"" + rom + "\"",
			};

		}

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            var process = Process.Start(path);

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                //get emulator window and set to fullscreen with ALT+ENTER
                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;
                System.Threading.Thread.Sleep(500);
                SendKeys.SendWait("%"+"{ENTER}");
                break;
            }

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
            if (process != null)
                process.WaitForExit();
            if (bezel != null)
                bezel.Dispose();

            process.WaitForExit();
            int exitCode = process.ExitCode;
            return exitCode;
        }

    }
}
