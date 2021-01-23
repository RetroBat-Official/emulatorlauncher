using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class TsugaruGenerator : Generator
    {
        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            _resolution = resolution;
            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            string path = AppConfig.GetFullPath("tsugaru");

            string exe = Path.Combine(path, "Tsugaru_CUI.exe");
            if (!File.Exists(exe))
                return null;

            List<string> commandArray = new List<string>();

            string biosPath = null;

            if (!string.IsNullOrEmpty(AppConfig["bios"]))
            {
                if (Directory.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "fmtowns")))
                    biosPath = Path.Combine(AppConfig.GetFullPath("bios"), "fmtowns");
                else if (Directory.Exists(Path.Combine(path, "roms")))
                    biosPath = Path.Combine(path, "roms");
                else if (Directory.Exists(Path.Combine(path, "fmtownsux")))
                    biosPath = Path.Combine(path, "fmtownsux");
            }

            if (string.IsNullOrEmpty(biosPath) || !File.Exists(Path.Combine(biosPath, "FMT_SYS.ROM")))
            {
                SimpleLogger.Instance.Info("TsugaruGenerator : Bios path not found");
                return null;
            }

            commandArray.Add(biosPath);
                    
            commandArray.Add("-CMOS");
            commandArray.Add(Path.Combine(biosPath, "CMOS.DAT"));

            commandArray.Add("-WINDOWSHIFT");

            if (Directory.Exists(rom))
            {
                var cueFile = Directory.GetFiles(rom, "*.cue").FirstOrDefault();
                if (!string.IsNullOrEmpty(cueFile))
                {
                    commandArray.Add("-CD");
                    commandArray.Add(cueFile);
                }
                else
                {
                    SimpleLogger.Instance.Info("TsugaruGenerator : Cue file not found");
                    return null;
                }
            }
            else
            {
                string ext = Path.GetExtension(rom).ToLowerInvariant();
                if (ext == ".cue")
                {
                    commandArray.Add("-CD");
                    commandArray.Add(rom);
                }
                else
                {
                    commandArray.Add("-FD0");
                    commandArray.Add(rom);
                }                
            }
            
            commandArray.Add("-GAMEPORT0");
            /*
            if (Program.Controllers.Any(c => c.Config != null && c.Config.Type != "keyboard"))
                commandArray.Add("PHYS0");
            else*/
                commandArray.Add("KEY");

            commandArray.Add("-GAMEPORT1");
            commandArray.Add("MOUSE");

            commandArray.Add("-AUTOSCALE");
            commandArray.Add("-MAXIMIZE");

            commandArray.Add("-FREQ");
            commandArray.Add("25");

            commandArray.Add("-MEMSIZE");
            commandArray.Add("4");

            commandArray.Add("-NOCATCHUPREALTIME");

            commandArray.Add("-MOUSEINTEGSPD");
            commandArray.Add("32");


            for (int i = 0; i < commandArray.Count; i++)
                commandArray[i] = "\"" + commandArray[i] + "\"";

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                WindowStyle = ProcessWindowStyle.Minimized,
                Arguments = args,
            };
        }

        public override void RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

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
                if (name != null && name.Contains("TSUGARU"))
                {
                    var style = User32.GetWindowStyle(hWnd);
                    if (style.HasFlag(WS.CAPTION))
                    {
                        int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                        int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                        style &= ~WS.CAPTION;
                        style &= ~WS.THICKFRAME;
                        style &= ~WS.MAXIMIZEBOX;
                        style &= ~WS.MINIMIZEBOX;
                        style &= ~WS.OVERLAPPED;
                        style &= ~WS.SYSMENU;
                        User32.SetWindowStyle(hWnd, style);

                        User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);

                        if (_bezelFileInfo != null)
                            bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
                    }

                    break;
                }
            }

            if (process != null)
                process.WaitForExit();

            if (bezel != null)
                bezel.Dispose();
        }

    }
}
