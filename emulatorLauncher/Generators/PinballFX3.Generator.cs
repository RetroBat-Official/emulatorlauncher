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
        public PinballFX3Generator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {

            List<string> commandArray = new List<string>();

            string path = AppConfig.GetFullPath("steam");
            if (string.IsNullOrEmpty(path) || (core == "pinballfx-hack" || core == "hack"))
                path = AppConfig.GetFullPath("pinballfx3");

            string exe = Path.Combine(path, "Pinball FX3.exe");
            if (!File.Exists(exe) && (core == "pinballfx-steam" || core == "steam"))
                exe = Path.Combine(path, "pinballfx3.cmd");

            if (!File.Exists(exe))
            {
                string folder = Path.GetDirectoryName(rom);
                while (folder != null)
                {
                    exe = Path.Combine(folder, "Pinball FX3.exe");
                    if (File.Exists(exe))
                    {
                        core = "pinballfx3-nosteam";
                        path = folder;
                        break;
                    }

                    folder = Path.GetDirectoryName(folder);
                }
            }

            if (!File.Exists(exe))
                return null;

            if (core == "pinballfx3-nosteam" || core == "pinballfx3-hack" || core == "hack")
            {
                commandArray.Add("-offline");
                commandArray.Add("-class");
                commandArray.Add("-table_");
            }

            string _args = string.Join(" ", commandArray);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path
            };
            
            if (_args != null)
                ret.Arguments = _args + Path.GetFileNameWithoutExtension(rom);

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