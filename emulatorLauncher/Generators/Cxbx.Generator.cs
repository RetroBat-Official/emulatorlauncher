using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class CxbxGenerator : Generator
    {
        #region XboxIsoVfs management
        private string _dokanDriveLetter;

        private string MountIso(string rom)
        {
            if (Path.GetExtension(rom).ToLowerInvariant() != ".iso")
                return rom;
                                
            string xboxIsoVfsPath = Path.Combine(Path.GetDirectoryName(typeof(Installer).Assembly.Location), "xbox-iso-vfs.exe");
            if (!File.Exists(xboxIsoVfsPath))
                throw new ApplicationException("xbox-iso-vfs is required and is not installed");

            // Check dokan is installed
            string dokan = Environment.GetEnvironmentVariable("DokanLibrary1");
            if (!Directory.Exists(dokan))
            {
                MountFile.ShowDownloadDokanPage();
                throw new ApplicationException("Dokan 1.4.0.1000 is required and is not installed");
            }

            dokan = Path.Combine(dokan, "dokan1.dll");
            if (!File.Exists(dokan))
            {
                MountFile.ShowDownloadDokanPage();
                throw new ApplicationException("Dokan 1.4.0.1000 is required and is not installed");
            }

            var drive = FileTools.FindFreeDriveLetter();
            if (drive == null)
                throw new ApplicationException("Unable to find a free drive letter to mount");

            var xboxIsoVfs = Process.Start(new ProcessStartInfo()
            {
                FileName = xboxIsoVfsPath,
                WorkingDirectory = Path.GetDirectoryName(rom),
                Arguments = "\"" + rom + "\" " + drive,
                UseShellExecute = false,    
                CreateNoWindow = true
            });

            int time = Environment.TickCount;
            int elapsed = 0;

            while (elapsed < 5000)
            {
                if (xboxIsoVfs.WaitForExit(10))
                    return rom;

                if (Directory.Exists(drive))
                {
                    Job.Current.AddProcess(xboxIsoVfs);

                    _dokanDriveLetter = drive;
                    return drive;
                }

                int newTime = Environment.TickCount;
                elapsed = time - newTime;
                time = newTime;
            }

            try { xboxIsoVfs.Kill(); }
            catch { }

            return rom;
        }

        #endregion

        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private bool _isUsingCxBxLoader = true;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = null;

            if ((core != null && core == "chihiro") || (emulator != null && emulator == "chihiro"))
                path = AppConfig.GetFullPath("chihiro");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-reloaded");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("cxbx-r");

            _isUsingCxBxLoader = true;

            string exe = Path.Combine(path, "cxbxr-ldr.exe");
            if (!File.Exists(exe))
            {
                _isUsingCxBxLoader = false;
                exe = Path.Combine(path, "cxbx.exe");
            }
            
            if (!File.Exists(exe))
                return null;

            rom = MountIso(rom);

            _resolution = resolution;

            if (_isUsingCxBxLoader)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            
            // If rom is a directory
            if (Directory.Exists(rom))
            {
                var xbeFiles = Directory.GetFiles(rom, "*.xbe");

                var xbe = xbeFiles.FirstOrDefault(r => Path.GetFileNameWithoutExtension(r).ToLowerInvariant() == "default");
                if (string.IsNullOrEmpty(xbe))
                    xbe = xbeFiles.FirstOrDefault();

                if (string.IsNullOrEmpty(xbe))
                    throw new ApplicationException("Unable to find XBE file");

                rom = xbe;
            }

            //Configuration of .ini file
            using (var ini = IniFile.FromFile(Path.Combine(path, "settings.ini"), IniOptions.KeepEmptyValues | IniOptions.AllowDuplicateValues | IniOptions.UseSpaces))
            {
                var res = resolution;
                if (res == null)
                    res = ScreenResolution.CurrentResolution;

                //Fulscreen Management
                if (_isUsingCxBxLoader)
                {
                    ini.WriteValue("video", "FullScreen", "false");
                }
                else
                {
                    string videoResolution = res.Width + " x " + res.Height + " 32bit x8r8g8b8 (" + (res.DisplayFrequency <= 0 ? 60 : res.DisplayFrequency).ToString() + " hz)";
                    ini.WriteValue("video", "VideoResolution", videoResolution);
                    ini.WriteValue("video", "FullScreen", "true");
                }

                //Vsync
                ini.WriteValue("video", "VSync", SystemConfig["VSync"] != "false" ? "true" : "false");

                //Aspect ratio : keep original or stretch
                if (Features.IsSupported("ratio") && SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "stretch")
                {
                    ini.WriteValue("video", "MaintainAspect", "false");
                    _bezelFileInfo = null;
                }
                else
                    ini.WriteValue("video", "MaintainAspect", "true");

                //Internal resolution
                if (Features.IsSupported("internalresolution") && SystemConfig.isOptSet("internalresolution") && !string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                    ini.WriteValue("video", "RenderResolution", SystemConfig["internalresolution"]);
                else
                    ini.WriteValue("video", "RenderResolution", "3");

                //XBE signature
                if (SystemConfig.isOptSet("xbeSignature") && SystemConfig.getOptBoolean("xbeSignature"))
                    ini.WriteValue("gui", "IgnoreInvalidXbeSig", "true");
                else if (Features.IsSupported("xbeSignature"))
                    ini.WriteValue("gui", "IgnoreInvalidXbeSig", "false");

                //hacks
                if (SystemConfig.isOptSet("disablePixelShaders") && SystemConfig.getOptBoolean("disablePixelShaders"))
                    ini.WriteValue("hack", "DisablePixelShaders", "true");
                else if (Features.IsSupported("disablePixelShaders"))
                    ini.WriteValue("hack", "DisablePixelShaders", "false");

                if (SystemConfig.isOptSet("useallcores") && SystemConfig.getOptBoolean("useallcores"))
                    ini.WriteValue("hack", "UseAllCores", "true");
                else if (Features.IsSupported("useallcores"))
                    ini.WriteValue("hack", "UseAllCores", "false");

                if (SystemConfig.isOptSet("rdtscPatching") && SystemConfig.getOptBoolean("rdtscPatching"))
                    ini.WriteValue("hack", "SkipRdtscPatching", "true");
                else if (Features.IsSupported("rdtscPatching"))
                    ini.WriteValue("hack", "SkipRdtscPatching", "false");

            }

            if (_isUsingCxBxLoader)
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    WindowStyle = ProcessWindowStyle.Maximized,
                    Arguments = "/load \"" + rom + "\"",
                };
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }
        
        public override int RunAndWait(ProcessStartInfo path)
        {
            if (!_isUsingCxBxLoader)
                return base.RunAndWait(path);

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

                var name = User32.GetClassName(hWnd);
                if (name != null && name.Contains("CxbxRender"))
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

            if (process != null)
            {
                try { return process.ExitCode; }
                catch { }
            }

            return -1;
        }

    }
}
