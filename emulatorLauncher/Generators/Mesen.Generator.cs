using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using EmulatorLauncher.Common.Lightguns;
using System.Linq;

namespace EmulatorLauncher
{
    partial class MesenGenerator : Generator
    {
        protected override bool UseGenericScreenPlacement { get { return true; } }
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        static List<string> _m3uSystems = new List<string>() { "pcenginecd", "turbografxcd" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mesen");

            string exe = Path.Combine(path, "Mesen.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = ShouldRunFullscreen();

            // m3u management in some cases
            if (_m3uSystems.Contains(system))
            {
                if (Path.GetExtension(rom).ToLower() == ".m3u")
                {
                    string tempRom = File.ReadLines(rom).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                    if (tempRom != null)
                    {
                        if (FileTools.IsPathFullyQualified(tempRom))
                            rom = tempRom;
                        else
                            rom = Path.Combine(Path.GetDirectoryName(rom), tempRom);
                    }
                }
            }

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            _resolution = resolution;

            // settings (xml configuration)
            SetupJsonConfiguration(path, system, rom);


            // command line parameters
            var commandArray = new List<string>
            {
                "\"" + rom + "\""
            };

            if (fullscreen)
                commandArray.Add("--fullscreen");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupJsonConfiguration(string path, string system, string rom)
        {
            string settingsFile = Path.Combine(path, "settings.json");
            if (!File.Exists(settingsFile))
                File.WriteAllText(settingsFile, "{}");

            JObject json = JObject.Parse(File.ReadAllText(settingsFile));

            string mesenSystem = GetMesenSystem(system);
            if (mesenSystem == "none")
                return;

            json["FirstRun"] = false;

            // System preferences
            var systemSection = GetOrCreateContainer(json, mesenSystem);
            ConfigureNes(systemSection, mesenSystem, system, path);
            ConfigureSMS(systemSection, mesenSystem, system);
            ConfigurePCEngine(systemSection, mesenSystem, system, path);
            ConfigureSnes(systemSection, mesenSystem, system, path);
            ConfigureGameboy(systemSection, mesenSystem, system, path);
            ConfigureGba(systemSection, mesenSystem, system, path);
            ConfigureColeco(systemSection, mesenSystem, system, path);

            // Screen : Mesen restores its main window position (MainWindow.WindowLocation), then goes fullscreen on that screen.
            // Only when the user chose a screen, otherwise Mesen keeps its own placement.
            if (Program.IsMonitorIndexSet)
            {
                var bounds = Program.TargetScreen.Bounds;
                int width = bounds.Width / 2;
                int height = bounds.Height / 2;

                var mainWindow = GetOrCreateContainer(json, "MainWindow");
                mainWindow["WindowLocation"] = new JObject { ["X"] = bounds.Left + (bounds.Width - width) / 2, ["Y"] = bounds.Top + (bounds.Height - height) / 2 };
                mainWindow["WindowSize"] = new JObject { ["Width"] = width, ["Height"] = height };
                mainWindow["WindowIsMaximized"] = false;
            }

            // Emulator preferences
            var preference = GetOrCreateContainer(json, "Preferences");

            preference["AutomaticallyCheckForUpdates"] = false;
            preference["SingleInstance"] = true;
            preference["PauseWhenInBackground"] = SystemConfig.getOptBoolean("nopauseonlostfocus") ? false : true;
            preference["PauseWhenInMenusAndConfig"] = SystemConfig.getOptBoolean("nopauseonlostfocus") ? false : true;
            preference["AllowBackgroundInput"] = true;
            preference["ConfirmExitResetPower"] = false;
            preference["AssociateSnesRomFiles"] = false;
            preference["AssociateSnesMusicFiles"] = false;
            preference["AssociateNesRomFiles"] = false;
            preference["AssociateNesMusicFiles"] = false;
            preference["AssociateGbRomFiles"] = false;
            preference["AssociateGbMusicFiles"] = false;
            preference["AssociatePceRomFiles"] = false;
            preference["AssociatePceMusicFiles"] = false;

            if (SystemConfig.isOptSet("mesen_autosave") && SystemConfig["mesen_autosave"] != "false")
            {
                preference["EnableAutoSaveState"] = true;
                preference["AutoSaveStateDelay"] = SystemConfig["mesen_autosave"].ToInteger();
            }
            else
                preference["EnableAutoSaveState"] = false;

            BindBoolFeatureOn(preference, "AutoLoadPatches", "mesen_patches");
            BindBoolFeature(preference, "EnableRewind", "rewind");
            BindBoolFeatureDefaultFalse(preference, "DisableOsd", "mesen_osd");
            BindBoolFeature(preference, "ShowGameTimer", "mesen_timecounter");
            BindBoolFeature(preference, "ShowFps", "mesen_fps");

            // define folders
            string gamesFolder = Path.GetDirectoryName(rom);
            if (!string.IsNullOrEmpty(gamesFolder) && Directory.Exists(gamesFolder))
            {
                preference["OverrideGameFolder"] = true;
                preference["GameFolder"] = gamesFolder;
            }

            string recordsFolder = Path.Combine(AppConfig.GetFullPath("records"), "output", "mesen");
            if (!Directory.Exists(recordsFolder)) try { Directory.CreateDirectory(recordsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(recordsFolder) && Directory.Exists(recordsFolder))
            {
                preference["OverrideAviFolder"] = true;
                preference["AviFolder"] = recordsFolder;
            }

            string savesFolder = Path.Combine(AppConfig.GetFullPath("saves"), system);
            if (!Directory.Exists(savesFolder)) try { Directory.CreateDirectory(savesFolder); }
                catch { }
            if (!string.IsNullOrEmpty(savesFolder) && Directory.Exists(savesFolder))
            {
                preference["OverrideSaveDataFolder"] = true;
                preference["SaveDataFolder"] = savesFolder;
            }

            string saveStateFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, "mesen", "SaveStates");
            if (!Directory.Exists(saveStateFolder)) try { Directory.CreateDirectory(saveStateFolder); }
                catch { }
            if (!string.IsNullOrEmpty(saveStateFolder) && Directory.Exists(saveStateFolder))
            {
                preference["OverrideSaveStateFolder"] = true;
                preference["SaveStateFolder"] = saveStateFolder;
            }

            string screenshotsFolder = Path.Combine(AppConfig.GetFullPath("screenshots"), "mesen");
            if (!Directory.Exists(screenshotsFolder)) try { Directory.CreateDirectory(screenshotsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(screenshotsFolder) && Directory.Exists(screenshotsFolder))
            {
                preference["OverrideScreenshotFolder"] = true;
                preference["ScreenshotFolder"] = screenshotsFolder;
            }

            // Video menu
            var video = GetOrCreateContainer(json, "Video");
            BindFeature(video, "VideoFilter", "mesen_filter", "None");
            BindFeature(video, "AspectRatio", "mesen_ratio", "Auto");
            BindBoolFeature(video, "UseBilinearInterpolation", "bilinear_filtering");
            BindBoolFeatureOn(video, "VerticalSync", "mesen_vsync");
            BindFeatureSliderInt(video, "ScanlineIntensity", "mesen_scanlines", "0");
            BindBoolFeature(video, "FullscreenForceIntegerScale", "integerscale");
            // The overlay bezel cannot be displayed over an exclusive fullscreen
            if (SystemConfig.getOptBoolean("exclusivefs") && _bezelFileInfo == null)
                video["UseExclusiveFullscreen"] = true;
            else
                video["UseExclusiveFullscreen"] = false;

            // Emulation menu
            var emulation = GetOrCreateContainer(json, "Emulation");
            BindFeatureSliderInt(emulation, "RunAheadFrames", "mesen_runahead", "0");

            // Input menu
            var input = GetOrCreateContainer(json, "Input");
            if (SystemConfig.getOptBoolean("mesen_target"))
                input["HidePointerForLightGuns"] = false;
            else
                input["HidePointerForLightGuns"] = true;

            // Controllers configuration
            SetupControllers(preference, systemSection, mesenSystem);
            SetupGuns(systemSection, mesenSystem);

            // Save json file
            File.WriteAllText(settingsFile, json.ToString(Formatting.Indented));
        }

        private void ConfigureNes(JObject section, string mesenSystem, string system, string path)
        {
            if (mesenSystem != "Nes")
                return;
            section["AutoConfigureInput"] = false;
            BindBoolFeature(section, "EnableHdPacks", "mesen_customtextures");
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit");

            if (system == "fds")
            {
                BindBoolFeatureOn(section, "FdsAutoInsertDisk", "mesen_fdsautoinsertdisk");
                BindBoolFeature(section, "FdsFastForwardOnLoad", "mesen_fdsfastforwardload");
                section["FdsAutoLoadDisk"] = true;
            }

            // FDS BIOS is read from the emulator Firmware folder (.fds games are also accepted in the nes system)
            string targetFirmwarePath = Path.Combine(path, "Firmware");
            if (!Directory.Exists(targetFirmwarePath)) try { Directory.CreateDirectory(targetFirmwarePath); }
                catch { }
            string targetFirmware = Path.Combine(targetFirmwarePath, "disksys.rom");
            string sourceFirmware = Path.Combine(AppConfig.GetFullPath("bios"), "disksys.rom");

            if (File.Exists(sourceFirmware) && !File.Exists(targetFirmware) && Directory.Exists(targetFirmwarePath))
                File.Copy(sourceFirmware, targetFirmware);
        }

        private void ConfigureSMS(JObject section, string mesenSystem, string system)
        {
            if (mesenSystem != "Sms")
                return;
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindFeature(section, "Revision", "mesen_sms_revision", "Compatibility");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit");
            BindBoolFeatureOn(section, "EnableFmAudio", "mesen_sms_fmaudio");
        }

        private void ConfigurePCEngine(JObject section, string mesenSystem, string system, string path)
        {
            if (mesenSystem != "PcEngine")
                return;

            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit");
            BindFeature(section, "ConsoleType", "mesen_pce_console", "Auto");

            if (system == "pcenginecd" || system == "turbografxcd")
            {
                // Firmwares for pcenginecd need to be copied to emulator folder
                string targetFirmwarePath = Path.Combine(path, "Firmware");
                if (!Directory.Exists(targetFirmwarePath)) try { Directory.CreateDirectory(targetFirmwarePath); }
                    catch { }
                
                string targetFirmware1 = Path.Combine(targetFirmwarePath, "[BIOS] Super CD-ROM System (Japan) (v3.0).pce");
                string sourceFirmware1 = Path.Combine(AppConfig.GetFullPath("bios"), "syscard3.pce");

                // Games Express discs need their own card
                string targetFirmwareGE = Path.Combine(targetFirmwarePath, "[BIOS] Games Express CD Card (Japan).pce");
                string sourceFirmwareGE = Path.Combine(AppConfig.GetFullPath("bios"), "gexpress.pce");

                if (File.Exists(sourceFirmwareGE) && !File.Exists(targetFirmwareGE) && Directory.Exists(targetFirmwarePath))
                    File.Copy(sourceFirmwareGE, targetFirmwareGE);
                if (File.Exists(sourceFirmware1) && !File.Exists(targetFirmware1) && Directory.Exists(targetFirmwarePath))
                    File.Copy(sourceFirmware1, targetFirmware1);

                if (!File.Exists(targetFirmware1))
                    throw new ApplicationException("PCE CD firmware is missing (syscard3.pce)");
            }
        }

        private void ConfigureGameboy(JObject section, string mesenSystem, string system, string path)
        {
            if (mesenSystem != "Gameboy")
                return;

            if (system == "gb")
                section["Model"] = "Gameboy";
            else if (system == "gbc")
                section["Model"] = "GameboyColor";
            else if (system == "sgb")
            {
                section["Model"] = "SuperGameboy";
                
                // SGB2 needs SGB2.sfc, SGB1 is used when only SGB1.sfc is present
                if (SystemConfig.isOptSet("mesen_sgb2"))
                    BindBoolFeatureOn(section, "UseSgb2", "mesen_sgb2");
                else
                    section["UseSgb2"] = File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "SGB2.sfc")) || !File.Exists(Path.Combine(AppConfig.GetFullPath("bios"), "SGB1.sfc"));

                BindBoolFeature(section, "HideSgbBorders", "mesen_hidesgbborders");

                // Firmwares for sgb need to be copied to emulator folder
                string targetFirmwarePath = Path.Combine(path, "Firmware");
                if (!Directory.Exists(targetFirmwarePath)) try { Directory.CreateDirectory(targetFirmwarePath); }
                    catch { }
                string targetFirmware1 = Path.Combine(targetFirmwarePath, "SGB1.sfc");
                string targetFirmware2 = Path.Combine(targetFirmwarePath, "SGB2.sfc");

                string sourceFirmware1 = Path.Combine(AppConfig.GetFullPath("bios"), "SGB1.sfc");
                string sourceFirmware2 = Path.Combine(AppConfig.GetFullPath("bios"), "SGB2.sfc");

                if (File.Exists(sourceFirmware1) && !File.Exists(targetFirmware1) && Directory.Exists(targetFirmwarePath))
                    File.Copy(sourceFirmware1, targetFirmware1);
                if (File.Exists(sourceFirmware2) && !File.Exists(targetFirmware2) && Directory.Exists(targetFirmwarePath))
                    File.Copy(sourceFirmware2, targetFirmware2);

                if (!File.Exists(targetFirmware1) && !File.Exists(targetFirmware2))
                    throw new ApplicationException("Super Gameboy firmware is missing (SGB1.sfc and/or SGB2.sfc)");
            }
        }

        private void ConfigureGba(JObject section, string mesenSystem, string system, string path)
        {
            if (mesenSystem != "Gba")
                return;

            BindBoolFeature(section, "SkipBootScreen", "mesen_gba_skipboot");

            // Firmware for gba needs to be copied to emulator folder
            string targetFirmwarePath = Path.Combine(path, "Firmware");
            if (!Directory.Exists(targetFirmwarePath)) try { Directory.CreateDirectory(targetFirmwarePath); }
                catch { }
            string targetFirmware1 = Path.Combine(targetFirmwarePath, "gba_bios.bin");

            string sourceFirmware1 = Path.Combine(AppConfig.GetFullPath("bios"), "gba_bios.bin");

            if (File.Exists(sourceFirmware1) && !File.Exists(targetFirmware1) && Directory.Exists(targetFirmwarePath))
                File.Copy(sourceFirmware1, targetFirmware1);
        }

        private void ConfigureColeco(JObject section, string mesenSystem, string system, string path)
        {
            if (mesenSystem != "Cv")
                return;

            BindFeature(section, "Region", "mesen_coleco_region", "Auto");

            // Firmware for coleco needs to be copied to emulator folder
            string targetFirmwarePath = Path.Combine(path, "Firmware");
            if (!Directory.Exists(targetFirmwarePath)) try { Directory.CreateDirectory(targetFirmwarePath); }
                catch { }
            string targetFirmware1 = Path.Combine(targetFirmwarePath, "bios.col");

            string sourceFirmware1 = Path.Combine(AppConfig.GetFullPath("bios"), "colecovision.rom");

            if (File.Exists(sourceFirmware1) && !File.Exists(targetFirmware1) && Directory.Exists(targetFirmwarePath))
                File.Copy(sourceFirmware1, targetFirmware1);

            if (!File.Exists(targetFirmware1))
                throw new ApplicationException("Colecovision firmware is missing (colecovision.rom)");
        }

        private void ConfigureSnes(JObject section, string mesenSystem, string system, string path)
        {
            if (mesenSystem != "Snes")
                return;

            BindFeature(section, "Region", "mesen_region", "Auto");

            // Coprocessor firmwares (DSP, ST01x) are read in the emulator folder : copy the ones found in bios
            string targetFirmwarePath = Path.Combine(path, "Firmware");
            if (!Directory.Exists(targetFirmwarePath)) try { Directory.CreateDirectory(targetFirmwarePath); }
                catch { }

            foreach (string firmware in new string[] { "dsp1.rom", "dsp1b.rom", "dsp2.rom", "dsp3.rom", "dsp4.rom", "st010.rom", "st011.rom", "st018.rom" })
            {
                string sourceFirmware = Path.Combine(AppConfig.GetFullPath("bios"), firmware);
                string targetFirmware = Path.Combine(targetFirmwarePath, firmware);
                if (File.Exists(sourceFirmware) && !File.Exists(targetFirmware) && Directory.Exists(targetFirmwarePath))
                    try { File.Copy(sourceFirmware, targetFirmware); } catch { }
            }
        }

        /// <summary>
        /// Light guns: use_guns enables the gun, mesen_zapper / mesen_superscope only choose its port.
        /// NES Zapper and SNES Super Scope in port 2 by default, SMS Light Phaser in port 1.
        /// </summary>
        private void SetupGuns(JObject section, string mesenSystem)
        {
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
            }

            foreach (var port in nesPorts)
            {
                var portSection = GetOrCreateContainer(section, port);
                var mapping = GetOrCreateContainer(portSection, "Mapping1");
                mapping["ZapperButtons"] = JValue.CreateNull();
            }

            if (mesenSystem == "Nes")
            {
                string gunPort = GetGunPort("mesen_zapper", "Port2");
                if (gunPort != null)
                    SetGunPort(section, gunPort, "NesZapper", "ZapperButtons", new List<int> { 512, 513 });
            }
            else if (mesenSystem == "Sms")
            {
                foreach (var port in smsPorts)
                {
                    var portSection = GetOrCreateContainer(section, port);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    mapping["LightPhaserButtons"] = JValue.CreateNull();
                }

                string gunPort = GetGunPort("mesen_zapper", "Port1");
                if (gunPort != null)
                    SetGunPort(section, gunPort, "SmsLightPhaser", "LightPhaserButtons", new List<int> { 512, 513 });
            }
            else if (mesenSystem == "Snes")
            {
                foreach (var port in snesPorts)
                {
                    var portSection = GetOrCreateContainer(section, port);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    mapping["SuperScopeButtons"] = JValue.CreateNull();
                }

                string gunPort = GetGunPort("mesen_superscope", "Port2");
                if (gunPort != null)
                    SetGunPort(section, gunPort, "SuperScope", "SuperScopeButtons", new List<int> { 512, 513, 514, 6 });
            }
        }

