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
    class Mame64Generator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mame");
            if (string.IsNullOrEmpty(path) && Environment.Is64BitOperatingSystem)
                path = AppConfig.GetFullPath("mame64");

            string exe = Path.Combine(path, "mame.exe");
            if (!File.Exists(exe) && Environment.Is64BitOperatingSystem)
                exe = Path.Combine(path, "mame64.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "mame32.exe");

            if (!File.Exists(exe))
                return null;

            _exeName = Path.GetFileNameWithoutExtension(exe);

            string args = null;

            MessSystem messMode = MessSystem.GetMessSystem(system, core);
            if (messMode == null)
            {
                List<string> commandArray = new List<string>();

                commandArray.Add("-skip_gameinfo");

                // rompath
                commandArray.Add("-rompath");
                if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig["bios"]))
                    commandArray.Add(AppConfig.GetFullPath("bios") + ";" + Path.GetDirectoryName(rom));
                else
                    commandArray.Add(Path.GetDirectoryName(rom));

                // Unknown system, try to run with rom name only
                commandArray.Add(Path.GetFileName(rom));

                args = string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());
            }
            else
                args = messMode.GetMameCommandLineArguments(system, rom, false);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private string _exeName;

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _exeName, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }
    }
}
