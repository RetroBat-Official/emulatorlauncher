using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Xml.Serialization;

namespace EmulatorLauncher.Common.EmulationStation
{
    public static class EmulationStationServices
    {
        private static string ESURL(string relative)
        {
            return "http://" + "127.0.0.1:1234" + relative;
        }

        private static bool IsEmulationStationRunning()
        {
            return Process.GetProcessesByName("emulationstation").Length > 0;
        }

        public static bool AddImageToGameListIfMissing(string system, string rom, byte[] bytes, string contentType)
        {
            if (!IsEmulationStationRunning())
                return false;

            try
            {
                var gameId = getFileDataId(rom);

                var dd = WebTools.DownloadString(ESURL("/systems/" + system + "/games/" + gameId));
                if (dd == null || dd.Contains("\"image\":"))
                    return false;

                WebTools.PostBytes(ESURL("/systems/" + system + "/games/" + gameId + "/media/image"), bytes, contentType);
                return true;
            }
            catch { }

            return false;
        }

        public static string getFileDataId(string rom)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                rom = Path.GetFullPath(rom).Replace("\\", "/");
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(rom));
                string md = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return md;
            }
        }

        public static Game GetRunningGameInfo()
        {
            if (!IsEmulationStationRunning())
                return null;

            try
            {
                var dd = WebTools.DownloadString(ESURL("/runningGame"));
                if (dd != null)
                    return JsonToGame(dd);
            }
            catch { }

            return null;
        }

        public static Game GetGameInfo(string system, string rom)
        {
            if (!IsEmulationStationRunning())
                return null;

            try
            {
                var gameId = getFileDataId(rom);
                var dd = WebTools.DownloadString(ESURL("/systems/" + system + "/games/" + gameId));
                if (dd != null)
                    return JsonToGame(dd);
            }
            catch { }

            return null;
        }

        private static Game JsonToGame(string jsonString)
        {
            Game game = new Game();

            var json = EmulatorLauncher.Common.FileFormats.DynamicJson.Parse(jsonString);

            foreach (var mbr in json.GetDynamicMemberNames())
            {
                string value = json[mbr];

                var prop = typeof(Game).GetProperty(mbr);
                if (prop == null)
                    prop = typeof(Game).GetProperties().FirstOrDefault(p => p.GetCustomAttributes(typeof(XmlElementAttribute), false).OfType<XmlElementAttribute>().Any(e => e.ElementName == mbr));

                if (prop != null)
                {
                    try { prop.SetValue(game, Convert.ChangeType(value, prop.PropertyType), null); }
                    catch { }
                }
            }

            if (!string.IsNullOrEmpty(game.Path))
                return game;

            return null;
        }
    }

}
