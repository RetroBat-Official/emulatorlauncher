using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class BlastemGenerator : Generator
    {
        public BlastemGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private string _system;
        private SdlVersion _sdlVersion = SdlVersion.SDL2_26;
        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private BlastemSaveStatesMonitor _saveStatesWatcher;
        private string _statesPath;
        private bool _fullscreen;
        private static bool _pad2KeyOverride;

        private static readonly Dictionary<string, string> machineTypes = new Dictionary<string, string>()
        {
            { "mastersystem", "sms" },
            { "gamegear",     "gg"  },
            { "sg1000",       "sg"  },
            { "sc3000",       "sc"  },
            { "sega32x",      "32x" },
            { "32x",          "32x" },
            { "pico",         "pico" },
        };

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("blastem");

            string exe = Path.Combine(path, "blastem.exe");
            if (!File.Exists(exe))
                return null;

            _resolution = resolution;

            // The GUID layout depends on the SDL version BlastEm is linked against, so read it from
            // the DLL that ships next to the executable rather than assuming one.
            string sdl2 = Path.Combine(path, "SDL2.dll");
            if (File.Exists(sdl2))
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersion(sdl2);
            else
                _sdlVersion = SdlJoystickGuidManager.GetSdlVersionFromStaticBinary(exe);

            SimpleLogger.Instance.Info("[Generator] BlastEm SDL version : " + _sdlVersion.ToString());

            _fullscreen = ShouldRunFullscreen();

            //Applying bezels
            if (!_fullscreen)
                SystemConfig["forceNoBezel"] = "1";
            else
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
                _statesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "blastem", Path.GetFileNameWithoutExtension(rom));

                _saveStatesWatcher = new BlastemSaveStatesMonitor(rom, _statesPath, localPath,
                    Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png"),
                    SystemConfig["blastem_state_format"] == "gst");

                _saveStatesWatcher.PrepareEmulatorRepository();
            }

            NeutralizeUserConfig();
            SetupConfiguration(path, system, rom, core, resolution);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
            };

            var args = new List<string>();

            string machine;
            if (SystemConfig.isOptSet("blastem_machine") && !string.IsNullOrEmpty(SystemConfig["blastem_machine"]))
                args.Add("-m " + SystemConfig["blastem_machine"]);
            else if (machineTypes.TryGetValue(system, out machine))
                args.Add("-m " + machine);

            args.Add("\"" + rom + "\"");

            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(SystemConfig["state_file"]) && File.Exists(SystemConfig["state_file"]))
                args.Add("-s \"" + SystemConfig["state_file"] + "\"");

            ret.Arguments = string.Join(" ", args);

            return ret;
        }

        /// <summary>
        /// BlastEm reads %LOCALAPPDATA%\blastem\blastem.cfg FIRST and only falls back on the file
        /// sitting next to the executable. 
        /// Move it out of the way for the session and put it back on exit.
        /// </summary>
        private void NeutralizeUserConfig()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "blastem");
            if (!Directory.Exists(appData))
                return;

            foreach (var name in new[] { "blastem.cfg", "controller_types.cfg", "sticky_path" })
            {
                string file = Path.Combine(appData, name);

                AddFileForRestoration(file);

                if (File.Exists(file))
                {
                    SimpleLogger.Instance.Info("[Generator] Moving user configuration aside : " + file);
                    try { File.Delete(file); }
                    catch { SimpleLogger.Instance.Warning("[Generator] Could not remove " + file + ", it will shadow the generated configuration."); }
                }
            }
        }

        private void SetupConfiguration(string path, string system, string rom, string core, ScreenResolution resolution)
        {
            _system = system;

            // Start from the previous session's file when there is one, so that anything the user
            // changed by hand survives, and from the shipped default.cfg otherwise. BlastEm does not
            // merge the two files : whichever it finds first is used whole.
            string targetCfg = Path.Combine(path, "blastem.cfg");
            string sourceCfg = File.Exists(targetCfg) ? targetCfg : Path.Combine(path, "default.cfg");

            if (!File.Exists(sourceCfg))
            {
                SimpleLogger.Instance.Warning("[Generator] Neither blastem.cfg nor default.cfg found in " + path);
                return;
            }

            var cfg = BlastemConfigFile.FromFile(sourceCfg);

            var ui = cfg.GetOrCreateSection("ui");
            var video = cfg.GetOrCreateSection("video");
            var audio = cfg.GetOrCreateSection("audio");
            var sys = cfg.GetOrCreateSection("system");

            // Portable mode : writes blastem.cfg and controller_types.cfg next to the executable and
            // deletes the %LOCALAPPDATA% copy on the first save.
            ui["config_in_exe_dir"] = "on";

            SetupPaths(ui, system, rom, path);
            SetupHotkeys(cfg, core);
            SetupVideo(video, resolution);
            SetupAudio(audio);
            SetupSystem(sys, path);

            CreateControllerConfiguration(cfg, path);

            cfg.Save(targetCfg);
        }

        private void SetupPaths(BlastemConfigNode ui, string system, string rom, string path)
        {
            // SRAM, EEPROM and savestates. Left alone, BlastEm writes them under
            // %LOCALAPPDATA%\blastem ($USERDATA/blastem/$ROMNAME, blastem.c : get_save_dir).
            string saves = AppConfig.GetFullPath("saves");
            if (!string.IsNullOrEmpty(saves))
            {
                string savePath = Path.Combine(saves, system, "blastem");
                if (!Directory.Exists(savePath))
                    try { Directory.CreateDirectory(savePath); } catch { }

                // The literal folder is written rather than $ROMNAME so that C# and BlastEm cannot
                // disagree on how the game name is derived, which matters because the savestate
                // monitor watches that exact folder.
                ui["save_path"] = _statesPath ?? Path.Combine(savePath, Path.GetFileNameWithoutExtension(rom));
                ui["vgm_path"] = savePath;
            }

            string screenshots = AppConfig.GetFullPath("screenshots");
            if (!string.IsNullOrEmpty(screenshots))
                ui["screenshot_path"] = screenshots;

            string roms = AppConfig.GetFullPath("roms");
            if (!string.IsNullOrEmpty(roms))
                ui["initial_path"] = Path.Combine(roms, system);

            // "remember_path" makes BlastEm persist a "sticky_path" file on exit. Turning it off
            // keeps the browser deterministic and removes one more file to manage.
            ui["remember_path"] = "off";

            // Always use the built-in file browser : the native one opens a Windows dialog that
            // cannot be driven with a gamepad.
            ui["use_native_filechooser"] = "off";

            ui["state_format"] = SystemConfig.GetValueOrDefault("blastem_state_format", "native");
        }

        private void SetupVideo(BlastemConfigNode video, ScreenResolution resolution)
        {
            video["fullscreen"] = _fullscreen ? "on" : "off";

            if (resolution != null)
            {
                video["width"] = resolution.Width.ToString();
                video["height"] = resolution.Height.ToString();
            }

            if (_bezelFileInfo != null)
            {
                video["aspect"] = "4:3";
                video["integer_scaling"] = "off";
            }
            else
            {
                BindFeature(video, "aspect", "blastem_aspect", "4:3");
                BindBoolFeature(video, "integer_scaling", "blastem_integerscale", "on", "off");
            }

            BindFeature(video, "scaling", "blastem_scaling", "linear");
            BindBoolFeature(video, "scanlines", "blastem_scanlines", "on", "off");
            BindBoolFeatureOn(video, "gl", "blastem_opengl", "on", "off");
            BindFeature(video, "vsync", "blastem_vsync", "off");
        }

        private void SetupAudio(BlastemConfigNode audio)
        {
            BindFeature(audio, "rate", "blastem_audio_rate", "48000");
            BindFeature(audio, "buffer", "blastem_audio_buffer", "512");
            BindFeature(audio, "lowpass_cutoff", "blastem_lowpass", "3390");
        }

        private void SetupSystem(BlastemConfigNode sys, string path)
        {
            BindFeature(sys, "default_region", "blastem_region", "U");
            BindBoolFeature(sys, "force_region", "blastem_force_region", "on", "off");
            BindFeature(sys, "model", "blastem_model", "md1va3");
            BindFeature(sys, "sync_source", "blastem_sync", "audio");

            // MegaWiFi lets a ROM reach the network, keep it off unless explicitly enabled.
            BindBoolFeature(sys, "megawifi", "blastem_megawifi", "on", "off");

            // Sega CD and Colecovision BIOS. BlastEm accepts an absolute path here
            // (segacd.c / coleco.c, guarded by media_path_is_external == is_absolute_path).
            string bios = AppConfig.GetFullPath("bios");
            if (string.IsNullOrEmpty(bios))
                return;

            SetBiosPath(sys, "scd_bios_us", bios, "bios_CD_U.bin");
            SetBiosPath(sys, "scd_bios_eu", bios, "bios_CD_E.bin");
            SetBiosPath(sys, "scd_bios_jp", bios, "bios_CD_J.bin");
            SetBiosPath(sys, "coleco_bios_path", bios, "colecovision.rom");

            SetBiosPath(sys, "s32x_main_bios", bios, "32X_M_BIOS.BIN");
            SetBiosPath(sys, "s32x_sub_bios", bios, "32X_S_BIOS.BIN");
            SetBiosPath(sys, "s32x_68k_bios", bios, "32X_G_BIOS.BIN");
        }

        private static void SetBiosPath(BlastemConfigNode sys, string key, string biosPath, string fileName)
        {
            string file = Path.Combine(biosPath, fileName);
            if (File.Exists(file))
                sys[key] = file;
            else
                SimpleLogger.Instance.Info("[Generator] BIOS not found, leaving " + key + " alone : " + file);
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2KeyOverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
            }

            return mapping;
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

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }
    }
}