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

namespace emulatorLauncher
{
    class PinballFX3Generator : Generator
    {

        bool _steam = false;

        public PinballFX3Generator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {

            List<string> commandArray = new List<string>();

            string folderName = (emulator == "pinballfx3" || core == "pinballfx3") ? "pinballfx3" : "steam";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("pinballfx3");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("steam");

            if (core == "pinballfx3" || core == "pinballfx3-steam" || core == "steam")
                path = AppConfig.GetFullPath("steam");

            string exe = Path.Combine(path, "Pinball FX3.exe");
            if (!File.Exists(exe))
            {
                _steam = true;
                exe = Path.Combine(path, "pinballfx3.cmd");
            }

            if (!File.Exists(exe))
                return null;

            commandArray.Add("-applaunch 442120");
            commandArray.Add("-offline");
            commandArray.Add("-class");

            if (core == "pinballfx3-nosteam" || core == "pinballfx3-hack" || core == "hack" || _steam == false)
            {
                commandArray.Remove("-applaunch 442120");
            }
            else if (core == "pinballfx3" || core == "pinballfx3-steam" || core == "steam" || _steam == true)
            {
                commandArray.Remove("-applaunch 442120");
                commandArray.Remove("-offline");
                commandArray.Remove("-class");
            }

            string _args = string.Join(" ", commandArray);
            /*
            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = _args + " -table_" + Path.GetFileNameWithoutExtension(rom),
                WorkingDirectory = path,
            };
            */
            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path
            };

            if (_args != null)
                ret.Arguments = _args + " -table_" + Path.GetFileNameWithoutExtension(rom);

            string ext = Path.GetExtension(exe).ToLower();
            if (ext == ".bat" || ext == ".cmd")
            {
                ret.WindowStyle = ProcessWindowStyle.Hidden;
                ret.UseShellExecute = true;
            }

            return ret;


    }

    }
}