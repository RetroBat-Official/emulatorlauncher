using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;

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
            SetupOptions(gameResolution);

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

        private static void SetupOptions(string gameResolution)
        {
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
                regKeyc = regKeyc.CreateSubKey("Future Pinball").CreateSubKey("GamePlayer");

            if (regKeyc != null)
            {
                regKeyc.SetValue("FullScreen", 1);
                regKeyc.SetValue("Height", Screen.PrimaryScreen.Bounds.Height);
                regKeyc.SetValue("Width", Screen.PrimaryScreen.Bounds.Width);
                regKeyc.SetValue("BitsPerPixel", Screen.PrimaryScreen.BitsPerPixel);

                if (!string.IsNullOrEmpty(gameResolution) && gameResolution != "auto")
                {
                    var values = gameResolution.Split(new char[] { 'x' }, StringSplitOptions.RemoveEmptyEntries);
                    if (values.Length == 4)
                    {
                        int x;
                        if (int.TryParse(values[0], out x))
                            regKeyc.SetValue("Width", x);

                        if (int.TryParse(values[1], out x))
                            regKeyc.SetValue("Height", x);

                        if (int.TryParse(values[2], out x))
                            regKeyc.SetValue("BitsPerPixel", x);
                    }
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
