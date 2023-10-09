using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Steam_Library_Manager.Framework;
using Microsoft.Win32;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    class SteamGame
    {
        public string InstallDir { get; set; }
        public string Executable { get; set; }
    }

    class SteamAppInfoReader
    {
        public static SteamGame FindGameInformations(int steamAppId)
        {
            string path = null;
            string gameInstallationPath = null;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam"))
                {
                    if (key != null)
                    {
                        Object o = key.GetValue("InstallPath");
                        if (o != null)
                        {
                            path = o as string;
                        }
                    }
                }
            }
            catch
            {
                throw new ApplicationException("Can not find Steam installation folder in registry.");
            }

            string libraryfoldersPath = Path.Combine(path, "config", "libraryfolders.vdf");
            string appinfoPath = Path.Combine(path, "appcache", "appinfo.vdf");

            if (!File.Exists(appinfoPath))
                SimpleLogger.Instance.Info("[WARNING] Missing file " + appinfoPath);

            try
            {
                var libraryfolders = new KeyValue();
                libraryfolders.ReadFileAsText(libraryfoldersPath);

                SimpleLogger.Instance.Info("[INFO] Reading Steam file 'libraryfolders.vdf'");

                gameInstallationPath = libraryfolders.Children
                    .Where(t => t.Traverse(x => x.Children).Any(v => v.Name == steamAppId.ToString()))
                    .SelectMany(t => t.Children.Where(x => x.Name == "path" && x.Value != null))
                    .Select(x => x.Value.ToString())
                    .FirstOrDefault();

                SimpleLogger.Instance.Info("[INFO] Game installation folder found \"" + gameInstallationPath + "\"");
            }
            catch{}

            try 
            {
                var reader = new SteamAppInfoReader();
                reader.Read(appinfoPath);

                SimpleLogger.Instance.Info("[INFO] Reading Steam file 'appinfo.vdf'");

                var app = reader.Apps.FirstOrDefault(a => a.AppID == steamAppId);
                if (app == null)
                    return null;

                SimpleLogger.Instance.Info("[INFO] Found Game \"" + steamAppId + "\" in 'appinfo.vdf'");

                SteamGame g = new SteamGame();
                g.InstallDir = app.Data
                    .Traverse(d => d.Children)
                    .Where(d => d.Children.Any(c => c.Name == "installdir"))
                    .Select(c => c.Children.FirstOrDefault(e => e.Name == "installdir" && e.Value != null))
                    .Select(c => c.Value.ToString())
                    .FirstOrDefault();

                if (gameInstallationPath != null && g.InstallDir != null)
                    g.InstallDir = Path.Combine(gameInstallationPath, g.InstallDir);

                SimpleLogger.Instance.Info("[INFO] Found install directory \"" + g.InstallDir + "\" in 'appinfo.vdf'");

                var executables = app.Data.Traverse(d => d.Children).Where(d => d.Children.Any(c => c.Name == "executable")).ToArray();
                foreach (var exe in executables)
                {
                    var config = exe.Children.Where(c => c.Name == "config").SelectMany(c => c.Children).Where(c => c.Name == "oslist" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                    if ("windows".Equals(config))
                    {
                        var type = exe.Children.Where(c => c.Name == "type" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (type != "default")
                            SimpleLogger.Instance.Info("[INFO] No default 'type' found, using first executable found.");

                        g.Executable = exe.Children.Where(c => c.Name == "executable" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (!string.IsNullOrEmpty(g.Executable))
                        {
                            SimpleLogger.Instance.Info("[INFO] Game executable " + g.Executable + " found.");
                            return g;
                        }
                    }
                    else
                    {
                        SimpleLogger.Instance.Info("[INFO] No 'windows' specific config found, using first executable found.");

                        var type = exe.Children.Where(c => c.Name == "type" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (type != "default")
                            SimpleLogger.Instance.Info("[INFO] No default 'type' found, using first executable found.");

                        g.Executable = exe.Children.Where(c => c.Name == "executable" && c.Value != null).Select(c => c.Value.ToString()).FirstOrDefault();
                        if (!string.IsNullOrEmpty(g.Executable))
                        {
                            SimpleLogger.Instance.Info("[INFO] Game executable " + g.Executable + " found.");
                            return g;
                        }
                    }

                    SimpleLogger.Instance.Info("[WARNING] No game executable found, cannot put ES in Wait-mode.");
                }
            }
            catch { }

            return null;
        }

        private SteamAppInfoReader()
        {
            Apps = new List<SteamAppInfo>();
        }

        private const uint Magic28 = 0x07564428;
        private const uint Magic = 0x07564427;

        public List<SteamAppInfo> Apps { get; private set; }

        /// <summary>
        /// Opens and reads the given filename.
        /// </summary>
        /// <param name="filename">The file to open and read.</param>
        public void Read(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Read(fs);
        }

        /// <summary>
        /// Reads the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The input <see cref="Stream"/> to read from.</param>
        public void Read(Stream input)
        {
            using (var reader = new BinaryReader(input))
            {
                var magic = reader.ReadUInt32();

                if (magic != Magic && magic != Magic28)
                {
                    throw new InvalidDataException("Unknown magic header");
                }

                uint universe = reader.ReadUInt32();

                var deserializer = ValveKeyValue.KVSerializer.Create(ValveKeyValue.KVSerializationFormat.KeyValues1Binary);

                do
                {
                    var appid = reader.ReadUInt32();
                    if (appid == 0)
                        break;

                    uint size = reader.ReadUInt32(); // size until end of Data

                    var app = new SteamAppInfo
                    {
                        AppID = appid,
                        InfoState = reader.ReadUInt32(),
                        LastUpdated = DateTimeFromUnixTime(reader.ReadUInt32()),
                        Token = reader.ReadUInt64(),
                        Hash = reader.ReadBytes(20),
                        ChangeNumber = reader.ReadUInt32(),
                    };

                    uint remaining = size - 4 - 4 - 8 - 20 - 4;

                    if (magic == Magic28)
                    {
                        app.BinaryDataHash = reader.ReadBytes(20);
                        remaining = remaining - 20;
                    }

                    app.Data = deserializer.Deserialize(input);

                    Apps.Add(app);
                } while (true);
            }
        }

        public static DateTime DateTimeFromUnixTime(uint unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
        }
    }

    class SteamAppInfo
    {
        public uint AppID { get; set; }
        public uint InfoState { get; set; }
        public DateTime LastUpdated { get; set; }
        public ulong Token { get; set; }
        public byte[] Hash { get; set; }
        public byte[] BinaryDataHash { get; set; }
        public uint ChangeNumber { get; set; }
        public ValveKeyValue.KVObject Data { get; set; }
    }
        
}
