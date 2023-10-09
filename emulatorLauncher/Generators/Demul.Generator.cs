using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
{
    class DemulGenerator : Generator
    {
        private bool _oldVersion = false;
        private bool _isUsingReshader = false;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

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

            string demulCore = GetDemulCore(emulator, core, system);

            // Allow fake decorations if ratio is set to 4/3, otherwise disable bezels
            if (SystemConfig.isOptSet("demul_ratio") && SystemConfig["demul_ratio"] != "1")
                SystemConfig["bezel"] = "none";

            var bezels = BezelFiles.GetBezelFiles(system, rom, resolution);
            _isUsingReshader = ReshadeManager.Setup(ReshadeBezelType.dxgi, ReshadePlatform.x86, system, rom, path, resolution, bezels != null);
            if (_isUsingReshader)
            {
                if (bezels != null)
                    SystemConfig["demul_ratio"] = "0"; // Force stretch mode if bezel is used
            }
            else 
            {
                _bezelFileInfo = bezels;
                _resolution = resolution;
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

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-run=" + demulCore + " -rom=\"" + Path.GetFileNameWithoutExtension(rom).ToLower() + "\"",
            };            
        }

        private void SetupGeneralConfig(string path, string rom, string system, string core, string demulCore)
        {
            string iniFile = Path.Combine(path, "Demul.ini");

            try
            {
                using (var ini = IniFile.FromFile(iniFile, IniOptions.UseSpaces))
                {
                    // Set Window position to screen center
                    ini.WriteValue("main", "windowX", Screen.PrimaryScreen.Bounds.Left.ToString());
                    ini.WriteValue("main", "windowY", Screen.PrimaryScreen.Bounds.Top.ToString());

                    // Rom paths
                    var biosPath = AppConfig.GetFullPath("bios");
                    var romsPath = AppConfig.GetFullPath("roms");
                    
                    var romsPaths = new List<string>();
                    romsPaths.Add(Path.Combine(biosPath, "dc"));
                    romsPaths.Add(biosPath);
                    romsPaths.Add(Path.GetDirectoryName(rom));

                    foreach (var sys in new string[] { "dreamcast", "naomi", "naomi2", "hikaru", "gaelco", "atomiswave" })
                    {
                        var sysPath = Path.Combine(romsPath, sys);
                        if (Directory.Exists(sysPath) && !romsPaths.Contains(sysPath))
                            romsPaths.Add(sysPath);
                    }

                    for(int i = 0 ; i < romsPaths.Count ; i++)
                        ini.WriteValue("files", "roms" + i, romsPaths[i]);

                    ini.WriteValue("files", "romsPathsCount", romsPaths.Count.ToString());

                    // Plugins
                    ini.WriteValue("plugins", "directory", @".\plugins\");

                    string gpu = "gpuDX11.dll";
                    if (_oldVersion || core == "gaelco" || system == "galeco")
                    {
                        _videoDriverName = "gpuDX11old";
                        gpu = "gpuDX11old.dll";
                    }

                    if (Features.IsSupported("internal_resolution") && SystemConfig.isOptSet("internal_resolution") && SystemConfig["internal_resolution"] != "1")
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

                    if (SystemConfig.isOptSet("demul_videomode") && !string.IsNullOrEmpty(SystemConfig["demul_videomode"]))
                        ini.WriteValue("main", "videomode", SystemConfig["demul_videomode"]);
                    else if (Features.IsSupported("demul_videomode"))
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
                    ini.WriteValue("main", "UseFullscreen", _isUsingReshader ? "1" : "0");
                    ini.WriteValue("main", "Vsync", SystemConfig["VSync"] != "false" ? "1" : "0");
                    ini.WriteValue("resolution", "Width", resolution.Width.ToString());
                    ini.WriteValue("resolution", "Height", resolution.Height.ToString());

                    BindIniFeature(ini, "main", "scaling", "internal_resolution", "1");
                    BindIniFeature(ini, "main", "aspect", "demul_ratio", "1");

                    if (SystemConfig.isOptSet("smooth"))
                        ini.WriteValue("main", "bilinearfb", SystemConfig.getOptBoolean("smooth") ? "true" : "false");
                    else if (Features.IsSupported("smooth"))
                        ini.WriteValue("main", "bilinearfb", "true");
                }
            }
            catch { }
        }

        private string GetDemulCore(string emulator, string core, string system)
        {
            if (emulator == "demul-hikaru" || core == "hikaru")
                return "hikaru";
            else if (emulator == "demul-gaelco" || core == "gaelco")
                return "gaelco";
            else if (emulator == "demul-atomiswave" || core == "atomiswave")
                return "awave";
            else if (emulator == "demul-naomi" || emulator == "demul-naomi2" || core == "naomi")
                return "naomi";
            else
            {
                switch (system)
                {
                    case "hikaru":
                    case "gaelco":
                    case "naomi":
                        return system;
                    case "naomi2":
                        return "naomi";
                    case "atomiswave":
                        return "awave";
                }
            }
            
            return "dc";
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            path.WindowStyle = ProcessWindowStyle.Maximized;
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
                    var style = User32.GetWindowStyle(hWnd);
                    if (style.HasFlag(WS.CAPTION))
                    {
                        if (_isUsingReshader)
                            SendKeys.SendWait("%~");
                        else
                        {
                            int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                            int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);
                            style &= ~WS.CAPTION;
                            style &= ~WS.BORDER;
                            style &= ~WS.DLGFRAME;
                            style &= ~WS.SYSMENU;

                            User32.SetWindowStyle(hWnd, style);
                            User32.SetMenu(hWnd, IntPtr.Zero);
                            User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);
                        }
                    }

                    break;
                }
            }

            if (process != null)
            {
                process.WaitForExit();

                if (bezel != null)
                    bezel.Dispose();

                try { return process.ExitCode; }
                catch { }
            }

            if (bezel != null)
                bezel.Dispose();

            return -1;
        }
    }
}
