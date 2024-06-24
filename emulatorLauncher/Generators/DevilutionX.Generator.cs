using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class DevilutionXGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(system);
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "devilutionx.exe");
            if (!File.Exists(exe))
                return null;

            //string conf = Path.Combine(path, "diablo.ini");       TO BE USED LATER FOR FEATURES
            if (!File.Exists(exe))
                return null;

            //bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string gamePath = AppConfig.GetFullPath(rom);
            string gameExtension = Path.GetExtension(rom).Replace(".", "");
            string gameName = new DirectoryInfo(gamePath).Name.Replace("." + gameExtension, "");

            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, gameName);
            if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                catch { }

            // Command line arguments
            List<string> commandArray = new List<string>
            {
                "--data-dir" + " " + gamePath,
                "--config-dir" + " " + gamePath,
                "--save-dir" + " " + savesPath
            };

            // Command line argument to start in windowed mode seems not to work (devilutionx bug ?). Leave it commented for now.
            /*
            if (!fullscreen)
                commandArray.Add("-x");
            */

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
            int ret = base.RunAndWait(path);
            return ret;
        }
    }
}
