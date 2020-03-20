using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

namespace emulatorLauncher
{
    class FpinballGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, string gameResolution)
        {
            string path = AppConfig.GetFullPath("fpinball");

            string bam = Path.Combine(path, "BAM", "FPLoader.exe");
            string exe = Path.Combine(path, "Future Pinball.exe");
            if (!File.Exists(exe))
            {
                exe = Path.Combine(path, "FuturePinball.exe");
                if (!File.Exists(exe))
                    return null;
            }

            if (File.Exists(bam))
                SetAsAdmin(bam);

            SetAsAdmin(exe);
            SetupOptions();

            return new ProcessStartInfo()
            {
                FileName = File.Exists(bam) ? bam : exe,
                Arguments = "/open \"" + rom + "\" /play /exit",            
            };
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

        private static void SetupOptions()
        {
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
                regKeyc = regKeyc.CreateSubKey("Future Pinball").CreateSubKey("GamePlayer");

            if (regKeyc != null)
            {
                regKeyc.SetValue("FullScreen", 1);
                //  regKeyc.SetValue("Height", Screen.PrimaryScreen.Bounds.Height);
                //  regKeyc.SetValue("Width", Screen.PrimaryScreen.Bounds.Width);
                //  regKeyc.SetValue("BitsPerPixel", Screen.PrimaryScreen.BitsPerPixel);

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
