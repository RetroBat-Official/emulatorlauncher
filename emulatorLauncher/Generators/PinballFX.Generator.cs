using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using EmulatorLauncher.Common;

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

        private static List<string> pinballfxsystems = new List<string>() { "pinballfx", "pinballfx2", "pinballfx3", "pinballm" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            List<string> commandArray = new List<string>();

            string path = null;
            string exe = null;
            _corename = core;

            if (system == "pinballfx")
                _exename = "PinballFX";

            if (system == "pinballfx2")
                _exename = "Pinball FX2";

            if (system == "pinballfx3")
                _exename = "Pinball FX3";

            if (system == "pinballm")
                _exename = "PinballM";

            if (core == "steam")
            {
                path = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null);
                if (string.IsNullOrEmpty(path))
                    path = Path.Combine(Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%"), "Steam");

                exe = Path.Combine(path, "steam.exe");

                commandArray.Add("-nofriendsui");
                commandArray.Add("-silent");
                commandArray.Add("-applaunch");

                if (system == "pinballfx")
                    commandArray.Add("2328760");

                if (system == "pinballfx2")
                    commandArray.Add("226980");

                if (system == "pinballfx3")
                    commandArray.Add("442120");

                if (system == "pinballm")
                    commandArray.Add("2337640");

            }

            if (core == "hack" || core == "nonsteam")
            {
                path = AppConfig.GetFullPath(system);
                exe = Path.Combine(path, _exename + ".exe");

            }

            if (system == "pinballfx" || system == "pinballm")
            {
                if (!File.Exists(rom) || Path.GetExtension(rom).ToLower() != ".table")
                    return null;

                string[] lines = File.ReadAllLines(rom);
                if (lines.Length == 0)
                    return null;

                rom = lines[0];
                int tableId = rom.ToInteger();

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
                throw new ApplicationException("There is a problem: The Game is not installed");

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };

        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_corename == "steam")
            {
                foreach (var z in Process.GetProcessesByName(_exename))
                    try
                    {
                        z.Kill();
                        z.WaitForExit(3000);
                    }
                    catch { }

                Process process = Process.Start(path);

                int i = 1;
                Process[] pinballfxlist = Process.GetProcessesByName(_exename);

                while (i <= 5 && pinballfxlist.Length == 0)
                {
                    pinballfxlist = Process.GetProcessesByName(_exename);
                    Thread.Sleep(8000);
                    i++;
                }

                if (pinballfxlist.Length == 0)
                    return 0;
                else
                {
                    Process pinballfx = pinballfxlist.OrderBy(p => p.StartTime).FirstOrDefault();
                    pinballfx.WaitForExit();
                    if (SystemConfig.isOptSet("killsteam") && SystemConfig.getOptBoolean("killsteam"))
                    {
                        foreach (var p in Process.GetProcessesByName("steam"))
                        {
                            p.Kill();
                        }
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