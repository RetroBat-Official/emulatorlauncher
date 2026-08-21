using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace EmulatorLauncher
{
    partial class DecompLauncherGenerator : Generator
    {
        private string _exeName;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            bool fullscreen = ShouldRunFullscreen();
            string path = Directory.Exists(rom) ? rom : Path.GetDirectoryName(rom);
            string arguments = "";
            string exe = null;
            _exeName = null;

            // If rom is a folder
            if (Directory.Exists(rom))
            {
                var exeFiles = Directory.GetFiles(rom, "*.exe", SearchOption.TopDirectoryOnly).ToArray();
                if (exeFiles.Length == 0)
                {
                    exeFiles = Directory.GetFiles(rom, "*.exe", SearchOption.AllDirectories).ToArray();
                }
                if (exeFiles.Length == 0)
                    return null;

                exe = exeFiles[0];
            }
            else if (File.Exists(rom)) // rom is a file
            {
                string line = FileTools.ReadFirstValidLine(rom);
                var exeFiles = Directory.GetFiles(Path.GetDirectoryName(rom), "*.exe", SearchOption.TopDirectoryOnly).ToArray();
                if (exeFiles.Length == 0)
                    return null;

                if (line == null)
                {
                    exe = exeFiles[0];
                }
                else
                {
                    exe = exeFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(line, StringComparison.InvariantCultureIgnoreCase));
                    if (exe == null)
                        exe = exeFiles[0];
                }
            }
            else
                return null;

            SimpleLogger.Instance.Info("[INFO] Executable to run: " + exe);

            if (!GetProcessFromFile(rom))
                _exeName = Path.GetFileNameWithoutExtension(exe);


            if (string.IsNullOrEmpty(_exeName))
            {
                SimpleLogger.Instance.Info("[INFO] No executable found.");
                return null;
            }

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = arguments,
            };


            SimpleLogger.Instance.Info("[INFO] Executable name : " + _exeName);

            return ret;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _exeName, InputKey.hotkey | InputKey.start, "(%{CLOSE})");
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            Process.Start(path);
            SimpleLogger.Instance.Info("Process started : " + _exeName);

            Process game = WaitForGameProcess(5, _exeName);
            
            if (game == null)
            {
                SimpleLogger.Instance.Info("Process : " + _exeName + " not running.");
            }
            else
            {
                SimpleLogger.Instance.Info("Process : " + _exeName + " found, waiting to exit");
                Job.Current.AddProcess(game);
                game.WaitForExit();
            }
            return 0;
        }

        private bool GetProcessFromFile(string rom)
        {
            string dir = rom;
            if (!Directory.Exists(dir))
                dir = Path.GetDirectoryName(rom);
            if (!Directory.Exists(dir))
                return false;

            string file = Path.GetFileNameWithoutExtension(rom);

            if (string.IsNullOrEmpty(file))
                file = "default";

            string executableFile = Path.Combine(dir, file + ".gameexe");

            if (!File.Exists(executableFile))
                return false;

            string line = FileTools.ReadFirstValidLine(executableFile);
            if (line == null)
                return false;
            else
            {
                if (line.ToLowerInvariant().EndsWith(".exe"))
                    line = line.Substring(0, line.Length - 4);

                _exeName = line;
                SimpleLogger.Instance.Info("[INFO] Executable name specified in .gameexe file: " + _exeName);

                return true;
            }
        }

        private Process WaitForGameProcess(int waitSeconds, string exeName)
        {
            Thread.Sleep(1000);

            for (int i = 0; i < waitSeconds; i++)
            {
                var list = Process.GetProcessesByName(exeName);
                if (list.Length > 0)
                    return list.OrderBy(p => p.StartTime).FirstOrDefault();
                Thread.Sleep(1000);
            }
            return null;
        }
    }
}
