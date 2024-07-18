using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
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
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("applewin");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "applewin.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            var versionInfo = FileVersionInfo.GetVersionInfo(exe);
            
            var version = versionInfo.ProductVersion;
            if (!string.IsNullOrEmpty(version))
                WriteApple2Option("Version", version.Replace(",", ".").Replace(" ", ""));

            var commandArray = new List<string>();
            if (fullscreen)
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

            var bezels = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            // Check if it's retrobat version
            if (!string.IsNullOrEmpty(versionInfo.FileDescription) && versionInfo.FileDescription.Contains("Retrobat"))
            {
                // Disable internal effects ( scanlines )
                WriteApple2Option("Video Style", "0");

                commandArray.Add("-opengl");

                usingReshader = ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, emulator, bezels != null);
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

            // Treatment of multi-discs games
            List<string> disks = new List<string>();
            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                string dskPath = Path.GetDirectoryName(rom);

                foreach (var line in File.ReadAllLines(rom))
                {
                    string dsk = Path.Combine(dskPath, line);
                    if (File.Exists(dsk))
                        disks.Add(dsk);
                    else
                        throw new ApplicationException("File '" + Path.Combine(dskPath, line) + "' does not exist");
                }

                if (disks.Count == 0)
                    throw new ApplicationException("m3u file does not contain any game file.");

                else if (disks.Count == 1)
                {
                    commandArray.Add("-d1");
                    commandArray.Add("\"" + disks[0] + "\"");
                }

                else
                {
                    commandArray.Add("-d1");
                    commandArray.Add("\"" + disks[0] + "\"");
                    commandArray.Add("-d2");
                    commandArray.Add("\"" + disks[1] + "\"");
                }
            }
            else
            {
                commandArray.Add("-d1");
                commandArray.Add("\"" + rom + "\"");
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void BindAppleWinFeature(string settingName, string featureName, string defaultValue, bool force = false)
        {
            if (force || Features.IsSupported(featureName))
                WriteApple2Option(settingName, SystemConfig.GetValueOrDefault(featureName, defaultValue));
        }

        private void WriteApple2Option(string name, string value)
        {
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\AppleWin\CurrentVersion\Configuration", true) ?? Registry.CurrentUser.CreateSubKey(@"SOFTWARE\AppleWin\CurrentVersion\Configuration");
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

            bezel?.Dispose();

            return ret;
        }

    }
}
