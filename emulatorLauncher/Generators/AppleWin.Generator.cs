using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace emulatorLauncher
{
    class AppleWinGenerator : Generator
    {
        public AppleWinGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("applewin");

            string exe = Path.Combine(path, "applewin.exe");
            if (!File.Exists(exe))
                return null;

            var versionInfo = FileVersionInfo.GetVersionInfo(exe);
            
            var version = versionInfo.ProductVersion;
            if (!string.IsNullOrEmpty(version))
                WriteApple2Option("Version", version.Replace(",", ".").Replace(" ", ""));

            var commandArray = new List<string>();
            commandArray.Add("-f");
            commandArray.Add("-no-printscreen-dlg");
            commandArray.Add("-alt-enter=toggle-full-screen");

            if (Features.IsSupported("cputype"))
            {
                commandArray.Add("-model");
                commandArray.Add(SystemConfig.GetValueOrDefault("cputype", "apple2e"));
            }

            BindAppleWinFeature("Video Emulation", "screentype", "1");

            WriteApple2Option("Full-screen show subunit status", "0");
            WriteApple2Option("Joystick0 Emu Type v3", Program.Controllers.Any(c => c.WinmmJoystick != null) ? "1" : "2");

            bool usingReshader = false;

            var bezels = BezelFiles.GetBezelFiles(system, rom, resolution);

            // Check if it's retrobat version
            if (!string.IsNullOrEmpty(versionInfo.FileDescription) && versionInfo.FileDescription.Contains("Retrobat"))
            {
                // Disable internal effects ( scanlines )
                WriteApple2Option("Video Style", "0");

                commandArray.Add("-opengl");

                usingReshader = ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, bezels != null);
                if (usingReshader && bezels != null)
                    commandArray.Add("-stretch");
                else
                    commandArray.Add("-no-stretch");
            }

            if (!usingReshader)
            {
                _bezelFileInfo = bezels;
                _resolution = resolution;
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args + " -d1 \"" + rom + "\"",
            };
        }

        private void BindAppleWinFeature(string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                WriteApple2Option(settingName, SystemConfig.GetValueOrDefault(featureName, defaultValue));
        }

        private void WriteApple2Option(string name, string value)
        {
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\AppleWin\CurrentVersion\Configuration", true);
            if (regKeyc == null)
                regKeyc = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AppleWin\CurrentVersion\Configuration");

            if (regKeyc != null)
            {
                regKeyc.SetValue(name, value);
                regKeyc.Close();
            }
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

    }
}
