using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class DolphinGenerator : Generator
    {
        public DolphinGenerator()
        {
            DependsOnDesktopResolution = true;
        }

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

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _triforce = false;
        private Rectangle _windowRect = Rectangle.Empty;
        private bool _runWiiMenu = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            _triforce = (emulator == "dolphin-triforce" || core == "dolphin-triforce" || emulator == "triforce" || core == "triforce");

            string folderName = _triforce ? "dolphin-triforce" : "dolphin-emu";

            string path = AppConfig.GetFullPath(folderName);
            if (!_triforce && string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("dolphin");

            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "Dolphin.exe");
            if (!File.Exists(exe))
            {                
                exe = Path.Combine(path, "DolphinWX.exe");
                _triforce = File.Exists(exe);
            }

            if (!File.Exists(exe))
                return null;

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            if ((system == "gamecube" && SystemConfig["ratio"] == "") || SystemConfig["ratio"] == "4/3")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            if (system == "wii")
            {
                string sysconf = Path.Combine(AppConfig.GetFullPath("saves"), "dolphin", "User", "Wii", "shared2", "sys", "SYSCONF");
                if (File.Exists(sysconf))
                    WriteWiiSysconfFile(sysconf);
                else
                    SimpleLogger.Instance.Info("[WARNING] Wii Nand file not found in : " + sysconf);
            }
            
            _runWiiMenu = SystemConfig.getOptBoolean("dolphin_wiimenu");

            SetupGeneralConfig(path, system, emulator, core, rom);
            SetupGfxConfig(path);
            SetupStateSlotConfig(path);
            SetupCheevos(path);

            DolphinControllers.WriteControllersConfig(path, system, _triforce);

            if (Path.GetExtension(rom).ToLowerInvariant() == ".m3u")
                rom = rom.Replace("\\", "/");

            string[] extensions = new string[] { ".m3u", ".gcz", ".iso", ".ciso", ".wbfs", ".wad", ".rvz", ".wia" };
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

            string saveState = "";
            string runBatch = "-b ";
            if (SystemConfig.getOptBoolean("dolphin_gui"))
                runBatch = "";

            if (File.Exists(SystemConfig["state_file"]))
                saveState = " --save_state=\"" + Path.GetFullPath(SystemConfig["state_file"]) + "\"";

            if (_runWiiMenu)
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = runBatch + "-n 0000000100000002",
                    WorkingDirectory = path,
                };

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = runBatch + "-e \"" + rom + "\"" + saveState,
                WorkingDirectory = path,
                WindowStyle = (_bezelFileInfo == null ? ProcessWindowStyle.Normal : ProcessWindowStyle.Maximized)
            };
        }

        /// <summary>
        /// Works since to this PR : https://github.com/dolphin-emu/dolphin/pull/12201
        /// </summary>
        /// <param name="path"></param>
        private void SetupStateSlotConfig(string path)
        {
            if (_saveStatesWatcher == null)
                return;

            var slot = _saveStatesWatcher.Slot;

            int id = Math.Max(1, ((slot - 1) % 10) + 1);

            string iniFile = Path.Combine(path, "User", "Config", "QT.ini");
            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                ini.WriteValue("Emulation", "StateSlot", id.ToString());
        }

        private void SetupGfxConfig(string path)
        {
            string iniFile = Path.Combine(path, "User", "Config", "GFX.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    // Draw FPS
                    if (SystemConfig.isOptSet("dolphin_showfps") && SystemConfig["dolphin_showfps"] == "full")
                    {
                        ini.WriteValue("Settings", "ShowFTimes", "True");
                        ini.WriteValue("Settings", "ShowFPS", "True");
                        ini.WriteValue("Settings", "ShowGraphs", "True");
                        ini.WriteValue("Settings", "ShowSpeed", "True");
                    }
                    else if (SystemConfig.isOptSet("dolphin_showfps") && SystemConfig["dolphin_showfps"] == "fps_only")
                    {
                        ini.WriteValue("Settings", "ShowFTimes", "False");
                        ini.WriteValue("Settings", "ShowFPS", "True");
                        ini.WriteValue("Settings", "ShowGraphs", "False");
                        ini.WriteValue("Settings", "ShowSpeed", "False");
                    }
                    else
                    {
                        ini.WriteValue("Settings", "ShowFTimes", "False");
                        ini.WriteValue("Settings", "ShowFPS", "False");
                        ini.WriteValue("Settings", "ShowGraphs", "False");
                        ini.WriteValue("Settings", "ShowSpeed", "False");
                    }

                    // Fullscreen
                    if (_bezelFileInfo != null)
                        ini.WriteValue("Settings", "BorderlessFullscreen", "True");

                    // Ratio
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

                    // Anti-aliasing
                    if (SystemConfig.isOptSet("antialiasing"))
                        ini.WriteValue("Settings", "MSAA", SystemConfig["antialiasing"]);
                    else
                    {
                        ini.WriteValue("Settings", "MSAA", "0x00000001");
                        ini.WriteValue("Settings", "SSAA", "False");
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

                    BindBoolIniFeatureOn(ini, "Hacks", "XFBToTextureEnable", "dolphin_xfbtotexture", "True", "False");

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

                    // HiResTextures
                    BindBoolIniFeature(ini, "Settings", "HiresTextures", "hires_textures", "True", "False");
                    BindBoolIniFeature(ini, "Settings", "CacheHiresTextures", "CacheHiresTextures", "True", "False");
                    BindBoolIniFeature(ini, "Settings", "EnableMods", "dolphin_graphicsmods", "True", "False");

                    // Other settings
                    BindBoolIniFeatureOn(ini, "Hardware", "VSync", "dolphin_vsync", "True", "False");
                    BindIniFeatureSlider(ini, "Settings", "InternalResolution", "internal_resolution", "0");
                    BindIniFeature(ini, "Enhancements", "ForceTextureFiltering", "ForceFiltering", "0");
                    BindIniFeature(ini, "Enhancements", "PostProcessingShader", "dolphin_shaders", "(off)");
                    BindBoolIniFeature(ini, "Hacks", "VertexRounding", "VertexRounding", "True", "False");
                    BindBoolIniFeature(ini, "Hacks", "VISkip", "VISkip", "True", "False");
                    BindBoolIniFeature(ini, "Hacks", "FastTextureSampling", "manual_texture_sampling", "False", "True");
                    BindBoolIniFeature(ini, "Settings", "WaitForShadersBeforeStarting", "WaitForShadersBeforeStarting", "True", "False");
                    BindIniFeature(ini, "Settings", "ShaderCompilationMode", "ShaderCompilationMode", "2");
                    BindBoolIniFeature(ini, "Hacks", "EFBAccessEnable", "EFBAccessEnable", "False", "True");
                    BindBoolIniFeatureOn(ini, "Hacks", "EFBScaledCopy", "EFBScaledCopy", "True", "False");
                    BindBoolIniFeature(ini, "Hacks", "EFBEmulateFormatChanges", "EFBEmulateFormatChanges", "True", "False");
                    BindIniFeature(ini, "Enhancements", "MaxAnisotropy", "anisotropic_filtering", "0");
                    BindBoolIniFeature(ini, "Settings", "SSAA", "ssaa", "True", "False");
                    BindBoolIniFeature(ini, "Settings", "Crop", "dolphin_crop", "True", "False");
                    BindBoolIniFeature(ini, "Enhancements", "HDROutput", "enable_hdr", "True", "False");
                    BindIniFeature(ini, "Enhancements", "OutputResampling", "OutputResampling", "0");
                }
            }
            catch { }
        }

        private void SetupCheevos(string path)
        {
            string iniFile = Path.Combine(path, "User", "Config", "RetroAchievements.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    // Enable cheevos is needed
                    if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
                    {
                        ini.WriteValue("Achievements", "Enabled", "True");
                        ini.WriteValue("Achievements", "AchievementsEnabled", "True");
                        ini.WriteValue("Achievements", "EncoreEnabled", SystemConfig.getOptBoolean("retroachievements.encore") ? "True" : "False");
                        ini.WriteValue("Achievements", "HardcoreEnabled", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "True" : "False");
                        ini.WriteValue("Achievements", "LeaderboardsEnabled", SystemConfig.getOptBoolean("retroachievements.leaderboards") ? "True" : "False");
                        ini.WriteValue("Achievements", "DiscordPresenceEnabled", SystemConfig.getOptBoolean("retroachievements.richpresence") ? "True" : "False");
                        ini.WriteValue("Achievements", "UnofficialEnabled", "False");
                        ini.WriteValue("Achievements", "BadgesEnabled", "True");
                        ini.WriteValue("Achievements", "ProgressEnabled", SystemConfig.getOptBoolean("retroachievements.challenge_indicators") ? "True" : "False");

                        // Inject credentials
                        if (SystemConfig.isOptSet("retroachievements.username") && SystemConfig.isOptSet("retroachievements.token"))
                        {
                            ini.WriteValue("Achievements", "Username", SystemConfig["retroachievements.username"]);
                            ini.WriteValue("Achievements", "ApiToken", SystemConfig["retroachievements.token"]);
                        }
                    }
                    else
                    {
                        ini.WriteValue("Achievements", "Enabled", "False");
                        ini.WriteValue("Achievements", "AchievementsEnabled", "False");
                        ini.WriteValue("Achievements", "HardcoreEnabled", "False");
                        ini.WriteValue("Achievements", "BadgesEnabled", "False");
                        ini.WriteValue("Achievements", "ProgressEnabled", "False");
                    }
                }
            }
            catch { }
        }

        private string GetGameCubeLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, string>() 
            { 
                {"en", "0" }, { "de", "1" }, { "fr", "2" }, { "es", "3" }, { "it", "4" }, { "nl", "5" } 
            };

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }

            return "0";
        }

        private int GetWiiLangFromEnvironment()
        {
            var availableLanguages = new Dictionary<string, int>()
            {
                {"jp", 0 }, {"en", 1 }, { "de", 2 }, { "fr", 3 }, { "es", 4 }, { "it", 5 }, { "nl", 6 }
            };

            var lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out int ret))
                    return ret;
            }

            return 1;
        }

        private void WriteWiiSysconfFile(string path)
        {
            if (!File.Exists(path))
                return;

            SimpleLogger.Instance.Info("[INFO] Writing to wii system nand in : " + path);

            int langId;
            int barPos = 0;
            int ratio = 1;
            int progScan = 0;

            if (SystemConfig.isOptSet("wii_language") && !string.IsNullOrEmpty(SystemConfig["wii_language"]))
                langId = SystemConfig["wii_language"].ToInteger();
            else
                langId = GetWiiLangFromEnvironment();

            if (SystemConfig.isOptSet("sensorbar_position") && !string.IsNullOrEmpty(SystemConfig["sensorbar_position"]))
                barPos = SystemConfig["sensorbar_position"].ToInteger();

            if (SystemConfig.isOptSet("wii_tvmode") && !string.IsNullOrEmpty(SystemConfig["wii_tvmode"]))
                ratio = SystemConfig["wii_tvmode"].ToInteger();

            if (SystemConfig.isOptSet("wii_progscan") && SystemConfig.getOptBoolean("wii_progscan"))
                progScan = 1;

            // Read SYSCONF file
            byte[] bytes = File.ReadAllBytes(path);

            // Search IPL.LNG pattern and replace with target language
            byte[] langPattern = new byte[] { 0x49, 0x50, 0x4C, 0x2E, 0x4C, 0x4E, 0x47 };
            int index = bytes.IndexOf(langPattern);
            if (index >= 0 && index + langPattern.Length + 1 < bytes.Length)
            {
                var toSet = new byte[] { 0x49, 0x50, 0x4C, 0x2E, 0x4C, 0x4E, 0x47, (byte)langId };
                for (int i = 0; i < toSet.Length; i++)
                    bytes[index + i] = toSet[i];
            }
            SimpleLogger.Instance.Info("[INFO] Writing language " + langId.ToString() + " to wii system nand");

            // Search BT.BAR pattern and replace with target position
            byte[] barPositionPattern = new byte[] { 0x42, 0x54, 0x2E, 0x42, 0x41, 0x52 };
            int index2 = bytes.IndexOf(barPositionPattern);
            if (index2 >= 0 && index2 + barPositionPattern.Length + 1 < bytes.Length)
            {
                var toSet = new byte[] { 0x42, 0x54, 0x2E, 0x42, 0x41, 0x52, (byte)barPos };
                for (int i = 0; i < toSet.Length; i++)
                    bytes[index2 + i] = toSet[i];
            }
            SimpleLogger.Instance.Info("[INFO] Writing sensor bar position " + barPos.ToString() + " to wii system nand");
            
            // Search IPL.AR pattern and replace with target position
            byte[] ratioPositionPattern = new byte[] { 0x49, 0x50, 0x4C, 0x2E, 0x41, 0x52 };
            int index3 = bytes.IndexOf(ratioPositionPattern);
            if (index3 >= 0 && index3 + ratioPositionPattern.Length + 1 < bytes.Length)
            {
                var toSet = new byte[] { 0x49, 0x50, 0x4C, 0x2E, 0x41, 0x52, (byte)ratio };
                for (int i = 0; i < toSet.Length; i++)
                    bytes[index3 + i] = toSet[i];
            }
            SimpleLogger.Instance.Info("[INFO] Writing wii screen ratio " + ratio.ToString() + " to wii system nand");

            // Search IPL.PGS pattern and replace with target position
            byte[] pgsPositionPattern = new byte[] { 0x49, 0x50, 0x4C, 0x2E, 0x50, 0x47, 0x53 };
            int index4 = bytes.IndexOf(pgsPositionPattern);
            if (index4 >= 0 && index4 + pgsPositionPattern.Length + 1 < bytes.Length)
            {
                var toSet = new byte[] { 0x49, 0x50, 0x4C, 0x2E, 0x50, 0x47, 0x53, (byte)progScan };
                for (int i = 0; i < toSet.Length; i++)
                    bytes[index4 + i] = toSet[i];
            }
            SimpleLogger.Instance.Info("[INFO] Writing wii Progressive Scan " + progScan.ToString() + " to wii system nand");

            File.WriteAllBytes(path, bytes);
        }

        private void SetupGeneralConfig(string path, string system, string emulator, string core, string rom)
        {
            string iniFile = Path.Combine(path, "User", "Config", "Dolphin.ini");
            
            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues))
                {
                    if (IsEmulationStationWindowed(out Rectangle emulationStationBounds, true) && !SystemConfig.getOptBoolean("forcefullscreen"))
                    {
                        _windowRect = emulationStationBounds;
                        _bezelFileInfo = null;
                        ini.WriteValue("Display", "Fullscreen", "False");
                    }
                    else
                        ini.WriteValue("Display", "Fullscreen", "True");

                    // Discord
                    BindBoolIniFeature(ini, "General", "UseDiscordPresence", "discord", "True", "False");

                    // Skip BIOS
                    BindBoolIniFeatureOn(ini, "Core", "SkipIPL", "skip_bios", "True", "False");

                    // OSD Messages
                    BindBoolIniFeatureOn(ini, "Interface", "OnScreenDisplayMessages", "OnScreenDisplayMessages", "True", "False");

                    // don't ask about statistics
                    ini.WriteValue("Analytics", "PermissionAsked", "True");
                    ini.WriteValue("Analytics", "Enabled", "False");

                    // don't confirm at stop
                    ini.WriteValue("Interface", "ConfirmStop", "False");

                    ini.WriteValue("Display", "KeepWindowOnTop", "False");

                    // language (for gamecube at least)
                    if (Features.IsSupported("gamecube_language") && SystemConfig.isOptSet("gamecube_language"))
                        ini.WriteValue("Core", "SelectedLanguage", SystemConfig["gamecube_language"]);
                    else
                        ini.WriteValue("Core", "SelectedLanguage", GetGameCubeLangFromEnvironment());

                    // Audio
                    if (SystemConfig.isOptSet("enable_dpl2") && SystemConfig.getOptBoolean("enable_dpl2"))
                    {
                        ini.WriteValue("Core", "DSPHLE", "False");
                        ini.WriteValue("Core", "DPL2Decoder", "True");
                        ini.WriteValue("DSP", "EnableJIT", "True");
                    }
                    else
                    {
                        ini.WriteValue("Core", "DSPHLE", "True");
                        ini.WriteValue("Core", "DPL2Decoder", "False");
                        ini.WriteValue("DSP", "EnableJIT", "False");
                    }

                    BindIniFeature(ini, "DSP", "Backend", "dolphin_audiobackend", "Cubeb");

                    // Video backend - Default
                    BindIniFeature(ini, "Core", "GFXBackend", "dolphin_gfxbackend", "Vulkan");

                    // Cheats - default false
                    if (!_triforce)
                        BindBoolIniFeature(ini, "Core", "EnableCheats", "enable_cheats", "True", "False");

                    // Fast Disc Speed - Default Off
                    BindBoolIniFeature(ini, "Core", "FastDiscSpeed", "enable_fastdisc", "True", "False");

                    // Enable MMU - Default On
                    BindBoolIniFeature(ini, "Core", "MMU", "enable_mmu", "True", "False");

                    // CPU Thread (Dual Core)
                    BindBoolIniFeatureOn(ini, "Core", "CPUThread", "dolphin_cputhread", "True", "False");

                    // gamecube pads forced as standard pad
                    bool emulatedWiiMote = (system == "wii" && Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig.getOptBoolean("emulatedwiimotes"));
                    bool realWiimoteAsEmulated = (system == "wii" && Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig["emulatedwiimotes"] != "0" && Program.SystemConfig["emulatedwiimotes"] != "1");
                    
                    // wiimote scanning
                    if (emulatedWiiMote || system == "gamecube" || _triforce)
                        ini.WriteValue("Core", "WiimoteContinuousScanning", "False");
                    else
                        ini.WriteValue("Core", "WiimoteContinuousScanning", "True");

                    // Real wiimote as emulated
                    if (realWiimoteAsEmulated)
                        ini.WriteValue("Core", "WiimoteControllerInterface", "True");
                    else
                        ini.WriteValue("Core", "WiimoteControllerInterface", "False");

                    // Write texture paths
                    if (!_triforce)
                    {
                        string savesPath = AppConfig.GetFullPath("saves");
                        string dolphinLoadPath = Path.Combine(savesPath, "dolphin", "User", "Load");
                        if (!Directory.Exists(dolphinLoadPath)) try { Directory.CreateDirectory(dolphinLoadPath); }
                            catch { }
                        string dolphinResourcesPath = Path.Combine(savesPath, "dolphin", "User", "ResourcePacks");
                        if (!Directory.Exists(dolphinResourcesPath)) try { Directory.CreateDirectory(dolphinResourcesPath); }
                            catch { }

                        ini.WriteValue("General", "LoadPath", dolphinLoadPath);
                        ini.WriteValue("General", "ResourcePackPath", dolphinResourcesPath);

                        if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
                        {
                            string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);

                            _saveStatesWatcher = new DolphinSaveStatesMonitor(rom, Path.Combine(path, "User", "StateSaves"), localPath);
                            _saveStatesWatcher.PrepareEmulatorRepository();
                        }
                        // DumpPath
                        string dumpPath = Path.Combine(savesPath, "dolphin", "User", "Dump");
                        if (!Directory.Exists(dumpPath)) try { Directory.CreateDirectory(dumpPath); }
                            catch { }
                        ini.WriteValue("General", "DumpPath", dumpPath);

                        // WFSPath
                        string wfsPath = Path.Combine(savesPath, "dolphin", "User", "WFS");
                        if (!Directory.Exists(wfsPath)) try { Directory.CreateDirectory(wfsPath); }
                            catch { }
                        ini.WriteValue("General", "WFSPath", wfsPath);

                        // Wii NAND path
                        string wiiNandPath = Path.Combine(savesPath, "dolphin", "User", "Wii");
                        if (!Directory.Exists(wiiNandPath)) try { Directory.CreateDirectory(wiiNandPath); }
                            catch { }
                        ini.WriteValue("General", "NANDRootPath", wiiNandPath);

                        // Gamecube saves
                        string gc_region = "EUR";
                        if (SystemConfig.isOptSet("dolphin_gcregion") && !string.IsNullOrEmpty(SystemConfig["dolphin_gcregion"]))
                            gc_region = SystemConfig["dolphin_gcregion"];

                        ini.WriteValue("Core", "SlotA", SystemConfig["dolphin_slotA"] == "1" ? "1" : "8");

                        string gcSavePath = Path.Combine(savesPath, "dolphin", "User", "GC", gc_region);
                        if (!Directory.Exists(gcSavePath)) try { Directory.CreateDirectory(gcSavePath); }
                            catch { }
                        string sramFile = Path.Combine(savesPath, "dolphin", "User", "GC", "SRAM." + gc_region + ".raw");

                        ini.WriteValue("Core", "GCIFolderAPath", gcSavePath);
                        ini.WriteValue("Core", "MemcardAPath", sramFile);
                    }

                    // Add rom path to isopath
                    AddPathToIsoPath(Path.GetFullPath(Path.GetDirectoryName(rom)), ini);

                    // Triforce specifics (AM-baseboard in SID devices, panic handlers)
                    if (_triforce)
                    {
                        ini.WriteValue("Core", "SerialPort1", "6");                     // AM Baseboard
                        ini.WriteValue("Core", "SIDevice0", "11");                      // AM Baseboard player 1
                        ini.WriteValue("Core", "SIDevice1", "11");                      // AM Baseboard player 2
                        ini.WriteValue("Core", "SIDevice2", "0");
                        ini.WriteValue("Core", "SIDevice3", "0");
                        ini.WriteValue("Interface", "UsePanicHandlers", "False");       // Disable panic handlers
                        ini.WriteValue("Core", "EnableCheats", "True");                 // Cheats must be enabled
                    }

                    // Set SID devices (controllers)
                    else if (!((Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")))
                    {
                        bool wiiGCPad = system == "wii" && SystemConfig.isOptSet("wii_gamecube") && SystemConfig.getOptBoolean("wii_gamecube");
                        for (int i = 0; i < 4; i++)
                        {
                            var ctl = Controllers.FirstOrDefault(c => c.PlayerIndex == i + 1);
                            bool gcPad = (system == "gamecube" && SystemConfig.isOptSet("gamecubepad" + i) && SystemConfig.getOptBoolean("gamecubepad" + i));

                            if (wiiGCPad || gcPad)
                                ini.WriteValue("Core", "SIDevice" + i, "12");

                            else if (ctl != null && ctl.Config != null)
                                ini.WriteValue("Core", "SIDevice" + i, "6");
                            
                            else
                                ini.WriteValue("Core", "SIDevice" + i, "0");
                        }
                    }

                    // Disable auto updates
                    string updateTrack = ini.GetValue("AutoUpdate", "UpdateTrack");
                    if (updateTrack != "")
                        ini.WriteValue("AutoUpdate", "UpdateTrack", "''");

                    // Set defaultISO when running the Wii Menu
                    if (_runWiiMenu)
                        ini.WriteValue("Core", "DefaultISO", rom);
                    else
                        ini.WriteValue("Core", "defaultISO", "\"\"");
                }
            }
            catch { }
        }

        private static void AddPathToIsoPath(string romPath, IniFile ini)
        {
            int isoPathsCount = (ini.GetValue("General", "ISOPaths") ?? "0").ToInteger();
            for (int i = 0; i < isoPathsCount; i++)
            {
                var isoPath = ini.GetValue("General", "ISOPath" + i);
                if (isoPath != null && Path.GetFullPath(isoPath).Equals(romPath, StringComparison.InvariantCultureIgnoreCase))
                    return;
            }

            ini.WriteValue("General", "ISOPaths", (isoPathsCount + 1).ToString());
            ini.WriteValue("General", "ISOPath" + isoPathsCount, romPath);
        }

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

            bezel?.Dispose();

            return ret;
        }
    }
}
