using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class DemulGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "demul-old" || core == "demul-old") ? "demul-old" : "demul";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("demul");

            string exe = Path.Combine(path, "demul.exe");
            if (!File.Exists(exe))
                return null;

            SetupGeneralConfig(path, rom, system);
            SetupDx11Config(path, rom, system);

            string demulCore = "dreamcast";

            if (emulator == "demul-hikaru" || core == "hikaru")
                demulCore = "hikaru";
            else if (emulator == "demul-gaelco" || core == "hikaru")
                demulCore = "gaelco";
            else if (emulator == "demul-atomiswave" || core == "atomiswave")
                demulCore = "awave";
            else if (emulator == "demul-naomi" || emulator == "demul-naomi2" || core == "naomi")
                demulCore = "naomi";
            else
            {
                switch (system)
                {
                    case "hikaru":
                        demulCore = "hikaru"; break;
                    case "gaelco":
                        demulCore = "gaelco"; break;
                    case "naomi":
                    case "naomi2":
                        demulCore = "naomi"; break;
                    case "atomiswave":
                        demulCore = "awave"; break;
                }
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-run=" + demulCore + " -rom=\"" + Path.GetFileNameWithoutExtension(rom).ToLower() + "\"",
            };
        }

        private void SetupGeneralConfig(string path, string rom, string system)
        {
            string iniFile = Path.Combine(path, "Demul.ini");
            if (!File.Exists(iniFile))
                return;

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("files", "roms0", AppConfig.GetFullPath("bios"));
                    ini.WriteValue("files", "roms1", Path.GetDirectoryName(rom));
                    ini.WriteValue("files", "romsPathsCount", "2");

                    ini.WriteValue("plugins", "directory", @".\plugins\");
                    ini.WriteValue("plugins", "gpu", "gpuDX11.dll");
                    ini.WriteValue("plugins", "pad", "padDemul.dll");

                    if (ini.GetValue("plugins", "gpu") == null)
                        ini.WriteValue("plugins", "gdr", "gdrCHD.dll");

                    if (ini.GetValue("plugins", "spu") == null)
                        ini.WriteValue("plugins", "spu", "spuDemul.dll");

                    if (ini.GetValue("plugins", "net") == null)
                        ini.WriteValue("plugins", "net", "netDemul.dll");
                }
            }

            catch { }
        }

        private void SetupDx11Config(string path, string rom, string system)
        {
            string iniFile = Path.Combine(path, "gpuDX11.ini");
            if (!File.Exists(iniFile))
                return;

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("main", "UseFullscreen", "1");
                    ini.WriteValue("main", "Vsync", SystemConfig["VSync"] != "false" ? "1" : "0");                    
                }
            }

            catch { }
        }
    }
}
