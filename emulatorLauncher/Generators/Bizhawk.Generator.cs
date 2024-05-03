using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;
using System;

namespace EmulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        private static readonly List<string> preferredRomExtensions = new List<string>() { ".bin", ".cue", ".img", ".iso", ".rom" };
        private static readonly List<string> zipSystems = new List<string>() { "psx", "saturn", "n64", "n64dd", "pcenginecd", "jaguarcd", "vectrex", "odyssey2", "uzebox" };

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

            string[] romExtensions = new string[] { ".m3u", ".chd", ".cue", ".ccd", ".cdi", ".iso", ".mds", ".nrg", ".z64", ".n64", ".v64", ".ndd", ".vec", ".uze", ".o2"};

            if (zipSystems.Contains(system) && (Path.GetExtension(rom).ToLowerInvariant() == ".zip" || Path.GetExtension(rom).ToLowerInvariant() == ".7z"))
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath).OrderBy(file => Array.IndexOf(romExtensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => romExtensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            if (Path.GetExtension(rom) == ".chd")
                throw new ApplicationException("Extension CHD not compatible with Bizhawk");

            // Json Config file
            string configFile = Path.Combine(path, "config.ini");

            if (File.Exists(configFile))
            {
                var json = DynamicJson.Load(configFile);

                SetupGeneralConfig(json,system, core, rom, emulator);
                SetupCoreOptions(json, system, core, rom);
                SetupFirmwares(json, system);
                SetupRetroAchievements(json);
                CreateControllerConfiguration(json, system, core);

                //save config file
                json.Save();
            }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            // Bezels
            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            // case of DSI nand loading
            if (core == "melonDS" && Path.GetExtension(rom) == ".bin")
            {
                string romPath = Path.GetDirectoryName(rom);
                var romToLaunch = Directory.EnumerateFiles(romPath, "*.nds")
                    .FirstOrDefault();
                rom = romToLaunch;
            }

            // Command line arguments
            var commandArray = new List<string>
            {
                "\"" + rom + "\""
            };

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

        private static readonly Dictionary<string, string> bizhawkPreferredCore = new Dictionary<string, string>()
        {
            { "gb", "GB" },
            { "gbc", "GBC" },
            { "gb2players", "GBL" },
            { "mastersystem", "SMS" },
            { "n64", "N64" },
            { "nds", "NDS" },
            { "nes", "NES" },
            { "pcengine", "PCE" },
            { "pcenginecd", "PCECD" },
            { "psx", "PSX" },
            { "sgb", "SGB" },
            { "snes", "SNES" },
            { "supergrafx", "SGX" },
            { "ti83", "TI83" },
        };

        private void SetupGeneralConfig(DynamicJson json, string system, string core, string rom, string emulator)
        {
            // First, set core to use !
            if (bizhawkPreferredCore.ContainsKey(system))
            {
                string systemName = bizhawkPreferredCore[system];
                var preferredcores = json.GetOrCreateContainer("PreferredCores");
                preferredcores[systemName] = core;
            }

            // If using some extensions, config file needs to be updated to specify the system to autorun
            string romExtension = Path.GetExtension(rom).ToLowerInvariant();
            if (preferredRomExtensions.Contains(romExtension) && bizHawkShortSystems.ContainsKey(system))
            {
                var preferredextensions = json.GetOrCreateContainer("PreferredPlatformsForExtensions");
                preferredextensions[romExtension] = bizHawkShortSystems[system];
            }

            // General settings
            json["PauseWhenMenuActivated"] = "true";
            json["SingleInstanceMode"] = "true";
            json["ShowContextMenu"] = "false";
            json["UpdateAutoCheckEnabled"] = "false";
            json["HostInputMethod"] = "0";

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

            if (bizHawkSystems.ContainsKey(system))
            {
                saveStatePath["System"] = bizHawkSystems[system];
                paths.Add(saveStatePath);
            }

            string saveRAMFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, emulator);
            if (!Directory.Exists(saveRAMFolder))
                try { Directory.CreateDirectory(saveRAMFolder); }
                catch { }
            var saveRAMPath = new DynamicJson();
            saveRAMPath["Type"] = "Save RAM";
            saveRAMPath["Path"] = saveRAMFolder;

            if (bizHawkSystems.ContainsKey(system))
            {
                saveRAMPath["System"] = bizHawkSystems[system];
                paths.Add(saveRAMPath);
            }

            string screenshotsFolder = Path.Combine(AppConfig.GetFullPath("screenshots"), emulator);
            if (!Directory.Exists(screenshotsFolder))
                try { Directory.CreateDirectory(screenshotsFolder); }
                catch { }
            var screenshotsPath = new DynamicJson();
            screenshotsPath["Type"] = "Screenshots";
            screenshotsPath["Path"] = screenshotsFolder;

            if (bizHawkSystems.ContainsKey(system))
            {
                screenshotsPath["System"] = bizHawkSystems[system];
                paths.Add(screenshotsPath);
            }

            string cheatsFolder = Path.Combine(AppConfig.GetFullPath("cheats"), emulator, system);
            if (!Directory.Exists(cheatsFolder))
                try { Directory.CreateDirectory(cheatsFolder); }
                catch { }
            var cheatsPath = new DynamicJson();
            cheatsPath["Type"] = "Cheats";
            cheatsPath["Path"] = cheatsFolder;

            if (bizHawkSystems.ContainsKey(system))
            {
                cheatsPath["System"] = bizHawkSystems[system];
                paths.Add(cheatsPath);
            }

            pathEntries.SetObject("Paths", paths);

            // Display options
            json["DispFixAspectRatio"] = "true";
            json["DispFullscreenHacks"] = "true";
            BindBoolFeature(json, "DisplayFps", "bizhawk_fps", "true", "false");
            BindBoolFeature(json, "DispFixScaleInteger", "integerscale", "true", "false");
            BindFeature(json, "TargetDisplayFilter", "bizhawk_filter", "0");
            BindFeature(json, "DispFinalFilter", "bizhawk_finalfilter", "0");

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

            // Specific system settings
            if (system == "sgb")
                json["GbAsSgb"] = "true";
            else
                json["GbAsSgb"] = "false";
        }

        private void SetupFirmwares(DynamicJson json, string system)
        {
            var firmware = json.GetOrCreateContainer("FirmwareUserSpecifications");

            if (system == "apple2")
            {
                // Apple2 roms
                string apple2DiskBios = Path.Combine(AppConfig.GetFullPath("bios"), "AppleIIe_DiskII.rom");
                if (File.Exists(apple2DiskBios))
                    firmware["AppleII+DiskII"] = apple2DiskBios;
                string apple2eBios = Path.Combine(AppConfig.GetFullPath("bios"), "AppleIIe.rom");
                if (File.Exists(apple2eBios))
                    firmware["AppleII+AppleIIe"] = apple2eBios;
            }

            if (system == "atari7800")
            {
                // Atari7800 firmware
                string hscBios = Path.Combine(AppConfig.GetFullPath("bios"), "A78_highscore.bin");
                if (File.Exists(hscBios))
                    firmware["A78+Bios_HSC"] = hscBios;
                string palBios = Path.Combine(AppConfig.GetFullPath("bios"), "7800 BIOS (E).rom");
                if (File.Exists(palBios))
                    firmware["A78+Bios_PAL"] = palBios;
                string ntscBios = Path.Combine(AppConfig.GetFullPath("bios"), "7800 BIOS (U).rom");
                if (File.Exists(ntscBios))
                    firmware["A78+Bios_NTSC"] = ntscBios;
            }

            if (system == "colecovision")
            {
                // Colecovision firmware
                string colecoBios = Path.Combine(AppConfig.GetFullPath("bios"), "colecovision.rom");
                if (File.Exists(colecoBios))
                    firmware["Coleco+Bios"] = colecoBios;
            }

            if (system == "gb" || system == "sgb")
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

            if (system == "lynx")
            {
                // Lynx firmware
                string lynxBiosPath = Path.Combine(AppConfig.GetFullPath("bios"), "lynxboot.img");
                if (File.Exists(lynxBiosPath))
                    firmware["Lynx+Boot"] = lynxBiosPath;
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
                string dsiNand = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_nand.bin");
                if (File.Exists(dsiNand))
                {
                    firmware["NDS+NAND (EUR)"] = dsiNand;
                    firmware["NDS+NAND (JPN)"] = dsiNand;
                    firmware["NDS+NAND (USA)"] = dsiNand;
                    firmware["NDS+NAND (AUS)"] = dsiNand;
                    firmware["NDS+NAND (CHN)"] = dsiNand;
                    firmware["NDS+NAND (KOR)"] = dsiNand;
                }
            }

            if (system == "nes")
            {
                // NES firmware
                string disksysPath = Path.Combine(AppConfig.GetFullPath("bios"), "disksys.rom");
                if (File.Exists(disksysPath))
                    firmware["NES+Bios_FDS"] = disksysPath;
            }

            if (system == "odyssey2")
            {
                // Odyssey2 firmware
                string o2Bios = Path.Combine(AppConfig.GetFullPath("bios"), "o2rom.bin");
                if (File.Exists(o2Bios))
                    firmware["O2+BIOS-O2"] = o2Bios;
                string c52Bios = Path.Combine(AppConfig.GetFullPath("bios"), "c52.bin");
                if (File.Exists(c52Bios))
                    firmware["O2+BIOS-C52"] = c52Bios;
                string g7400Bios = Path.Combine(AppConfig.GetFullPath("bios"), "g7400.bin");
                if (File.Exists(g7400Bios))
                    firmware["O2+BIOS-G7400"] = g7400Bios;
            }

            if (system == "pcfx")
            {
                // PC-FX firmware
                string scsiRom = Path.Combine(AppConfig.GetFullPath("bios"), "fx-scsi.rom");
                if (File.Exists(scsiRom))
                    firmware["PCFX+SCSIROM"] = scsiRom;
                string pcfxrom = Path.Combine(AppConfig.GetFullPath("bios"), "pcfx.rom");
                if (File.Exists(pcfxrom))
                    firmware["PCFX+BIOS"] = pcfxrom;
            }

            if (system == "psx")
            {
                // PSX firmware
                if (!SystemConfig.getOptBoolean("bizhawk_psx_original_bios"))
                {
                    string pspBios = Path.Combine(AppConfig.GetFullPath("bios"), "psxonpsp660.bin");
                    if (File.Exists(pspBios))
                    {
                        firmware["PSX+U"] = pspBios;
                        firmware["PSX+J"] = pspBios;
                        firmware["PSX+E"] = pspBios;
                    }
                    else if (File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "ps1_rom.bin")))
                    {
                        string ps3Bios = Path.Combine(AppConfig.GetFullPath("bios"), "ps1_rom.bin");
                        firmware["PSX+U"] = ps3Bios;
                        firmware["PSX+J"] = ps3Bios;
                        firmware["PSX+E"] = ps3Bios;
                    }
                    else
                    {
                        string usBios = Path.Combine(AppConfig.GetFullPath("bios"), "scph5501.bin");
                        if (File.Exists(usBios))
                            firmware["PSX+U"] = usBios;
                        string jpBios = Path.Combine(AppConfig.GetFullPath("bios"), "scph5500.bin");
                        if (File.Exists(jpBios))
                            firmware["PSX+J"] = jpBios;
                        string euBios = Path.Combine(AppConfig.GetFullPath("bios"), "scph5502.bin");
                        if (File.Exists(euBios))
                            firmware["PSX+E"] = euBios;
                    }
                }
                
                else
                {
                    string usBios = Path.Combine(AppConfig.GetFullPath("bios"), "scph5501.bin");
                    if (File.Exists(usBios))
                        firmware["PSX+U"] = usBios;
                    string jpBios = Path.Combine(AppConfig.GetFullPath("bios"), "scph5500.bin");
                    if (File.Exists(jpBios))
                        firmware["PSX+J"] = jpBios;
                    string euBios = Path.Combine(AppConfig.GetFullPath("bios"), "scph5502.bin");
                    if (File.Exists(euBios))
                        firmware["PSX+E"] = euBios;
                }
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

            if (system == "sega32x")
            {
                // 32X firmware
                string gBios = Path.Combine(AppConfig.GetFullPath("bios"), "32X_G_BIOS.BIN");
                if (File.Exists(gBios))
                    firmware["32X+G"] = gBios;
                string mBios = Path.Combine(AppConfig.GetFullPath("bios"), "32X_M_BIOS.BIN");
                if (File.Exists(mBios))
                    firmware["32X+M"] = mBios;
                string sBios = Path.Combine(AppConfig.GetFullPath("bios"), "32X_S_BIOS.BIN");
                if (File.Exists(sBios))
                    firmware["32X+S"] = sBios;
            }

            if (system == "vectrex")
            {
                // Vectrex BIOS
                string vectrexBios = Path.Combine(AppConfig.GetFullPath("bios"), "Vectrex_Bios.bin");
                if (File.Exists(vectrexBios))
                    firmware["VEC+Bios"] = vectrexBios;
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

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }

        private static readonly Dictionary<string, string> bizHawkSystems = new Dictionary<string, string>()
        {
            { "amstradcpc", "AmstradCPC" },
            { "apple2", "AppleII" },
            { "atari2600", "A26" },
            { "atari7800", "A78" },
            { "c64", "C64" },
            { "channelf", "ChannelF" },
            { "colecovision", "Coleco" },
            { "gamegear", "GG" },
            { "gb", "GB_GBC_SGB" },
            { "gb2players", "GBL" },
            { "gba", "GBA" },
            { "gbc", "GB_GBC_SGB" },
            { "gbc2players", "GBL" },
            { "intellivision", "INTV" },
            { "jaguar", "Jaguar" },
            { "jaguarcd", "Jaguar" },
            { "lynx", "Lynx" },
            { "mastersystem", "SMS" },
            { "megadrive", "GEN" },
            { "msx", "MSX" },
            { "multivision", "SG" },
            { "n64", "N64" },
            { "nes", "NES" },
            { "nds", "NDS" },
            { "ngp", "NGP" },
            { "odyssey2", "O2" },
            { "pcengine", "PCE_PCECD_SGX_SGXCD" },
            { "pcenginecd", "PCE_PCECD_SGX_SGXCD" },
            { "pcfx", "PCFX" },
            { "psx", "PSX" },
            { "satellaview", "BSX" },
            { "saturn", "SAT" },
            { "sega32x", "32X" },
            { "sg1000", "SG" },
            { "sgb", "GB_GBC_SGB" },
            { "snes", "SNES" },
            { "supergrafx", "PCE_PCECD_SGX_SGXCD" },
            { "ti83", "TI83" },
            { "tic80", "TIC80" },
            { "uzebox", "UZE" },
            { "vectrex", "VEC" },
            { "virtualboy", "VB" },
            { "wswan", "WSWAN" },
            { "wswanc", "WSWAN" },
            { "zxspectrum", "ZXSpectrum" },
        };

        private static readonly Dictionary<string, string> bizHawkShortSystems = new Dictionary<string, string>()
        {
            { "atari2600", "A26" },
            { "atari7800", "A78" },
            { "jaguar", "Jaguar" },
            { "jaguarcd", "Jaguar" },
            { "lynx", "Lynx" },
            { "nes", "NES" },
            { "snes", "SNES" },
            { "n64", "N64" },
            { "gb", "GB" },
            { "gba", "GBA" },
            { "psx", "PSX" },
            { "mastersystem", "SMS" },
            { "megadrive", "GEN" },
            { "sega32x", "32X" },
            { "saturn", "SAT" },
            { "pcengine", "PCE" },
            { "colecovision", "Coleco" },
            { "ti83", "TI83" },
            { "wswan", "WSWAN" },
            { "c64", "C64" },
            { "apple2", "AppleII" },
            { "intellivision", "INTV" },
            { "zxspectrum", "ZXSpectrum" },
            { "amstradcpc", "AmstradCPC" },
            { "channelf", "ChannelF" },
            { "odyssey2", "O2" },
            { "vectrex", "VEC" },
            { "msx", "MSX" },
            { "nds", "NDS" },
            { "gb2players", "GB" },
            { "gbc", "GB" },
            { "gbc2players", "GB" },
            { "pcenginecd", "PCE" },
            { "sgb", "GB" },
        };
    }
}
