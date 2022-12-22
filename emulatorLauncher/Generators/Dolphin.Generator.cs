using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Drawing;

namespace emulatorLauncher
{
    class DolphinGenerator : Generator
    {
        public DolphinGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _triforce = false;
        private Rectangle _windowRect = Rectangle.Empty;

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = 0;

            if (_windowRect.IsEmpty)
                ret = base.RunAndWait(path);
            else
            {
                var process = Process.Start(path);

                while (process != null)
                {
                    try
                    {
                        var hWnd = process.MainWindowHandle;
                        if (hWnd != IntPtr.Zero)
                        {
                            User32.SetWindowPos(hWnd, IntPtr.Zero, _windowRect.Left, _windowRect.Top, _windowRect.Width, _windowRect.Height, SWP.NOZORDER);
                            break;
                        }
                    }
                    catch { }
                    
                    if (process.WaitForExit(1))
                    {
                        try { ret = process.ExitCode; }
                        catch { }
                        process = null;
                        break;
                    }

                }

                if (process != null)
                {
                    process.WaitForExit();
                    try { ret = process.ExitCode; }
                    catch { }
                }
            }

            if (bezel != null)
                bezel.Dispose();

            return ret;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "dolphin-triforce" || core == "dolphin-triforce" || emulator == "triforce" || core == "triforce") ? "dolphin-triforce" : "dolphin-emu";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("dolphin");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("dolphin-emu");

            string exe = Path.Combine(path, "Dolphin.exe");
            if (!File.Exists(exe))
            {
                _triforce = true;
                exe = Path.Combine(path, "DolphinWX.exe");
            }

            if (!File.Exists(exe))
                return null;

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            if ((system == "gamecube" && SystemConfig["ratio"] == "") || SystemConfig["ratio"] == "4/3")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            SetupGeneralConfig(path, system);
            SetupGfxConfig(path);

