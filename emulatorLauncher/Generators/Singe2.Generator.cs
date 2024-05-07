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

            if (!ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

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

        private readonly string[] excludeList = { "globals", "hscore", "main", "map", "service", "toolbox" };

        private bool TryToFindVideoAndScriptPaths(string rom)
        {
            bool foundScript = false;
            bool foundVideo = false;
            string videoFolder = Path.Combine(rom, "video");
            string scriptsFolder = Path.Combine(rom, "Script");
            string firstChar = Path.GetFileNameWithoutExtension(rom).Substring(0, 1);

            if (!Directory.Exists(videoFolder) || !Directory.Exists(scriptsFolder))
                return false;

            // Get video file
            string[] videoFiles = Directory.GetFiles(videoFolder, "*.mp4");
            if (videoFiles.Length == 0)
            {
                SimpleLogger.Instance.Info("[Generator] No video files found in 'video' folder.");
                return false;
            }
            else
            {
                var filteredVideoFiles = videoFiles.Where(file => Path.GetFileNameWithoutExtension(file).Substring(0, 1) == firstChar).ToArray();

                if (filteredVideoFiles.Length == 0)
                {
                    SimpleLogger.Instance.Info("[Generator] No video files found in 'video' folder that match the first character of the rom.");
                    return false;
                }
                else
                {
                    _videoFile = filteredVideoFiles[0];
                    foundVideo = true;
                }
            }

            // Get script file
            string[] scriptFiles = Directory.GetFiles(scriptsFolder, "*.singe");
            if (scriptFiles.Length == 0)
            {
                SimpleLogger.Instance.Info("[Generator] No script files found in 'Script' folder.");
                return false;
            }
            else
            {
                var filteredScriptFiles = scriptFiles.Where(file => !excludeList.Any(excludeItem => Path.GetFileNameWithoutExtension(file).Equals(excludeItem, StringComparison.OrdinalIgnoreCase)) && Path.GetFileNameWithoutExtension(file).Substring(0, 1) == firstChar).ToArray();

                if (filteredScriptFiles.Length == 0)
                {
                    SimpleLogger.Instance.Info("[Generator] No script files found in 'Script' folder that match the first character of the rom.");
                    return false;
                }
                else
                {
                    _singeFile = filteredScriptFiles[0];
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