using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;
using emulatorLauncher.Tools;
using System.Threading;

namespace emulatorLauncher
{
    class FpinballGenerator : Generator
    {
        string _bam;
        string _rom;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("fpinball");

            _rom = rom;

            if ("bam".Equals(emulator, StringComparison.InvariantCultureIgnoreCase) || "bam".Equals(core, StringComparison.InvariantCultureIgnoreCase))
                _bam = Path.Combine(path, "BAM", "FPLoader.exe");

            string exe = Path.Combine(path, "Future Pinball.exe");
            if (!File.Exists(exe))
            {
                exe = Path.Combine(path, "FuturePinball.exe");
                if (!File.Exists(exe))
                    return null;
            }

            if (_bam != null && File.Exists(_bam))
                SetAsAdmin(_bam);

            SetAsAdmin(exe);
            SetupOptions(resolution);

            return new ProcessStartInfo()
            {
                FileName = _bam != null && File.Exists(_bam) ? _bam : exe,
                Arguments = "/open \"" + rom + "\" /play /exit",            
            };
        }

        public override void RunAndWait(ProcessStartInfo path)
        {
            Process process = null;

            if (_bam != null && File.Exists(_bam))
            {
                Process.Start(path);

                int tickCount = Environment.TickCount;
                string fileNameWithoutExtension = "Future Pinball";

                process = Process.GetProcessesByName(fileNameWithoutExtension).FirstOrDefault<Process>();
                while (process == null && (Environment.TickCount - tickCount < 1000))
                {
                    process = Process.GetProcessesByName(fileNameWithoutExtension).FirstOrDefault<Process>();
                    if (process == null)
                        Thread.Sleep(10);
                }
            }
            else
                process = Process.Start(path);

            if (process != null)
                process.WaitForExit();
        }

        public override void Cleanup()
        {
            PerformBamCapture();
            base.Cleanup();
        }

        private void PerformBamCapture()
        {
            if (_bam != null && !File.Exists(_bam))
                return;

            string bamPng = Path.Combine(Path.GetDirectoryName(_bam), Path.ChangeExtension(Path.GetFileName(_rom), ".png"));
            if (File.Exists(bamPng))
            {
                ScreenCapture.AddImageToGameList(_rom, bamPng);

                try { File.Delete(bamPng); }
                catch { }
            }
        }

        private static void SetAsAdmin(string path)
        {
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", true);
            if (regKeyc != null)
            {
                if (regKeyc.GetValue(path) == null)
                    regKeyc.SetValue(path, "~ RUNASADMIN");

                regKeyc.Close();
            }
        }

        private static void SetupOptions(ScreenResolution resolution)
        {
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
                regKeyc = regKeyc.CreateSubKey("Future Pinball").CreateSubKey("GamePlayer");

            if (regKeyc != null)
            {
                regKeyc.SetValue("FullScreen", 1);

                if (resolution != null)
                {
                    regKeyc.SetValue("Width", resolution.Width);
                    regKeyc.SetValue("Height", resolution.Height);
                    regKeyc.SetValue("BitsPerPixel", resolution.BitsPerPel);
                }
                else
                {
                    regKeyc.SetValue("Height", Screen.PrimaryScreen.Bounds.Height);
                    regKeyc.SetValue("Width", Screen.PrimaryScreen.Bounds.Width);
                    regKeyc.SetValue("BitsPerPixel", Screen.PrimaryScreen.BitsPerPixel);
                }

                if (regKeyc.GetValue("DefaultCamera") == null)
                    regKeyc.SetValue("DefaultCamera", 1);

                if (regKeyc.GetValue("CameraFollowsTheBall") == null)
                    regKeyc.SetValue("CameraFollowsTheBall", 0);

                //  if (regKeyc.GetValue("AspectRatio") == null && Screen.PrimaryScreen.Bounds.Height == 1080 && Screen.PrimaryScreen.Bounds.Width == 1920)
                //      regKeyc.SetValue("AspectRatio", 169);

                regKeyc.Close();
            }
        }
    }
}
