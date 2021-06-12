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
                    var wineCommand = File.ReadAllText(rom);

                    int idx = wineCommand.IndexOf("CMD=");
                    if (idx >= 0)
                        wineCommand = wineCommand.Substring(idx + 4).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                    var args = SplitCommandLine(wineCommand);
                    if (args.Length > 0)
                    {
                        string exe = Path.Combine(path, args[0]);
                        if (File.Exists(exe))
                        {
                            rom = exe;
                            
                            if (args.Length > 1)
                                arguments = string.Join(" ", args.Skip(1).ToArray());
                        }
                    }
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

            UpdateMugenConfig(path);

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

            return ret;
        }

        static string[] SplitCommandLine(string commandLine)
        {
            char[] parmChars = commandLine.ToCharArray();
            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++)
            {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }
            return (new string(parmChars)).Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Replace("\"", "")).ToArray();
        }

        private string _systemName;
        private string _exename;

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_systemName != "mugen")
                return mapping;

            if (string.IsNullOrEmpty(_exename))
                return mapping;

            if (Program.Controllers.Count(c => c.Config != null && c.Config.DeviceName != "Keyboard") == 0)
                return mapping;

            if (mapping != null && mapping[_exename] != null && mapping[_exename][InputKey.hotkey | InputKey.start] != null)
                return mapping;

            if (mapping == null)
                mapping = new PadToKeyboard.PadToKey();

            var app = new PadToKeyApp();
            app.Name = _exename;

            PadToKeyInput mouseInput = new PadToKeyInput();
            mouseInput.Name = InputKey.hotkey | InputKey.start;
            mouseInput.Type = PadToKeyType.Keyboard;
            mouseInput.Key = "(%{KILL})";
            app.Input.Add(mouseInput);
            mapping.Applications.Add(app);

            return mapping;
        }

        private void UpdateMugenConfig(string path)
        {
            if (_systemName != "mugen")
                return;

            var cfg = Path.Combine(path, "data", "mugen.cfg");
            if (!File.Exists(cfg))
                return;

            var data = File.ReadAllText(cfg);
            data = data.Replace("FullScreen = 0", "FullScreen = 1");
            File.WriteAllText(cfg, data);
        }
    }
}
