using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class EDukeGenerator : Generator
    {
        public EDukeGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("eduke32");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "eduke32.exe");
            if (!File.Exists(exe))
                return null;

            string grp = Path.GetFileName(rom);

            SetupConfiguration(path, grp, resolution);

            var commandArray = new List<string>
            {
                "-gamegrp",
                "\"" + grp + "\"",
                "-j",
                "\"" + Path.GetDirectoryName(rom) + "\"",
                "-nosetup"
            };

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private void SetupConfiguration(string path, string grp, ScreenResolution resolution)
        {
            try
            {
                using (IniFile ini = new IniFile(Path.Combine(path, "eduke32.cfg"), IniOptions.UseSpaces))
                {
                    ini.WriteValue("Setup", "ForceSetup", "0");
                    BindBoolIniFeature(ini, "Setup", "NoAutoLoad", "autoload", "0", "1");
                    ini.WriteValue("Setup", "SelectedGRP", "\"" + grp + "\"");
                    ini.WriteValue("Updates", "CheckForUpdates", "0");

                    if (SystemConfig.isOptSet("eduke_video") && SystemConfig["eduke_video"] == "polymer")
                    {
                        ini.WriteValue("Screen Setup", "Polymer", "1");
                        ini.WriteValue("Screen Setup", "ScreenBPP", "32");
                    }
                    else
                    {
                        ini.WriteValue("Screen Setup", "Polymer", "0");
                        BindIniFeature(ini, "Screen Setup", "ScreenBPP", "eduke_video", "32");
                    }

                    if (!SystemConfig.isOptSet("eduke_resolution") && resolution != null)
                    {
                        ini.WriteValue("Screen Setup", "ScreenHeight", resolution.Height.ToString());
                        ini.WriteValue("Screen Setup", "ScreenWidth", resolution.Width.ToString());
                    }
                    else if (SystemConfig.isOptSet("eduke_resolution") && !string.IsNullOrEmpty(SystemConfig["eduke_resolution"]))
                    {
                        string res = SystemConfig["eduke_resolution"];
                        string[] parts = res.Split('x');
                        string width = parts[0];
                        string height = parts[1];
                        ini.WriteValue("Screen Setup", "ScreenHeight", height);
                        ini.WriteValue("Screen Setup", "ScreenWidth", width);
                    }
                    else
                    {
                        var res = ScreenResolution.CurrentResolution;
                        ini.WriteValue("Screen Setup", "ScreenHeight", res.Height.ToString());
                        ini.WriteValue("Screen Setup", "ScreenWidth", res.Width.ToString());
                    }

                    ini.WriteValue("Controls", "UseJoystick", "1");
                }
            }
            catch { }
         }
    }
}
