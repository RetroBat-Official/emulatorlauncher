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
    class KegaFusionGenerator : Generator
    {
        public KegaFusionGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("kega-fusion");

            string exe = Path.Combine(path, "Fusion.exe");
            if (!File.Exists(exe))
                return null;

            List<string> commandArray = new List<string>();

            if (core == "mastersystem" || core == "sms")
            {                
                commandArray.Add("-sms");
            }
            else if (core == "gamegear" || core == "gg")
            {
                commandArray.Add("-gg");
            }
            else if (core == "megadrive" || core == "md")
            {
                commandArray.Add("-md");
            }
            else if (core == "genesis" || core == "gen")
            {                
                commandArray.Add("-gen");
            }
			else if (core == "sega32x")
            {                
                commandArray.Add("-32x");
            }
			else if (core == "megacd")
            {                
                commandArray.Add("-mcd");
            }
			else if (core == "segacd")
            {                
                commandArray.Add("-scd");
            }
			else if (core == "auto")
            {                
                commandArray.Add("-auto");
            }
            
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int exitCode = base.RunAndWait(path);

            // Fusion always returns 1....
            if (exitCode == 1)
                return 0;

            return exitCode;
        }
    }
}