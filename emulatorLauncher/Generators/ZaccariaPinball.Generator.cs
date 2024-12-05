using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.PadToKeyboard;
using System.Threading;
using Microsoft.Win32;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    class ZaccariaPinballGenerator : Generator
    {
        public ZaccariaPinballGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _exename;
        private string _corename;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = null;
            string exe = null;
            _corename = core;
            _exename = "ZaccariaPinball";

            List<string> commandArray = new List<string>();

            if (core == "steam")
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam"))
                    {
                        if (key != null)
                        {
                            Object o = key.GetValue("InstallPath");
                            if (o != null)
                            {
                                path = o as string;
                            }
                        }
                    }
                }
                catch
                {
                    throw new ApplicationException("Steam is not installed");
                }

                if (!Directory.Exists(path))
                    return null;

                exe = Path.Combine(path, "steam.exe");
                if (!File.Exists(exe))
                    return null;

                commandArray.Add("-nofriendsui");
                commandArray.Add("-silent");
                commandArray.Add("-applaunch");
                commandArray.Add("444930");
            }

            else if (core == "zaccariapinball-nonsteam" || core == "nonsteam")
            {
                path = AppConfig.GetFullPath("zaccariapinball");
                if (!Directory.Exists(path))
                    return null;
                
                exe = Path.Combine(path, "ZaccariaPinball.exe");
                if (!File.Exists(exe))
                    return null;
            }
            
            commandArray.Add("-skipmenu");

            string table = "\"" + Path.GetFileNameWithoutExtension(rom) + "\"";
            commandArray.Add(table);

            if (SystemConfig.isOptSet("zaccaria_rotate") && !string.IsNullOrEmpty(SystemConfig["zaccaria_rotate"]))
            {
                string rotatedirection = SystemConfig["zaccaria_rotate"];
                commandArray.Add("-rotate");
                commandArray.Add(rotatedirection);
            }

            if (SystemConfig.isOptSet("zaccaria_players") && !string.IsNullOrEmpty(SystemConfig["zaccaria_players"]))
            {
                string players = SystemConfig["zaccaria_players"];
                commandArray.Add("-skipmenu_player");
                commandArray.Add(players);
            }
            else
            {
                commandArray.Add("-skipmenu_player");
                commandArray.Add("1");
            }

            if (SystemConfig.isOptSet("zaccaria_gamemode") && !string.IsNullOrEmpty(SystemConfig["zaccaria_gamemode"]))
            {
                string gamemode = SystemConfig["zaccaria_gamemode"];
                commandArray.Add("-skipmenu_gamemode");
                commandArray.Add(gamemode);
            }
            else
            {
                commandArray.Add("-skipmenu_gamemode");
                commandArray.Add("classic_arcade");
            }

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_corename == "steam")
            {
                foreach (var z in Process.GetProcessesByName("ZaccariaPinball"))
                    try 
                    { 
                        z.Kill();
                        z.WaitForExit(3000);
                    } 
                    catch { }

                Process process = Process.Start(path);
                
                int i = 1;
                Process[] zaccarialist = Process.GetProcessesByName("ZaccariaPinball");

                while (i <= 5 && zaccarialist.Length == 0)
                {
                    zaccarialist = Process.GetProcessesByName("ZaccariaPinball");
                    Thread.Sleep(8000);
                    i++;
                }

                if (zaccarialist.Length == 0)
                    return 0;
                else
                {
                    Process zaccaria = zaccarialist.OrderBy(p => p.StartTime).FirstOrDefault();
                    zaccaria.WaitForExit();
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