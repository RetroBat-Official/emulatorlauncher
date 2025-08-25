using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

            var commandArray = new List<string>();

            if (Path.GetExtension(rom).ToLower() == ".eduke32")
            {
                var edukeArgs = Eduke32Arg.ParseConfig(rom, Path.GetDirectoryName(rom));
                commandArray.AddRange(edukeArgs);
            }

            else
            {
                commandArray.Add("-gamegrp");
                commandArray.Add("\"" + grp + "\"");
                commandArray.Add("-j");
                commandArray.Add("\"" + Path.GetDirectoryName(rom) + "\"");
            }

            commandArray.Add("-nosetup");

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
    class Eduke32Arg
    {
        public static List<Eduke32Arg> Eduke32Args = new List<Eduke32Arg>()
        {
            new Eduke32Arg("DIR", "-j", false),
            new Eduke32Arg("FILE", "-gamegrp", true),
            new Eduke32Arg("FILE+", "-g", false),
            new Eduke32Arg("CON", "-x", true),
            new Eduke32Arg("CON+", "-mx", false),
            new Eduke32Arg("DEF", "-h", true),
            new Eduke32Arg("DEF+", "-mh", false),
            new Eduke32Arg("MAP", "-map", true),
        };

        public Eduke32Arg(string key, string argument, bool single)
        {
            Key = key;
            Argument = argument;
            Single = single;
        }

        public string Key { get; set; }
        public string Argument { get; set; }
        public bool Single { get; set; }


        public static List<string> ParseConfig(string filePath, string basePath)
        {
            var lines = File.ReadAllLines(filePath);
            var argsList = new List<string>();

            var availableArgs = new List<Eduke32Arg>(Eduke32Args);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    continue;

                var parts = line.Split(new[] { '=' }, 2);
                var key = parts[0].Trim();
                var value = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                var argDef = availableArgs.FirstOrDefault(a => a.Key == key);
                if (argDef != null)
                {
                    argsList.Add(argDef.Argument);

                    if (value.StartsWith("/") || value.StartsWith("\\"))
                    {
                        value = value.Substring(1);
                        string fullPath = Path.Combine(basePath, value);
                        argsList.Add(fullPath);
                    }

                    else
                        argsList.Add(value);

                    // Ensure we do not add twice a single-use argument
                    if (argDef.Single)
                        availableArgs.Remove(argDef);
                }
            }

            return argsList;
        }
    }
}
