using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;

namespace EmulatorLauncher
{
    partial class DuckstationGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private SaveStatesWatcher _saveStatesWatcher;

        public DuckstationGenerator()
        {
            DependsOnDesktopResolution = true;
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

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("duckstation");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "duckstation-qt-x64-ReleaseLTCG.exe");
            if (!File.Exists(exe))
                return null;

            _resolution = resolution;

            string[] extensions = new string[] { ".m3u", ".chd", ".cue", ".img", ".pbp", ".iso", ".cso" };

            if (Path.GetExtension(rom).ToLower() == ".zip" || Path.GetExtension(rom).ToLower() == ".7z" || Path.GetExtension(rom).ToLower() == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    string[] romFiles = Directory.GetFiles(uncompressedRomPath, "*.*", SearchOption.AllDirectories).OrderBy(file => Array.IndexOf(extensions, Path.GetExtension(file).ToLowerInvariant())).ToArray();
                    rom = romFiles.FirstOrDefault(file => extensions.Any(ext => Path.GetExtension(file).Equals(ext, StringComparison.OrdinalIgnoreCase)));
                    ValidateUncompressedGame();
                }
            }

            SetupSettings(path, rom, system);

            //Applying bezels
            string renderer = "OpenGL";
            if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                renderer = SystemConfig["gfxbackend"];
            
            switch (renderer)
            {
                case "OpenGL":
                    ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, path);
                    if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                        _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                    break;
                case "Vulkan":
                case "Software":
                    ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, path);
                    ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path);
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                    break;
                case "D3D11":
                case "D3D12":
                    ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path);
                    if (!ReshadeManager.Setup(ReshadeBezelType.dxgi, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                        _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                    break;
            }

            _resolution = resolution;

            //setting up command line parameters
            var commandArray = new List<string>();

            if (SystemConfig.isOptSet("fullboot") && !SystemConfig.getOptBoolean("fullboot"))
                commandArray.Add("-slowboot");
            else
                commandArray.Add("-fastboot");

            commandArray.Add("-batch");
            commandArray.Add("-portable");

            if ((!SystemConfig.getOptBoolean("disable_fullscreen") && !IsEmulationStationWindowed()) || SystemConfig.getOptBoolean("forcefullscreen"))
                commandArray.Add("-fullscreen");

            if (File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("-statefile");
                commandArray.Add("\"" + Path.GetFullPath(SystemConfig["state_file"]) + "\"");
            }

            commandArray.Add("--");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };

        }

        private string GetDefaultpsxLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "jp", "ja" },
                { "en", "en" },
                { "fr", "fr" },
                { "de", "de" },
                { "it", "it" },
                { "es", "es-es" },
                { "zh", "zh-cn" },
                { "nl", "nl" },
                { "pl", "pl" },
                { "pt", "pt-pt" },
                { "ru", "ru" },
            };

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }
            return "en";
        }

        private void SetupSettings(string path, string rom, string system)
        {
            if (SystemConfig.getOptBoolean("disableautoconfig"))
                return;

            string iniFile = Path.Combine(path, "settings.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    string biosPath = AppConfig.GetFullPath("bios");
                    if (!string.IsNullOrEmpty(biosPath))
                    {
                        ini.WriteValue("BIOS", "SearchDirectory", biosPath);

                        if (SystemConfig.isOptSet("duck_bios") && !string.IsNullOrEmpty(SystemConfig["duck_bios"]))
                        {
                            ini.WriteValue("BIOS", "PathNTSCJ", SystemConfig["duck_bios"]);
                            ini.WriteValue("BIOS", "PathNTSCU", SystemConfig["duck_bios"]);
                            ini.WriteValue("BIOS", "PathPAL", SystemConfig["duck_bios"]);
                        }
                        else
                        {
                            ini.WriteValue("BIOS", "PathNTSCJ", null);
                            ini.WriteValue("BIOS", "PathNTSCU", null);
                            ini.WriteValue("BIOS", "PathPAL", null);
                        }
                    }

                    BindBoolIniFeatureOn(ini, "BIOS", "PatchFastBoot", "fullboot", "false", "true");

                    if (SystemConfig.isOptSet("duckstation_memcardtype") && !string.IsNullOrEmpty(SystemConfig["duckstation_memcardtype"]))
                    {
                        ini.WriteValue("MemoryCards", "Card1Type", SystemConfig["duckstation_memcardtype"]);
                        ini.WriteValue("MemoryCards", "Card2Type", SystemConfig["duckstation_memcardtype"]);
                    }
                    else
                    {
                        ini.WriteValue("MemoryCards", "Card1Type", "PerGameTitle");
                        ini.WriteValue("MemoryCards", "Card2Type", "PerGameTitle");
                    }

                    string memCardsPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "duckstation", "memcards");
                    if (!string.IsNullOrEmpty(memCardsPath))
                        ini.WriteValue("MemoryCards", "Directory", memCardsPath);

                    if (SystemConfig["duckstation_memcardtype"] == "Shared")
                    {
                        ini.WriteValue("MemoryCards", "Card1Path", "shared_card_1.mcd");
                        ini.WriteValue("MemoryCards", "Card2Path", "shared_card_2.mcd");
                    }

                    ini.WriteValue("MemoryCards", "UsePlaylistTitle", "true");

                    // SaveStates
                    bool newSaveStates = Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported("duckstation");
                    
                    string savesPath = newSaveStates ?
                        Program.EsSaveStates.GetSavePath(system, "duckstation", "duckstation") :
                        Path.Combine(AppConfig.GetFullPath("saves"), "psx", "duckstation", "sstates");

                    FileTools.TryCreateDirectory(savesPath);

                    if (!string.IsNullOrEmpty(savesPath))
                    {
                        if (newSaveStates)
                        {
                            // Keep the original folder, we'll listen to it, and inject in our custom folder
                            ini.WriteValue("Folders", "SaveStates", "savestates");

                            _saveStatesWatcher = new DuckStationSaveStatesMonitor(rom, Path.Combine(path, "savestates"), savesPath);
                            _saveStatesWatcher.PrepareEmulatorRepository();
                        }
                        else
                            ini.WriteValue("Folders", "SaveStates", savesPath);
                    }

                    // autosave
                    if (_saveStatesWatcher != null)
                        ini.WriteValue("Main", "SaveStateOnExit", _saveStatesWatcher.IsLaunchingAutoSave() || SystemConfig.getOptBoolean("autosave") ? "true" : "false");
                    else
                        ini.WriteValue("Main", "SaveStateOnExit", "false");

                    string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "duckstation");
                    if (!string.IsNullOrEmpty(cheatsPath))
                        ini.WriteValue("Folders", "Cheats", cheatsPath);

                    string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "duckstation");
                    if (!string.IsNullOrEmpty(screenshotsPath))
                        ini.WriteValue("Folders", "Screenshots", screenshotsPath);

                    //Enable cheevos is needed
                    if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
                    {
                        ini.WriteValue("Cheevos", "Enabled", "true");
                        ini.WriteValue("Cheevos", "UnofficialTestMode", "false");
                        ini.WriteValue("Cheevos", "UseFirstDiscFromPlaylist", "true");
                        ini.WriteValue("Cheevos", "SoundEffects", "true");
                        ini.WriteValue("Cheevos", "Notifications", "true");
                        ini.WriteValue("Cheevos", "ChallengeMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "true" : "false");
                        ini.WriteValue("Cheevos", "LeaderboardNotifications", SystemConfig.getOptBoolean("retroachievements.leaderboards") ? "true" : "false");
                        ini.WriteValue("Cheevos", "EncoreMode", SystemConfig.getOptBoolean("retroachievements.encore") ? "true" : "false");

                        // Inject credentials
                        if (SystemConfig.isOptSet("retroachievements.username") && SystemConfig.isOptSet("retroachievements.token"))
                        {
                            ini.WriteValue("Cheevos", "Username", SystemConfig["retroachievements.username"]);
                            ini.WriteValue("Cheevos", "Token", SystemConfig["retroachievements.token"]);

                            if (string.IsNullOrEmpty(ini.GetValue("Cheevos", "Token")))
                                ini.WriteValue("Cheevos", "LoginTimestamp", Convert.ToString((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds));
                        }
                    }
                    else
                    {
                        ini.WriteValue("Cheevos", "Enabled", "false");
                        ini.WriteValue("Cheevos", "ChallengeMode", "false");
                    }


                    if (SystemConfig.isOptSet("psx_ratio") && !string.IsNullOrEmpty(SystemConfig["psx_ratio"]))
                        ini.WriteValue("Display", "AspectRatio", SystemConfig["psx_ratio"]);
                    else if (Features.IsSupported("psx_ratio"))
                        ini.WriteValue("Display", "AspectRatio", "Auto (Game Native)");


                    BindBoolIniFeatureOn(ini, "Display", "VSync", "VSync", "true", "false");
                    BindBoolIniFeature(ini, "Display", "OptimalFramePacing", "duckstation_optimalframepacing", "true", "false");
                    BindIniFeature(ini, "Display", "DeinterlacingMode", "duckstation_deinterlace", "Adaptive");
                    BindIniFeatureSlider(ini, "GPU", "ResolutionScale", "internal_resolution", "1");

                    if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                        ini.WriteValue("GPU", "Renderer", SystemConfig["gfxbackend"]);
                    else if (Features.IsSupported("gfxbackend"))
                        ini.WriteValue("GPU", "Renderer", "Automatic");

                    if (SystemConfig.isOptSet("Texture_Enhancement") && !string.IsNullOrEmpty(SystemConfig["Texture_Enhancement"]))
                        ini.WriteValue("GPU", "TextureFilter", SystemConfig["Texture_Enhancement"]);
                    else if (Features.IsSupported("Texture_Enhancement"))
                        ini.WriteValue("GPU", "TextureFilter", "Nearest");

                    BindBoolIniFeatureOn(ini, "GPU", "DisableInterlacing", "duck_deinterlace", "true", "false");
                    BindBoolIniFeature(ini, "GPU", "ForceNTSCTimings", "NTSC_Timings", "true", "false");
                    BindBoolIniFeature(ini, "GPU", "WidescreenHack", "Widescreen_Hack", "true", "false");
                    BindBoolIniFeature(ini, "GPU", "TrueColor", "Disable_Dithering", "true", "false");
                    BindBoolIniFeature(ini, "GPU", "ScaledDithering", "Scaled_Dithering", "true", "false");

                    if (SystemConfig.isOptSet("duck_pgxp") && SystemConfig.getOptBoolean("duck_pgxp"))
                    {
                        ini.WriteValue("GPU", "PGXPEnable", "true");
                        ini.WriteValue("GPU", "PGXPCulling", "true");
                        ini.WriteValue("GPU", "PGXPTextureCorrection", "true");
                        ini.WriteValue("GPU", "PGXPColorCorrection", "true");
                        ini.WriteValue("GPU", "PGXPPreserveProjFP", "true");
                    }
                    else
                    {
                        ini.WriteValue("GPU", "PGXPEnable", "false");
                        ini.WriteValue("GPU", "PGXPCulling", "false");
                        ini.WriteValue("GPU", "PGXPTextureCorrection", "false");
                        ini.WriteValue("GPU", "PGXPColorCorrection", "false");
                        ini.WriteValue("GPU", "PGXPPreserveProjFP", "false");
                    }

                    if (SystemConfig.isOptSet("duck_msaa") && !string.IsNullOrEmpty(SystemConfig["duck_msaa"]))
                    {
                        string[] msaaValues = SystemConfig["duck_msaa"].Split('_');

                        if (msaaValues.Length == 2)
                        {
                            ini.WriteValue("GPU", "Multisamples", msaaValues[1]);
                            ini.WriteValue("GPU", "PerSampleShading", msaaValues[0] == "msaa" ? "false" : "true");
                        }
                    }
                    else if (Features.IsSupported("duck_msaa"))
                    {
                        ini.WriteValue("GPU", "Multisamples", "1");
                        ini.WriteValue("GPU", "PerSampleShading", "false");
                    }

                    if (SystemConfig.isOptSet("psx_region") && !string.IsNullOrEmpty(SystemConfig["psx_region"]))
                        ini.WriteValue("Console", "Region", SystemConfig["psx_region"]);
                    else if (Features.IsSupported("psx_region"))
                        ini.WriteValue("Console", "Region", "Auto");

                    BindBoolIniFeature(ini, "Console", "EnableCheats", "duckstation_cheats", "true", "false");
                    BindBoolIniFeature(ini, "Console", "Enable8MBRAM", "duckstation_ram", "true", "false");

                    if (SystemConfig.isOptSet("ExecutionMode") && !string.IsNullOrEmpty(SystemConfig["ExecutionMode"]))
                        ini.WriteValue("CPU", "ExecutionMode", SystemConfig["ExecutionMode"]);
                    else if (Features.IsSupported("ExecutionMode"))
                        ini.WriteValue("CPU", "ExecutionMode", "Recompiler");

                    // Performance statistics
                    if (SystemConfig.isOptSet("performance_overlay") && SystemConfig["performance_overlay"] == "detailed")
                    {
                        ini.WriteValue("Display", "ShowFPS", "true");
                        ini.WriteValue("Display", "ShowSpeed", "true");
                        ini.WriteValue("Display", "ShowResolution", "true");
                        ini.WriteValue("Display", "ShowCPU", "true");
                        ini.WriteValue("Display", "ShowGPU", "true");
                    }
                    else if (SystemConfig.isOptSet("performance_overlay") && SystemConfig["performance_overlay"] == "simple")
                    {
                        ini.WriteValue("Display", "ShowFPS", "true");
                        ini.WriteValue("Display", "ShowSpeed", "false");
                        ini.WriteValue("Display", "ShowResolution", "false");
                        ini.WriteValue("Display", "ShowCPU", "false");
                        ini.WriteValue("Display", "ShowGPU", "false");
                    }
                    else
                    {
                        ini.WriteValue("Display", "ShowFPS", "false");
                        ini.WriteValue("Display", "ShowSpeed", "false");
                        ini.WriteValue("Display", "ShowResolution", "false");
                        ini.WriteValue("Display", "ShowCPU", "false");
                        ini.WriteValue("Display", "ShowGPU", "false");
                    }

                    // Internal shaders
                    // First delete existing shaders

                    for (int i = 1; i <= 8; i++)
                    {
                        string section = "PostProcessing/Stage" + i;
                        ini.ClearSection(section);
                    }

                    if (SystemConfig.isOptSet("duck_shaders") && !string.IsNullOrEmpty(SystemConfig["duck_shaders"]))
                    {
                        ini.WriteValue("PostProcessing", "Enabled", "true");
                        ini.WriteValue("PostProcessing", "StageCount", "1");
                        ini.WriteValue("PostProcessing/Stage1", "ShaderName", SystemConfig["duck_shaders"].Replace("_", "/"));
                    }
                    else
                    {
                        ini.WriteValue("PostProcessing", "Enabled", "false");
                        ini.WriteValue("PostProcessing", "StageCount", "0");
                    }

                    BindBoolIniFeatureOn(ini, "Display", "ShowOSDMessages", "duckstation_osd_enabled", "true", "false");

                    if (SystemConfig.isOptSet("audiobackend") && !string.IsNullOrEmpty(SystemConfig["audiobackend"]))
                    ini.WriteValue("Audio", "Backend", SystemConfig["audiobackend"]);
                    else if (Features.IsSupported("audiobackend"))
                        ini.WriteValue("Audio", "Backend", "Cubeb");

                    if (SystemConfig.isOptSet("rewind") && SystemConfig.getOptBoolean("rewind"))
                        ini.WriteValue("Main", "RewindEnable", "true");
                    else
                        ini.WriteValue("Main", "RewindEnable", "false");

                    if (SystemConfig.isOptSet("runahead") && !string.IsNullOrEmpty(SystemConfig["runahead"]))
                    {
                        ini.WriteValue("Main", "RunaheadFrameCount", SystemConfig["runahead"].ToIntegerString());
                        ini.WriteValue("Main", "RewindEnable", "false");
                    }
                    else
                        ini.WriteValue("Main", "RunaheadFrameCount", "0");

                    ini.WriteValue("Main", "ConfirmPowerOff", "false");

                    // fullscreen (disable fullscreen start option, workaround for people with multi-screen that cannot get emulator to start fullscreen on the correct monitor)

                    bool fullscreen = (!IsEmulationStationWindowed() && !SystemConfig.getOptBoolean("disable_fullscreen")) || SystemConfig.getOptBoolean("forcefullscreen");

                    if (!fullscreen)
                        ini.WriteValue("Main", "StartFullscreen", "false");
                    else
                        ini.WriteValue("Main", "StartFullscreen", "true");

                    if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                        ini.WriteValue("Main", "EnableDiscordPresence", "true");
                    else
                        ini.WriteValue("Main", "EnableDiscordPresence", "false");

                    ini.WriteValue("Main", "PauseOnFocusLoss", "true");
                    ini.WriteValue("Main", "DoubleClickTogglesFullscreen", "false");
                    ini.WriteValue("Main", "Language", GetDefaultpsxLanguage());
                    
                    BindBoolIniFeature(ini, "Main", "DisableAllEnhancements", "duckstation_disable_enhancement", "true", "false");

                    ini.WriteValue("AutoUpdater", "CheckAtStartup", "false");

                    // Controller configuration
                    CreateControllerConfiguration(ini);

                    // Gun configuration
                    CreateGunConfiguration(ini);
                }
                
            }
            catch { }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, path.WorkingDirectory);
            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
        }
    }
}
