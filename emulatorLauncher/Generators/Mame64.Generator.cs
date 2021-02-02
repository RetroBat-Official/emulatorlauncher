using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class Mame64Generator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mame64");
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("mame");

            string exe = Path.Combine(path, "mame64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "mame32.exe");

            if (!File.Exists(exe))
                return null;

            if (core == "fmtownsux" || core == "fmtowns")
            {
                string args = "-cdrom \"" + rom + "\"";

                SetupFmTownsRomPaths(path, rom);

                if (Directory.Exists(rom))
                {
                    var cueFile = Directory.GetFiles(rom, "*.cue").FirstOrDefault();
                    if (!string.IsNullOrEmpty(cueFile))
                        args = "-cdrom \"" + cueFile + "\"";
                    else
                    {
                        SimpleLogger.Instance.Info("TsugaruGenerator : Cue file not found");
                        return null;
                    }
                }
                else
                {
                    string ext = Path.GetExtension(rom).ToLowerInvariant();
                    if (ext == ".cue" || ext == ".iso")
                        args = "-cdrom \"" + rom + "\"";
                    else
                        args = "-flop1 \"" + rom + "\"";
                }

                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "fmtownsux " + args,
                };
            }
            
            SetupRomPaths(path, rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }

        private void SetupFmTownsRomPaths(string path, string rom)
        {
            try
            {
                string biosPath = null;

                if (!string.IsNullOrEmpty(AppConfig["bios"]))
                {
                    if (Directory.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "fmtownsux")))
                        biosPath = Path.Combine(AppConfig.GetFullPath("bios"));
                }

                if (string.IsNullOrEmpty(biosPath))
                    return;

                string iniFile = GetIniPaths(path)
                        .Select(p => Path.Combine(AbsolutePath(path, p), "fmtownsux.ini"))
                        .Where(p => File.Exists(p))
                        .FirstOrDefault();

                if (string.IsNullOrEmpty(iniFile))
                    iniFile = Path.Combine(path, "fmtownsux.ini");

                if (!File.Exists(iniFile))
                    File.WriteAllText(iniFile, Properties.Resources.mame);

                var lines = File.ReadAllLines(iniFile).ToList();
                int idx = lines.FindIndex(l => l.StartsWith("rompath"));
                if (idx >= 0)
                {
                    
                    var line = lines[idx];
                    var name = line.Substring(0, 26);

                    var paths = line.Substring(26)
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(p => Directory.Exists(AbsolutePath(path, p)))
                        .ToList();

                    if (!paths.Contains(biosPath))
                    {
                        paths.Add(biosPath);
                        lines[idx] = name + string.Join(";", paths.ToArray());
                        File.WriteAllLines(iniFile, lines);
                    }
                }
            }
            catch { }
        }

        private string AbsolutePath(string path, string val)
        {
            if (string.IsNullOrEmpty(val))
                return val;

            if (val == ".")
                return path;

            return Path.Combine(path, val);
        }

        private string[] GetIniPaths(string path)
        {
            try
            {
                string iniFile = Path.Combine(path, "mame.ini");
                if (File.Exists(iniFile))
                {
                    var lines = File.ReadAllLines(iniFile).ToList();
                    int idx = lines.FindIndex(l => l.StartsWith("inipath"));
                    if (idx >= 0)
                    {
                        return lines[idx]
                            .Substring(26)
                            .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .ToArray();
                    }
                }
            }
            catch { }

            return new string[] { ".", "ini", "ini/presets" };
        }

        private void SetupRomPaths(string path, string rom)
        {
            try
            {
                string romPath = Path.GetDirectoryName(rom);

                string iniFile = Path.Combine(path, "mame.ini");
                if (!File.Exists(iniFile))
                    File.WriteAllText(iniFile, Properties.Resources.mame);
                var lines = File.ReadAllLines(iniFile).ToList();
                int idx = lines.FindIndex(l => l.StartsWith("rompath"));
                if (idx >= 0)
                {

                    var line = lines[idx];
                    var name = line.Substring(0, 26);

                    var paths = line.Substring(26)
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(p => Directory.Exists(AbsolutePath(path, p)))
                        .ToList();

                    if (!paths.Contains(romPath))
                    {
                        paths.Add(romPath);
                        lines[idx] = name + string.Join(";", paths.ToArray());
                        File.WriteAllLines(iniFile, lines);
                    }
                }
            }
            catch { }
        }
    }
}
