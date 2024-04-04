using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private ScreenResolution _resolution;
        private string _system;
        private string _emulator;
        private string _path;
        private string _exeName;
        private bool _fullscreen;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            _system = system;
            _emulator = emulator;
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            _path = AppConfig.GetFullPath(emulator);
            if (!Directory.Exists(_path))
                return null;

            _exeName = exeDictionnary[emulator];
            string exe = Path.Combine(_path, _exeName);
            if (!File.Exists(exe))
                return null;


            List<string> commandArray = new List<string>();

            ConfigurePort(commandArray, rom, exe);

            string args = null;
            if (commandArray.Count > 0)
                args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args,
            };
        }

        private readonly Dictionary<string, string> exeDictionnary = new Dictionary<string, string>
        {
            { "sonic3air", "Sonic3AIR.exe"},
        };
    }
}
