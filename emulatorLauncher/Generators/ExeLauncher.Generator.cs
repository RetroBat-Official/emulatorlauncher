using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class ExeLauncherGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            rom = this.TryUnZipGameIfNeeded(system, rom);

            _systemName = system;

            string path = Path.GetDirectoryName(rom);
            string arguments = null;

            if (Directory.Exists(rom)) // If rom is a directory ( .pc .win .windows, .wine )
            {
                path = rom;
                if (File.Exists(Path.Combine(rom, "autorun.cmd")))
                    rom = Path.Combine(rom, "autorun.cmd");
                else if (File.Exists(Path.Combine(rom, "autorun.bat")))
                    rom = Path.Combine(rom, "autorun.bat");
                else if (File.Exists(Path.Combine(rom, "autoexec.cmd")))
                    rom = Path.Combine(rom, "autoexec.cmd");
                else if (File.Exists(Path.Combine(rom, "autoexec.bat")))
                    rom = Path.Combine(rom, "autoexec.bat");
                else
                    rom = Directory.GetFiles(path, "*.exe").FirstOrDefault();
                
                if (Path.GetFileName(rom) == "autorun.cmd")
                {
                    var wineCmd = File.ReadAllLines(rom);
                    if (wineCmd == null || wineCmd.Length == 0)
                        throw new Exception("autorun.cmd is empty");

                    var dir = wineCmd.Where(l => l.StartsWith("DIR=")).Select(l => l.Substring(4)).FirstOrDefault();

                    var wineCommand = wineCmd.Where(l => l.StartsWith("CMD=")).Select(l => l.Substring(4)).FirstOrDefault();
                    if (string.IsNullOrEmpty(wineCommand) && wineCmd.Length > 0)
                        wineCommand = wineCmd.FirstOrDefault();

                    var args = wineCommand.SplitCommandLine();
                    if (args.Length > 0)
                    {
                        string exe = string.IsNullOrEmpty(dir) ? Path.Combine(path, args[0]) : Path.Combine(path, dir.Replace("/", "\\"), args[0]);
                        if (File.Exists(exe))
                        {
                            rom = exe;

                            if (args.Length > 1)
                                arguments = string.Join(" ", args.Skip(1).ToArray());
                        }
                        else
                            throw new Exception("Invalid autorun.cmd executable");
                    }
                    else
                        throw new Exception("Invalid autorun.cmd command");
                }
            }

            if (!File.Exists(rom))
                return null;

            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(path, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            UpdateMugenConfig(path, resolution);

            var ret = new ProcessStartInfo()
            {
                FileName = rom,
                WorkingDirectory = path            
            };

            if (arguments != null)
                ret.Arguments = arguments;

            string ext = Path.GetExtension(rom).ToLower();
            if (ext == ".bat" || ext == ".cmd")
            {
                ret.WindowStyle = ProcessWindowStyle.Hidden;
                ret.UseShellExecute = true;
            }
            else
                _exename = Path.GetFileNameWithoutExtension(rom);

            // If game was uncompressed, say we are going to launch, so the deletion will not be silent
            ValidateUncompressedGame();

            return ret;
        }

        private string _systemName;
        private string _exename;

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_systemName != "mugen" || string.IsNullOrEmpty(_exename))
                return mapping;

            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

        private void UpdateMugenConfig(string path, ScreenResolution resolution)
        {
            if (_systemName != "mugen")
                return;

            var cfg = Path.Combine(path, "data", "mugen.cfg");
            if (!File.Exists(cfg))
                return;

            using (var ini = IniFile.FromFile(cfg, IniOptions.UseSpaces | IniOptions.AllowDuplicateValues | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                if (resolution == null)
                    resolution = ScreenResolution.CurrentResolution;

                if (!string.IsNullOrEmpty(ini.GetValue("Config", "GameWidth")))
                {
                    ini.WriteValue("Config", "GameWidth", resolution.Width.ToString());
                    ini.WriteValue("Config", "GameHeight", resolution.Height.ToString());
                }

                ini.WriteValue("Video", "Width", resolution.Width.ToString());
                ini.WriteValue("Video", "Height", resolution.Height.ToString());
              
                ini.WriteValue("Video", "VRetrace", SystemConfig["VSync"] != "false" ? "1" : "0");                
                ini.WriteValue("Video", "FullScreen", "1");
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_systemName == "windows")
            {
                using (var frm = new System.Windows.Forms.Form())
                {
                    // Some games fail to allocate DirectX surface if EmulationStation is showing fullscren : pop an invisible window between ES & the game solves the problem
                    frm.ShowInTaskbar = false;
                    frm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
                    frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                    frm.Opacity = 0;
                    frm.Show();

                    System.Windows.Forms.Application.DoEvents();
                    base.RunAndWait(path);
                }
            }
            else
                base.RunAndWait(path);

            return 0;
        }
    }
}
