using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.PadToKeyboard;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    /// <summary>
    /// Model 3-Vulkan (Igloo-Garage/Model-3-Vulkan), Supermodel fork with a Vulkan 1.3 renderer and SDL3 inputs.
    /// Everything lives in the "Data" folder next to Model3-Vulkan.exe.
    /// </summary>
    partial class Model3VulkanGenerator : Generator
    {
        private const string GlobalSection = " Global ";

        private BezelFiles _bezelFileInfo;
        private string _path;
        private string _romName;
        private SaveStatesWatcher _saveStatesWatcher;
        private bool _pad2Keyoverride = false;

        public Model3VulkanGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("model3-vulkan");
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "Model3-Vulkan.exe");
            if (!File.Exists(exe))
                return null;

            Sdl3GameController.SetEnumerationHints(Sdl3GameController.Sdl3HintProfile.Sdl3Default);
            SetSdl3EmulatorVersion(exe);

            ConfigureMameHook();

            _path = path;
            _romName = Path.GetFileNameWithoutExtension(rom);

            bool fullscreen = ShouldRunFullscreen();

            string widescreen = SystemConfig["widescreen"];
            if (fullscreen && widescreen != "1" && widescreen != "2")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            SetupConfiguration(path, resolution, fullscreen);
            SetupSaveStates(system, rom);

            var commandArray = new List<string>();

            commandArray.Add(fullscreen ? "-fullscreen" : "-window");

            string stateFile = SystemConfig["state_file"];
            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(stateFile) && File.Exists(stateFile))
                commandArray.Add("-load-state=\"" + stateFile + "\"");

            commandArray.Add("\"" + rom + "\"");

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
            };
        }

        private void ConfigureMameHook()
        {
            if (!SystemConfig.getOptBoolean("use_mamehooker"))
            {
                MameHooker.KillMameHooker();
                return;
            }

            string romName = Path.GetFileNameWithoutExtension(SystemConfig["rom"]).ToLower();
            if (!MameHooker.Supermodel.ConfigureSupermodel(romName))
                return;

            Process mameHookProcess = MameHooker.StartMameHooker();
            if (mameHookProcess == null)
            {
                SimpleLogger.Instance.Error("[ERROR] Failed to start MameHooker");
                return;
            }

            mameHookProcess.WaitForInputIdle(2000);
            System.Threading.Thread.Sleep(2000);
        }

        private void SetupConfiguration(string path, ScreenResolution resolution, bool fullscreen)
        {
            string configPath = Path.Combine(path, "Data", "Config");
            FileTools.TryCreateDirectory(configPath);

            // The settings file : when it does not exist,
            // the emulator uses its built-in defaults for every key that RetroBat does not write.
            string iniPath = Path.Combine(configPath, "Model3-Vulkan.ini");

            try
            {
                using (IniFile ini = new IniFile(iniPath, IniOptions.UseSpaces))
                {
                    SetupDisplay(ini, resolution, fullscreen);
                    SetupBezel(ini);

                    // Video
                    BindBoolIniFeatureOn(ini, GlobalSection, "VSync", "m3_vsync", "1", "0");
                    BindBoolIniFeatureOn(ini, GlobalSection, "Throttle", "throttle", "1", "0");
                    BindIniFeature(ini, GlobalSection, "RefreshRateMode", "m3v_refreshrate", "1");
                    BindBoolIniFeature(ini, GlobalSection, "LowLatency", "m3v_lowlatency", "1", "0");
                    BindIniFeature(ini, GlobalSection, "InternalResolution", "m3v_internalres", "0");
                    BindBoolIniFeature(ini, GlobalSection, "IntegerScale", "integerscale", "1", "0");
                    BindIniFeature(ini, GlobalSection, "MSAA", "m3v_msaa", "0");
                    BindIniFeature(ini, GlobalSection, "TexFilter", "m3v_texfilter", "0");
                    BindIniFeature(ini, GlobalSection, "AlphaSortMode", "m3v_alphasort", "0");
                    BindBoolIniFeatureOn(ini, GlobalSection, "QuadInterpolation", "m3v_quadinterpolation", "1", "0");
                    BindIniFeature(ini, GlobalSection, "UpscaleMode", "m3v_upscale", "2");
                    BindIniFeature(ini, GlobalSection, "CRTcolors", "m3v_crtcolors", "0");
                    BindIniFeature(ini, GlobalSection, "PostCRT", "m3v_crt", "0");
                    BindBoolIniFeature(ini, GlobalSection, "PostFXAA", "m3v_fxaa", "1", "0");
                    BindBoolIniFeature(ini, GlobalSection, "NoWhiteFlash", "m3v_nowhiteflash", "1", "0");
                    BindBoolIniFeature(ini, GlobalSection, "LoadHDTextures", "m3v_hdtextures", "1", "0");

                    // On-screen information
                    BindBoolIniFeature(ini, GlobalSection, "ShowStatsOsd", "m3v_stats", "1", "0");
                    ini.WriteValue(GlobalSection, "ShowFrameRate", "0");

                    // Emulation
                    BindIniFeatureSlider(ini, GlobalSection, "PowerPCFrequency", "m3_ppc_frequency", "0");
                    BindBoolIniFeature(ini, GlobalSection, "MultiThreaded", "m3_thread", "0", "1");

                    // Outputs for MameHooker (lamps, drive board, recoil)
                    if (SystemConfig.isOptSet("supermodel_output") && !string.IsNullOrEmpty(SystemConfig["supermodel_output"]))
                        ini.WriteValue(GlobalSection, "Outputs", SystemConfig["supermodel_output"]);
                    else
                        ini.WriteValue(GlobalSection, "Outputs", "none");

                    // ROM folder
                    ini.WriteValue(GlobalSection, "ROMPath", "\"" + Path.Combine(AppConfig.GetFullPath("roms"), "model3") + "\"");

                    // Inputs
                    ini.WriteValue(GlobalSection, "ControlPreset", "custom");

                    WriteKeyboardHotkeys(ini);
                    CreateControllerConfiguration(ini);

                    ini.Save();
                }
            }
            catch (System.Exception ex)
            {
                SimpleLogger.Instance.Error("[ERROR] Unable to write " + iniPath + " : " + ex.Message);
            }
        }

        private void SetupDisplay(IniFile ini, ScreenResolution resolution, bool fullscreen)
        {
            // Fullscreen is a borderless window sized to the panel
            ini.WriteValue(GlobalSection, "FullScreen", fullscreen ? "1" : "0");
            ini.WriteValue(GlobalSection, "BorderlessWindow", "0");
            ini.WriteValue(GlobalSection, "ExclusiveFullscreen", fullscreen && SystemConfig.getOptBoolean("exclusivefs") ? "1" : "0");

            // Window size (render size in windowed mode) : RetroBat resolution, otherwise the emulator default (1280x960)
            if (!fullscreen && resolution != null)
            {
                ini.WriteValue(GlobalSection, "XResolution", resolution.Width.ToString());
                ini.WriteValue(GlobalSection, "YResolution", resolution.Height.ToString());
            }
            else
            {
                ini.Remove(GlobalSection, "XResolution");
                ini.Remove(GlobalSection, "YResolution");
            }

            // Without a position, the window is centred on the DisplayNumber output
            ini.Remove(GlobalSection, "WindowXPosition");
            ini.Remove(GlobalSection, "WindowYPosition");

            // DisplayNumber is the Windows display number ("\\.\DISPLAY2" => 2, matched with EnumDisplayMonitors), 0 = primary display.
            // MonitorIndex not set : the emulator keeps its own choice.
            if (Program.IsMonitorIndexSet && Program.TargetScreen != null)
            {
                string digits = new string((Program.TargetScreen.DeviceName ?? "").Where(char.IsDigit).ToArray());
                ini.WriteValue(GlobalSection, "DisplayNumber", string.IsNullOrEmpty(digits) ? "0" : digits.ToInteger().ToString());
            }

            // Aspect : 4:3 by default (with bezel), widescreen (3D and 2D background expanded) or stretched
            string widescreen = SystemConfig["widescreen"];
            ini.WriteValue(GlobalSection, "WideScreen", widescreen == "1" ? "1" : "0");
            ini.WriteValue(GlobalSection, "WideBackground", widescreen == "1" ? "1" : "0");
            ini.WriteValue(GlobalSection, "Stretch", widescreen == "2" ? "1" : "0");
        }

        private void SetupBezel(IniFile ini)
        {
            if (_bezelFileInfo != null && !string.IsNullOrEmpty(_bezelFileInfo.PngFile) && File.Exists(_bezelFileInfo.PngFile))
            {
                ini.WriteValue(GlobalSection, "BezelMode", "2");
                ini.WriteValue(GlobalSection, "BezelImage", "\"" + _bezelFileInfo.PngFile + "\"");
                return;
            }

            // No RetroBat bezel : black bars rather than the cabinet picture shipped with the emulator
            ini.WriteValue(GlobalSection, "BezelMode", "0");
            ini.Remove(GlobalSection, "BezelImage");
        }

        /// <summary>
        /// Model3-Vulkan saves in Data\Saves\<game>.zst<slot> (slots 1 to 16) : the watcher copies each new state to the RetroBat folder.
        /// A state chosen in EmulationStation is loaded with -load-state, the emulator folder is never changed.
        /// </summary>
        private void SetupSaveStates(string system, string rom)
        {
            if (!Program.HasEsSaveStates || !Program.EsSaveStates.IsEmulatorSupported("model3-vulkan"))
                return;

            string localPath = Program.EsSaveStates.GetSavePath(system, "model3-vulkan", "model3-vulkan");
            string emulatorPath = Path.Combine(_path, "Data", "Saves");
            string icon = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png");

            _saveStatesWatcher = new Model3VulkanSaveStatesMonitor(rom, emulatorPath, localPath, icon);
            SimpleLogger.Instance.Info("[INFO] SavesStatesWatcher enabled.");
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
                PadToKey.AddOrUpdateKeyMapping(mapping, "Model3-Vulkan", InputKey.hotkey | InputKey.start, "(%{CLOSE})");
            }

            return mapping;
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
