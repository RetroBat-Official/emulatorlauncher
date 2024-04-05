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
            
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // Specific cases for some emulators
            if (rom.Contains("soniccd") || SystemConfig.getOptBoolean("sonicretro_sonicCD"))
                emulator = "sonicretrocd";

            _emulator = emulator;
            _path = AppConfig.GetFullPath(emulator);
            if (!Directory.Exists(_path))
                return null;

            _exeName = exeDictionnary[emulator];
            string exe = Path.Combine(_path, _exeName);
            if (!File.Exists(exe))
                return null;

            _resolution = resolution;

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
            { "sonicmania", "RSDKv5U_x64.exe"},
            { "sonicretro", "RSDKv4_64.exe"},
            { "sonicretrocd", "RSDKv3_64.exe"},
        };
    }
}
