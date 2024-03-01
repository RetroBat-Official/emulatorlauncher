using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
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

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string iniPath = Path.ChangeExtension(exe, ".ini");

            SetupConfiguration(iniPath, system, rom, fullscreen);

            List<string> commandArray = new List<string>();

            if (_bezelFileInfo == null && fullscreen)
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

        private void SetupConfiguration(string iniPath, string system, string rom, bool fullscreen = true)
        {
            using (IniFile ini = new IniFile(iniPath))
            {
                if (Features.IsSupported("gfx_mode") && SystemConfig.isOptSet("gfx_mode"))
                    ini.WriteValue("scummvm", "gfx_mode", SystemConfig["gfx_mode"]);
                else
                    ini.WriteValue("scummvm", "gfx_mode", "opengl");

                if (Features.IsSupported("render_mode") && SystemConfig.isOptSet("render_mode"))
                    ini.WriteValue("scummvm", "render_mode", SystemConfig["render_mode"]);
                else
                    ini.Remove("scummvm", "render_mode");

                ini.WriteValue("scummvm", "confirm_exit", "false");
                ini.WriteValue("scummvm", "gui_return_to_launcher_at_exit", "false");
                ini.WriteValue("scummvm", "window_maximized", "false");

                // Discord
                if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                    ini.WriteValue("scummvm", "discord_rpc", "true");
                else
                    ini.WriteValue("scummvm", "discord_rpc", "false");

                if (_bezelFileInfo != null || !fullscreen)
                    ini.WriteValue("scummvm", "fullscreen", "false");
                else
                    ini.WriteValue("scummvm", "fullscreen", "true");

                if (Features.IsSupported("ratio") && SystemConfig.getOptBoolean("ratio"))
                    ini.WriteValue("scummvm", "aspect_ratio", "false");
                else
                    ini.WriteValue("scummvm", "aspect_ratio", "true");

                if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                    ini.WriteValue("scummvm", "vsync", "false");
                else
                    ini.WriteValue("scummvm", "vsync", "true");

                if (Features.IsSupported("scaler"))
                {
                    switch (SystemConfig["scaler"])
                    {
                        case "normal1":
                            ini.WriteValue("scummvm", "scale_factor", "1");
                            ini.WriteValue("scummvm", "scaler", "normal");
                            break;
                        case "normal2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "normal");
                            break;
                        case "normal3":
                            ini.WriteValue("scummvm", "scale_factor", "3");
                            ini.WriteValue("scummvm", "scaler", "normal");
                            break;
                        case "normal4":
                            ini.WriteValue("scummvm", "scale_factor", "4");
                            ini.WriteValue("scummvm", "scaler", "normal");
                            break;
                        case "normal5":
                            ini.WriteValue("scummvm", "scale_factor", "5");
                            ini.WriteValue("scummvm", "scaler", "normal");
                            break;
                        case "hq2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "hq");
                            break;
                        case "hq3":
                            ini.WriteValue("scummvm", "scale_factor", "3");
                            ini.WriteValue("scummvm", "scaler", "hq");
                            break;
                        case "edge2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "edge");
                            break;
                        case "edge3":
                            ini.WriteValue("scummvm", "scale_factor", "3");
                            ini.WriteValue("scummvm", "scaler", "edge");
                            break;
                        case "advmame2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "advmame");
                            break;
                        case "advmame3":
                            ini.WriteValue("scummvm", "scale_factor", "3");
                            ini.WriteValue("scummvm", "scaler", "advmame");
                            break;
                        case "advmame4":
                            ini.WriteValue("scummvm", "scale_factor", "4");
                            ini.WriteValue("scummvm", "scaler", "advmame");
                            break;
                        case "sai2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "sai");
                            break;
                        case "supersai2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "supersai");
                            break;
                        case "supereagle2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "supereagle");
                            break;
                        case "pm2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "pm");
                            break;
                        case "dotmatrix2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "dotmatrix");
                            break;
                        case "tv2":
                            ini.WriteValue("scummvm", "scale_factor", "2");
                            ini.WriteValue("scummvm", "scaler", "tv");
                            break;
                        default:
                            ini.Remove("scummvm", "scale_factor");
                            ini.Remove("scummvm", "scaler");
                            break;
                    }
                }

                if (Features.IsSupported("smooth"))
                {
                    if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
                        ini.WriteValue("scummvm", "filtering", "true");
                    else
                        ini.WriteValue("scummvm", "filtering", "false");
                }

                if (Features.IsSupported("scumm_subtitles") && SystemConfig["scumm_subtitles"] == "subtitles")
                {
                    ini.WriteValue("scummvm", "subtitles", "true");
                    ini.WriteValue("scummvm", "speech_mute", "true");
                }
                else if (Features.IsSupported("scumm_subtitles") && SystemConfig["scumm_subtitles"] == "voice")
                {
                    ini.WriteValue("scummvm", "subtitles", "false");
                    ini.WriteValue("scummvm", "speech_mute", "false");
                }
                else
                {
                    ini.WriteValue("scummvm", "subtitles", "true");
                    ini.WriteValue("scummvm", "speech_mute", "false");
                }

                if (Features.IsSupported("antialiasing") && SystemConfig.isOptSet("antialiasing"))
                    ini.WriteValue("scummvm", "antialiasing", SystemConfig["antialiasing"]);
                else
                    ini.Remove("scummvm", "antialiasing");

                ini.WriteValue("scummvm", "updates_check", "0");

                if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
                {
                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }

                    ini.WriteValue("scummvm", "savepath", savePath);
                }

                // Write themepath and extrapath (same location as libretro scummvm core)
                string biosPath = AppConfig.GetFullPath("bios");
                string themePath = Path.Combine(biosPath, "scummvm", "theme");
                string extraPath = Path.Combine(biosPath, "scummvm", "extra");

                if (Directory.Exists(themePath))
                {
                    ini.WriteValue("scummvm", "themepath", themePath);
                }
                if (Directory.Exists(extraPath))
                {
                    ini.WriteValue("scummvm", "extrapath", extraPath);
                }

                if (Features.IsSupported("unsupported_games") && SystemConfig.getOptBoolean("unsupported_games"))
                    ini.WriteValue("scummvm", "enable_unsupported_game_warning", "false");
                else
                    ini.Remove("scummvm", "enable_unsupported_game_warning");

                //SetupGameSettings(ini, rom);

                ini.Save();
            }
        }

        private void SetupGameSettings(IniFile ini, string rom)
        {
            if (!File.Exists(rom) || !rom.EndsWith(".scummvm", StringComparison.OrdinalIgnoreCase))
                return;

            var lines = File.ReadAllLines(rom);
            
            if (lines == null)
                return;
            
            if (lines.Length == 0)
                return;

            string gameName = lines[0].Trim();
            string engine = null;
            string gameid = null;
            bool noCreateSection = false;

            if (string.IsNullOrEmpty(gameName))
                return;

            // Since recent versions of scummvm, .scummvm need to contain engine:gameid
            if (gameName.Contains(":"))
            {
                string[] parts = gameName.Split(new[] { ':' }, 2);
                if (parts.Length > 1)
                {
                    gameName = parts[1].Trim();
                    gameid = gameName;
                    engine = parts[0].Trim();
                }
            }

            // Case where games are already installed in scummvm and .scummvm only contains gameid of installed game
            else
            {
                string delimiter = "-";
                int lastOccurrenceIndex = gameName.LastIndexOf(delimiter);
                if (lastOccurrenceIndex != -1)
                    gameid = gameName.Substring(0, lastOccurrenceIndex).Trim();
                else
                    gameid = gameName;
                noCreateSection = true;
            }

            var iniSection = ini.EnumerateSections();

            // Do not create section if we don't know engine + gameID and return if section does not exist
            if (noCreateSection && !iniSection.Contains(gameName))
                return;

            // Create game section with basic data if not existing already
            if (!iniSection.Contains(gameName))
            {
                ini.WriteValue(gameName, "gameid", gameid);
                ini.WriteValue(gameName, "description", Path.GetFileName(Path.GetDirectoryName(rom)));
                
                if (engine != null)
                    ini.WriteValue(gameName, "engineid", engine);
                
                ini.WriteValue(gameName, "path", Path.GetDirectoryName(rom));
            }

            // Language
            if (SystemConfig.isOptSet("scumm_language") && !string.IsNullOrEmpty(SystemConfig["scumm_language"]))
                ini.WriteValue(gameName, "language", SystemConfig["scumm_language"]);
            else if (ini.EnumerateValues(gameName).Any(v => v.Key == "language"))
                ini.Remove(gameName, "language");

            // other interesting values (to be investigated in future)
            // original_gui
            // platform
            // extra
            // enhancements
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
