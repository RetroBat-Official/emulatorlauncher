using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;
using Microsoft.Win32;
using System.Runtime.Serialization;
using System.Threading;
using System.Data.SQLite;
using System.IO.Compression;

namespace emulatorLauncher
{
    class ExeLauncherGenerator : Generator
    {
        private bool _isSteam = false;
        private bool _isEpic = false;
        private bool _isAmazon = false;
        private string _epicexename;
        private string _amazonexename;

        static List<string> epicList = new List<string>() { "com.epicgames.launcher" };
        static List<string> steamList = new List<string>() { "steam" };
        static List<string> amazonList = new List<string>() { "amazon-games" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            rom = this.TryUnZipGameIfNeeded(system, rom);

            _systemName = system.ToLowerInvariant();

            string path = Path.GetDirectoryName(rom);
            string arguments = null;

            string extension = Path.GetExtension(rom);

            // Define if shortcut is an EpicGame or Steam shortcut
            if (extension == ".url")
            {
                var file = File.ReadAllLines(rom);
                if (file.Length == 0)
                    return null;

                string index = "URL=";
                string line = Array.Find(file, s => s.StartsWith(index));
                if (line == null)
                    return null;

                string url = line.Replace(index, "");

                _isAmazon = amazonList.Any(url.StartsWith);
                _isEpic = epicList.Any(url.StartsWith);
                _isSteam = steamList.Any(url.StartsWith);

                if (_isEpic)
                    _epicexename = GetEpicGameExecutableName(url);

                else if (_isAmazon)
                {
                    InstallSqlite();
                    _amazonexename = GetAmazonGameExecutableName(url);
                }

            }


            if (Directory.Exists(rom)) // If rom is a directory ( .pc .win .windows, .wine )
            {
                path = rom;

                if (File.Exists(Path.Combine(rom, "autorun.cmd")))
                    rom = Path.Combine(rom, "autorun.cmd");
                else if (File.Exists(Path.Combine(rom, "autorun.bat")))
                    rom = Path.Combine(rom, "autorun.bat");
                else if (File.Exists(Path.Combine(rom, "autoexec.cmd")))
                    rom = Path.Combine(rom, "autoexec.cmd");
                else if (File.Exists(Path.Combine(rom, "autoexec.bat")))
                    rom = Path.Combine(rom, "autoexec.bat");
                else
                    rom = Directory.GetFiles(path, "*.exe").FirstOrDefault();

                if (Path.GetFileName(rom) == "autorun.cmd")
                {
                    var wineCmd = File.ReadAllLines(rom);
                    if (wineCmd == null || wineCmd.Length == 0)
                        throw new Exception("autorun.cmd is empty");

                    var dir = wineCmd.Where(l => l.StartsWith("DIR=")).Select(l => l.Substring(4)).FirstOrDefault();

                    var wineCommand = wineCmd.Where(l => l.StartsWith("CMD=")).Select(l => l.Substring(4)).FirstOrDefault();
                    if (string.IsNullOrEmpty(wineCommand) && wineCmd.Length > 0)
                        wineCommand = wineCmd.FirstOrDefault();

                    var args = wineCommand.SplitCommandLine();
                    if (args.Length > 0)
                    {
                        string exe = string.IsNullOrEmpty(dir) ? Path.Combine(path, args[0]) : Path.Combine(path, dir.Replace("/", "\\"), args[0]);
                        if (File.Exists(exe))
                        {
                            rom = exe;

                            if (!string.IsNullOrEmpty(dir))
                            {
                                string customDir = Path.Combine(path, dir);
                                path = Directory.Exists(customDir) ? customDir : Path.GetDirectoryName(rom);
                            }
                            else
                                path = Path.GetDirectoryName(rom);

                            if (args.Length > 1)
                                arguments = string.Join(" ", args.Skip(1).ToArray());
                        }
                        else
                            throw new Exception("Invalid autorun.cmd executable");
                    }
                    else
                        throw new Exception("Invalid autorun.cmd command");
                }
            }

            if (!File.Exists(rom))
                return null;

            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(path, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            UpdateMugenConfig(path, resolution);

            var ret = new ProcessStartInfo()
            {
                FileName = rom,
                WorkingDirectory = path
            };

            if (arguments != null)
                ret.Arguments = arguments;

            string ext = Path.GetExtension(rom).ToLower();
            if (ext == ".bat" || ext == ".cmd")
            {
                ret.WindowStyle = ProcessWindowStyle.Hidden;
                ret.UseShellExecute = true;
            }
            else
                _exename = Path.GetFileNameWithoutExtension(rom);

            // If game was uncompressed, say we are going to launch, so the deletion will not be silent
            ValidateUncompressedGame();

            return ret;
        }

        private string _systemName;
        private string _exename;

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_isEpic)
                return PadToKey.AddOrUpdateKeyMapping(mapping, _epicexename, InputKey.hotkey | InputKey.start, "(%{KILL})");

