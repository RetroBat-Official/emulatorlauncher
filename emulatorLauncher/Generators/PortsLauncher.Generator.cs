using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class PortsLauncherGenerator : Generator
    {
        private ScreenResolution _resolution;
        private BezelFiles _bezelFileInfo;
        private string _emulator;
        private string _path;
        private string _exeName;
        private string _romPath;
        private bool _fullscreen;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // Specific cases for some emulators
            if (rom.Contains("soniccd") || SystemConfig.getOptBoolean("sonicretro_sonicCD"))
                emulator = "sonicretrocd";

            _emulator = emulator;
            _path = AppConfig.GetFullPath(emulator);
            if (!Directory.Exists(_path))
                return null;

            _exeName = exeDictionnary[emulator];
            string exe = Path.Combine(_path, _exeName);
            if (!File.Exists(exe))
                return null;

            _romPath = Path.GetDirectoryName(rom);

            if (systemBezels.ContainsKey(emulator) && systemBezels[emulator] != "no")
            {
                string bezelType = systemBezels[emulator];

                switch (bezelType)
                {
                    case "reshade":
                        ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x64, system, rom, _path, resolution, emulator);   // TO BE DONE LATER
                        break;
                    default:
                        _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                        break;
                }
            }

            _resolution = resolution;

            List<string> commandArray = new List<string>();

            CopySavesToEmulator();
            ConfigurePort(commandArray, rom, exe);

            string args = null;
            if (commandArray.Count > 0)
                args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args,
            };
        }

        private readonly Dictionary<string, string> exeDictionnary = new Dictionary<string, string>
        {
            { "sonic3air", "Sonic3AIR.exe"},
            { "sonicmania", "RSDKv5U_x64.exe"},
            { "sonicretro", "RSDKv4_64.exe"},
            { "sonicretrocd", "RSDKv3_64.exe"},
            { "opengoal", "gk.exe"},
            { "cgenius", "CGenius.exe"}
        };

        private readonly Dictionary<string, string> systemBezels = new Dictionary<string, string>
        {
            { "cgenius", "yes"},
            { "sonic3air", "no"},
            { "sonicmania", "no"},
            { "sonicretro", "no"},
            { "sonicretrocd", "no"},
            { "opengoal", "yes"},
        };

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
