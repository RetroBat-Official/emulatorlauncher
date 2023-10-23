using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class AresGenerator : Generator
    {
        public AresGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("ares");

            string exe = Path.Combine(path, "ares.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //Applying bezels
            if (!fullscreen)
                SystemConfig["forceNoBezel"] = "1";

            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            string aresSystem = core;

            if (aresSystems.ContainsKey(core))
                aresSystem = aresSystems[core];

            List<string> commandArray = new List<string>();
            
            commandArray.Add("--system");
            commandArray.Add("\"" + aresSystem + "\"");

            // if running a n64dd game, both roms need to be specified with the ndd rom last
            if (system == "n64dd" && Path.GetExtension(rom) == ".ndd")
            {
                string n64Rom = rom.Remove(rom.Length - 4);
                if (File.Exists(n64Rom))
                    commandArray.Add("\"" + n64Rom + "\"");
            }

            commandArray.Add("\"" + rom + "\"");

            if (system == "n64dd" && Path.GetExtension(rom) != ".ndd")
            {
                string nddRom = rom + ".ndd";
                if (File.Exists(nddRom))
                    commandArray.Add("\"" + nddRom + "\"");
            }

            commandArray.Add("--no-file-prompt");

            if (fullscreen)
                commandArray.Add("--fullscreen");

            string args = string.Join(" ", commandArray);

            var bml = BmlFile.Load(Path.Combine(path, "settings.bml"));
            SetupConfiguration(bml, path, system, core, rom);
            SetupFirmwares(bml, path, system, core, rom);
            WriteKeyboardHotkeys(bml, path);
            CreateControllerConfiguration(bml, path);

            bml.Save();

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,                
            };
        }

        /// <summary>
        /// Setup settings.bml file
        /// </summary>
        /// <param name="path"></param>
        private void SetupConfiguration(BmlFile bml, string path, string system, string core, string rom)
        {
            // Set driver for input
            var input = bml.GetOrCreateContainer("Input");
            input["Driver"] = "SDL";

            // Video
            var video = bml.GetOrCreateContainer("Video");
            video["AspectCorrection"] = "true";
            video["AdaptiveSizing"] = "true";
            BindFeature(video, "Driver", "ares_renderer", "OpenGL 3.2");
            BindFeature(video, "Output", "ares_aspect", "Scale");

            if (SystemConfig.isOptSet("ares_shaders") && SystemConfig["ares_shaders"] == "none")
                video["Shader"] = "None";
            else if (SystemConfig.isOptSet("ares_shaders") && SystemConfig["ares_shaders"] == "Blur")
                video["Shader"] = "Blur";
            else if (SystemConfig.isOptSet("ares_shaders") && !string.IsNullOrEmpty(SystemConfig["ares_shaders"]))
            {
                string shader = SystemConfig["ares_shaders"];
                string pathShader = Path.Combine(path, "Shaders", shader + "/").Replace("\\", "/");
                video["Shader"] = pathShader;
            }

            BindBoolFeature(video, "ColorBleed", "ares_colobleed", "true", "false");
            BindBoolFeature(video, "ColorEmulation", "ares_coloremulation", "false", "true");
            BindBoolFeature(video, "InterframeBlending", "ares_interframe_blend", "false", "true");
            BindBoolFeature(video, "Overscan", "ares_overscan", "true", "false");
            BindBoolFeature(video, "PixelAccuracy", "ares_pixel_accurate", "true", "false");
            BindFeature(video, "Quality", "ares_n64_quality", "SD");

            if (!SystemConfig.isOptSet("ares_n64_quality") || SystemConfig["ares_n64_quality"] == "SD")
                    video["Supersampling"] = "false";
            else
                BindBoolFeature(video, "Supersampling", "ares_supersampling", "true", "false");
            
            bool supersample = SystemConfig.isOptSet("ares_supersampling") && SystemConfig.getOptBoolean("ares_supersampling");

            if (!supersample)
                BindBoolFeature(video, "WeaveDeinterlacing", "ares_weavedeinterlacing", "false", "true");
            else
                video["WeaveDeinterlacing"] = "false";

            // Audio
            var audio = bml.GetOrCreateContainer("Audio");
            BindFeature(audio, "Driver", "ares_audio_renderer", "WASAPI");

            // General Settings
            var general = bml.GetOrCreateContainer("General");
            BindBoolFeature(general, "Rewind", "rewind", "true", "false");
            BindBoolFeature(general, "RunAhead", "ares_runahead", "true", "false");
            BindBoolFeature(general, "AutoSaveMemory", "autosave", "true", "false");

            // Boot
            var boot = bml.GetOrCreateContainer("Boot");
            BindFeature(boot, "Prefer", "ares_region", "PAL");
            BindBoolFeature(boot, "Fast", "ares_fastboot", "true", "false");

            // Paths
            var paths = bml.GetOrCreateContainer("Paths");
            
            string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "ares");
            if (!Directory.Exists(screenshotsPath)) try { Directory.CreateDirectory(screenshotsPath); }
                catch { }
            string aresScreenshotsPath = screenshotsPath + "/";
            
            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "ares");
            if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                catch { }
            string aresSavesPath = savesPath + "/";
            
            paths["Screenshots"] = aresScreenshotsPath.Replace("\\", "/");
            paths["Saves"] = aresSavesPath.Replace("\\", "/");

            // Current rom path
            var aresCore = bml.GetOrCreateContainer(core);
            aresCore["Path"] = Path.GetDirectoryName(rom).Replace("\\", "/") + "/";
        }

        private void WriteKeyboardHotkeys(BmlFile bml, string path)
        {
            // Use padtokey mapping to map these keys to controllers as Ares does not allow combos
            var hotkey = bml.GetOrCreateContainer("Hotkey");
            hotkey["ToggleFullscreen"] = "0x1/0/90;;";      // TAB
            hotkey["FastForward"] = "0x1/0/9;;";            // F9
            hotkey["Rewind"] = "0x1/0/8;;";                 // F8
            hotkey["ToggleFastForward"] = "0x1/0/10;;";     // F10
            hotkey["FrameAdvance"] = "0x1/0/11;;";          // F11
            hotkey["CaptureScreenshot"] = "0x1/0/5;;";      // F5
            hotkey["SaveState"] = "0x1/0/1;;";              // F1
            hotkey["LoadState"] = "0x1/0/2;;";              // F2
            hotkey["DecrementStateSlot"] = "0x1/0/3;;";     // F3
            hotkey["IncrementStateSlot"] = "0x1/0/4;;";     // F4
            hotkey["PauseEmulation"] = "0x1/0/6;;";         // F6
            hotkey["QuitEmulator"] = "0x1/0/12;;";          // F12
        }

        private void SetupFirmwares(BmlFile bml, string path, string system, string core, string rom)
        {
            if (system == "colecovision")
            {
                string colecoBios = Path.Combine(AppConfig.GetFullPath("bios"), "colecovision.rom");
                if (File.Exists(colecoBios))
                {
                    var sys = bml.GetOrCreateContainer("ColecoVision");
                    var firmware = sys.GetOrCreateContainer("Firmware");
                    firmware["BIOS.World"] = colecoBios.Replace("\\", "/");
                }
            }

            if (system == "fds")
            {
                string fdsBios = Path.Combine(AppConfig.GetFullPath("bios"), "disksys.rom");
                if (File.Exists(fdsBios))
                {
                    var sys = bml.GetOrCreateContainer("FamicomDiskSystem");
                    var firmware = sys.GetOrCreateContainer("Firmware");
                    firmware["BIOS.Japan"] = fdsBios.Replace("\\", "/");
                }
            }

            if (system == "gba")
            {
                string gbaBios = Path.Combine(AppConfig.GetFullPath("bios"), "gba_bios.bin");
                if (File.Exists(gbaBios))
                {
                    var sys = bml.GetOrCreateContainer("GameBoyAdvance");
                    var firmware = sys.GetOrCreateContainer("Firmware");
                    firmware["BIOS.World"] = gbaBios.Replace("\\", "/");
                }
            }

            if (system == "mastersystem")
            {
                var sys = bml.GetOrCreateContainer("MasterSystem");
                var firmware = sys.GetOrCreateContainer("Firmware");

                string bios_euus = Path.Combine(AppConfig.GetFullPath("bios"), "[BIOS] Sega Master System (USA, Europe) (v1.3).sms");
                string bios_japan = Path.Combine(AppConfig.GetFullPath("bios"), "[BIOS] Sega Master System (Japan) (v2.1).sms");

                if (File.Exists(bios_japan))
                    firmware["BIOS.Japan"] = bios_japan.Replace("\\", "/");
                if (File.Exists(bios_euus))
                {
                    firmware["BIOS.Europe"] = bios_euus.Replace("\\", "/");
                    firmware["BIOS.US"] = bios_euus.Replace("\\", "/");
                }
            }

            if (system == "n64dd")
            {
                var sys = bml.GetOrCreateContainer("Nintendo64DD");
                var firmware = sys.GetOrCreateContainer("Firmware");

                string n64dd_japan = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "IPL_JAP.n64");
                string n64dd_us = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "IPL_USA.n64");
                string n64dd_dev = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "IPL_DEV.n64");
                if (File.Exists(n64dd_japan))
                    firmware["BIOS.Japan"] = n64dd_japan.Replace("\\", "/");
                if (File.Exists(n64dd_us))
                    firmware["BIOS.US"] = n64dd_us.Replace("\\", "/");
                if (File.Exists(n64dd_dev))
                    firmware["BIOS.DEV"] = n64dd_dev.Replace("\\", "/");
            }

            if (system == "ngp")
            {
                string ngpBios = Path.Combine(AppConfig.GetFullPath("bios"), "[BIOS] SNK Neo Geo Pocket (Japan, Europe) (En,Ja).ngp");
                if (File.Exists(ngpBios))
                {
                    var sys = bml.GetOrCreateContainer("NeoGeoPocket");
                    var firmware = sys.GetOrCreateContainer("Firmware");
                    firmware["BIOS.World"] = ngpBios.Replace("\\", "/");
                }
            }

            if (system == "ngpc")
            {
                string ngpBios = Path.Combine(AppConfig.GetFullPath("bios"), "[BIOS] SNK Neo Geo Pocket (Japan, Europe) (En,Ja).ngp");
                if (File.Exists(ngpBios))
                {
                    var sys = bml.GetOrCreateContainer("NeoGeoPocketColor");
                    var firmware = sys.GetOrCreateContainer("Firmware");
                    firmware["BIOS.World"] = ngpBios.Replace("\\", "/");
                }
            }

            if (system == "pcenginecd")
            {
                var sys = bml.GetOrCreateContainer("PCEngineCD");
                var firmware = sys.GetOrCreateContainer("Firmware");

                string biospcecd = Path.Combine(AppConfig.GetFullPath("bios"), "syscard3.pce");
                if (File.Exists(biospcecd))
                {
                    firmware["BIOS.US"] = biospcecd.Replace("\\", "/");
                    firmware["BIOS.Japan"] = biospcecd.Replace("\\", "/");
                }
            }

            if (system == "psx")
            {
                var sys = bml.GetOrCreateContainer("PlayStation");
                var firmware = sys.GetOrCreateContainer("Firmware");

                string bios_us = Path.Combine(AppConfig.GetFullPath("bios"), "scph5501.bin");
                string bios_japan = Path.Combine(AppConfig.GetFullPath("bios"), "scph5500.bin");
                string bios_eu = Path.Combine(AppConfig.GetFullPath("bios"), "scph5502.bin");
                if (File.Exists(bios_us))
                    firmware["BIOS.US"] = bios_us.Replace("\\", "/");
                if (File.Exists(bios_japan))
                    firmware["BIOS.Japan"] = bios_japan.Replace("\\", "/");
                if (File.Exists(bios_eu))
                    firmware["BIOS.Europe"] = bios_eu.Replace("\\", "/");
            }

            if (system == "segacd")
            {
                var sys = bml.GetOrCreateContainer("MegaCD");
                var firmware = sys.GetOrCreateContainer("Firmware");

                string bios_japan = Path.Combine(AppConfig.GetFullPath("bios"), "bios_CD_J.bin");
                string bios_us = Path.Combine(AppConfig.GetFullPath("bios"), "bios_CD_U.bin");
                string bios_eu = Path.Combine(AppConfig.GetFullPath("bios"), "bios_CD_E.bin");
                if (File.Exists(bios_japan))
                    firmware["BIOS.Japan"] = bios_japan.Replace("\\", "/");
                if (File.Exists(bios_us))
                    firmware["BIOS.US"] = bios_us.Replace("\\", "/");
                if (File.Exists(bios_eu))
                    firmware["BIOS.Europe"] = bios_eu.Replace("\\", "/");
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

        private static Dictionary<string, string> aresSystems = new Dictionary<string, string>
        {
            { "Atari2600", "Atari2600" },
            { "WonderSwan", "WonderSwan" },
            { "WonderSwanColor", "WonderSwanColor" },
            { "PocketChallengeV2", "PocketChallengeV2" },
            { "ColecoVision", "ColecoVision" },
            { "MyVision", "MyVision" },
            { "MSX", "MSX" },
            { "MSX2", "MSX2" },
            { "PCEngine", "PCEngine" },
            { "PCEngineCD", "PCEngineCD" },
            { "SuperGrafx", "SuperGrafx" },
            { "SuperGrafxCD", "SuperGrafxCD" },
            { "Famicom", "Famicom" },
            { "FamicomDiskSystem", "FamicomDiskSystem" },
            { "SuperFamicom", "SuperFamicom" },
            { "Nintendo64", "Nintendo64" },
            { "Nintendo64DD", "Nintendo64DD" },
            { "GameBoy", "GameBoy" },
            { "GameBoyColor", "GameBoyColor" },
            { "GameBoyAdvance", "GameBoyAdvance" },
            { "SG-1000", "SG-1000" },
            { "MasterSystem", "MasterSystem" },
            { "GameGear", "GameGear" },
            { "MegaDrive", "MegaDrive" },
            { "Mega32X", "Mega32X" },
            { "MegaCD", "MegaCD" },
            { "MegaCD32X", "MegaCD32X" },
            { "NeoGeoAES", "NeoGeoAES" },
            { "NeoGeoMVS", "NeoGeoMVS" },
            { "NeoGeoPocket", "NeoGeoPocket" },
            { "NeoGeoPocketColor", "NeoGeoPocketColor" },
            { "PlayStation", "PlayStation" },
            { "ZXSpectrum", "ZXSpectrum" },
            { "ZXSpectrum128", "ZXSpectrum128" }
        };
    }
}
