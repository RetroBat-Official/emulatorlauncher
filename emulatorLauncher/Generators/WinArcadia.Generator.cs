using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class WinArcadiaGenerator : Generator
    {
        public WinArcadiaGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "WinArcadia.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // Retroachievements
            SetupRetroAchievements(path);
            SetupConfiguration(path, fullscreen);

            // Command line arguments
            List<string> commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("FULLSCREEN=ON");
            else
                commandArray.Add("FULLSCREEN=OFF");

            commandArray.Add("FILE=\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };

            return ret;
        }

        private void SetupRetroAchievements(string path)
        {

            if (!Features.IsSupported("cheevos"))
                return;

            if (SystemConfig.getOptBoolean("retroachievements"))
            {
                string cheevosPrefs = Path.Combine(path, "RAPrefs_WinArcadia.cfg");
                string json = "{\"Username\":\"\",\"Token\":\"\",\"Hardcore Active\":true,\"Non Hardcore Warning\":false,\"Achievement Triggered Notification Display\":\"BottomLeft\",\"Achievement Triggered Screenshot\":false,\"Mastery Notification Display\":\"TopMiddle\",\"Mastery Screenshot\":false,\"Leaderboards Active\":true,\"Leaderboard Notification Display\":\"BottomLeft\",\"Leaderboard Cancel Display\":\"BottomLeft\",\"Leaderboard Counter Display\":\"BottomRight\",\"Leaderboard Scoreboard Display\":\"BottomRight\",\"Challenge Notification Display\":\"BottomRight\",\"Informational Notification Display\":\"BottomLeft\",\"Prefer Decimal\":false,\"Num Background Threads\":8}";
                JObject jsonObject = JObject.Parse(json);

                jsonObject["Username"] = SystemConfig["retroachievements.username"];
                jsonObject["Token"] = SystemConfig["retroachievements.token"];

                jsonObject["Hardcore Active"] = SystemConfig.getOptBoolean("retroachievements.hardcore") ? true : false;
                jsonObject["Leaderboards Active"] = SystemConfig.getOptBoolean("retroachievements.leaderboards") ? true : false;

                string result = JsonConvert.SerializeObject(jsonObject, Formatting.None);

                File.WriteAllText(cheevosPrefs, result);
            }
        }

        private void SetupConfiguration(string path, bool fullscreen)
        {
            string configFile = Path.Combine(path, "Configs", "WinArcadia.ini");

            using (IniFile ini = new IniFile(configFile, IniOptions.KeepEmptyValues))
            {
                // Cheevos
                if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
                    ini.WriteValue("", "retroachievements", "true");
                else if (Features.IsSupported("cheevos"))
                    ini.WriteValue("", "retroachievements", "false");

                ini.WriteValue("", "fullscreen", fullscreen ? "true" : "false");
                ini.WriteValue("", "memorymap", "6");   // Machine = Arcadia
                ini.WriteValue("", "recent_0", null);

                // Directories
                string gamePath = Path.Combine(AppConfig.GetFullPath("roms"), "arcadia");
                string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "winarcadia");
                if (!Directory.Exists(screenshotsPath))
                    try {Directory.CreateDirectory(screenshotsPath);} catch { }
                ini.WriteValue("", "path_games", gamePath);
                ini.WriteValue("", "path_screenshots", screenshotsPath);

                ini.Save();
            }
        }
    }
}
