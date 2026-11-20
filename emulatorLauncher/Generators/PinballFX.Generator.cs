using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmulatorLauncher
{
    class PinballFXGenerator : Generator
    {
        public PinballFXGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _exename;
        private string _corename;
        private List<string> _exenames;

        private static readonly List<string> pinballfxsystems = new List<string>() { "pinballfx", "pinballfx2", "pinballfx3", "pinballm" };
        private static readonly Dictionary<string, string> _systemExeNames = new Dictionary<string, string>()
        {
            { "pinballfx",  "PinballFX" },
            { "pinballfx2", "Pinball FX2" },
            { "pinballfx3", "Pinball FX Classic" },
            { "pinballm",   "Pinball FX Midnight" }
        };

        private static readonly Dictionary<string, string> _steamAppIds = new Dictionary<string, string>()
        {
            { "pinballfx",  "2328760" },
            { "pinballfx2", "226980" },
            { "pinballfx3", "442120" },
            { "pinballm",   "2337640" }
        };
        // Exe names (current Steam name first, then older names and other stores) and Unreal Engine game process.
        // Case does not matter (File.Exists, Process.GetProcessesByName).
        private static readonly Dictionary<string, List<string>> _fallbackExeNames = new Dictionary<string, List<string>>()
        {
            { "pinballfx",  new List<string> { "PinballFX", "Pinball FX", "Pinball_FX", "PinballFX-Win64-Shipping" } },
            { "pinballfx2", new List<string> { "Pinball FX2", "PinballFX2", "Pinball_FX2" } },
            { "pinballfx3", new List<string> { "Pinball FX Classic", "Pinball FX3", "PinballFX3", "Pinball_FX3" } },
            { "pinballm",   new List<string> { "Pinball FX Midnight", "Pinball M", "PinballM", "PinballM-Win64-Shipping" } }
        };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            List<string> commandArray = new List<string>();

            string path = null;
            string exe = null;
            _corename = core;

            if (_systemExeNames.TryGetValue(system, out string exeName))
                _exename = exeName;

            if (_fallbackExeNames.TryGetValue(system, out var candidates))
                _exenames = candidates;

            if (core == "steam")
            {
                path = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null);
                if (string.IsNullOrEmpty(path))
                    path = Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%"), "Steam");

                exe = Path.Combine(path, "steam.exe");

                commandArray.Add("-nofriendsui");
                commandArray.Add("-silent");
                commandArray.Add("-applaunch");

                if (_steamAppIds.TryGetValue(system, out string appId))
                    commandArray.Add(appId);
            }

            if (core == "hack" || core == "nonsteam")
            {
                // Game folder: <system> key of emulatorLauncher.cfg if any, otherwise emulators\<system>
                path = AppConfig.GetFullPath(system);
                if (string.IsNullOrEmpty(path))
                    path = Path.Combine(AppConfig.GetFullPath("retrobat"), "emulators", system);
                exe = _exenames
                    .Select(name => Path.Combine(path, name + ".exe"))
                    .FirstOrDefault(File.Exists) ?? Path.Combine(path, _exename + ".exe");

                _exename = Path.GetFileNameWithoutExtension(exe);
            }

            if (system == "pinballfx" || system == "pinballm")
            {
                if (!File.Exists(rom) || Path.GetExtension(rom).ToLower() != ".table")
                    return null;

                // First non-empty line of the .table file = table number
                string firstLine = File.ReadAllLines(rom).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
                int tableId;
                if (firstLine == null || !int.TryParse(firstLine, out tableId) || tableId <= 0)
                    throw new ApplicationException("'" + Path.GetFileName(rom) + "' must contain the table number on its first line.");

                commandArray.Add("-Table" + " " + tableId);

                switch (system)
                {
                    case "pinballfx":
                        if (SystemConfig.isOptSet("pinballfx_gamemode") && !string.IsNullOrEmpty(SystemConfig["pinballfx_gamemode"]))
                            commandArray.Add("-GameMode" + " " + SystemConfig["pinballfx_gamemode"]);
                        break;
                    case "pinballm":
                        if (SystemConfig.isOptSet("pinballm_gamemode") && !string.IsNullOrEmpty(SystemConfig["pinballm_gamemode"]))
                            commandArray.Add("-GameMode" + " " + SystemConfig["pinballm_gamemode"]);
                        break;
                }
            }

            else if (system == "pinballfx2")
            {
                if (!File.Exists(rom))
                    return null;

                if (core == "steam")
                    commandArray.Add(Path.GetFileNameWithoutExtension(rom));
                else if (core == "hack" || core == "nonsteam")
                    commandArray.Add("/LoadTable " + "\"" + Path.GetFileNameWithoutExtension(rom) + "\"");

            }

            else if (system == "pinballfx3")
            {
                if (SystemConfig.isOptSet("pinballfx3_offline") && SystemConfig.getOptBoolean("pinballfx3_offline"))
                    commandArray.Add("-offline");

                if (SystemConfig.isOptSet("pinballfx3_classic") && SystemConfig.getOptBoolean("pinballfx3_classic"))
                    commandArray.Add("-class");

                if (SystemConfig.isOptSet("pinballfx3_players") && SystemConfig["pinballfx3_players"] != "1")
                    commandArray.Add("-hotseat_" + SystemConfig["pinballfx3_players"]);

                commandArray.Add("-table_" + Path.GetFileNameWithoutExtension(rom));
            }

            string args = string.Join(" ", commandArray);

            if (!File.Exists(exe) || !pinballfxsystems.Contains(system))
            {
                if (core == "steam")
                    throw new ApplicationException("Steam is not installed.");

                throw new ApplicationException("Game not found: copy it in '" + path + "' (" + Path.GetFileName(exe) + ").");
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };

        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            foreach (var name in _exenames)
                PadToKey.AddOrUpdateKeyMapping(mapping, name, InputKey.hotkey | InputKey.start, "(%{CLOSE})");

            return mapping;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_corename == "steam")
            {
                foreach (var name in _exenames)
                    foreach (var z in Process.GetProcessesByName(name))
                        try
                        {
                            z.Kill();
                            z.WaitForExit(3000);
                        }
                        catch { }

                Process process = Process.Start(path);

                int i = 1;
                Process[] pinballfxlist = new Process[0];

                while (i <= 10 && pinballfxlist.Length == 0)
                {
                    pinballfxlist = _exenames
                        .SelectMany(name => Process.GetProcessesByName(name))
                        .ToArray();
                    Thread.Sleep(4000);
                    i++;
                }

                if (pinballfxlist.Length == 0)
                    return 0;
                else
                {
                    Process pinballfx = pinballfxlist.OrderBy(p => p.StartTime).FirstOrDefault();
                    SimpleLogger.Instance.Info("PinballFX process found: " + pinballfx.ProcessName);
                    Job.Current.AddProcess(pinballfx);
                    pinballfx.WaitForExit();
                    if (SystemConfig.isOptSet("killsteam") && SystemConfig.getOptBoolean("killsteam"))
                    {
                        foreach (var p in Process.GetProcessesByName("steam"))
                            p.Kill();
                    }
                    else
                        return 0;
                }
                return 0;                
            }
            else
                base.RunAndWait(path);

            return 0;
        }
    }
}