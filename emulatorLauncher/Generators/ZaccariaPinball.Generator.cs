using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using emulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using System.Threading;
using Microsoft.Win32;

namespace emulatorLauncher
{
    class ZaccariaPinballGenerator : Generator
    {
        public ZaccariaPinballGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {

            List<string> commandArray = new List<string>();

            string path = AppConfig.GetFullPath("steam");
            if (string.IsNullOrEmpty(path) || (core == "zaccariapinball-nonsteam" || core == "nosteam"))
                path = AppConfig.GetFullPath("zaccariapinball");

            string exe = Path.Combine(path, "ZaccariaPinball.exe");
            if (!File.Exists(exe) && (core == "zaccariapinball-steam" || core == "steam"))
                exe = Path.Combine(path, "zaccariapinball.cmd");

            if (!File.Exists(exe))
            {
                string folder = Path.GetDirectoryName(rom);
                while (folder != null)
                {
                    exe = Path.Combine(folder, "ZaccariaPinball.exe");
                    if (File.Exists(exe))
                    {
                        core = "zaccariapinball-nosteam";
                        path = folder;
                        break;
                    }

                    folder = Path.GetDirectoryName(folder);
                }
            }

            if (!File.Exists(exe))
                return null;

            if (core == "zaccariapinball-nosteam" || core == "nosteam")
            {
                commandArray.Add("-skipmenu");
            }

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

            string _args = string.Join(" ", commandArray);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path
            };

            if (_args != null)
                ret.Arguments = _args;

            string ext = Path.GetExtension(exe).ToLower();
            if (ext == ".bat" || ext == ".cmd")
            {
                ret.WindowStyle = ProcessWindowStyle.Hidden;
                ret.UseShellExecute = true;
            }

            return ret;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            if (exitCode == 1)
                return 0;

            return exitCode;
        }
    }
}