using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System.Text.RegularExpressions;
using System.Linq;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class Singe2Generator : Generator
    {
        private string _videoFile = null;
        private string _singeFile = null;
        private string _symLink;
        private bool _emuDirExists = false;
        private bool _actionMax;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator);
            string exe = Path.Combine(AppConfig.GetFullPath(emulator), "Singe-v2.10-Windows-x86_64.exe");
            string romName = Path.GetFileName(rom);
            string directoryName = romName.Replace(".singe", "");
            string originalRom = rom;
            string actionMaxRom = Path.Combine(Path.GetDirectoryName(rom), "ActionMax.singe");
            if (!Directory.Exists(actionMaxRom))
                actionMaxRom = Path.Combine(Path.GetDirectoryName(rom), "ActionMax");

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            _actionMax = system == "actionmax";

            if (_actionMax && !Directory.Exists(actionMaxRom))
                throw new Exception("ActionMax rom folder not found: ensure it is named 'ActionMax.singe' or 'ActionMax'");

            if (!File.Exists(exe))
            {
                ExitCode = ExitCodes.EmulatorNotInstalled;
                return null;
            }

            if (!_actionMax)
                _symLink = Path.Combine(path, directoryName);
            else
                _symLink = Path.Combine(path, "ActionMax");

            if (!Directory.Exists(_symLink))
                FileTools.CreateSymlink(_symLink, _actionMax ? actionMaxRom : rom, true);
            else
                _emuDirExists = true;

            if (!Directory.Exists(_symLink))
            {
                this.SetCustomError("Unable to create symbolic link. Please activate developer mode in Windows settings to allow this.");
                ExitCode = ExitCodes.CustomError;
                return null;
            }

            // Find the videofile & the .singe file

            if (system != "actionmax")
            {
                if (!ReadDatFile(_symLink))
                {
                    SimpleLogger.Instance.Info("[Generator] Using '.game' file to find script & video.");

                    if (!ReadGameFile(_symLink, originalRom))
                    {
                        SimpleLogger.Instance.Info("[Generator] Trying to find script & video based on folder structure.");
                        if (!TryToFindVideoAndScriptPaths(_symLink))
                            return null;
                    }
                }
            }
            else
            {
                string gameName = romName.Replace(".actionmax", "");
                _videoFile = Path.Combine(AppConfig.GetFullPath(emulator), "ActionMax", ActionMaxVideoFiles[gameName]);
                _singeFile = Path.Combine(AppConfig.GetFullPath(emulator), "ActionMax", ActionMaxScriptFiles[gameName]);
            }

            ConfigureControls(_symLink, Path.Combine(path, "Singe"));

            if (!ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            // Define command line arguments
            List<string> commandArray = new List<string>()
            {
                "-k",
            };

            if (fullscreen)
                commandArray.Add("-w");
            else
            {
                int height = resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height;
                int width = resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width;
                commandArray.Add("-x");
                commandArray.Add(width.ToString());
                commandArray.Add("-y");
                commandArray.Add(height.ToString());
            }
             
            if (!SystemConfig.isOptSet("singe2_debug") || !SystemConfig.getOptBoolean("singe2_debug"))
                commandArray.Add("-z");

            if (SystemConfig.getOptBoolean("singe2_crosshair"))
                commandArray.Add("-n");

            commandArray.Add("-v");
            commandArray.Add("\"" + _videoFile + "\"");
            commandArray.Add("\"" + _singeFile + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private void ConfigureControls(string path, string singePath)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (Program.Controllers.Count == 0)
                return;

            if (Controllers.Any(c => !c.IsKeyboard))
            {

                SimpleLogger.Instance.Info("[SINGE2] Configuring controls.");

                // Define gamepad index in LUA file controls.cfg
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                int index = c1.DirectInput != null ? c1.DirectInput.DeviceIndex : c1.DeviceIndex;

                if (SystemConfig.isOptSet("singe2_padIndex") && !string.IsNullOrEmpty(SystemConfig["singe2_padIndex"]))
                    index = SystemConfig["singe2_padIndex"].ToInteger();

                SimpleLogger.Instance.Info("[SINGE2] Gamepad index : " + index);

                int mouseIndex = 0;
                if (SystemConfig.isOptSet("singe2_mouseIndex") && !string.IsNullOrEmpty(SystemConfig["singe2_mouseIndex"]))
                    mouseIndex = SystemConfig["singe2_mouseIndex"].ToInteger();

                SimpleLogger.Instance.Info("[SINGE2] MOUSE index : " + mouseIndex);

                string deadzone = "15000";
                if (SystemConfig.isOptSet("singe2_deadzone") && !string.IsNullOrEmpty(SystemConfig["singe2_deadzone"]))
                    deadzone = SystemConfig["singe2_deadzone"];

                string file = Path.Combine(path, "controls.cfg");
                string[] lines = 
                {
                    "myGamePad = GAMEPAD_" + index,
                    "myMouse = MOUSE_" + mouseIndex,
                    "",
                    "-- Default Mappings",
                    "",
                    "DEAD_ZONE = " + deadzone,
                    "",
                    "INPUT_UP           = { SCANCODE.UP,    SCANCODE.KP_8, myGamePad.AXIS_LEFT_Y_U, myGamePad.AXIS_RIGHT_Y_U, myGamePad.DPAD_UP    }",
                    "INPUT_LEFT         = { SCANCODE.LEFT,  SCANCODE.KP_4, myGamePad.AXIS_LEFT_X_L, myGamePad.AXIS_RIGHT_X_L, myGamePad.DPAD_LEFT  }",
                    "INPUT_DOWN         = { SCANCODE.DOWN,  SCANCODE.KP_2, myGamePad.AXIS_LEFT_Y_D, myGamePad.AXIS_RIGHT_Y_D, myGamePad.DPAD_DOWN  }",
                    "INPUT_RIGHT        = { SCANCODE.RIGHT, SCANCODE.KP_6, myGamePad.AXIS_LEFT_X_R, myGamePad.AXIS_RIGHT_X_R, myGamePad.DPAD_RIGHT }",
                    "INPUT_1P_COIN      = { SCANCODE.MAIN_5, SCANCODE.C, myGamePad.BUTTON_BACK }",
                    "INPUT_2P_COIN      = { SCANCODE.MAIN_6 }",
                    "INPUT_1P_START     = { SCANCODE.MAIN_1, myGamePad.BUTTON_START }",
                    "INPUT_2P_START     = { SCANCODE.MAIN_2 }",
                    "INPUT_ACTION_1     = { SCANCODE.SPACE, SCANCODE.LCTRL, myGamePad.BUTTON_A, myMouse.BUTTON_RIGHT }",
                    "INPUT_ACTION_2     = { SCANCODE.LALT,   myGamePad.BUTTON_B, myMouse.BUTTON_MIDDLE }",
                    "INPUT_ACTION_3     = { SCANCODE.LSHIFT, myGamePad.BUTTON_X, myMouse.BUTTON_LEFT }",
                    "INPUT_ACTION_4     = { SCANCODE.RSHIFT, myGamePad.BUTTON_Y, myMouse.BUTTON_X1 }",
                    "INPUT_SKILL_EASY   = { SCANCODE.KP_DIVIDE }",
                    "INPUT_SKILL_MEDIUM = { SCANCODE.KP_MULTIPLY }",
                    "INPUT_SKILL_HARD   = { SCANCODE.KP_MINUS }",
                    "INPUT_SERVICE      = { SCANCODE.MAIN_9 }",
                    "INPUT_TEST_MODE    = { SCANCODE.F2 }",
                    "INPUT_RESET_CPU    = { SCANCODE.F3 }",
                    "INPUT_SCREENSHOT   = { SCANCODE.F12, SCANCODE.F11 }",
                    "INPUT_QUIT         = { SCANCODE.ESCAPE, SCANCODE.Q }",
                    "INPUT_PAUSE        = { SCANCODE.P }",
                    "INPUT_CONSOLE      = { SCANCODE.GRAVE }",
                    "INPUT_TILT         = { SCANCODE.T }",
                    "INPUT_GRAB         = { SCANCODE.G }"
                };

                SimpleLogger.Instance.Info("[SINGE2] Generating controls.cfg file.");

                try { File.WriteAllLines(file, lines); }
                catch { }
                
                string singeFile = Path.Combine(singePath, "controls.cfg");

                try { File.Copy(file, singeFile, true); }
                catch { }
            }
        }

        private bool ReadDatFile(string path)
        {
            string datFile = Path.Combine(AppConfig.GetFullPath(path), "games.dat");
            char separator = '/';

            if (!File.Exists(datFile))
            {
                SimpleLogger.Instance.Info("[Generator] Singe2Generator: games.dat file not found in game folder");
                return false;
            }

            bool foundScript = false;
            bool foundVideo = false;

            string datInfo = File.ReadAllText(datFile);
            string videoPattern = @"VIDEO\s*=\s*""([^""]*)""";
            string scriptPattern = @"SCRIPT\s*=\s*""([^""]*)""";

            Match matchVideo = Regex.Match(datInfo, videoPattern);
            Match matchScript = Regex.Match(datInfo, scriptPattern);

            if (matchVideo.Success)
            {
                string videoPath = matchVideo.Groups[1].Value;
                int firstIndex = videoPath.IndexOf(separator);

                if (firstIndex != -1)
                {
                    // Extract the substring after the last occurrence of the separator
                    string videoRelativePath = videoPath.Substring(firstIndex + 1);
                    _videoFile = Path.Combine(AppConfig.GetFullPath(path), videoRelativePath.Replace("/", "\\"));
                    foundVideo = true;
                }

                SimpleLogger.Instance.Info("[Generator] VIDEO path: " + videoPath);
            }
            else
                SimpleLogger.Instance.Info("[Generator] VIDEO path not found.");

            if (matchScript.Success)
            {
                string scriptPath = matchScript.Groups[1].Value;
                int firstIndex = scriptPath.IndexOf(separator);

                if (firstIndex != -1)
                {
                    // Extract the substring after the last occurrence of the separator
                    string scriptRelativePath = scriptPath.Substring(firstIndex + 1);
                    _singeFile = Path.Combine(AppConfig.GetFullPath(path), scriptRelativePath.Replace("/", "\\"));
                    foundScript = true;
                }

                SimpleLogger.Instance.Info("[Generator] SCRIPT path: " + scriptPath);
            }
            else
                SimpleLogger.Instance.Info("[Generator] SCRIPT path not found.");

            if (foundScript && foundVideo)
                return true;
            else
                return false;
        }

        private bool ReadGameFile(string rom, string originalRom)
        {
            bool foundScript = false;
            bool foundVideo = false;
            string path = Path.GetDirectoryName(rom);
            string gameInfoFile = originalRom.Replace(".singe", ".gameinfo");

            if (!File.Exists(gameInfoFile))
            {
                SimpleLogger.Instance.Info("[Generator] No '.gameinfo' file found.");
                return false;
            }

            string[] lines = File.ReadAllLines(gameInfoFile);
            if (lines.Length < 2)
            {
                SimpleLogger.Instance.Info("[Generator] No information of script and video paths inside '.gameinfo' file.");
                return false;
            }

            string[] videoPath = lines[0].Split('=');
            if (videoPath.Length < 2)
            {
                SimpleLogger.Instance.Info("[Generator] No video path found in '.gameinfo' file.");
                return false;
            }
            else
            {
                string videoRelativePath = videoPath[1].Trim();
                _videoFile = Path.Combine(AppConfig.GetFullPath(path), videoRelativePath.Replace("/", "\\"));
                foundVideo = true;
            }

            string[] scriptPath = lines[1].Split('=');
            if (scriptPath.Length < 2)
            {
                SimpleLogger.Instance.Info("[Generator] No script path found in '.gameinfo' file.");
                return false;
            }
            else
            {
                string scriptRelativePath = scriptPath[1].Trim();
                _singeFile = Path.Combine(AppConfig.GetFullPath(path), scriptRelativePath.Replace("/", "\\"));
                foundScript = true;
            }

            if (foundScript && foundVideo)
                return true;
            else
                return false;
        }

        private bool TryToFindVideoAndScriptPaths(string rom)
        {
            bool foundScript = false;
            bool foundVideo = false;
            string[] videoFolders = { Path.Combine(rom, "video"), Path.Combine(rom, "Video") };
            string[] scriptFolders = { Path.Combine(rom, "script"), Path.Combine(rom, "Script"), Path.Combine(rom, "Scripts"), Path.Combine(rom, "scripts") };
            string[] videoExtensions = { ".mp4", ".m2v" };
            string videoFolder = null;
            string scriptFolder = null;
            List<string> singeScripts = new List<string>() { "addons", "globals", "hscore", "main", "map", "save", "service", "toolbox"};
            string romBeginning = Path.GetFileNameWithoutExtension(rom).Substring(0, 2);

            // Video file
            bool videoFolderExists = false;

            foreach (string folder in videoFolders)
            {
                if (Directory.Exists(folder))
                {
                    videoFolder = folder;
                    videoFolderExists = true;
                    break;
                }
            }

            if (videoFolderExists)
            {
                var files = Directory.GetFiles(videoFolder).Where(file => videoExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).ToList();

                if (files.Any())
                {
                    _videoFile = files.First();
                    foundVideo = true;
                }
                else
                {
                    SimpleLogger.Instance.Info("[Generator] No video files found in 'video' folder.");
                    return false;
                }
            }
            else
            {
                var files = Directory.GetFiles(rom).Where(file => videoExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)).ToList();
                if (files.Any())
                {
                    _videoFile = files.First();
                    foundVideo = true;
                }
                else
                {
                    SimpleLogger.Instance.Info("[Generator] No video files found in game folder.");
                    return false;
                }
            }

            // Get script file
            bool scriptFolderExists = false;

            foreach (string folder in scriptFolders)
            {
                if (Directory.Exists(folder))
                {
                    scriptFolder = folder;
                    scriptFolderExists = true;
                    break;
                }
            }

            var singeFiles = Directory.GetFiles(rom).Where(file => file.EndsWith("singe", StringComparison.OrdinalIgnoreCase)).ToList();

            if (singeFiles.Any())
            {
                _singeFile = singeFiles.First();
                foundScript = true;
            }

            else if (scriptFolderExists)
            {
                var scriptFiles = Directory.GetFiles(scriptFolder, "*.singe").Where(file => !singeScripts.Contains(Path.GetFileNameWithoutExtension(file)) && Path.GetFileNameWithoutExtension(file).StartsWith(romBeginning)).ToList();

                if (scriptFiles.Any())
                {
                    _singeFile = scriptFiles.First();
                    foundScript = true;
                }
            }

            if (foundScript && foundVideo)
                return true;
            else
                return false;
        }

        private readonly Dictionary<string, string> ActionMaxVideoFiles = new Dictionary<string, string>()
        {
            { "38ambushalley", "video_38AmbushAlley.mkv" },
            { "bluethunder", "video_BlueThunder.mkv" },
            { "hydrosub2021", "video_Hydrosub2021.mkv" },
            { "popsghostly", "video_PopsGhostly.mkv" },
            { "sonicfury", "video_SonicFury.mkv" },
        };

        private readonly Dictionary<string, string> ActionMaxScriptFiles = new Dictionary<string, string>()
        {
            { "38ambushalley", "38AmbushAlley.singe" },
            { "bluethunder", "BlueThunder.singe" },
            { "hydrosub2021", "Hydrosub2021.singe" },
            { "popsghostly", "PopsGhostly.singe" },
            { "sonicfury", "SonicFury.singe" },
        };

        public override void Cleanup()
        {
            base.Cleanup();

            if (!_emuDirExists)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_symLink) && Directory.Exists(_symLink))
                        Directory.Delete(_symLink);
                }
                catch { }
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
                return 0;

            return ret;
        }
    }
}