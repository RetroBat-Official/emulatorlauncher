using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

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
            string path = AppConfig.GetFullPath("duckstation");

            _resolution = resolution;

            string exe = Path.Combine(path, "duckstation-qt-x64-ReleaseLTCG.exe");

            if (!File.Exists(exe))
                return null;

            SetupSettings(path, rom, system);

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            //setting up command line parameters
            var commandArray = new List<string>();

            if (SystemConfig.isOptSet("fullboot") && SystemConfig.getOptBoolean("fullboot"))
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
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
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
                        ini.WriteValue("BIOS", "SearchDirectory", biosPath.Replace("\\", "\\\\"));

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
                        ini.WriteValue("MemoryCards", "Directory", memCardsPath.Replace("\\", "\\\\"));

                    if (SystemConfig["duckstation_memcardtype"] == "Shared")
                    {
                        ini.WriteValue("MemoryCards", "Card1Path", "shared_card_1.mcd");
                        ini.WriteValue("MemoryCards", "Card2Path", "shared_card_2.mcd");
                    }

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
                            ini.WriteValue("Folders", "SaveStates", savesPath.Replace("\\", "\\\\"));
                    }

                    // autosave
                    if (_saveStatesWatcher != null)
                        ini.WriteValue("Main", "SaveStateOnExit", _saveStatesWatcher.IsLaunchingAutoSave() || SystemConfig.getOptBoolean("autosave") ? "true" : "false");
                    else
                        ini.WriteValue("Main", "SaveStateOnExit", "false");

                    string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "duckstation");
                    if (!string.IsNullOrEmpty(cheatsPath))
                        ini.WriteValue("Folders", "Cheats", cheatsPath.Replace("\\", "\\\\"));

                    string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "duckstation");
                    if (!string.IsNullOrEmpty(screenshotsPath))
                        ini.WriteValue("Folders", "Screenshots", screenshotsPath.Replace("\\", "\\\\"));

                    //Enable cheevos is needed
                    if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
                    {
                        ini.WriteValue("Cheevos", "Enabled", "true");
                        ini.WriteValue("Cheevos", "TestMode", "false");
                        ini.WriteValue("Cheevos", "UnofficialTestMode", "false");
                        ini.WriteValue("Cheevos", "UseFirstDiscFromPlaylist", "true");
                        ini.WriteValue("Cheevos", "SoundEffects", "true");
                        ini.WriteValue("Cheevos", "Notifications", "true");
                        ini.WriteValue("Cheevos", "RichPresence", SystemConfig.getOptBoolean("retroachievements.richpresence") ? "true" : "false");
                        ini.WriteValue("Cheevos", "ChallengeMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "true" : "false");
                        ini.WriteValue("Cheevos", "Leaderboards", SystemConfig.getOptBoolean("retroachievements.leaderboards") ? "true" : "false");
                        ini.WriteValue("Cheevos", "PrimedIndicators", SystemConfig.getOptBoolean("retroachievements.challenge_indicators") ? "true" : "false");

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

                    if (SystemConfig.isOptSet("internal_resolution") && !string.IsNullOrEmpty(SystemConfig["internal_resolution"]))
                        ini.WriteValue("GPU", "ResolutionScale", SystemConfig["internal_resolution"]);
                    else if (Features.IsSupported("internal_resolution"))
                        ini.WriteValue("GPU", "ResolutionScale", "1");

                    if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                        ini.WriteValue("GPU", "Renderer", SystemConfig["gfxbackend"]);
                    else if (Features.IsSupported("gfxbackend"))
                        ini.WriteValue("GPU", "Renderer", "Vulkan");

                    if (SystemConfig.isOptSet("Texture_Enhancement") && !string.IsNullOrEmpty(SystemConfig["Texture_Enhancement"]))
                        ini.WriteValue("GPU", "TextureFilter", SystemConfig["Texture_Enhancement"]);
                    else if (Features.IsSupported("Texture_Enhancement"))
                        ini.WriteValue("GPU", "TextureFilter", "Nearest");

                    if (SystemConfig.isOptSet("interlace") && !string.IsNullOrEmpty(SystemConfig["interlace"]))
                        ini.WriteValue("GPU", "DisableInterlacing", SystemConfig["interlace"]);
                    else if (Features.IsSupported("DisableInterlacing"))
                        ini.WriteValue("GPU", "DisableInterlacing", "true");

                    if (SystemConfig.isOptSet("NTSC_Timings") && !string.IsNullOrEmpty(SystemConfig["NTSC_Timings"]))
                        ini.WriteValue("GPU", "ForceNTSCTimings", SystemConfig["NTSC_Timings"]);
                    else if (Features.IsSupported("NTSC_Timings"))
                        ini.WriteValue("GPU", "ForceNTSCTimings", "false");

                    if (SystemConfig.isOptSet("Widescreen_Hack") && !string.IsNullOrEmpty(SystemConfig["Widescreen_Hack"]))
                        ini.WriteValue("GPU", "WidescreenHack", SystemConfig["Widescreen_Hack"]);
                    else if (Features.IsSupported("Widescreen_Hack"))
                        ini.WriteValue("GPU", "WidescreenHack", "false");

                    if (SystemConfig.isOptSet("Disable_Dithering") && !string.IsNullOrEmpty(SystemConfig["Disable_Dithering"]))
                        ini.WriteValue("GPU", "TrueColor", SystemConfig["Disable_Dithering"]);
                    else if (Features.IsSupported("Disable_Dithering"))
                        ini.WriteValue("GPU", "TrueColor", "false");

                    if (SystemConfig.isOptSet("Scaled_Dithering") && !string.IsNullOrEmpty(SystemConfig["Scaled_Dithering"]))
                        ini.WriteValue("GPU", "ScaledDithering", SystemConfig["Scaled_Dithering"]);
                    else if (Features.IsSupported("Scaled_Dithering"))
                        ini.WriteValue("GPU", "ScaledDithering", "false");

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

                    if (SystemConfig.isOptSet("VSync") && !string.IsNullOrEmpty(SystemConfig["VSync"]))
                        ini.WriteValue("Display", "VSync", SystemConfig["VSync"]);
                    else if (Features.IsSupported("VSync"))
                        ini.WriteValue("Display", "VSync", "true");

                    if (SystemConfig.isOptSet("Linear_Filtering") && !string.IsNullOrEmpty(SystemConfig["Linear_Filtering"]))
                        ini.WriteValue("Display", "LinearFiltering", SystemConfig["Linear_Filtering"]);
                    else if (Features.IsSupported("Linear_Filtering"))
                        ini.WriteValue("Display", "LinearFiltering", "true");

                    if (SystemConfig.isOptSet("Integer_Scaling") && !string.IsNullOrEmpty(SystemConfig["Integer_Scaling"]))
                        ini.WriteValue("Display", "IntegerScaling", SystemConfig["Integer_Scaling"]);
                    else if (Features.IsSupported("Integer_Scaling"))
                        ini.WriteValue("Display", "IntegerScaling", "false");

                    if (SystemConfig.isOptSet("psx_region") && !string.IsNullOrEmpty(SystemConfig["psx_region"]))
                        ini.WriteValue("Console", "Region", SystemConfig["psx_region"]);
                    else if (Features.IsSupported("psx_region"))
                        ini.WriteValue("Console", "Region", "Auto");

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

                    if (SystemConfig["shaderset"] == "none" && SystemConfig.isOptSet("duck_shaders") && !string.IsNullOrEmpty(SystemConfig["duck_shaders"]))
                    {
                        ini.WriteValue("Display", "PostProcessing", "true");
                        ini.WriteValue("Display", "PostProcessChain", SystemConfig["duck_shaders"].Replace("_", "/"));
                    }
                    else
                    {
                        ini.WriteValue("Display", "PostProcessing", "false");
                        ini.WriteValue("Display", "PostProcessChain", "");
                    }

                    if (SystemConfig.isOptSet("duckstation_osd_enabled") && !SystemConfig.getOptBoolean("duckstation_osd_enabled"))
                        ini.WriteValue("Display", "ShowOSDMessages", "false");
                    else
                        ini.WriteValue("Display", "ShowOSDMessages", "true");

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
                        ini.WriteValue("Main", "RunaheadFrameCount", SystemConfig["runahead"]);
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

                    ini.WriteValue("Main", "ApplyGameSettings", "true");

                    if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                        ini.WriteValue("Main", "EnableDiscordPresence", "true");
                    else
                        ini.WriteValue("Main", "EnableDiscordPresence", "false");

                    ini.WriteValue("Main", "PauseOnFocusLoss", "true");
                    ini.WriteValue("Main", "DoubleClickTogglesFullscreen", "false");
                    ini.WriteValue("Main", "Language", GetDefaultpsxLanguage());
                    
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

            if (bezel != null)
                bezel.Dispose();

            return ret;
        }
    }
}
