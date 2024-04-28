using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    // Generator for Rosalie's Mupen64Plus GUI
    partial class Mupen64Generator : Generator
    {
        public Mupen64Generator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        private SaveStatesWatcher _saveStatesWatcher;
        
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
            string path = AppConfig.GetFullPath("mupen64");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "RMG.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfiguration(path, rom, system, emulator, core, fullscreen);
            
            if (!SystemConfig.isOptSet("gfxplugin") || SystemConfig["gfxplugin"] == "glide")
                SetupGFX(path);

            List<string> commandArray = new List<string>();
            
            if (fullscreen)
                commandArray.Add("-f");
            
            if (!SystemConfig.isOptSet("show_gui") || !SystemConfig.getOptBoolean("show_gui"))
                commandArray.Add("-n");

            commandArray.Add("-q");

            //Applying bezels
            if (SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] != "1")
                SystemConfig["forceNoBezel"] = "1";

            if (fullscreen)
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            }

            _resolution = resolution;

            if (system == "n64dd" && Path.GetExtension(rom).ToLowerInvariant() != ".ndd")
            {
                string n64ddrom = rom + ".ndd";
                if (File.Exists(n64ddrom))
                {
                    commandArray.Add("--disk");
                    commandArray.Add("\"" + n64ddrom + "\"");
                }

                commandArray.Add("\"" + rom + "\"");
            }

            else if (system == "n64dd" && Path.GetExtension(rom).ToLowerInvariant() == ".ndd")
            {
                string romPath = Path.GetDirectoryName(rom);
                string n64rom = Path.Combine(romPath, Path.GetFileNameWithoutExtension(rom));
                if (File.Exists(n64rom))
                {
                    commandArray.Add("--disk");
                    commandArray.Add("\"" + rom + "\"");
                }

                commandArray.Add("\"" + n64rom + "\"");
            }

            else
                commandArray.Add("\"" + rom + "\"");

            /*
            if (File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("--savestate");
                commandArray.Add(Path.GetFullPath(SystemConfig["state_file"]));
            }
            */

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfiguration(string path, string rom, string system, string emulator, string core, bool fullscreen)
        {
            string conf = Path.Combine(path, "Config", "mupen64plus.cfg");

            using (var ini = IniFile.FromFile(conf, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                // Add rom path
                ini.WriteValue("Rosalie's Mupen GUI RomBrowser", "Directory", Path.GetDirectoryName(rom).Replace("\\", "/"));

                // Other paths
                string screenshotPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "mupen64");
                FileTools.TryCreateDirectory(screenshotPath);                
                ini.WriteValue("Core", "ScreenshotPath", screenshotPath.Replace("\\", "/"));

                bool incrementSlot = SystemConfig["incrementalsavestates"] != "2";

                if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
                {
                    string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);

                    _saveStatesWatcher = new Mupen64SaveStatesMonitor(rom, Path.Combine(path, "Save", "State"), localPath);
                    _saveStatesWatcher.PrepareEmulatorRepository();

                    ini.WriteValue("Core", "SaveStatePath", "Save/State");
                    ini.WriteValue("Core", "CurrentStateSlot", _saveStatesWatcher.Slot.ToString());
                    ini.WriteValue("Core", "SaveFilenameFormat", "1");
                    ini.WriteValue("Core", "AutoStateSlotIncrement", "False");
                }
                else
                {
                    string saveStatePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "mupen64");
                    FileTools.TryCreateDirectory(saveStatePath);

                    ini.WriteValue("Core", "SaveStatePath", saveStatePath.Replace("\\", "/"));
                    ini.WriteValue("Core", "AutoStateSlotIncrement", incrementSlot ? "True" : "False");
                }

                string saveSRAMPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "sram");
                FileTools.TryCreateDirectory(saveSRAMPath);                
                ini.WriteValue("Core", "SaveSRAMPath", saveSRAMPath.Replace("\\", "/"));

                // Default settings                
                ini.WriteValue("Rosalie's Mupen GUI", "HideCursorInFullscreenEmulation", "True");

                if (SystemConfig.isOptSet("mupen64_pause_on_focus_lost") && SystemConfig.getOptBoolean("mupen64_pause_on_focus_lost"))
                {
                    ini.WriteValue("Rosalie's Mupen GUI", "PauseEmulationOnFocusLoss", "True");
                    ini.WriteValue("Rosalie's Mupen GUI", "ResumeEmulationOnFocus", "True");
                }
                else
                {
                    ini.WriteValue("Rosalie's Mupen GUI", "PauseEmulationOnFocusLoss", "False");
                    ini.WriteValue("Rosalie's Mupen GUI", "ResumeEmulationOnFocus", "False");
                }
                
                ini.WriteValue("Rosalie's Mupen GUI", "AutomaticFullscreen", fullscreen ? "True" : "False");
                ini.WriteValue("Rosalie's Mupen GUI", "ShowVerboseLogMessages", "False");
                ini.WriteValue("Rosalie's Mupen GUI", "CheckForUpdates", "False");

                // CPU Emulator (n64dd does not worked with dynamic recompiler)
                if (system == "n64dd" && (!SystemConfig.isOptSet("cpucore") || SystemConfig["cpucore"] == "2"))
                {
                    ini.WriteValue("Core", "R4300Emulator", "0");
                    ini.WriteValue("Rosalie's Mupen GUI Core Overlay", "CPU_Emulator", "0");
                }
                else if (SystemConfig.isOptSet("cpucore") && !string.IsNullOrEmpty(SystemConfig["cpucore"]))
                {
                    ini.WriteValue("Core", "R4300Emulator", SystemConfig["cpucore"]);
                    ini.WriteValue("Rosalie's Mupen GUI Core Overlay", "CPU_Emulator", SystemConfig["cpucore"]);
                }
                else
                {
                    ini.WriteValue("Core", "R4300Emulator", "2");
                    ini.WriteValue("Rosalie's Mupen GUI Core Overlay", "CPU_Emulator", "2");
                }

                // Discord                
                if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                    ini.WriteValue("Rosalie's Mupen GUI", "DiscordRpc", "True");
                else
                    ini.WriteValue("Rosalie's Mupen GUI", "DiscordRpc", "False");

                // N64DD bios paths
                string IPLJap = Path.Combine(AppConfig.GetFullPath("Bios"), "Mupen64plus", "IPL_JAP.n64");
                if (File.Exists(IPLJap))
                    ini.WriteValue("Rosalie's Mupen GUI Core 64DD", "64DD_JapaneseIPL", IPLJap.Replace("\\", "/"));

                string IPLUSA = Path.Combine(AppConfig.GetFullPath("Bios"), "Mupen64plus", "IPL_USA.n64");
                if (File.Exists(IPLUSA))
                    ini.WriteValue("Rosalie's Mupen GUI Core 64DD", "64DD_AmericanIPL", IPLUSA.Replace("\\", "/"));

                string IPLDev = Path.Combine(AppConfig.GetFullPath("Bios"), "Mupen64plus", "IPL_DEV.n64");
                if (File.Exists(IPLDev))
                    ini.WriteValue("Rosalie's Mupen GUI Core 64DD", "64DD_DevelopmentIPL", IPLDev.Replace("\\", "/"));

                // Parallel options in case GFX is parallel
                if (SystemConfig.isOptSet("gfxplugin") && SystemConfig["gfxplugin"] == "parallel")
                {
                    ini.WriteValue("Rosalie's Mupen GUI Core", "GFX_Plugin", "mupen64plus-video-parallel.dll");
                    ini.WriteValue("Rosalie's Mupen GUI Core", "RSP_Plugin", "mupen64plus-rsp-parallel.dll");

                    // Vsync
                    if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                        ini.WriteValue("Video-Parallel", "VSync", "0");
                    else
                        ini.WriteValue("Video-Parallel", "VSync", "1");

                    // Widescreen
                    if (SystemConfig.isOptSet("ratio") && (SystemConfig["ratio"] == "2" || SystemConfig["ratio"] == "0"))
                        ini.WriteValue("Video-Parallel", "WidescreenStretch", "True");
                    else
                        ini.WriteValue("Video-Parallel", "WidescreenStretch", "False");

                    // Upscaling
                    if (SystemConfig.isOptSet("parallel_upscaling") && !string.IsNullOrEmpty(SystemConfig["parallel_upscaling"]))
                        ini.WriteValue("Video-Parallel", "Upscaling", SystemConfig["parallel_upscaling"]);
                    else
                        ini.WriteValue("Video-Parallel", "Upscaling", "1");
                }

                else if (SystemConfig.isOptSet("gfxplugin") && SystemConfig["gfxplugin"] == "angrylion")
                {
                    ini.WriteValue("Rosalie's Mupen GUI Core", "GFX_Plugin", "mupen64plus-video-angrylion-plus.dll");

                    // Resolution
                    if (SystemConfig.isOptSet("resolution") && !string.IsNullOrEmpty(SystemConfig["resolution"]))
                    {
                        var res = SystemConfig["resolution"];

                        switch (res)
                        {
                            case "0":
                            case "1":
                                ini.WriteValue("Video-AngrylionPlus", "ScreenWidth", "640");
                                ini.WriteValue("Video-AngrylionPlus", "ScreenHeight", "480");
                                break;
                            case "2":
                                ini.WriteValue("Video-AngrylionPlus", "ScreenWidth", "1280");
                                ini.WriteValue("Video-AngrylionPlus", "ScreenHeight", "960");
                                break;
                            case "3":
                                ini.WriteValue("Video-AngrylionPlus", "ScreenWidth", "1440");
                                ini.WriteValue("Video-AngrylionPlus", "ScreenHeight", "1080");
                                break;
                            case "4":
                            case "5":
                            case "6":
                            case "7":
                            case "8":
                            case "9":
                            case "10":
                            case "11":
                            case "12":
                            case "13":
                            case "14":
                            case "15":
                            case "16":
                                ini.WriteValue("Video-AngrylionPlus", "ScreenWidth", "1600");
                                ini.WriteValue("Video-AngrylionPlus", "ScreenHeight", "1200");
                                break;
                            default:
                                ini.WriteValue("Video-AngrylionPlus", "ScreenWidth", "640");
                                ini.WriteValue("Video-AngrylionPlus", "ScreenHeight", "480");
                                break;
                        }
                    }
                    else
                    {
                        ini.WriteValue("Video-AngrylionPlus", "ScreenWidth", "640");
                        ini.WriteValue("Video-AngrylionPlus", "ScreenHeight", "480");
                    }

                    // Widescreen
                    if (SystemConfig.isOptSet("ratio") && (SystemConfig["ratio"] == "2" || SystemConfig["ratio"] == "0"))
                        ini.WriteValue("Video-AngrylionPlus", "ViWidescreen", "True");
                    else
                        ini.WriteValue("Video-AngrylionPlus", "ViWidescreen", "False");

                    // RSP Plugin
                    if (SystemConfig.isOptSet("rsp_plugin") && !string.IsNullOrEmpty(SystemConfig["rsp_plugin"]))
                        ini.WriteValue("Rosalie's Mupen GUI Core", "RSP_Plugin", SystemConfig["rsp_plugin"]);
                    else
                        ini.WriteValue("Rosalie's Mupen GUI Core", "RSP_Plugin", "mupen64plus-rsp-hle.dll");
                }
                
                else
                {
                    ini.WriteValue("Rosalie's Mupen GUI Core", "GFX_Plugin", "mupen64plus-video-GLideN64.dll");
                    
                    // RSP Plugin
                    if (SystemConfig.isOptSet("rsp_plugin") && !string.IsNullOrEmpty(SystemConfig["rsp_plugin"]))
                        ini.WriteValue("Rosalie's Mupen GUI Core", "RSP_Plugin", SystemConfig["rsp_plugin"]);
                    else
                        ini.WriteValue("Rosalie's Mupen GUI Core", "RSP_Plugin", "mupen64plus-rsp-hle.dll");
                }

                // Input plugin
                if (SystemConfig.isOptSet("inputplugin") && !string.IsNullOrEmpty(SystemConfig["inputplugin"]))
                    ini.WriteValue("Rosalie's Mupen GUI Core", "INPUT_Plugin", SystemConfig["inputplugin"]);
                else
                    ini.WriteValue("Rosalie's Mupen GUI Core", "INPUT_Plugin", "RMG-Input.dll");

                if (!SystemConfig.isOptSet("inputplugin") || SystemConfig["inputplugin"] == "RMG-Input.dll")
                    CreateControllerConfiguration(ini);
            }
        }

        private void SetupGFX(string path)
        {
            string gfxConf = Path.Combine(path, "Config", "GLideN64.ini");

            using (var ini = IniFile.FromFile(gfxConf, IniOptions.KeepEmptyValues))
            {
                // Vsync
                if (SystemConfig.isOptSet("vsync") && !SystemConfig.getOptBoolean("vsync"))
                    ini.WriteValue("User", "video\\verticalSync", "0");
                else
                    ini.WriteValue("User", "video\\verticalSync", "1");

                // Aspect Ratio
                if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                    ini.WriteValue("User", "frameBufferEmulation\\aspect", SystemConfig["ratio"]);
                else
                    ini.WriteValue("User", "frameBufferEmulation\\aspect", "1");

                // Anti-alisaing
                if (SystemConfig.isOptSet("antialiasing") && SystemConfig["antialiasing"] == "1")
                {
                    ini.WriteValue("User", "video\\multisampling", "0");
                    ini.WriteValue("User", "video\\fxaa", "1");
                }
                else if (SystemConfig.isOptSet("antialiasing") && SystemConfig["antialiasing"] != "0" && SystemConfig["antialiasing"] != "1")
                {
                    ini.WriteValue("User", "video\\multisampling", SystemConfig["antialiasing"]);
                    ini.WriteValue("User", "video\\fxaa", "0");
                }
                else
                {
                    ini.WriteValue("User", "video\\multisampling", "0");
                    ini.WriteValue("User", "video\\fxaa", "0");
                }

                // Anisotropic filtering
                if (SystemConfig.isOptSet("anisotropic_filtering") && !string.IsNullOrEmpty(SystemConfig["anisotropic_filtering"]))
                    ini.WriteValue("User", "texture\\anisotropy", SystemConfig["anisotropic_filtering"]);
                else
                    ini.WriteValue("User", "texture\\anisotropy", "0");

                // Resolution
                if (SystemConfig.isOptSet("resolution") && !string.IsNullOrEmpty(SystemConfig["resolution"]))
                    ini.WriteValue("User", "frameBufferEmulation\\nativeResFactor", SystemConfig["resolution"]);
                else
                    ini.WriteValue("User", "frameBufferEmulation\\nativeResFactor", "0");

                // Custom textures
                string texturePath = Path.Combine(AppConfig.GetFullPath("bios"), "Mupen64plus", "hires_texture");
                ini.WriteValue("User", "textureFilter\\txPath", texturePath.Replace("\\", "/"));

                if (SystemConfig.isOptSet("hires_textures") && SystemConfig.getOptBoolean("hires_textures"))
                    ini.WriteValue("User", "textureFilter\\txHiresEnable", "1");
                else
                    ini.WriteValue("User", "textureFilter\\txHiresEnable", "0");
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
        }
    }
}
