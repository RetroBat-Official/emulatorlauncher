using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using System;

namespace EmulatorLauncher
{
    partial class BigPEmuGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _path;
        private SaveStatesWatcher _saveStatesWatcher;
        private int _saveStateSlot = 0;

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("bigpemu");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "BigPEmu.exe");
            if (!File.Exists(exe))
                return null;

            string[] extensions = new string[] { ".cue", ".cdi", ".j64",".jag", ".rom", ".bin", ".prg", ".cof", ".abs" };
            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip" || Path.GetExtension(rom).ToLowerInvariant() == ".7z" || Path.GetExtension(rom).ToLowerInvariant() == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
                string emulatorPath = Path.Combine(path, "userdata");

                _saveStatesWatcher = new BigPemuSaveStatesMonitor(rom, emulatorPath, localPath, Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png"));
                _saveStatesWatcher.PrepareEmulatorRepository();
                _saveStateSlot = _saveStatesWatcher.Slot != -1 ? _saveStatesWatcher.Slot - 1 : 0;

                if (_saveStateSlot > 0 && SystemConfig.isOptSet("state_file") && !string.IsNullOrEmpty(SystemConfig["state_file"]) && File.Exists(SystemConfig["state_file"]))
                    _saveStateSlot++;
            }
            else
                _saveStatesWatcher = null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            _path = path;

            //Applying bezels
            if (fullscreen && (!SystemConfig.isOptSet("bigpemu_renderer") || SystemConfig["bigpemu_renderer"] == "BigPEmu_Video_OpenGL"))
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            else if (fullscreen && (SystemConfig["bigpemu_renderer"] == "BigPEmu_Video_D3D12"))
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.dxgi, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            _resolution = resolution;

            List<string> commandArray = new List<string>
            {
                //arguments:
                //first argument must always be the rom
                //-localdata : specify to use the config file stored in "userdata" folder within emulator folder instead of the one in %APPDATA%
                "\"" + rom + "\"",
                "-localdata"
            };

            string args = string.Join(" ", commandArray);

            SetupConfiguration(path, system, rom, resolution, fullscreen);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        //Configuration file in json format "BigPEmuConfig.bigpcfg"
        private void SetupConfiguration(string path, string system, string rom, ScreenResolution resolution = null, bool fullscreen = false)
        {
            //open userdata config file
            string folder = Path.Combine(path, "userdata");
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); }
                catch { }
            }

            string configfile = Path.Combine(folder, "BigPEmuConfig.bigpcfg");

            if (File.Exists(configfile))
            {
                var json = DynamicJson.Load(configfile);
                var bigpemucore = json.GetOrCreateContainer("BigPEmuConfig");

                BindFeature(bigpemucore, "VideoPlugin", "bigpemu_renderer", "BigPEmu_Video_OpenGL");

                //system part
                var jsonSystem = bigpemucore.GetOrCreateContainer("System");
                BindBoolFeature(jsonSystem, "PALMode", "pal_mode", "1", "0");
                if (_saveStatesWatcher != null)
                    jsonSystem["StateSlot"] = _saveStateSlot.ToString();
                
                jsonSystem["PerGameSlots"] = "1";
                jsonSystem["SaveAutoIncr"] = "1";
                
                if (system == "jaguarcd" || Path.GetExtension(rom).ToLowerInvariant() == ".cue")
                {
                    jsonSystem["AttachButch"] = "1";
                    jsonSystem["AttachMT"] = "1";
                }
                else
                {
                    jsonSystem["AttachButch"] = "0";
                    jsonSystem["AttachMT"] = "0";
                }
                BindBoolFeature(jsonSystem, "ForceJGD", "bigpemu_jgd", "1", "0");

                //video part
                var video = bigpemucore.GetOrCreateContainer("Video");
                BindFeature(video, "DisplayMode", "displaymode", fullscreen ? "0" : "1");      //0 for borderless windows, 1 for windowed, 2 for fullscreen
                BindBoolFeatureOn(video, "VSync", "vsync", "1", "0");                  // vsync on as default setting
                BindFeature(video, "HDROutput", "enable_hdr", "0");
                video["ShittyFreqWarn"] = "0";

                if (resolution != null)
                {
                    video["DisplayWidth"] = (resolution.Width).ToString();
                    video["DisplayHeight"] = (resolution.Height).ToString();
                }
                else
                {
                    video["DisplayWidth"] = (ScreenResolution.CurrentResolution.Width).ToString();
                    video["DisplayHeight"] = (ScreenResolution.CurrentResolution.Height).ToString();
                }

                if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                {
                    string emuMonitor = "\\\\" + ".\\" + "DISPLAY" + SystemConfig["MonitorIndex"];
                    video["Display"] = emuMonitor;
                }

                BindFeature(video, "MSAAPref", "bigpemu_antialiasing", "0");
                BindFeature(video, "ScreenFilter", "smooth", "0");
                BindFeature(video, "ScreenAspect", "bigpemu_ratio", "2");
                BindFeature(video, "ScreenScaling", "bigpemu_scaling", "5");

                // Allow use of internal effects if shaders are set to none
                if (SystemConfig["shaderset"] == "none" && SystemConfig.isOptSet("bigpemu_shader") && !string.IsNullOrEmpty(SystemConfig["bigpemu_shader"]))
                    bigpemucore["ScreenEffect"] = SystemConfig["bigpemu_shader"];
                else
                    bigpemucore["ScreenEffect"] = "";

                ConfigureControllers(bigpemucore);

                //save
                json.Save();
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
            {
                ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, _path);
                ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, _path);
                return 0;
            }

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, _path);
            ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, _path);
            return ret;
        }
    }
}