            DolphinControllers.WriteControllersConfig(path, system, rom);

            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
                rom = rom.Replace("\\", "/");

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-b -e \"" + rom + "\"",
                WorkingDirectory = path,
                WindowStyle = (_bezelFileInfo == null ? ProcessWindowStyle.Normal : ProcessWindowStyle.Maximized)
            };
        }

        private void SetupGfxConfig(string path)
        {
            string iniFile = Path.Combine(path, "User", "Config", "GFX.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
					
					if (SystemConfig.isOptSet("ratio"))
					{
						if (SystemConfig["ratio"] == "4/3")
						{
							ini.WriteValue("Settings", "AspectRatio", "2");
						}
						else if (SystemConfig["ratio"] == "16/9")
							ini.WriteValue("Settings", "AspectRatio", "1");
						else if (SystemConfig["ratio"] == "Stretched")
							ini.WriteValue("Settings", "AspectRatio", "3");
					}
					else
						ini.WriteValue("Settings", "AspectRatio", "0");
					
					// widescreen hack but only if enable cheats is not enabled - Default Off
					if (SystemConfig.isOptSet("widescreen_hack") && SystemConfig.getOptBoolean("widescreen_hack"))
					{
						ini.WriteValue("Settings", "wideScreenHack", "True");

                        // Set Stretched only if ratio is not forced to 16/9 
                        if (!SystemConfig.isOptSet("ratio") || SystemConfig["ratio"] != "16/9")
                        {
                            _bezelFileInfo = null;
                            ini.WriteValue("Settings", "AspectRatio", "3");
                        }
					}
                    else
                        ini.Remove("Settings", "wideScreenHack");

                    // draw or not FPS
                    if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                        ini.WriteValue("Settings", "ShowFPS", "True");
                    else
                        ini.WriteValue("Settings", "ShowFPS", "False");

                    if (_bezelFileInfo != null)
                        ini.WriteValue("Settings", "BorderlessFullscreen", "True");
                    else 
                        ini.WriteValue("Settings", "BorderlessFullscreen", "False");

                    ini.WriteValue("Hardware", "VSync", SystemConfig["VSync"] != "false" ? "True" : "False");

                    // internal resolution
                    if (SystemConfig.isOptSet("internalresolution") && !string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                        ini.WriteValue("Settings", "InternalResolution", SystemConfig["internalresolution"]);
                    else if (SystemConfig.isOptSet("internal_resolution") && !string.IsNullOrEmpty(SystemConfig["internal_resolution"]))
                        ini.WriteValue("Settings", "InternalResolution", SystemConfig["internal_resolution"]);
                    else
                        ini.WriteValue("Settings", "InternalResolution", "0");

                    // HiResTextures
                    if (SystemConfig.isOptSet("hires_textures") && SystemConfig.getOptBoolean("hires_textures"))
                        ini.WriteValue("Settings", "HiresTextures", "True");
                    else
                        ini.WriteValue("Settings", "HiresTextures", "False");

                    if (SystemConfig.isOptSet("CacheHiresTextures") && SystemConfig.getOptBoolean("CacheHiresTextures"))
                        ini.WriteValue("Settings", "CacheHiresTextures", "True");
                    else
                        ini.WriteValue("Settings", "CacheHiresTextures", "False");

                    // anisotropic filtering - Auto 0
                    if (SystemConfig.isOptSet("anisotropic_filtering"))                    
                        ini.WriteValue("Enhancements", "MaxAnisotropy", SystemConfig["anisotropic_filtering"]);
                    else
                        ini.WriteValue("Enhancements", "MaxAnisotropy", "0");

                    // antialiasing (new dolhpin version adds SSAA)
                    if (SystemConfig.isOptSet("ssaa") && SystemConfig.getOptBoolean("ssaa"))
                        ini.WriteValue("Settings", "SSAA", SystemConfig["ssaa"]);
                    else
                        ini.WriteValue("Settings", "SSAA", "false");
                    
                    if (SystemConfig.isOptSet("antialiasing"))
                        ini.WriteValue("Settings", "MSAA", "0x0000000" + SystemConfig["antialiasing"]);
                    else
                    {
                        ini.WriteValue("Settings", "MSAA", "0x00000001");
                        ini.WriteValue("Settings", "SSAA", "false");
                    }

                    // various performance hacks - Default Off
                    if (SystemConfig.isOptSet("perf_hacks"))
                    {
                        if (SystemConfig.getOptBoolean("perf_hacks"))
                        {
                            ini.WriteValue("Hacks", "BBoxEnable", "False");
                            ini.WriteValue("Hacks", "SkipDuplicateXFBs", "True");
                            ini.WriteValue("Hacks", "XFBToTextureEnable", "True");
                            ini.WriteValue("Enhancements", "ArbitraryMipmapDetection", "True");
                            ini.WriteValue("Enhancements", "DisableCopyFilter", "True");
                            ini.WriteValue("Enhancements", "ForceTrueColor", "True");
                        }
                        else
                        {
                            ini.Remove("Hacks", "BBoxEnable");
                            ini.Remove("Hacks", "SkipDuplicateXFBs");
                            ini.Remove("Hacks", "XFBToTextureEnable");
                            ini.Remove("Enhancements", "ArbitraryMipmapDetection");
                            ini.Remove("Enhancements", "DisableCopyFilter");
                            ini.Remove("Enhancements", "ForceTrueColor");
                        }
                    }

                    // shaders compilation
                    if (Features.IsSupported("WaitForShadersBeforeStarting"))
                    {
                        if (SystemConfig.isOptSet("WaitForShadersBeforeStarting"))
                            ini.WriteValue("Settings", "WaitForShadersBeforeStarting", SystemConfig["WaitForShadersBeforeStarting"]);
                        else
                            ini.WriteValue("Settings", "WaitForShadersBeforeStarting", "False");
                    }

                    if (Features.IsSupported("ShaderCompilationMode"))
                    {
                        if (SystemConfig.isOptSet("ShaderCompilationMode"))
                            ini.WriteValue("Settings", "ShaderCompilationMode", SystemConfig["ShaderCompilationMode"]);
                        else
                            ini.WriteValue("Settings", "ShaderCompilationMode", "2");
                    }

                    // Skip EFB Access
                    if (Features.IsSupported("EFBAccessEnable"))
                    {
                        if (SystemConfig.isOptSet("EFBAccessEnable"))
                            ini.WriteValue("Hacks", "EFBAccessEnable", SystemConfig["EFBAccessEnable"]);
                        else
                            ini.WriteValue("Hacks", "EFBAccessEnable", "False");
                    }

                    // Scaled EFB copy
                    if (Features.IsSupported("EFBScaledCopy"))
                    {
                        if (SystemConfig.isOptSet("EFBScaledCopy"))
                            ini.WriteValue("Hacks", "EFBScaledCopy", SystemConfig["EFBScaledCopy"]);
                        else
                            ini.WriteValue("Hacks", "EFBScaledCopy", "True");
                    }

                    // EFB emulate format
                    if (Features.IsSupported("EFBEmulateFormatChanges"))
                    {
                        if (SystemConfig.isOptSet("EFBEmulateFormatChanges"))
                            ini.WriteValue("Hacks", "EFBEmulateFormatChanges", SystemConfig["EFBEmulateFormatChanges"]);
                        else
                            ini.WriteValue("Hacks", "EFBEmulateFormatChanges", "True");
                    }

                    // Store EFB Copies
                    if (Features.IsSupported("EFBCopies"))
                    {
                        if (SystemConfig["EFBCopies"] == "efb_to_texture_defer")
                        {
                            ini.WriteValue("Hacks", "EFBToTextureEnable", "True");
                            ini.WriteValue("Hacks", "DeferEFBCopies", "True");
                        }
                        else if (SystemConfig["EFBCopies"] == "efb_to_ram_defer")
                        {
                            ini.WriteValue("Hacks", "EFBToTextureEnable", "False");
                            ini.WriteValue("Hacks", "DeferEFBCopies", "True");
                        }
                        else if (SystemConfig["EFBCopies"] == "efb_to_ram")
                        {
                            ini.WriteValue("Hacks", "EFBToTextureEnable", "False");
                            ini.WriteValue("Hacks", "DeferEFBCopies", "False");
                        }
                        else
                        {
                            ini.Remove("Hacks", "EFBToTextureEnable");
                            ini.Remove("Hacks", "DeferEFBCopies");
                        }
                    }

                    // Force texture filtering
                    if (Features.IsSupported("ForceFiltering"))
                    {
                        if (SystemConfig.isOptSet("ForceFiltering"))
                            ini.WriteValue("Enhancements", "ForceFiltering", SystemConfig["ForceFiltering"]);
                        else
                            ini.WriteValue("Enhancements", "ForceFiltering", "False");
                    }

                }
            }
            catch { }
        }
    
        private string getGameCubeLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, string>() 
            { 
                {"en", "0" }, { "de", "1" }, { "fr", "2" }, { "es", "3" }, { "it", "4" }, { "nl", "5" } 
            };

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }

            return "0";
        }

        private void SetupGeneralConfig(string path, string system)
        {
            string iniFile = Path.Combine(path, "User", "Config", "Dolphin.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    Rectangle emulationStationBounds;
                    if (IsEmulationStationWindowed(out emulationStationBounds, true))
                    {
                        _windowRect = emulationStationBounds;
                        _bezelFileInfo = null;
                        ini.WriteValue("Display", "Fullscreen", "False");
                    }
                    else
                        ini.WriteValue("Display", "Fullscreen", "True");

                    // draw or not FPS
                    /*if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                    {
                        ini.WriteValue("General", "ShowLag", "True");
                        ini.WriteValue("General", "ShowFrameCount", "True");
                    }
                    else
                    {
                        ini.WriteValue("General", "ShowLag", "False");
                        ini.WriteValue("General", "ShowFrameCount", "False");
                    }
                    */

                    // Skip BIOS
                    if (SystemConfig.isOptSet("skip_bios") && !SystemConfig.getOptBoolean("skip_bios"))
                        ini.WriteValue("Core", "SkipIPL", "False");
                    else
                        ini.WriteValue("Core", "SkipIPL", "True");

                    // OSD Messages
                    if (SystemConfig.isOptSet("OnScreenDisplayMessages") && SystemConfig.getOptBoolean("OnScreenDisplayMessages"))
                        ini.WriteValue("Interface", "OnScreenDisplayMessages", "True");
                    else
                        ini.WriteValue("Interface", "OnScreenDisplayMessages", "False");

                    // don't ask about statistics
                    ini.WriteValue("Analytics", "PermissionAsked", "True");

                    // don't confirm at stop
                    ini.WriteValue("Interface", "ConfirmStop", "False");

                    ini.WriteValue("Display", "KeepWindowOnTop", "False");

                    // language (for gamecube at least)
                    if (Features.IsSupported("gamecube_language") && SystemConfig.isOptSet("gamecube_language"))
                    {
                        ini.WriteValue("Core", "SelectedLanguage", SystemConfig["gamecube_language"]);
                        ini.WriteValue("Core", "GameCubeLanguage", SystemConfig["gamecube_language"]);
                    }
                    else
                    {
                        ini.WriteValue("Core", "SelectedLanguage", getGameCubeLangFromEnvironment());
                        ini.WriteValue("Core", "GameCubeLanguage", getGameCubeLangFromEnvironment());
                    }

                    // backend - Default
                    if (SystemConfig.isOptSet("gfxbackend"))
                        ini.WriteValue("Core", "GFXBackend", SystemConfig["gfxbackend"]);
                    else
                        ini.WriteValue("Core", "GFXBackend", "Vulkan");

                    // Cheats - default false
                    if (SystemConfig.isOptSet("enable_cheats"))
                    {
                        if (SystemConfig.getOptBoolean("enable_cheats"))
                            ini.WriteValue("Core", "EnableCheats", "True");
                        else
                            ini.WriteValue("Core", "EnableCheats", "False");
                    }

                    // Fast Disc Speed - Default Off
                    if (SystemConfig.isOptSet("enable_fastdisc"))
                    {
                        if (SystemConfig.getOptBoolean("enable_fastdisc"))
                            ini.WriteValue("Core", "FastDiscSpeed", "True");
                        else
                            ini.WriteValue("Core", "FastDiscSpeed", "False");
                    }

                    // Enable MMU - Default On
                    if (SystemConfig.isOptSet("enable_mmu") && SystemConfig.getOptBoolean("enable_mmu"))
                        ini.WriteValue("Core", "MMU", "True");
                    else
                        ini.WriteValue("Core", "MMU", "False");

                    // CPU Thread (Dual Core)
                    if (SystemConfig.isOptSet("CPUThread") && SystemConfig.getOptBoolean("CPUThread"))
                        ini.WriteValue("Core", "CPUThread", "True");
                    else
                        ini.WriteValue("Core", "CPUThread", "False");

                    // gamecube pads forced as standard pad
                    bool emulatedWiiMote = (system == "wii" && Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig.getOptBoolean("emulatedwiimotes"));
                    
                    // wiimote scanning
                    if (emulatedWiiMote || system == "gamecube")
                        ini.WriteValue("Core", "WiimoteContinuousScanning", "False");
                    else
                        ini.WriteValue("Core", "WiimoteContinuousScanning", "True");

                    if (_triforce)
                    {
                        ini.WriteValue("Core", "SerialPort1", "6");                        
                        ini.WriteValue("Core", "SIDevice0", "11");
                        ini.WriteValue("Core", "SIDevice1", "0");
                        ini.WriteValue("Core", "SIDevice2", "0");
                        ini.WriteValue("Core", "SIDevice3", "0");
                    }
                    else if (!((Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var ctl = Controllers.FirstOrDefault(c => c.PlayerIndex == i + 1);

                            if (ctl != null && ctl.Config != null && !emulatedWiiMote)
                            {
                                /*if (ctl.Input.Type == "keyboard")
                                    ini.WriteValue("Core", "SIDevice" + i, "7");
                                else*/
                                ini.WriteValue("Core", "SIDevice" + i, "6");
                            }
                            else
                                ini.WriteValue("Core", "SIDevice" + i, "0");

                            if ((Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig["emulatedwiimotes"] == "2"))
                                ini.WriteValue("Core", "SIDevice" + i, "12");

                        }
                    }

                    // disable auto updates
                    ini.WriteValue("AutoUpdate", "UpdateTrack", " ");
                }
            }

            catch { }
        }
    }
}
