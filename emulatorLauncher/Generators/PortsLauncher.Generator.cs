using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
{
    partial class OpenJazzGenerator : PortsLauncherGenerator
    { public OpenJazzGenerator() { _exeName = "OpenJazz.exe"; DependsOnDesktopResolution = true; } }

    partial class OpenGoalGenerator : PortsLauncherGenerator
    { public OpenGoalGenerator() { _exeName = "gk.exe"; DependsOnDesktopResolution = true; } }

    partial class CDogsGenerator : PortsLauncherGenerator
    { public CDogsGenerator() { _exeName = "bin\\cdogs-sdl.exe"; DependsOnDesktopResolution = true; } }

    partial class CGeniusGenerator : PortsLauncherGenerator
    { public CGeniusGenerator() { _exeName = "CGenius.exe"; DependsOnDesktopResolution = true; } }

    partial class PDarkGenerator : PortsLauncherGenerator
    { public PDarkGenerator() { DependsOnDesktopResolution = false; } }

    partial class SohGenerator : PortsLauncherGenerator
    { public SohGenerator() { _exeName = "soh.exe"; DependsOnDesktopResolution = true; } }

    partial class StarshipGenerator : PortsLauncherGenerator
    { public StarshipGenerator() { _exeName = "Starship.exe"; DependsOnDesktopResolution = true; } }

    partial class CorsixTHGenerator : PortsLauncherGenerator
    { public CorsixTHGenerator() { _exeName = "CorsixTH.exe"; DependsOnDesktopResolution = true; } }

    partial class Dhewm3Generator : PortsLauncherGenerator
    { public Dhewm3Generator() { _exeName = "dhewm3.exe"; DependsOnDesktopResolution = false; } }

    partial class PortsLauncherGenerator : Generator
    {
        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private string _emulator;
        private string _path;
        protected string _exeName;
        private string _romPath;
        private string _workingPath = null;
        private bool _fullscreen;
        private bool _nobezels;
        private bool _useReshade = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            _nobezels = false;

            // Specific cases to override emulator
            if (rom.Contains("soniccd") || SystemConfig.getOptBoolean("sonicretro_sonicCD"))
                emulator = "sonicretrocd";

            _emulator = emulator;

            // Get emulator path, emulator must match the name in es_systems
            _path = AppConfig.GetFullPath(emulator);
            if (emulator == "cdogs")
            {
                _path = Path.Combine(_path, "bin");
                _workingPath = _path + "\\";
            }
            if (!Directory.Exists(_path))
                return null;

            // Get exe name for the port, using a dictionary (or specific method for pdark)
            _exeName = exeDictionnary[emulator];
            if (emulator == "pdark")
            {
                if (SystemConfig.isOptSet("pdark_region") && !string.IsNullOrEmpty(SystemConfig["pdark_region"]))
                {
                    string pdarkRegion = SystemConfig["pdark_region"];
                    switch (pdarkRegion)
                    {
                        case "EUR":
                            _exeName = "pd.pal.x86_64.exe";
                            break;
                        case "JPN":
                            _exeName = "pd.jpn.x86_64.exe";
                            break;
                        case "USA":
                            _exeName = "pd.x86_64.exe";
                            break;
                    }
                }
                else
                {
                    string romName = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
                    if (rom.Contains("eur"))
                        _exeName = "pd.pal.x86_64.exe";
                    else if (rom.Contains("jap") || rom.Contains("jp"))
                        _exeName = "pd.jpn.x86_64.exe";
                }
            }
            
            string exe = Path.Combine(_path, _exeName);
            if (!File.Exists(exe))
                return null;

            _romPath = Path.GetDirectoryName(rom);
            _resolution = resolution;

            // Copy existing saves from retrobat\saves to the emulator folder
            CopySavesToEmulator();

            // Initiate command array to pass it to the configuration part, do not add command line arguments here, add them in the port configuration part
            List<string> commandArray = new List<string>();

            ConfigurePort(commandArray, rom, exe);

            // Setting up bezels, can be with or without reshade based on Dictionary
            if (systemBezels.ContainsKey(emulator) && systemBezels[emulator] != "no" && _fullscreen && !_nobezels)
            {
                string bezelType = systemBezels[emulator];

                switch (bezelType)
                {
                    case "reshade":
                        if (!reshadePlatform.ContainsKey(emulator) || !reshadeType.ContainsKey(emulator))
                            break;
                        ReshadePlatform RSplatform = reshadePlatform[emulator];
                        ReshadeBezelType RStype = reshadeType[emulator];
                        SimpleLogger.Instance.Info("[INFO] Setting up Reshade");
                        ReshadeManager.Setup(RStype, RSplatform, system, rom, _path, resolution, emulator);
                        _useReshade = true;
                        break;
                    default:
                        _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                        break;
                }
            }

            string args = null;
            if (commandArray.Count > 0)
                args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _workingPath?? _path,
                Arguments = args,
            };
        }

        private readonly Dictionary<string, string> exeDictionnary = new Dictionary<string, string>
        {
            { "cdogs", "cdogs-sdl.exe"},
            { "cgenius", "CGenius.exe"},
            { "corsixth", "CorsixTH.exe"},
            { "dhewm3", "dhewm3.exe"},
            { "opengoal", "gk.exe"},
            { "openjazz", "OpenJazz.exe"},
            { "pdark", "pd.x86_64.exe"},
            { "soh", "soh.exe"},
            { "sonic3air", "Sonic3AIR.exe"},
            { "sonicmania", "RSDKv5U_x64.exe"},
            { "sonicretro", "RSDKv4_64.exe"},
            { "sonicretrocd", "RSDKv3_64.exe"},
            { "starship", "Starship.exe"}
        };

        private readonly Dictionary<string, string> systemBezels = new Dictionary<string, string>
        {
            { "cdogs", "no"},
            { "cgenius", "yes"},
            { "corsixth", "yes"},
            { "dhewm3", "reshade"},
            { "pdark", "yes"},
            { "sonic3air", "no"},
            { "sonicmania", "no"},
            { "sonicretro", "no"},
            { "sonicretrocd", "no"},
            { "opengoal", "yes"},
            { "openjazz", "no"},
            { "soh", "yes"},
            { "starship", "yes"}
        };

        // Dictionaries to use if a port uses Reshade to specify platform and type
        private readonly Dictionary<string, ReshadeBezelType> reshadeType = new Dictionary<string, ReshadeBezelType>()
        {
            { "dhewm3", ReshadeBezelType.opengl }
        };
        private readonly Dictionary<string, ReshadePlatform> reshadePlatform = new Dictionary<string, ReshadePlatform>()
        {
            { "dhewm3", ReshadePlatform.x86 }
        };

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_emulator == "pdark")
            {
                string exe = Path.GetFileNameWithoutExtension(_exeName);
                return mapping = PadToKey.AddOrUpdateKeyMapping(mapping, exe, InputKey.hotkey | InputKey.start, "(%{CLOSE})");
            }

            else
                return mapping;
        }

        // RunAnd Wait override
        // Used so far to dispose bezels
        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (_useReshade)
            {
                if (reshadeType.ContainsKey(_emulator))
                {
                    ReshadeBezelType RStype = reshadeType[_emulator];
                    ReshadeManager.UninstallReshader(RStype, _path);
                }
            }

            if (ret == 1)
                return 0;

            return ret;
        }

        // Cleanup override
        // Used so far to copy saves from emulator folder to Retrobat saves folder
        public override void Cleanup()
        {
            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"));
            if (_emulator == "cgenius")
            {
                string rbSavePath = Path.Combine(savesPath, "cgenius");
                if (!Directory.Exists(rbSavePath))
                    try { Directory.CreateDirectory(rbSavePath); } catch { }

                string emulatorSavesPath = Path.Combine(_path, "save");

                if (Directory.Exists(emulatorSavesPath) && Directory.Exists(rbSavePath))
                    try { FileTools.CopyDirectory(emulatorSavesPath, rbSavePath, true, true); } catch { }
            }

            else if (_emulator == "sonicmania")
            {
                string emulatorSaveFile = Path.Combine(_romPath, "SaveData.bin");
                string achievementsuFile = Path.Combine(_romPath, "Achievements.bin");
                string rbSavePath = Path.Combine(savesPath, "sonicmania");
                if (!Directory.Exists(rbSavePath))
                    try { Directory.CreateDirectory(rbSavePath); } catch { }

                if (File.Exists(emulatorSaveFile) && Directory.Exists(rbSavePath))
                {
                    string rbSaveFile = Path.Combine(rbSavePath, "SaveData.bin");
                    if (!File.Exists(rbSaveFile))
                        try { File.Copy(emulatorSaveFile, rbSaveFile, true); } catch { }
                    else if (File.GetLastWriteTime(emulatorSaveFile) > File.GetLastWriteTime(rbSaveFile))
                        try { File.Copy(emulatorSaveFile, rbSaveFile, true); } catch { }
                }
                if (File.Exists(achievementsuFile) && Directory.Exists(rbSavePath))
                {
                    string rbAchievementsFile = Path.Combine(rbSavePath, "Achievements.bin");
                    if (!File.Exists(rbAchievementsFile))
                        try { File.Copy(achievementsuFile, rbAchievementsFile, true); } catch { }
                    else if (File.GetLastWriteTime(achievementsuFile) > File.GetLastWriteTime(rbAchievementsFile))
                        try { File.Copy(achievementsuFile, rbAchievementsFile, true); } catch { }
                }
            }

            else if (_emulator == "sonicretro" || _emulator == "sonicretrocd")
            {
                string emulatorSaveFile = Path.Combine(_romPath, "SData.bin");
                string gameName = Path.GetFileName(_romPath);
                string rbSavePath = Path.Combine(savesPath, "sonicretro", gameName);
                if (!Directory.Exists(rbSavePath))
                    try { Directory.CreateDirectory(rbSavePath); } catch { }

                if (File.Exists(emulatorSaveFile) && Directory.Exists(rbSavePath))
                {
                    string rbSaveFile = Path.Combine(rbSavePath, "SData.bin");
                    if (!File.Exists(rbSaveFile))
                        try { File.Copy(emulatorSaveFile, rbSaveFile, true); } catch { }
                    else if (File.GetLastWriteTime(emulatorSaveFile) > File.GetLastWriteTime(rbSaveFile))
                        try { File.Copy(emulatorSaveFile, rbSaveFile, true); } catch { }
                }
            }

            else if (_emulator == "sonic3air")
            {
                string emulatorSaveFile = Path.Combine(_path, "savedata", "persistentdata.bin");
                string emulatorSramFile = Path.Combine(_path, "savedata", "sram.bin");
                string rbSavePath = Path.Combine(savesPath, "sonic3air");
                if (!Directory.Exists(rbSavePath))
                    try { Directory.CreateDirectory(rbSavePath); } catch { }

                if (File.Exists(emulatorSaveFile) && Directory.Exists(rbSavePath))
                {
                    string rbSaveFile = Path.Combine(rbSavePath, "persistentdata.bin");
                    if (!File.Exists(rbSaveFile))
                        try { File.Copy(emulatorSaveFile, rbSaveFile, true); } catch { }
                    else if (File.GetLastWriteTime(emulatorSaveFile) > File.GetLastWriteTime(rbSaveFile))
                        try { File.Copy(emulatorSaveFile, rbSaveFile, true); } catch { }
                }
                if (File.Exists(emulatorSramFile) && Directory.Exists(rbSavePath))
                {
                    string rbAchievementsFile = Path.Combine(rbSavePath, "sram.bin");
                    if (!File.Exists(rbAchievementsFile))
                        try { File.Copy(emulatorSramFile, rbAchievementsFile, true); } catch { }
                    else if (File.GetLastWriteTime(emulatorSramFile) > File.GetLastWriteTime(rbAchievementsFile))
                        try { File.Copy(emulatorSramFile, rbAchievementsFile, true); } catch { }
                }
            }

            base.Cleanup();
        }

        // Method to copy saves from retrobat\saves to emulator folder, called before the game
        private void CopySavesToEmulator()
        {
            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"));
            if (_emulator == "cgenius")
            {
                string rbSavePath = Path.Combine(savesPath, "cgenius");
                if (!Directory.Exists(rbSavePath))
                {
                    try { Directory.CreateDirectory(rbSavePath); } catch { }
                    return;
                }
                
                string emulatorSavesPath = Path.Combine(_path, "save");
                if (!Directory.Exists(emulatorSavesPath))
                    try { Directory.CreateDirectory(emulatorSavesPath); } catch { }

                if (Directory.Exists(emulatorSavesPath) && Directory.Exists(rbSavePath))
                    try { FileTools.CopyDirectory(rbSavePath, emulatorSavesPath, true, true); } catch { }
            }

            else if (_emulator == "sonicmania")
            {
                string rbSavePath = Path.Combine(savesPath, "sonicmania");
                if (!Directory.Exists(rbSavePath))
                {
                    try { Directory.CreateDirectory(rbSavePath); } catch { }
                    return;
                }

                string rbSavesFile = Path.Combine(rbSavePath, "SaveData.bin");
                if (File.Exists(rbSavesFile))
                {
                    string emulatorSavesFile = Path.Combine(_romPath, "SaveData.bin");

                    if (!File.Exists(emulatorSavesFile))
                        try { File.Copy(rbSavesFile, emulatorSavesFile, true); } catch { }
                    else if (File.GetLastWriteTime(rbSavesFile) > File.GetLastWriteTime(emulatorSavesFile))
                        try { File.Copy(rbSavesFile, emulatorSavesFile, true); } catch { }
                }

                string rbAchievementsFile = Path.Combine(rbSavePath, "Achievements.bin");
                if (File.Exists(rbSavesFile))
                {
                    string emulatorAchievementFile = Path.Combine(_romPath, "Achievements.bin");

                    if (!File.Exists(emulatorAchievementFile))
                        try { File.Copy(rbAchievementsFile, emulatorAchievementFile, true); } catch { }
                    else if (File.GetLastWriteTime(rbAchievementsFile) > File.GetLastWriteTime(emulatorAchievementFile))
                        try { File.Copy(rbAchievementsFile, emulatorAchievementFile, true); } catch { }
                }
            }

            else if (_emulator == "sonicretro" || _emulator == "sonicretrocd")
            {
                string gameName = Path.GetFileName(_romPath);
                string rbSavePath = Path.Combine(savesPath, "sonicretro", gameName);
                if (!Directory.Exists(rbSavePath))
                {
                    try { Directory.CreateDirectory(rbSavePath); } catch { }
                    return;
                }

                string rbSavesFile = Path.Combine(rbSavePath, "SData.bin");
                if (File.Exists(rbSavesFile))
                {
                    string emulatorSavesFile = Path.Combine(_romPath, "SData.bin");

                    if (!File.Exists(emulatorSavesFile))
                        try { File.Copy(rbSavesFile, emulatorSavesFile, true); } catch { }
                    else if (File.GetLastWriteTime(rbSavesFile) > File.GetLastWriteTime(emulatorSavesFile))
                        try { File.Copy(rbSavesFile, emulatorSavesFile, true); } catch { }
                }
            }

            else if (_emulator == "sonic3air")
            {
                string rbSavePath = Path.Combine(savesPath, "sonic3air");
                if (!Directory.Exists(rbSavePath))
                {
                    try { Directory.CreateDirectory(rbSavePath); } catch { }
                    return;
                }

                string rbSavesFile = Path.Combine(rbSavePath, "persistentdata.bin");
                if (File.Exists(rbSavesFile))
                {
                    string emulatorSavesFile = Path.Combine(_path, "savedata", "persistentdata.bin");

                    if (!File.Exists(emulatorSavesFile))
                        try { File.Copy(rbSavesFile, emulatorSavesFile, true); } catch { }
                    else if (File.GetLastWriteTime(rbSavesFile) > File.GetLastWriteTime(emulatorSavesFile))
                        try { File.Copy(rbSavesFile, emulatorSavesFile, true); } catch { }
                }

                string rbSramFile = Path.Combine(rbSavePath, "sram.bin");
                if (File.Exists(rbSavesFile))
                {
                    string emulatorSramFile = Path.Combine(_path, "savedata", "sram.bin");

                    if (!File.Exists(emulatorSramFile))
                        try { File.Copy(rbSramFile, emulatorSramFile, true); } catch { }
                    else if (File.GetLastWriteTime(rbSramFile) > File.GetLastWriteTime(emulatorSramFile))
                        try { File.Copy(rbSramFile, emulatorSramFile, true); } catch { }
                }
            }

            return;
        }
    }
}
