using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class MednafenGenerator : Generator
    {
        public MednafenGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mednafen");

            string exe = Path.Combine(path, "mednafen.exe");
            if (!File.Exists(exe))
                return null;

            var mednafenCore = GetMednafenCoreName(core);

            SetupConfig(path, mednafenCore);

            List<string> commandArray = new List<string>();
            
            commandArray.Add("-fps.scale 0");
            commandArray.Add("-sound.volume 120");
            commandArray.Add("-video.deinterlacer bob");
            commandArray.Add("-video.fs 1");
			commandArray.Add("-video.disable_composition 1");
			commandArray.Add("-video.glvsync 1");
            commandArray.Add("-video.driver opengl");
            
            if (mednafenCore == "pce" && AppConfig.isOptSet("bios"))
                commandArray.Add("-pce.cdbios \"" + Path.Combine(AppConfig.GetFullPath("bios"), "syscard3.pce") + "\"");

            commandArray.Add("-" + mednafenCore + ".special none");            
            commandArray.Add("-" + mednafenCore + ".shader none");
            commandArray.Add("-" + mednafenCore + ".xres 0");
            commandArray.Add("-" + mednafenCore + ".yres 0");
            commandArray.Add("-" + mednafenCore + ".shader.goat.fprog 1");
            commandArray.Add("-" + mednafenCore + ".shader.goat.slen 1");
            commandArray.Add("-" + mednafenCore + ".shader.goat.tp 0.25");
            commandArray.Add("-" + mednafenCore + ".shader.goat.hdiv 1");
            commandArray.Add("-" + mednafenCore + ".shader.goat.vdiv 1");

            if (Features.IsSupported("smooth") && SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                commandArray.Add("-" + mednafenCore + ".videoip 1");
            else
                commandArray.Add("-" + mednafenCore + ".videoip 0");
            
            var bezels = BezelFiles.GetBezelFiles(system, rom, resolution);

            var platform = ReshadeManager.GetPlatformFromFile(exe);
            if (ReshadeManager.Setup(ReshadeBezelType.opengl, platform, system, rom, path, resolution, bezels != null) && bezels != null)
                commandArray.Add("-" + mednafenCore + ".stretch full");
            else
                commandArray.Add("-" + mednafenCore + ".stretch aspect"); 

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfig(string path, string core)
        {
            var cfg = MednafenConfigFile.FromFile(Path.Combine(path, "mednafen.cfg"));

            var biosPath = AppConfig.GetFullPath("bios");
            if (!string.IsNullOrEmpty(biosPath))
                cfg["filesys.path_firmware"] = biosPath;

            cfg[core+".enable"] = "1";

            cfg.Save();
        }

        private string GetMednafenCoreName(string core)
        {
            switch (core)
            {
                case "megadrive":
                    return "md";
                case "pcengine":
                case "pcenginecd":
                case "supergrafx":
                    return "pce";
            }

            return core;
        }
    }
    
    class MednafenConfigFile
    {
        private string _fileName;
        private List<string> _lines;

        public static MednafenConfigFile FromFile(string file)
        {
            var ret = new MednafenConfigFile();
            ret._fileName = file;

            try
            {
                if (File.Exists(file))
                    ret._lines = File.ReadAllLines(file).ToList();
            }
            catch { }

            if (ret._lines == null)
                ret._lines = new List<string>();

            return ret;
        }

        public string this[string key]
        {
            get
            {
                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
                if (idx >= 0)
                {
                    int split = _lines[idx].IndexOf(" ");
                    if (split >= 0)
                        return _lines[idx].Substring(split + 1).Trim();
                }

                return string.Empty;
            }
            set
            {
                if (this[key] == value)
                    return;

                int idx = _lines.FindIndex(l => !string.IsNullOrEmpty(l) && l[0] != ';' && l.StartsWith(key + " "));
                if (idx >= 0)
                {
                    _lines.RemoveAt(idx);

                    if (!string.IsNullOrEmpty(value))
                        _lines.Insert(idx, key + " " + value);
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    _lines.Add(key + " " + value);
                    _lines.Add("");
                }

                IsDirty = true;
            }
        }

        public bool IsDirty { get; private set; }

        public void Save()
        {
            if (!IsDirty)
                return;

            File.WriteAllLines(_fileName, _lines);
            IsDirty = false;
        }
    }

}