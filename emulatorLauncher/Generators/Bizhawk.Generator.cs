using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        public BizhawkGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _path;

        static Dictionary<string, string> bizHawkSystems = new Dictionary<string, string>()
        {
            { "nes", "NES" },
            { "snes", "SNES" },
            { "n64", "N64" },
            { "gb", "GB" },
            { "gbc", "GBC" },
            { "pcengine", "PCE" },
            { "pcenginecd", "PCECD" }
        };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            // Define path
            _path = AppConfig.GetFullPath("bizhawk");

            if (string.IsNullOrEmpty(_path))
                return null;

            // Define exe
            string exe = Path.Combine(_path, "EmuHawk.exe");

            if (!File.Exists(exe))
                return null;

            // Bezels
            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            // Json Config file
            string configFile = Path.Combine(_path, "config.ini");

            if (File.Exists(configFile))
            {
                var json = DynamicJson.Load(configFile);

                SetupGeneralConfig(json,system, core, rom, emulator);
                SetupCoreOptions(json, system, core, rom);
                SetupFirmwares(json, system, core);   // Bizhawk uses '+' sign that is forbidden in xml and we use xml for json conversion
                SetupRetroAchievements(json);
                CreateControllerConfiguration(json, system, core);

                //save config file
                json.Save();
            }

            // Command line arguments
            var commandArray = new List<string>();

            commandArray.Add("\"" + rom + "\"");
            commandArray.Add("--fullscreen");

            string args = string.Join(" ", commandArray);

            // start emulator with arguments
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args,
            };
        }

        private void SetupGeneralConfig(DynamicJson json, string system, string core, string rom, string emulator)
        {
            // First, set core to use !
            if (bizHawkSystems.ContainsKey(system))
            {
                string systemName = bizHawkSystems[system];
                var preferredcores = json.GetOrCreateContainer("PreferredCores");
                preferredcores[systemName] = core;
            }

            // Set Paths
            var pathEntries = json.GetOrCreateContainer("PathEntries");
            pathEntries.Remove("Paths");

            var paths = new List<DynamicJson>();
            
            // Global Firmware Path
            string biosPath = AppConfig.GetFullPath("bios");
            var firmwarePath = new DynamicJson();
            firmwarePath["Type"] = "Firmware";
            firmwarePath["Path"] = biosPath;
            firmwarePath["System"] = "Global_NULL";
            paths.Add(firmwarePath);

            // Core paths
            string romFolder = Path.GetDirectoryName(rom);
            if (!Directory.Exists(romFolder))
                try { Directory.CreateDirectory(romFolder); }
                catch { }
            var romPath = new DynamicJson();
            romPath["Type"] = "ROM";
            romPath["Path"] = romFolder;
            romPath["System"] = bizHawkSystems[system];
            paths.Add(romPath);

            string saveStateFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, emulator, "sstates");
            if (!Directory.Exists(saveStateFolder))
                try { Directory.CreateDirectory(saveStateFolder); }
                catch { }
            var saveStatePath = new DynamicJson();
            saveStatePath["Type"] = "Savestates";
            saveStatePath["Path"] = saveStateFolder;
            saveStatePath["System"] = bizHawkSystems[system];
            paths.Add(saveStatePath);

            string saveRAMFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, emulator);
            if (!Directory.Exists(saveRAMFolder))
                try { Directory.CreateDirectory(saveRAMFolder); }
                catch { }
            var saveRAMPath = new DynamicJson();
            saveRAMPath["Type"] = "Save RAM";
            saveRAMPath["Path"] = saveRAMFolder;
            saveRAMPath["System"] = bizHawkSystems[system];
            paths.Add(saveRAMPath);

            string screenshotsFolder = Path.Combine(AppConfig.GetFullPath("screenshots"), emulator);
            if (!Directory.Exists(screenshotsFolder))
                try { Directory.CreateDirectory(screenshotsFolder); }
                catch { }
            var screenshotsPath = new DynamicJson();
            screenshotsPath["Type"] = "Screenshots";
            screenshotsPath["Path"] = screenshotsFolder;
            screenshotsPath["System"] = bizHawkSystems[system];
            paths.Add(screenshotsPath);

            string cheatsFolder = Path.Combine(AppConfig.GetFullPath("cheats"), emulator, system);
            if (!Directory.Exists(cheatsFolder))
                try { Directory.CreateDirectory(cheatsFolder); }
                catch { }
            var cheatsPath = new DynamicJson();
            cheatsPath["Type"] = "Cheats";
            cheatsPath["Path"] = cheatsFolder;
            cheatsPath["System"] = bizHawkSystems[system];
            paths.Add(cheatsPath);

            pathEntries.SetObject("Paths", paths);

            // Display options
            // Display driver
            if (SystemConfig.isOptSet("bizhawk_renderer") && !string.IsNullOrEmpty(SystemConfig["bizhawk_renderer"]))
                json["DispMethod"] = SystemConfig["bizhawk_renderer"];
            else
                json["DispMethod"] = "2";

            // Vsync
            if (SystemConfig.isOptSet("bizhawk_vsync") && SystemConfig["bizhawk_vsync"] == "alternate")
            {
                json["VSync"] = "true";
                json["DispAlternateVsync"] = "true";
            }
            else if (SystemConfig.isOptSet("bizhawk_vsync") && SystemConfig["bizhawk_vsync"] == "false")
            {
                json["VSync"] = "false";
                json["DispAlternateVsync"] = "false";
            }
            else
            {
                json["VSync"] = "true";
                json["DispAlternateVsync"] = "false";
            }

            // Audio driver
            if (SystemConfig.isOptSet("bizhawk_audiooutput") && !string.IsNullOrEmpty(SystemConfig["bizhawk_audiooutput"]))
                json["SoundOutputMethod"] = SystemConfig["bizhawk_audiooutput"];
            else
                json["SoundOutputMethod"] = "0";
        }

        private void SetupFirmwares(DynamicJson json, string system, string core)
        {
            var firmware = json.GetOrCreateContainer("FirmwareUserSpecifications");

            // NES firmware
            string disksysPath = Path.Combine(AppConfig.GetFullPath("bios"), "disksys.rom");
            if (File.Exists(disksysPath))
                firmware["NES+Bios_FDS"] = disksysPath;

        }

        private void SetupRetroAchievements(DynamicJson json)
        {
            // Enable cheevos is needed
            if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
            {
                json["SkipRATelemetryWarning"] = "true";
                json["RAUsername"] = SystemConfig["retroachievements.username"];
                json["RAToken"] = SystemConfig["retroachievements.token"];
                json["RACheevosActive"] = "true";
                json["RALBoardsActive"] = SystemConfig.getOptBoolean("retroachievements.leaderboards") ? "true" : "false";
                json["RARichPresenceActive"] = SystemConfig.getOptBoolean("retroachievements.richpresence") ? "true" : "false";
                json["RAHardcoreMode"] = SystemConfig.getOptBoolean("retroachievements.hardcore") ? "true" : "false";
                json["RASoundEffects"] = "true";
                json["RAAutostart"] = "true";
            }
            else
            {
                json["SkipRATelemetryWarning"] = "true";
                json["RAUsername"] = "";
                json["RAToken"] = "";
                json["RACheevosActive"] = "false";
                json["RALBoardsActive"] = "false";
                json["RARichPresenceActive"] = "false";
                json["RAHardcoreMode"] = "false";
                json["RASoundEffects"] = "false";
                json["RAAutostart"] = "false";
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
