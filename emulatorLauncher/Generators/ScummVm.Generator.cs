using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;
using VPinballLauncher;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class ScummVmGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("scummvm");

            string exe = Path.Combine(path, "scummvm.exe");
            if (!File.Exists(exe))
                return null;

            rom = this.TryUnZipGameIfNeeded(system, rom, true);

            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.scummvm").FirstOrDefault();
                if (string.IsNullOrEmpty(rom))
                    throw new ApplicationException("Unable to find scummvm file in the provided folder");
            }

            var platform = ReshadeManager.GetPlatformFromFile(exe);
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, platform, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            
            _resolution = resolution;

            string iniPath = Path.ChangeExtension(exe, ".ini");

            using (IniFile ini = new IniFile(iniPath))
            {
                ini.WriteValue("scummvm", "gfx_mode", "opengl");
                ini.WriteValue("scummvm", "confirm_exit", "false");
                ini.WriteValue("scummvm", "gui_return_to_launcher_at_exit", "false");
                ini.WriteValue("scummvm", "window_maximized", "false");

                if (_bezelFileInfo != null)
                    ini.WriteValue("scummvm", "fullscreen", "false");
                else
                    ini.WriteValue("scummvm", "fullscreen", "true");

                if (Features.IsSupported("ratio") && SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "stretch")
                {
                    ini.WriteValue("scummvm", "aspect_ratio", "false");                        
                    _bezelFileInfo = null;
                }
                else
                    ini.WriteValue("scummvm", "aspect_ratio", "true");

                if (SystemConfig.isOptSet("VSync") && !SystemConfig.getOptBoolean("VSync"))
                    ini.WriteValue("scummvm", "vsync", "false");
                else
                    ini.WriteValue("scummvm", "vsync", "true");

                if (Features.IsSupported("scaler"))
                {
                    if (SystemConfig.isOptSet("scaler"))
                        ini.WriteValue("scummvm", "scaler", SystemConfig["scaler"]);
                    else
                        ini.Remove("scummvm", "scaler");
                }

                if (Features.IsSupported("smooth"))
                {
                    if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                        ini.WriteValue("scummvm", "filtering", "true");
                    else
                        ini.WriteValue("scummvm", "filtering", "false");
                }

                ini.WriteValue("scummvm", "updates_check", "0");

                if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
                {
                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }

                    ini.WriteValue("scummvm", "savepath", savePath);
                }
            }

            List<string> commandArray = new List<string>();

            if (_bezelFileInfo == null)
                commandArray.Add("--fullscreen");

            commandArray.Add("--no-console");
            commandArray.Add("--config=\"" + iniPath + "\"");
            commandArray.Add("--logfile=\"" + Path.ChangeExtension(iniPath, ".log") + "\"");            
            commandArray.Add("-p\"" + Path.GetDirectoryName(rom)+"\"");

            string gameName = File.ReadAllText(rom);

            if (string.IsNullOrEmpty(gameName))
                gameName = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
            else
                gameName = gameName.Trim();               

            commandArray.Add("\"" + gameName + "\"");

            var args = string.Join(" ", commandArray.ToArray()); // .Select(a => a.Contains(" ") ? "\"" + a + "\"" : a)

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int ret = -1;

            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
            {
                if (_resolution == null)
                    _resolution = ScreenResolution.CurrentResolution;

                var frm = new Form()
                {
                    ShowInTaskbar = false,
                    WindowState = FormWindowState.Maximized,
                    FormBorderStyle = FormBorderStyle.None,
                    BackColor = System.Drawing.Color.FromArgb(204,102,0),
                };
                frm.Show();

                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

                System.Windows.Forms.Application.DoEvents();

                var process = Process.Start(path);
                while (process != null)
                {
                    if (process.WaitForExit(20))
                    {
                        process = null;
                        break;
                    }

                    var hWnd = User32.FindHwnds(process.Id, wnd => User32.GetClassName(wnd) == "SDL_app").FirstOrDefault();
                    if (hWnd == IntPtr.Zero || !User32.IsWindowVisible(hWnd))
                        continue;

                    var style = User32.GetWindowStyle(hWnd);
                    if ((style & WS.CAPTION) == WS.CAPTION)
                    {
                        System.Threading.Thread.Sleep(10);

                        style &= ~WS.BORDER;
                        style &= ~WS.CAPTION;
                        style &= ~WS.DLGFRAME;
                        style &= ~WS.MAXIMIZEBOX;
                        style &= ~WS.MINIMIZEBOX;
                        style &= ~WS.SYSMENU;
                        style &= ~WS.THICKFRAME;
                        style &= ~WS.MAXIMIZE;

                        User32.SetWindowStyle(hWnd, style);
                        User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, _resolution.Width, _resolution.Height, SWP.FRAMECHANGED);

                        if (frm != null)
                        {
                            frm.Dispose();
                            frm = null;
                        }

                        break;
                    }
                }

                if (process != null)
                {
                    process.WaitForExit();

                    try { ret = process.ExitCode; }
                    catch { }
                }
            }
            else
                ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            return ret;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "scummvm", InputKey.hotkey | InputKey.start, "(%{KILL})");
        }        
    }
}