        /// <summary>
        /// Port of the gun, null when the guns are not enabled. Old "none" values fall back to the default port.
        /// </summary>
        private string GetGunPort(string feature, string defaultPort)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return null;

            string port = SystemConfig[feature];
            return port == "Port1" || port == "Port2" ? port : defaultPort;
        }

        private void SetGunPort(JObject section, string port, string type, string buttonsKey, List<int> mouseButtons)
        {
            var portSection = GetOrCreateContainer(section, port);
            var mapping = GetOrCreateContainer(portSection, "Mapping1");
            mapping[buttonsKey] = new JArray(mouseButtons);
            portSection["Type"] = type;
        }

        private string GetMesenSystem(string System)
        {
            switch (System)
            {
                case "nes":
                case "famicom":
                case "fds":
                    return "Nes";
                case "snes":
                case "snes-msu1":
                case "superfamicom":
                case "sfamicom":
                    return "Snes";
                case "gba":
                    return "Gba";
                case "gb":
                case "gameboy":
                case "gbc":
                case "sgb":
                    return "Gameboy";
                case "pcengine":
                case "turbografx":
                case "turbografx16":
                case "turbografxcd":
                case "pcenginecd":
                case "supergrafx":
                    return "PcEngine";
                case "ms":
                case "sms":
                case "mastersystem":
                    return "Sms";
                case "colecovision":
                    return "Cv";
            }
            return "none";
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

        private JObject GetOrCreateContainer(JObject parent, string key)
        {
            if (parent[key] == null || parent[key].Type != JTokenType.Object)
            {
                parent[key] = new JObject();
            }
            return (JObject)parent[key];
        }
    }
}
