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
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _sindenSoft = false;
        static List<string> _m3uSystems = new List<string>() { "pcenginecd", "turbografxcd" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mesen");

            string exe = Path.Combine(path, "Mesen.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // m3u management in some cases
            if (_m3uSystems.Contains(system))
            {
                if (Path.GetExtension(rom).ToLower() == ".m3u")
                {
                    string tempRom = File.ReadLines(rom).FirstOrDefault();
                    if (File.Exists(tempRom))
                        rom = tempRom;
                    else
                        rom = Path.Combine(Path.GetDirectoryName(rom), tempRom);
                }
            }

            // settings (xml configuration)
            SetupJsonConfiguration(path, system, rom);

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            _resolution = resolution;
            

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
            ConfigureNes(systemSection, system);
            ConfigureSMS(systemSection, system);
            ConfigurePCEngine(systemSection, system, path);
            ConfigureSnes(systemSection, system);
            ConfigureGameboy(systemSection, system, path);
            ConfigureGba(systemSection, system, path);
            ConfigureColeco(systemSection, system, path);

            // Emulator preferences
            var preference = GetOrCreateContainer(json, "Preferences");

            preference["AutomaticallyCheckForUpdates"] = false;
            preference["SingleInstance"] = true;
            preference["PauseWhenInBackground"] = true;
            preference["PauseWhenInMenusAndConfig"] = true;
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
            BindBoolFeature(video, "UseExclusiveFullscreen", "exclusivefs");

            // Emulation menu
            var emulation = GetOrCreateContainer(json, "Emulation");
            BindFeatureSliderInt(emulation, "RunAheadFrames", "mesen_runahead", "0");

            // Input menu
            var input = GetOrCreateContainer(json, "Input");
            BindBoolFeatureOn(input, "HidePointerForLightGuns", "mesen_target");

            // Controllers configuration
            SetupControllers(preference, systemSection, mesenSystem);
            SetupGuns(systemSection, mesenSystem);

            // Save json file
            File.WriteAllText(settingsFile, json.ToString(Formatting.Indented));
        }

        private void ConfigureNes(JObject section, string system)
        {
            if (system != "nes" && system != "fds" && system != "famicom")
                return;
            section["AutoConfigureInput"] = false;
            BindBoolFeature(section, "EnableHdPacks", "mesen_customtextures");
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit");

            if (system == "fds")
            {
                BindBoolFeature(section, "FdsAutoInsertDisk", "mesen_fdsautoinsertdisk");
                BindBoolFeature(section, "FdsFastForwardOnLoad", "mesen_fdsfastforwardload");
                section["FdsAutoLoadDisk"] = true;
            }
        }

        private void ConfigureSMS(JObject section, string system)
        {
            if (system != "mastersystem")
                return;
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindFeature(section, "Revision", "mesen_sms_revision", "Compatibility");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit");
            BindBoolFeatureOn(section, "EnableFmAudio", "mesen_sms_fmaudio");
        }

        private void ConfigurePCEngine(JObject section, string system, string path)
        {
            if (system != "pcengine" && system != "pcenginecd" && system != "turbografx" && system != "turbografxcd" && system != "turbografx16")
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

                if (File.Exists(sourceFirmware1) && !File.Exists(targetFirmware1) && Directory.Exists(targetFirmwarePath))
                    File.Copy(sourceFirmware1, targetFirmware1);

                if (!File.Exists(sourceFirmware1))
                    throw new ApplicationException("PCE CD firmware is missing (syscard3.pce)");
            }
        }

        private void ConfigureGameboy(JObject section, string system, string path)
        {
            if (system != "gb" && system != "gbc" && system != "sgb")
                return;

            if (system == "gb")
                section["Model"] = "Gameboy";
            else if (system == "gbc")
                section["Model"] = "GameboyColor";
            else if (system == "sgb")
            {
                section["Model"] = "SuperGameboy";
                BindBoolFeatureOn(section, "UseSgb2", "mesen_sgb2");
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

                if (!File.Exists(sourceFirmware1) && !File.Exists(sourceFirmware2))
                    throw new ApplicationException("Super Gameboy firmware is missing (SGB1.sfc and/or SGB2.sfc)");
            }
        }

        private void ConfigureGba(JObject section, string system, string path)
        {
            if (system != "gba")
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

            if (!File.Exists(sourceFirmware1))
                throw new ApplicationException("GBA firmware is missing (gba_bios.bin)");
        }

        private void ConfigureColeco(JObject section, string system, string path)
        {
            if (system != "colecovision")
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

            if (!File.Exists(sourceFirmware1))
                throw new ApplicationException("Colecovision firmware is missing (colecovision.rom)");
        }

        private void ConfigureSnes(JObject section, string system)
        {
            if (system != "snes" && system != "sfc" && system != "superfamicom")
                return;

            BindFeature(section, "Region", "mesen_region", "Auto");
        }

        private void SetupGuns(JObject section, string mesenSystem)
        {
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
                _sindenSoft = true;
            }

            foreach (var port in nesPorts)
            {
                var portSection = GetOrCreateContainer(section, port);
                var mapping = GetOrCreateContainer(portSection, "Mapping1");
                mapping["ZapperButtons"] = JValue.CreateNull();
            }

            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_zapper") && !string.IsNullOrEmpty(SystemConfig["mesen_zapper"]) && SystemConfig["mesen_zapper"] != "none")
                {
                    var portSection = GetOrCreateContainer(section, SystemConfig["mesen_zapper"]);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping["ZapperButtons"] = new JArray(mouseID);

                    portSection["Type"] = "Zapper";
                }
            }

            else if (mesenSystem == "Sms")
            {
                foreach (var port in smsPorts)
                {
                    var portSection = GetOrCreateContainer(section, port);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    mapping["LightPhaserButtons"] = JValue.CreateNull();
                }
                
                if (SystemConfig.isOptSet("mesen_zapper") && !string.IsNullOrEmpty(SystemConfig["mesen_zapper"]) && SystemConfig["mesen_zapper"] != "none")
                {
                    var portSection = GetOrCreateContainer(section, SystemConfig["mesen_zapper"]);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping["LightPhaserButtons"] = new JArray(mouseID);

                    portSection["Type"] = "SmsLightPhaser";
                }
            }

            else if (mesenSystem == "Snes")
            {
                foreach (var port in snesPorts)
                {
                    var portSection = GetOrCreateContainer(section, port);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    mapping["SuperScopeButtons"] = JValue.CreateNull();
                }

                if (SystemConfig.isOptSet("mesen_superscope") && !string.IsNullOrEmpty(SystemConfig["mesen_superscope"]) && SystemConfig["mesen_superscope"] != "none")
                {
                    var portSection = GetOrCreateContainer(section, SystemConfig["mesen_superscope"]);
                    var mapping = GetOrCreateContainer(portSection, "Mapping1");
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513,
                        514,
                        6
                    };
                    mapping["SuperScopeButtons"] = new JArray(mouseID);

                    portSection["Type"] = "SuperScope";
                }
            }
        }

        private string GetMesenSystem(string System)
        {
            switch (System)
            {
                case "nes":
                case "fds":
                    return "Nes";
                case "snes":
                    return "Snes";
                case "gba":
                    return "Gba";
                case "gb":
                case "gbc":
                case "sgb":
                    return "Gameboy";
                case "pcengine":
                case "pcenginecd":
                case "supergrafx":
                    return "PcEngine";
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

            if (_sindenSoft)
                Guns.KillSindenSoftware();

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
