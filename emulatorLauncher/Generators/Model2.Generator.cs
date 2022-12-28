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
        static Dictionary<string, string> parentRoms = new Dictionary<string, string>() 
        { 
            // Daytona USA
            { "dayton93", "daytona" },
            { "daytonas", "daytona" },
            { "daytonase", "daytona" },
            { "daytonat", "daytona" },
            { "daytonata", "daytona" },
            { "daytonagtx", "daytona" },
            { "daytonam", "daytona" },
            // Dead Or Alive
            { "doaa", "doa" },
            // Dynamite Cop
            { "dynmcopb", "dynamcop" },
            { "dynmcopc", "dynamcop" },
            // Dynamite Deka 
            { "dyndeka2", "dynamcop" },
            { "dyndek2b", "dynamcop" },
            // Indianapolis 500
            { "indy500d", "indy500" },
            { "indy500to", "indy500" },
            // Last Bronx
            { "lastbrnxj", "lastbrnx" },
            { "lastbrnxu", "lastbrnx" },
            // Pilot Kids
            { "pltkidsa", "pltkids" },            
            // Over Rev
            { "overrevb", "overrev" }, 
            // Sega Rally Championship
            { "srallycb", "srallyc" },
            { "srallyp", "srallyc" },
            // Sega Touring Car Championship
            { "stcca", "stcc" },
            { "stccb", "stcc" },
            // Top Skater
            { "topskatrj", "topskatr" },
            { "topskatru", "topskatr" },
            // Sonic The Fighters
            { "sfight", "schamp" },            
            // Virtua Cop
            { "vcopa", "vcop" },            
            // Virtua Fighter 2
            { "vf2o", "vf2" },
            { "vf2a", "vf2" },
            { "vf2b", "vf2" },
            // Virtua Striker
            { "vstrikro", "vstriker" },
            // Virtual On Cybertroopers
            { "vonj", "von" },
            // Zero Gunner
            { "zerogunaj", "zerogun" },
            { "zerogunj", "zerogun" },
            { "zeroguna", "zerogun" },
        };

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public Model2Generator()
        {
            DependsOnDesktopResolution = false;
        }

        private string _destFile;
        private string _destParent;

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
            {
                File.Copy(rom, _destFile);
                
                try { new FileInfo(_destFile).Attributes &= ~FileAttributes.ReadOnly; }
                catch { }
            }

            string parentRom = null;
            if (parentRoms.TryGetValue(Path.GetFileNameWithoutExtension(rom).ToLowerInvariant(), out parentRom))
            {
                parentRom = Path.Combine(Path.GetDirectoryName(rom), parentRom + ".zip");
                _destParent = Path.Combine(pakDir, Path.GetFileName(parentRom));

                if (!File.Exists(_destParent))
                {
                    File.Copy(parentRom, _destParent);

                    try { new FileInfo(_destParent).Attributes &= ~FileAttributes.ReadOnly; }
                    catch { }
                }
            }
            
            _resolution = resolution;

            if (!ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x86, system, rom, path,  resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

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
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    if (_bezelFileInfo == null)
                    {
                        ini.WriteValue("Renderer", "FullMode", "4");
                        ini.WriteValue("Renderer", "AutoFull", "1");
                        ini.WriteValue("Renderer", "WideScreenWindow", "0");
                    }
                    else
                    {
                        ini.WriteValue("Renderer", "FullMode", "4");
                        ini.WriteValue("Renderer", "AutoFull", "0");
                        ini.WriteValue("Renderer", "WideScreenWindow", "1");
                    }

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

            if (_destParent != null && File.Exists(_destParent))
                File.Delete(_destParent);

            base.Cleanup();
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            try
            {
                var px = Process.Start(path);

                while (!px.HasExited)
                {
                    if (px.WaitForExit(10))
                        break;

                    if (_bezelFileInfo != null)
                    {
                        IntPtr hWnd = User32.FindHwnds(px.Id).FirstOrDefault(h => User32.GetClassName(h) == "MYWIN");
                        if (hWnd != IntPtr.Zero)
                        {
                            var style = User32.GetWindowStyle(hWnd);
                            if (style.HasFlag(WS.CAPTION))
                            {
                                int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                                int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                                User32.SetWindowStyle(hWnd, style & ~WS.CAPTION);
                                User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);
                                User32.SetMenu(hWnd, IntPtr.Zero);
                                
                                if (_bezelFileInfo != null && bezel == null)
                                    bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
                            }
                        }
                    }

                    Application.DoEvents();
                }

                return px.ExitCode;
            }
            catch { }
            finally
            {
                if (bezel != null)
                    bezel.Dispose();
            }

            return -1;
        }
    }
}
