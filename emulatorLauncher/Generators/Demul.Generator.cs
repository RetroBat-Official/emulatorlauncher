using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using emulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using System.Threading;

namespace emulatorLauncher
{
    class DemulGenerator : Generator
    {
        bool _oldVersion = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "demul-old" || core == "demul-old") ? "demul-old" : "demul";
            if (folderName == "demul-old")
                _oldVersion = true;

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("demul");

            string exe = Path.Combine(path, "demul.exe");
            if (!File.Exists(exe))
                return null;

            string demulCore = "dc";

            if (emulator == "demul-hikaru" || core == "hikaru")
                demulCore = "hikaru";
            else if (emulator == "demul-gaelco" || core == "gaelco")
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

            SetupGeneralConfig(path, rom, system, core, demulCore);
            SetupDx11Config(path, rom, system, resolution);

            if (demulCore == "dc")
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "-run=" + demulCore + " -image=\"" + rom + "\"",
                };
            }

            else
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = "-run=" + demulCore + " -rom=\"" + Path.GetFileNameWithoutExtension(rom).ToLower() + "\"",
                };
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            var process = Process.Start(path);

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                var hWnd = User32.FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;
                
                var name = User32.GetWindowText(hWnd);
                if (name != null && name.StartsWith("gpu"))
                {                    
                    SendKeys.SendWait("%~");
                    break;
                }
            }

            if (process != null)
            {
                process.WaitForExit();
                try { return process.ExitCode; }
                catch { }
            }

            return -1;
        }

        private void SetupGeneralConfig(string path, string rom, string system, string core, string demulCore)
        {
            string iniFile = Path.Combine(path, "Demul.ini");

            try
            {
                using (var ini = IniFile.FromFile(iniFile, IniOptions.UseSpaces))
                {
                    
                    ini.WriteValue("files", "roms0", Path.Combine(AppConfig.GetFullPath("bios"), "dc"));
                    ini.WriteValue("files", "roms1", AppConfig.GetFullPath("bios"));
                    ini.WriteValue("files", "roms2", Path.Combine(AppConfig.GetFullPath("roms"), "dreamcast"));
                    ini.WriteValue("files", "roms3", Path.Combine(AppConfig.GetFullPath("roms"), "naomi2"));
                    ini.WriteValue("files", "roms4", Path.Combine(AppConfig.GetFullPath("roms"), "hikaru"));
                    ini.WriteValue("files", "roms5", Path.Combine(AppConfig.GetFullPath("roms"), "gaelco"));
                    ini.WriteValue("files", "roms6", Path.Combine(AppConfig.GetFullPath("roms"), "atomiswave"));
                    ini.WriteValue("files", "roms7", Path.Combine(AppConfig.GetFullPath("roms"), "naomi"));
                    ini.WriteValue("files", "romsPathsCount", "8");

                    ini.WriteValue("plugins", "directory", @".\plugins\");

                    string gpu = "gpuDX11.dll";
                    if (_oldVersion || core == "gaelco" || system == "galeco")
                    {
                        _videoDriverName = "gpuDX11old";
                        gpu = "gpuDX11old.dll";
                    }

                    ini.WriteValue("plugins", "gpu", gpu);

                    if (string.IsNullOrEmpty(ini.GetValue("plugins", "pad")))
                        ini.WriteValue("plugins", "pad", "padDemul.dll");

                    if (demulCore == "dc" && Path.GetExtension(rom).ToLower() == ".chd")
                        ini.WriteValue("plugins", "gdr", "gdrCHD.dll");
                    else
                        ini.WriteValue("plugins", "gdr", "gdrimage.dll");

                    if (string.IsNullOrEmpty(ini.GetValue("plugins", "spu")))
                        ini.WriteValue("plugins", "spu", "spuDemul.dll");

                    if (ini.GetValue("plugins", "net") == null)
                        ini.WriteValue("plugins", "net", "netDemul.dll");

                    if (SystemConfig.isOptSet("cpumode") && !string.IsNullOrEmpty(SystemConfig["cpumode"]))
                        ini.WriteValue("main", "cpumode", SystemConfig["cpumode"]);
                    else if (Features.IsSupported("cpumode"))
                        ini.WriteValue("main", "cpumode", "1");

                    if (SystemConfig.isOptSet("videomode") && !string.IsNullOrEmpty(SystemConfig["videomode"]))
                        ini.WriteValue("main", "videomode", SystemConfig["videomode"]);
                    else if (Features.IsSupported("videomode"))
                        ini.WriteValue("main", "videomode", "1024");

                    if (SystemConfig.isOptSet("dc_region") && !string.IsNullOrEmpty(SystemConfig["dc_region"]))
                        ini.WriteValue("main", "region", SystemConfig["dc_region"]);
                    else if (Features.IsSupported("dc_region"))
                        ini.WriteValue("main", "region", "1");

                    if (SystemConfig.isOptSet("dc_broadcast") && !string.IsNullOrEmpty(SystemConfig["dc_broadcast"]))
                        ini.WriteValue("main", "broadcast", SystemConfig["dc_broadcast"]);
                    else if (Features.IsSupported("dc_broadcast"))
                        ini.WriteValue("main", "broadcast", "1");

                    ini.WriteValue("main", "timehack", SystemConfig["timehack"] != "false" ? "true" : "false");

                }
            }

            catch { }
        }

        private string _videoDriverName = "gpuDX11";

        private void SetupDx11Config(string path, string rom, string system, ScreenResolution resolution)
        {
            string iniFile = Path.Combine(path, _videoDriverName + ".ini");

            try
            {
                if (resolution == null)
                    resolution = ScreenResolution.CurrentResolution;

                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    ini.WriteValue("main", "UseFullscreen", SystemConfig["startfullscreen"] != "false" ? "1" : "0");
                    ini.WriteValue("main", "Vsync", SystemConfig["VSync"] != "false" ? "1" : "0");
                    ini.WriteValue("resolution", "Width", resolution.Width.ToString());
                    ini.WriteValue("resolution", "Height", resolution.Height.ToString());
                    
                    if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                        ini.WriteValue("main", "aspect", SystemConfig["ratio"]);
                    else if (Features.IsSupported("ratio"))
                        ini.WriteValue("main", "aspect", "1");
                }
            }

            catch { }
        }
    }
}
