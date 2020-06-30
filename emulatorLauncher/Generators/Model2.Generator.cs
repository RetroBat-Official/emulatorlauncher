using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class Model2Generator : Generator
    {
        public Model2Generator()
        {
            DependsOnDesktopResolution = false;
        }

        private string _destFile;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("m2emulator");

            string exe = Path.Combine(path, "emulator_multicpu.exe");
            if (core != null && core.ToLower().Contains("singlecpu"))
                exe = Path.Combine(path, "emulator.exe");

            if (!File.Exists(exe))
                return null;

            string pakDir = Path.Combine(path, "roms");
            if (!Directory.Exists(pakDir))
                Directory.CreateDirectory(pakDir);

            foreach (var file in Directory.GetFiles(pakDir))
            {
                if (Path.GetFileName(file) == Path.GetFileName(rom))
                    continue;

                File.Delete(file);
            }

            _destFile = Path.Combine(pakDir, Path.GetFileName(rom));
            if (!File.Exists(_destFile))
                File.Copy(rom, _destFile);
            
            SetupConfig(path, resolution);
            
            string arg = Path.GetFileNameWithoutExtension(_destFile);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = arg,
                WorkingDirectory = path,                
            };            
        }

        private void SetupConfig(string path, ScreenResolution resolution)
        {
            string iniFile = Path.Combine(path, "Emulator.ini");

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("Renderer", "FullMode", "4");
                    ini.WriteValue("Renderer", "AutoFull", "1");                    
                    ini.WriteValue("Renderer", "FullScreenWidth", (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString());
                    ini.WriteValue("Renderer", "FullScreenHeight", (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString());
                    ini.WriteValue("Renderer", "ForceSync", SystemConfig["VSync"] != "false" ? "1" : "0");                              
                }
            }

            catch { }
        }

        public override void Cleanup()
        {
            if (_destFile != null && File.Exists(_destFile))
                File.Delete(_destFile);
        }
    }
}
