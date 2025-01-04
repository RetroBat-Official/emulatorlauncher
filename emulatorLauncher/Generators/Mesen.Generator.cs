using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class MesenGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("mesen");

            string exe = Path.Combine(path, "Mesen.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

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

            var json = DynamicJson.Load(settingsFile);

            string mesenSystem = GetMesenSystem(system);
            if (mesenSystem == "none")
                return;

            json["FirstRun"] = "false";

            // System preferences
            var systemSection = json.GetOrCreateContainer(mesenSystem);
            ConfigureNes(systemSection, system);
            ConfigureSMS(systemSection, system);
            ConfigurePCEngine(systemSection, system);
            ConfigureSnes(systemSection, system);
            ConfigureGameboy(systemSection, system, path);

            // Emulator preferences
            var preference = json.GetOrCreateContainer("Preferences");

            preference["AutomaticallyCheckForUpdates"] = "false";
            preference["SingleInstance"] = "true";
            preference["PauseWhenInBackground"] = "true";
            preference["PauseWhenInMenusAndConfig"] = "true";
            preference["AllowBackgroundInput"] = "true";
            preference["ConfirmExitResetPower"] = "false";
            preference["AssociateSnesRomFiles"] = "false";
            preference["AssociateSnesMusicFiles"] = "false";
            preference["AssociateNesRomFiles"] = "false";
            preference["AssociateNesMusicFiles"] = "false";
            preference["AssociateGbRomFiles"] = "false";
            preference["AssociateGbMusicFiles"] = "false";
            preference["AssociatePceRomFiles"] = "false";
            preference["AssociatePceMusicFiles"] = "false";

            if (SystemConfig.isOptSet("mesen_autosave") && SystemConfig["mesen_autosave"] != "false")
            {
                preference["EnableAutoSaveState"] = "true";
                preference["AutoSaveStateDelay"] = SystemConfig["mesen_autosave"];
            }
            else
                preference["EnableAutoSaveState"] = "false";

            BindBoolFeatureOn(preference, "AutoLoadPatches", "mesen_patches", "true", "false");
            BindBoolFeature(preference, "EnableRewind", "rewind", "true", "false");
            BindBoolFeatureOn(preference, "DisableOsd", "mesen_osd", "false", "true");
            BindBoolFeature(preference, "ShowGameTimer", "mesen_timecounter", "true", "false");
            BindBoolFeature(preference, "ShowFps", "mesen_fps", "true", "false");

            // define folders
            string gamesFolder = Path.GetDirectoryName(rom);
            if (!string.IsNullOrEmpty(gamesFolder) && Directory.Exists(gamesFolder))
            {
                preference["OverrideGameFolder"] = "true";
                preference["GameFolder"] = gamesFolder;
            }

            string recordsFolder = Path.Combine(AppConfig.GetFullPath("records"), "output", "mesen");
            if (!Directory.Exists(recordsFolder)) try { Directory.CreateDirectory(recordsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(recordsFolder) && Directory.Exists(recordsFolder))
            {
                preference["OverrideAviFolder"] = "true";
                preference["AviFolder"] = recordsFolder;
            }

            string savesFolder = Path.Combine(AppConfig.GetFullPath("saves"), system);
            if (!Directory.Exists(savesFolder)) try { Directory.CreateDirectory(savesFolder); }
                catch { }
            if (!string.IsNullOrEmpty(savesFolder) && Directory.Exists(savesFolder))
            {
                preference["OverrideSaveDataFolder"] = "true";
                preference["SaveDataFolder"] = savesFolder;
            }

            string saveStateFolder = Path.Combine(AppConfig.GetFullPath("saves"), system, "mesen", "SaveStates");
            if (!Directory.Exists(saveStateFolder)) try { Directory.CreateDirectory(saveStateFolder); }
                catch { }
            if (!string.IsNullOrEmpty(saveStateFolder) && Directory.Exists(saveStateFolder))
            {
                preference["OverrideSaveStateFolder"] = "true";
                preference["SaveStateFolder"] = saveStateFolder;
            }

            string screenshotsFolder = Path.Combine(AppConfig.GetFullPath("screenshots"), "mesen");
            if (!Directory.Exists(screenshotsFolder)) try { Directory.CreateDirectory(screenshotsFolder); }
                catch { }
            if (!string.IsNullOrEmpty(screenshotsFolder) && Directory.Exists(screenshotsFolder))
            {
                preference["OverrideScreenshotFolder"] = "true";
                preference["ScreenshotFolder"] = screenshotsFolder;
            }

            // Video menu
            var video = json.GetOrCreateContainer("Video");
            BindFeature(video, "VideoFilter", "mesen_filter", "None");
            BindFeature(video, "AspectRatio", "mesen_ratio", "Auto");
            BindBoolFeature(video, "UseBilinearInterpolation", "bilinear_filtering", "true", "false");
            BindBoolFeatureOn(video, "VerticalSync", "mesen_vsync", "true", "false");
            BindFeatureSlider(video, "ScanlineIntensity", "mesen_scanlines", "0");
            BindBoolFeature(video, "FullscreenForceIntegerScale", "integerscale", "true", "false");

            // Emulation menu
            var emulation = json.GetOrCreateContainer("Emulation");
            BindFeatureSlider(emulation, "RunAheadFrames", "mesen_runahead", "0");

            // Input menu
            var input = json.GetOrCreateContainer("Input");
            BindBoolFeature(input, "HidePointerForLightGuns", "mesen_target", "false", "true");

            // Controllers configuration
            SetupControllers(preference, systemSection, mesenSystem);
            SetupGuns(systemSection, mesenSystem);

            // Save json file
            json.Save();
        }

        private void ConfigureNes(DynamicJson section, string system)
        {
            if (system != "nes" && system != "fds")
                return;
            section["AutoConfigureInput"] = "false";
            BindBoolFeature(section, "EnableHdPacks", "mesen_customtextures", "true", "false");
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit", "true", "false");

            if (system == "fds")
            {
                BindBoolFeature(section, "FdsAutoInsertDisk", "mesen_fdsautoinsertdisk", "true", "false");
                BindBoolFeature(section, "FdsFastForwardOnLoad", "mesen_fdsfastforwardload", "true", "false");
                section["FdsAutoLoadDisk"] = "true";
            }
        }

        private void ConfigureSMS(DynamicJson section, string system)
        {
            if (system != "mastersystem")
                return;
            BindFeature(section, "Region", "mesen_region", "Auto");
            BindFeature(section, "Revision", "mesen_sms_revision", "Compatibility");
            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit", "true", "false");
            BindBoolFeatureOn(section, "EnableFmAudio", "mesen_sms_fmaudio", "true", "false");
        }

        private void ConfigurePCEngine(DynamicJson section, string system)
        {
            if (system != "pcengine")
                return;

            BindBoolFeature(section, "RemoveSpriteLimit", "mesen_spritelimit", "true", "false");
            BindFeature(section, "ConsoleType", "mesen_pce_console", "Auto");
        }

        private void ConfigureGameboy(DynamicJson section, string system, string path)
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
                BindBoolFeatureOn(section, "UseSgb2", "mesen_sgb2", "true", "false");
                BindBoolFeature(section, "HideSgbBorders", "mesen_hidesgbborders", "true", "false");

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

        private void ConfigureSnes(DynamicJson section, string system)
        {
            if (system != "snes")
                return;

            BindFeature(section, "Region", "mesen_region", "Auto");
        }

        private void SetupGuns(DynamicJson section, string mesenSystem)
        {
            foreach (var port in nesPorts)
            {
                var portSection = section.GetOrCreateContainer(port);
                var mapping = portSection.GetOrCreateContainer("Mapping1");
                mapping.SetObject("ZapperButtons", null);
            }

            if (mesenSystem == "Nes")
            {
                if (SystemConfig.isOptSet("mesen_zapper") && !string.IsNullOrEmpty(SystemConfig["mesen_zapper"]) && SystemConfig["mesen_zapper"] != "none")
                {
                    var portSection = section.GetOrCreateContainer(SystemConfig["mesen_zapper"]);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping.SetObject("ZapperButtons", mouseID);

                    portSection["Type"] = "Zapper";
                }
            }

            else if (mesenSystem == "Sms")
            {
                foreach (var port in smsPorts)
                {
                    var portSection = section.GetOrCreateContainer(port);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    mapping.SetObject("LightPhaserButtons", null);
                }
                
                if (SystemConfig.isOptSet("mesen_zapper") && !string.IsNullOrEmpty(SystemConfig["mesen_zapper"]) && SystemConfig["mesen_zapper"] != "none")
                {
                    var portSection = section.GetOrCreateContainer(SystemConfig["mesen_zapper"]);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513
                    };
                    mapping.SetObject("LightPhaserButtons", mouseID);

                    portSection["Type"] = "SmsLightPhaser";
                }
            }

            else if (mesenSystem == "Snes")
            {
                foreach (var port in snesPorts)
                {
                    var portSection = section.GetOrCreateContainer(port);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    mapping.SetObject("SuperScopeButtons", null);
                }

                if (SystemConfig.isOptSet("mesen_superscope") && !string.IsNullOrEmpty(SystemConfig["mesen_superscope"]) && SystemConfig["mesen_superscope"] != "none")
                {
                    var portSection = section.GetOrCreateContainer(SystemConfig["mesen_superscope"]);
                    var mapping = portSection.GetOrCreateContainer("Mapping1");
                    List<int> mouseID = new List<int>
                    {
                        512,
                        513,
                        514,
                        6
                    };
                    mapping.SetObject("SuperScopeButtons", mouseID);

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
                case "gb":
                case "gbc":
                case "sgb":
                    return "Gameboy";
                case "pcengine":
                case "supergrafx":
                    return "PcEngine";
                case "mastersystem":
                    return "Sms";
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
    }
}
