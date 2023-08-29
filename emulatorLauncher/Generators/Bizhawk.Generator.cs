using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Management;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        private static Dictionary<string, string> bizHawkSystems = new Dictionary<string, string>()
        {
            { "nes", "NES" },
            { "snes", "SNES" },
            { "n64", "N64" },
            { "nds", "NDS" },
            { "gb", "GB" },
            { "gbc", "GBC" },
            { "pcengine", "PCE" },
            { "pcenginecd", "PCECD" }
        };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            // Define path
            string path = AppConfig.GetFullPath("bizhawk");

            if (string.IsNullOrEmpty(path))
                return null;

            // Define exe
            string exe = Path.Combine(path, "EmuHawk.exe");

            if (!File.Exists(exe))
                return null;

            // Json Config file
            string configFile = Path.Combine(path, "config.ini");

            if (File.Exists(configFile))
            {
                var json = DynamicJson.Load(configFile);

                SetupGeneralConfig(json,system, core, rom, emulator);
                SetupCoreOptions(json, system, core, rom);
                SetupFirmwares(json, system, core);
                SetupRetroAchievements(json);
                CreateControllerConfiguration(json, system, core);

                //save config file
                json.Save();
            }

            bool fullscreen = !IsEmulationStationWindowed(out _);
            // Bezels
            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            // Command line arguments
            var commandArray = new List<string>();

            commandArray.Add("\"" + rom + "\"");
            if (fullscreen)
                commandArray.Add("--fullscreen");

            string args = string.Join(" ", commandArray);

            // start emulator with arguments
            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
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

            if (bizHawkSystems.ContainsKey(system))
            {
                romPath["System"] = bizHawkSystems[system];
                paths.Add(romPath);
            }

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

            if (system == "gb")
            {
                // GB firmware
                string gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gb_bios.bin");
                if (File.Exists(gbBios))
                    firmware["GB+World"] = gbBios;
                string sgbBoot = Path.Combine(AppConfig.GetFullPath("bios"), "sgb_boot.bin");
                if (File.Exists(sgbBoot))
                    firmware["GB+SGB"] = sgbBoot;
                string sgb2Boot = Path.Combine(AppConfig.GetFullPath("bios"), "sgb2_boot.bin");
                if (File.Exists(sgb2Boot))
                    firmware["GB+SGB2"] = sgb2Boot;
            }

            if (system == "gba")
            {
                // GBA firmware
                string gbaBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), "gba_bios.bin");
                if (File.Exists(gbaBiosPath))
                    firmware["GBA+Bios"] = gbaBiosPath;
            }

            if (system == "gbc")
            {
                // GBC firmware
                string gbcBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), "gbc_bios.bin");
                if (File.Exists(gbcBiosPath))
                    firmware["GBC+World"] = gbcBiosPath;
            }

            if (system == "mastersystem")
            {
                // MASTER SYSTEM firmware
                string exportBios = Path.Combine(AppConfig.GetFullPath("bios"), "[BIOS] Sega Master System (USA, Europe) (v1.3).sms");
                if (File.Exists(exportBios))
                    firmware["SMS+Export"] = exportBios;
                string japanBios = Path.Combine(AppConfig.GetFullPath("bios"), "[BIOS] Sega Master System (Japan) (v2.1).sms");
                if (File.Exists(japanBios))
                    firmware["SMS+Japan"] = japanBios;
            }

            if (system == "nds")
            {
                // NDS firmware
                string bios7 = Path.Combine(AppConfig.GetFullPath("bios"), "bios7.bin");
                if (File.Exists(bios7))
                    firmware["NDS+bios7"] = bios7;
                string bios9 = Path.Combine(AppConfig.GetFullPath("bios"), "bios9.bin");
                if (File.Exists(bios9))
                    firmware["NDS+bios9"] = bios9;
                string ndsFirmware = Path.Combine(AppConfig.GetFullPath("bios"), "firmware.bin");
                if (File.Exists(ndsFirmware))
                    firmware["NDS+firmware"] = ndsFirmware;

                // Dsi firmware
                string dsibios7 = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_bios7.bin");
                if (File.Exists(dsibios7))
                    firmware["NDS+bios7i"] = dsibios7;
                string dsibios9 = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_bios9.bin");
                if (File.Exists(dsibios9))
                    firmware["NDS+bios9i"] = dsibios9;
                string dsiFirmware = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_firmware.bin");
                if (File.Exists(dsiFirmware))
                    firmware["NDS+firmwarei"] = dsiFirmware;
            }

            if (system == "nes")
            {
                // NES firmware
                string disksysPath = Path.Combine(AppConfig.GetFullPath("bios"), "disksys.rom");
                if (File.Exists(disksysPath))
                    firmware["NES+Bios_FDS"] = disksysPath;
            }

            if (system == "saturn")
            {
                // SATURN firmware
                string japBios = Path.Combine(AppConfig.GetFullPath("bios"), "saturn_bios.bin");
                if (File.Exists(japBios))
                    firmware["SAT+J"] = japBios;
                string useuBios = Path.Combine(AppConfig.GetFullPath("bios"), "mpr-17933.bin");
                if (File.Exists(useuBios))
                {
                    firmware["SAT+U"] = useuBios;
                    firmware["SAT+E"] = useuBios;
                }
                string kof95Bios = Path.Combine(AppConfig.GetFullPath("bios"), "mpr-18811-mx.ic1");
                if (File.Exists(kof95Bios))
                    firmware["SAT+KOF95"] = kof95Bios;
                string ultramanBios = Path.Combine(AppConfig.GetFullPath("bios"), "mpr-19367-mx.ic1");
                if (File.Exists(ultramanBios))
                    firmware["SAT+ULTRAMAN"] = ultramanBios;
            }
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
