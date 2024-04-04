using System.Collections.Generic;
using System.IO;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private void ConfigurePort(List<string> commandArray, string rom, string exe)
        {
            ConfigureSonic3air(commandArray, rom, exe);
        }

        private void ConfigureSonic3air(List<string> commandArray, string rom, string exe)
        {
            if (_emulator != "sonic3air")
                return;

            string configFolder = Path.Combine(_path, "savedata");
            if (!Directory.Exists(configFolder))
                try { Directory.CreateDirectory(configFolder); } catch { }

            // Settings file
            string settingsFile = Path.Combine(configFolder, "settings.json");

            var settings = DynamicJson.Load(settingsFile);

            settings["AutoAssignGamepadPlayerIndex"] = "-1";
            settings["GameExePath"] = exe.Replace("\\", "\\\\");
            settings["Fullscreen"] = _fullscreen ? "1" : "0";
            settings["RomPath"] = rom.Replace("\\", "/");

            ConfigureSonic3airControls(configFolder, settings);

            settings.Save();

            string configFile = Path.Combine(_path, "config.json");

            // Config file
            var config = DynamicJson.Load(configFile);

            var devmode = config.GetOrCreateContainer("DevMode");
            devmode["SkipExitConfirmation"] = "1";

            config.Save();
        }
    }
}
