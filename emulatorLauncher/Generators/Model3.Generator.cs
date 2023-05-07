using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    partial class Model3Generator : Generator
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

            bool isWideScreen = false;

            if (resolution != null)
            {
                isWideScreen = ((float)resolution.Width / (float)resolution.Height) > 1.75f;
                args.Add("-res=" + resolution.Width + "," + resolution.Height);
            }
            else
            {
                isWideScreen = ((float)Screen.PrimaryScreen.Bounds.Width / (float)Screen.PrimaryScreen.Bounds.Height) >= 1.75f;
                args.Add("-res=" + Screen.PrimaryScreen.Bounds.Width + "," + Screen.PrimaryScreen.Bounds.Height);
            }

            _resolution = resolution;

            bool wideScreen = SystemConfig["widescreen"] == "1" || SystemConfig["widescreen"] == "2" || (!SystemConfig.isOptSet("widescreen") && isWideScreen);
            if (wideScreen)
            {
                SystemConfig["forceNoBezel"] = "1";

                ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution);

                args.Add("-fullscreen");

                if (SystemConfig["widescreen"] == "2")
                    args.Add("-stretch");
                else
                    args.Add("-wide-screen");
            }
            else
            {
                if (ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                    args.Add("-fullscreen");
                else
                {
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
                    if (_bezelFileInfo == null)
                        args.Add("-fullscreen");
                }
            }

            // quad rendering
            if (SystemConfig.isOptSet("quadRendering") && SystemConfig.getOptBoolean("quadRendering"))
                args.Add("-quad-rendering");

            // crosshairs
            if (SystemConfig.isOptSet("crosshairs"))
                args.Add("-crosshairs=" + SystemConfig["crosshairs"]);
            
            // force feedback
            if (SystemConfig.isOptSet("forceFeedback") && SystemConfig.getOptBoolean("forceFeedback"))
                args.Add("-force-feedback");

            //Write config in supermodel.ini
            SetupConfiguration(path, wideScreen);

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
        private void SetupConfiguration(string path, bool wideScreen)
        {
            try
            {
                string iniPath = Path.Combine(path, "Config", "Supermodel.ini");
                if (File.Exists(iniPath))
                {
                    using (IniFile ini = new IniFile(iniPath, IniOptions.UseSpaces))
                    {
                        //Fullscreen and widescreen values (should we keep these as commandline take precedent ?
                        ini.WriteValue(" Global ", "FullScreen", _bezelFileInfo == null ? "1" : "0");
                        ini.WriteValue(" Global ", "WideScreen", wideScreen ? "1" : "0");
                        
                        //throttle - default on
                        if (SystemConfig.isOptSet("throttle") && SystemConfig.getOptBoolean("throttle"))
                            ini.WriteValue(" Global ", "Throttle", SystemConfig["throttle"]);
                        else if (Features.IsSupported("throttle"))
                            ini.WriteValue(" Global ", "Throttle", "1");

                        //New3DEngine - setting to OFF will use legacy 3D engine, fixes OpenGL error on older GPUs
                        if (SystemConfig.isOptSet("new3Dengine") && SystemConfig["new3Dengine"] != "1")
                            ini.WriteValue(" Global ", "New3DEngine", "0");
                        else if (Features.IsSupported("new3Dengine"))
                            ini.WriteValue(" Global ", "New3DEngine", "1");

                        //force rompath in GUI
                        string rompath = Path.Combine(AppConfig.GetFullPath("roms"), "model3");
                        ini.WriteValue(" Supermodel3 UI ", "Dir", rompath);
                        
                        //Create controller configuration
                        CreateControllerConfiguration(ini);
                    }
                }
            }
            catch { }
        }
    }
}
