using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System.Text.RegularExpressions;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Singe2Generator : Generator
    {
        private string _videoFile = null;
        private string _singeFile = null;
        private string _symLink;
        private bool _emuDirExists = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator);
            string exe = Path.Combine(AppConfig.GetFullPath(emulator), "Singe-v2.10-Windows-x86_64.exe");
            string directoryName = Path.GetFileName(rom).Replace(".singe", "");
            string originalRom = rom;
            //bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (!File.Exists(exe))
            {
                ExitCode = ExitCodes.EmulatorNotInstalled;
                return null;
            }

            _symLink = Path.Combine(path, directoryName);

            if (!Directory.Exists(_symLink))
                FileTools.CreateSymlink(_symLink, rom, true);
            else
                _emuDirExists = true;

            if (!Directory.Exists(_symLink))
            {
                this.SetCustomError("Unable to create symbolic link. Please activate developer mode in Windows settings to allow this.");
                ExitCode = ExitCodes.CustomError;
                return null;
            }

            // Find the videofile & the .singe file
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

            // Define command line arguments
            List<string> commandArray = new List<string>()
            {
                "-k",
                "-w",
                "-z",
                "-v",
                "\"" + _videoFile + "\"",
                "\"" + _singeFile + "\""
            };

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
    }
}