            else if (_isAmazon)
                return PadToKey.AddOrUpdateKeyMapping(mapping, _amazonexename, InputKey.hotkey | InputKey.start, "(%{KILL})");

            else if (_systemName != "mugen" || string.IsNullOrEmpty(_exename))
                return mapping;

            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{KILL})");
        }

        private void UpdateMugenConfig(string path, ScreenResolution resolution)
        {
            if (_systemName != "mugen")
                return;

            var cfg = Path.Combine(path, "data", "mugen.cfg");
            if (!File.Exists(cfg))
                return;

            using (var ini = IniFile.FromFile(cfg, IniOptions.UseSpaces | IniOptions.AllowDuplicateValues | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                if (resolution == null)
                    resolution = ScreenResolution.CurrentResolution;

                if (!string.IsNullOrEmpty(ini.GetValue("Config", "GameWidth")))
                {
                    ini.WriteValue("Config", "GameWidth", resolution.Width.ToString());
                    ini.WriteValue("Config", "GameHeight", resolution.Height.ToString());
                }

                ini.WriteValue("Video", "Width", resolution.Width.ToString());
                ini.WriteValue("Video", "Height", resolution.Height.ToString());

                ini.WriteValue("Video", "VRetrace", SystemConfig["VSync"] != "false" ? "1" : "0");
                ini.WriteValue("Video", "FullScreen", "1");
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            if (_systemName == "windows")
            {
                using (var frm = new System.Windows.Forms.Form())
                {
                    // Some games fail to allocate DirectX surface if EmulationStation is showing fullscren : pop an invisible window between ES & the game solves the problem
                    frm.ShowInTaskbar = false;
                    frm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
                    frm.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                    frm.Opacity = 0;
                    frm.Show();

                    System.Windows.Forms.Application.DoEvents();

                    if (_isEpic)
                    {
                        Process process = Process.Start(path);

                        int i = 1;
                        Process[] game = Process.GetProcessesByName(_epicexename);

                        while (i <= 5 && game.Length == 0)
                        {
                            game = Process.GetProcessesByName(_epicexename);
                            Thread.Sleep(4000);
                            i++;
                        }
                        Process[] epic = Process.GetProcessesByName("EpicGamesLauncher");

                        if (game.Length == 0)
                            return 0;
                        else
                        {
                            Process epicGame = game.OrderBy(p => p.StartTime).FirstOrDefault();
                            Process epicLauncher = null;

                            if (epic.Length > 0)
                                epicLauncher = epic.OrderBy(p => p.StartTime).FirstOrDefault();

                            epicGame.WaitForExit();

                            if (SystemConfig.isOptSet("notkillsteam") && SystemConfig.getOptBoolean("notkillsteam"))
                                return 0;
                            else if (epicLauncher != null)
                                epicLauncher.Kill();
                        }
                        return 0;
                    }

                    else if (_isAmazon)
                    {
                        Process process = Process.Start(path);

                        int i = 1;
                        Process[] game = Process.GetProcessesByName(_amazonexename);

                        while (i <= 5 && game.Length == 0)
                        {
                            game = Process.GetProcessesByName(_amazonexename);
                            Thread.Sleep(4000);
                            i++;
                        }
                        Process[] amazon = Process.GetProcessesByName("Amazon Games UI");

                        if (game.Length == 0)
                            return 0;
                        else
                        {
                            Process amazonGame = game.OrderBy(p => p.StartTime).FirstOrDefault();
                            Process amazonLauncher = null;

                            if (amazon.Length > 0)
                                amazonLauncher = amazon.OrderBy(p => p.StartTime).FirstOrDefault();

                            amazonGame.WaitForExit();

                            if (SystemConfig.isOptSet("notkillsteam") && SystemConfig.getOptBoolean("notkillsteam"))
                                return 0;
                            else if (amazonLauncher != null)
                                amazonLauncher.Kill();
                        }
                        return 0;
                    }
                    else
                        base.RunAndWait(path);
                }
            }
            else
                base.RunAndWait(path);

            return 0;
        }

        #region epic
        private string GetEpicGameExecutableName(string url)
        {
            string toRemove = "com.epicgames.launcher://apps/";
            string shorturl = url.Replace(toRemove, "");

            int index = shorturl.IndexOf('%');
            if (index > 0)
                shorturl = shorturl.Substring(0, index);
            else
                return null;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Epic Games\\EOS"))
                {
                    if (key != null)
                    {
                        Object o = key.GetValue("ModSdkMetadataDir");
                        if (o != null)
                        {
                            string manifestPath = o.ToString();

                            List<EpicGames> games = new List<EpicGames>();

                            foreach (var file in Directory.EnumerateFiles(manifestPath, "*.item"))
                            {
                                var rr = JsonSerializer.DeserializeString<EpicGames>(File.ReadAllText(file));
                                if (rr != null)
                                    games.Add(rr);
                            }

                            string gameExecutable = null;

                            if (games.Count > 0)
                                gameExecutable = games.Where(i => i.CatalogNamespace == shorturl).Select(i => i.LaunchExecutable).FirstOrDefault();

                            if (gameExecutable != null)
                                return Path.GetFileNameWithoutExtension(gameExecutable);
                        }
                    }
                }
            }
            catch
            {
                throw new ApplicationException("There is a problem: Epic Launcher is not installed or the Game is not installed");
            }
            return null;
        }

        [DataContract]
        public class EpicGames
        {
            [DataMember]
            public string CatalogNamespace { get; set; }

            [DataMember]
            public string LaunchExecutable { get; set; }
        }
        #endregion

        #region amazon
        private string GetAmazonGameExecutableName(string url)
        {
            string toRemove = "amazon-games://play/";
            string shorturl = url.Replace(toRemove, "");

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string amazonDB = Path.Combine(appData, "Amazon Games", "Data", "Games", "Sql", "GameInstallInfo.sqlite");

                if (File.Exists(amazonDB))
                {
                    SQLiteConnection sqlite_conn = new SQLiteConnection("Data Source = " + amazonDB);
                    sqlite_conn.Open();

                    SQLiteDataReader sqlite_datareader;
                    SQLiteCommand sqlite_cmd;
                    sqlite_cmd = sqlite_conn.CreateCommand();
                    sqlite_cmd.CommandText = "SELECT installDirectory FROM DbSet where Id = '" + shorturl + "'";

                    sqlite_datareader = sqlite_cmd.ExecuteReader();

                    if (!sqlite_datareader.HasRows)
                    {
                        sqlite_conn.Close();
                        throw new ApplicationException("There is a problem: the Game is not installed in Amazon Launcher");
                    }

                    else
                    {
                        string gameInstallPath = null;
                        while (sqlite_datareader.Read())
                        {
                            gameInstallPath = sqlite_datareader.GetString(0);
                        }
                        sqlite_conn.Close();
                        return GetAmazonGameExecutable(gameInstallPath);
                    }
                }
            }
            catch
            {
                throw new ApplicationException("There is a problem: Amazon Launcher is not installed or the Game is not installed");
            }
            return null;
        }

        private string GetAmazonGameExecutable(string path)
        {
            string fuelFile = Path.Combine(path, "fuel.json");
            string gameexe = null;

            if (!File.Exists(fuelFile))
            {
                throw new ApplicationException("There is a problem: game executable cannot be found");
            }

            var json = DynamicJson.Load(fuelFile);
            var jsonMain = json.GetObject("Main");

            if (jsonMain != null)
                gameexe = jsonMain["Command"];
            else
                return null;
            
            string amazonGameExecutable = Path.GetFileNameWithoutExtension(gameexe);

            if (amazonGameExecutable != null)
                return amazonGameExecutable;
            else
                return null;
        }

        private static void InstallSqlite()
        {
            string dllName = Path.Combine(Path.GetDirectoryName(typeof(ConfigFile).Assembly.Location), "SQLite.Interop.dll");
            int platform = IntPtr.Size;

            if (File.Exists(dllName))
            {
                try { File.Delete(dllName); }
                catch { }
            }

            if (platform == 4)
                GZipBytesToFile(Properties.Resources.SQLite_Interop_x86_gz, dllName);
            else
                GZipBytesToFile(Properties.Resources.SQLite_Interop_x64_gz, dllName);
        }
        #endregion

        static bool GZipBytesToFile(byte[] bytes, string fileName)
        {
            try
            {
                using (var reader = new MemoryStream(bytes))
                {
                    using (var decompressedStream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                    {
                        using (GZipStream decompressionStream = new GZipStream(reader, CompressionMode.Decompress))
                        {
                            decompressionStream.CopyTo(decompressedStream);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[ReadGZipStream] Failed " + ex.Message, ex);
            }

            return false;
        }
    }
}
