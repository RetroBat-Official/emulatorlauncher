using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class Model3Generator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("supermodel");

            string exe = Path.Combine(path, "supermodel.exe");            
            if (!File.Exists(exe))
                return null;

            List<string> args = new List<string>();

            if (resolution != null)
                args.Add("-res=" + resolution.Width + "," + resolution.Height);
            else
                args.Add("-res=" + Screen.PrimaryScreen.Bounds.Width + "," + Screen.PrimaryScreen.Bounds.Height);

            _resolution = resolution;

            if (ReshadeManager.Setup(ReshadeBezelType.opengl, system, rom, path, resolution))
                args.Add("-fullscreen");
            else
            {
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

                if (_bezelFileInfo == null)
                {
                    args.Add("-fullscreen");

                    if (SystemConfig["ratio"] != "4:3")
                    {
                        args.Add("-wide-screen");
                        args.Add("-stretch");
                    }
                }
            }
                            
            if (SystemConfig["VSync"] != "false")
                args.Add("-vsync");

            args.Add("\""+rom+"\"");

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(" ", args),
                WorkingDirectory = path,                
            };            
        }


        public override void RunAndWait(ProcessStartInfo path)
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
                        IntPtr hWnd = User32.FindHwnds(px.Id).FirstOrDefault(h => User32.GetWindowText(h).StartsWith("Supermodel"));
                        if (hWnd != IntPtr.Zero)
                        {
                            var style = User32.GetWindowStyle(hWnd);
                            if (style.HasFlag(WS.CAPTION))
                            {
                                int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                                int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                                User32.SetWindowStyle(hWnd, style & ~WS.CAPTION);
                                User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);

                                if (_bezelFileInfo != null && bezel == null)
                                    bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
                            }
                        }
                    }

                    Application.DoEvents();
                }
            }
            catch { }
            finally
            {
                if (bezel != null)
                    bezel.Dispose();
            }
        }

    }
}
