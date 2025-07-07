using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace EmulatorLauncher
{
    partial class DuckstationGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private SaveStatesWatcher _saveStatesWatcher;
        private Version _duckstationVersion;
        private bool _internalBezel = false;
        private bool _cleanupbezel = false;
        private string _path;
        private KeyValuePair<string, string>[] _stage1;
        private KeyValuePair<string, string>[] _stage2;
        private KeyValuePair<string, string>[] _stage3;
        private KeyValuePair<string, string>[] _stage4;
        private KeyValuePair<string, string>[] _stage5;
        private KeyValuePair<string, string>[] _stage6;
        private KeyValuePair<string, string>[] _stage7;
        private KeyValuePair<string, string>[] _stage8;
        private KeyValuePair<string, string>[] _postproc;
        private List<KeyValuePair<string, string>[]> stages = new List<KeyValuePair<string, string>[]>();
        private bool _restoreShaders = false;

        public DuckstationGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("duckstation");
            if (!Directory.Exists(path))
                return null;

            _path = path;

            bool fullscreen = (!SystemConfig.getOptBoolean("disable_fullscreen") && !IsEmulationStationWindowed()) || SystemConfig.getOptBoolean("forcefullscreen");

            string exe = Path.Combine(path, "duckstation-qt-x64-ReleaseLTCG.exe");
            if (!File.Exists(exe))
                return null;

            var ver = Version.TryParse(FileVersionInfo.GetVersionInfo(exe).FileVersion, out _duckstationVersion);

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

            //Applying bezels
            string renderer = "OpenGL";
            if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                renderer = SystemConfig["gfxbackend"];

            if (SystemConfig.getOptBoolean("duckstation_internalBezel") && fullscreen)
            {
                _internalBezel = true;
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            else if (fullscreen)
            {
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
            }

            SetupSettings(path, rom, system);

            _resolution = resolution;

            //setting up command line parameters
            var commandArray = new List<string>();

            if (SystemConfig.isOptSet("fullboot") && !SystemConfig.getOptBoolean("fullboot"))
                commandArray.Add("-slowboot");
            else
                commandArray.Add("-fastboot");

            commandArray.Add("-batch");
            //commandArray.Add("-portable");        DEPRECATED

            if (fullscreen)
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
            string iniFile = Path.Combine(path, "settings.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    ini.WriteValue("Main", "SetupWizardIncomplete", "false");

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
                            SimpleLogger.Instance.Info("[INFO] SavesStatesWatcher enabled.");
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
                        SimpleLogger.Instance.Info("[INFO] Getting Retroachievements information for Cheevos login.");

                        ini.WriteValue("Cheevos", "Enabled", "true");
                        ini.WriteValue("Cheevos", "EncoreMode", SystemConfig.getOptBoolean("retroachievements.encore") ? "true" : "false");
                        //ini.WriteValue("Cheevos", "UnofficialTestMode", SystemConfig.getOptBoolean("retroachievements.unofficial") ? "true" : "false");
                        ini.WriteValue("Cheevos", "ChallengeMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "true" : "false");
                        ini.WriteValue("Cheevos", "Notifications", "true");
                        ini.WriteValue("Cheevos", "LeaderboardNotifications", SystemConfig.getOptBoolean("retroachievements.leaderboards") ? "true" : "false");
                        ini.WriteValue("Cheevos", "SoundEffects", "true");
                        ini.WriteValue("Cheevos", "Overlays", SystemConfig.getOptBoolean("retroachievements.challenge_indicators") ? "true" : "false");

                        // Inject credentials
                        if (SystemConfig.isOptSet("retroachievements.username") && SystemConfig.isOptSet("retroachievements.token"))
                        {
                            ini.WriteValue("Cheevos", "Username", SystemConfig["retroachievements.username"]);

                            string cheevosToken = SystemConfig["retroachievements.token"];

                            var newCheevosVer = new Version(0, 1, 8770, 0);

                            if (_duckstationVersion >= newCheevosVer)
                            {
                                SimpleLogger.Instance.Info("[INFO] Duckstation version : " + newCheevosVer.ToString() + ", encrypting Cheevos token.");
                                try
                                {
                                    string newToken = EncryptLoginToken(cheevosToken, SystemConfig["retroachievements.username"]);
                                    cheevosToken = newToken;
                                }
                                catch { SimpleLogger.Instance.Warning("[WARNING] Unable to define cheevos token."); }
                            }

                            ini.WriteValue("Cheevos", "Token", cheevosToken);

                            if (string.IsNullOrEmpty(ini.GetValue("Cheevos", "Token")))
                                ini.WriteValue("Cheevos", "LoginTimestamp", Convert.ToString((int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds));
                        }
                    }
                    else
                    {
                        ini.WriteValue("Cheevos", "Enabled", "false");
                        ini.WriteValue("Cheevos", "ChallengeMode", "false");
                    }

                    // Internal Bezels
                    if (_internalBezel && _bezelFileInfo != null)
                    {
                        ini.WriteValue("BorderOverlay", "PresetName", "Custom");
                        ini.WriteValue("BorderOverlay", "ImagePath", _bezelFileInfo.PngFile);
                        string left = _bezelFileInfo.BezelInfos.left.ToString();
                        string top = _bezelFileInfo.BezelInfos.top.ToString();
                        string right = (_bezelFileInfo.BezelInfos.width - _bezelFileInfo.BezelInfos.right).ToString();
                        string bottom = (_bezelFileInfo.BezelInfos.height - _bezelFileInfo.BezelInfos.bottom).ToString();
                        ini.WriteValue("BorderOverlay", "DisplayStartX", left);
                        ini.WriteValue("BorderOverlay", "DisplayStartY", top);
                        ini.WriteValue("BorderOverlay", "DisplayEndX", right);
                        ini.WriteValue("BorderOverlay", "DisplayEndY", bottom);
                        _cleanupbezel = true;
                    }

                    if (SystemConfig.isOptSet("psx_ratio") && !string.IsNullOrEmpty(SystemConfig["psx_ratio"]))
                        ini.WriteValue("Display", "AspectRatio", SystemConfig["psx_ratio"]);
                    else if (Features.IsSupported("psx_ratio"))
                        ini.WriteValue("Display", "AspectRatio", "Auto (Game Native)");


                    BindBoolIniFeatureOn(ini, "Display", "VSync", "VSync", "true", "false");
                    BindBoolIniFeature(ini, "Display", "OptimalFramePacing", "duckstation_optimalframepacing", "true", "false");
                    BindIniFeature(ini, "GPU", "DeinterlacingMode", "duckstation_deinterlace", "Progressive");
                    BindIniFeature(ini, "GPU", "ResolutionScale", "internal_resolution", "1");

                    if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                        ini.WriteValue("GPU", "Renderer", SystemConfig["gfxbackend"]);
                    else if (Features.IsSupported("gfxbackend"))
                        ini.WriteValue("GPU", "Renderer", "Automatic");

                    if (SystemConfig.isOptSet("Texture_Enhancement") && !string.IsNullOrEmpty(SystemConfig["Texture_Enhancement"]))
                        ini.WriteValue("GPU", "TextureFilter", SystemConfig["Texture_Enhancement"]);
                    else if (Features.IsSupported("Texture_Enhancement"))
                        ini.WriteValue("GPU", "TextureFilter", "Nearest");

                    BindBoolIniFeature(ini, "GPU", "WidescreenHack", "Widescreen_Hack", "true", "false");
                    BindIniFeature(ini, "GPU", "DitheringMode", "duck_ditheringmode", "TrueColor");

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
                    _stage1 = ini.EnumerateValues("PostProcessing/Stage1");
                    _stage2 = ini.EnumerateValues("PostProcessing/Stage2");
                    _stage3 = ini.EnumerateValues("PostProcessing/Stage3");
                    _stage4 = ini.EnumerateValues("PostProcessing/Stage4");
                    _stage5 = ini.EnumerateValues("PostProcessing/Stage5");
                    _stage6 = ini.EnumerateValues("PostProcessing/Stage6");
                    _stage7 = ini.EnumerateValues("PostProcessing/Stage7");
                    _stage8 = ini.EnumerateValues("PostProcessing/Stage8");
                    _postproc = ini.EnumerateValues("PostProcessing");
                    stages.Add(_stage1); stages.Add(_stage2); stages.Add(_stage3); stages.Add(_stage4); stages.Add(_stage5); stages.Add(_stage6); stages.Add(_stage7); stages.Add(_stage8);


                    if (SystemConfig.isOptSet("duck_shaders") && !string.IsNullOrEmpty(SystemConfig["duck_shaders"]))
                    {
                        
                        for (int i = 1; i <= 8; i++)
                        {
                            string section = "PostProcessing/Stage" + i;
                            ini.ClearSection(section);
                        }

                        ini.WriteValue("PostProcessing", "Enabled", "true");
                        ini.WriteValue("PostProcessing", "StageCount", "1");
                        ini.WriteValue("PostProcessing/Stage1", "ShaderName", SystemConfig["duck_shaders"].Replace("_", "/"));
                        _restoreShaders = true;
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

                    // Add rom path to RecursivePaths
                    AddPathToRecursivePaths(Path.GetFullPath(Path.GetDirectoryName(rom)), ini);

                    // Controller configuration
                    CreateControllerConfiguration(ini);

                    // Gun configuration
                    CreateGunConfiguration(ini);

                    ini.Save();
                }
                
            }
            catch { }
        }

        private static void AddPathToRecursivePaths(string romPath, IniFile ini)
        {
            var recursivePaths = ini.EnumerateValues("GameList")
                .Where(e => e.Key == "RecursivePaths")
                .Select(e => Path.GetFullPath(e.Value))
                .ToList();

            if (!recursivePaths.Contains(romPath))
                ini.AppendValue("GameList", "RecursivePaths", romPath);
        }

        private const int AesBlockSize = 16;

        public static string EncryptLoginToken(string token, string username)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username))
                return string.Empty;

            // Generate 32-byte encryption key from username
            byte[] key = GetLoginEncryptionKey(username);

            // Split key into AES key and IV
            byte[] aesKey = key.Take(16).ToArray();  // First 16 bytes for AES key
            byte[] aesIV = key.Skip(16).Take(16).ToArray();  // Last 16 bytes for IV

            // Convert token to bytes and pad to block size
            byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
            byte[] paddedData = PadToBlockSize(tokenBytes, AesBlockSize);

            // Encrypt using AES-128-CBC
            byte[] encryptedData = EncryptAesCbc(paddedData, aesKey, aesIV);

            // Base64 encode the result
            return Convert.ToBase64String(encryptedData);
        }

        private static byte[] GetLoginEncryptionKey(string username)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(username));

                for (int i = 0; i < 100; i++) // 100 extra rounds of hashing
                    hash = sha256.ComputeHash(hash);

                return hash; // 32-byte key
            }
        }

        private static byte[] EncryptAesCbc(byte[] data, byte[] key, byte[] iv)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None; // No extra padding, we handle it manually

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    return encryptor.TransformFinalBlock(data, 0, data.Length);
                }
            }
        }

        private static byte[] PadToBlockSize(byte[] data, int blockSize)
        {
            int paddedSize = (data.Length + blockSize - 1) / blockSize * blockSize;
            byte[] paddedData = new byte[paddedSize];
            Array.Copy(data, paddedData, data.Length);
            return paddedData; // Zero-padding (matches C++ behavior)
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null && !_internalBezel)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, path.WorkingDirectory);
            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
        }

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            if (_sindenSoft)
                Guns.KillSindenSoftware();

            if (_cleanupbezel || _restoreShaders)
            {
                string iniFile = Path.Combine(_path, "settings.ini");

                try
                {
                    using (var ini = new IniFile(iniFile))
                    {
                        if (_cleanupbezel)
                            ini.ClearSection("BorderOverlay");

                        if (_restoreShaders && _stage1.Count() > 0)
                        {
                            if (_stage1.Count() != 0)
                            {
                                int i = 1;
                                foreach (var s in stages)
                                {
                                    if (s.Count() == 0)
                                        continue;

                                    string section = "PostProcessing/Stage" + i;

                                    foreach (var kvp in s)
                                    {
                                        ini.WriteValue(section, kvp.Key, kvp.Value);
                                    }

                                    i++;
                                }
                                foreach (var kvp in _postproc)
                                {
                                    ini.WriteValue("PostProcessing", kvp.Key, kvp.Value);
                                }
                            }
                        }
                        else
                        {
                            ini.ClearSection("PostProcessing");
                            for (int i = 1; i <= 8; i++)
                            {
                                string section = "PostProcessing/Stage" + i;
                                ini.ClearSection(section);
                            }
                        }
                    }
                }
                catch { }
            }

            base.Cleanup();
        }
    }
}
