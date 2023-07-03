using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Threading;
using System.Data.SQLite;
using System.IO.Compression;

namespace emulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class AmazonGameLauncher : GameLauncher
        {
            public AmazonGameLauncher(Uri uri)
            {
                string url = uri.ToString();
                InstallSqlite();

                _LauncherExeName = GetAmazonGameExecutableName(url);
            }

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

            public override int RunAndWait(ProcessStartInfo path)
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

                    Process process = Process.Start(path);

                    int i = 1;
                    Process[] game = Process.GetProcessesByName(_LauncherExeName);

                    while (i <= 5 && game.Length == 0)
                    {
                        game = Process.GetProcessesByName(_LauncherExeName);
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

                        if (Program.SystemConfig.isOptSet("notkillsteam") && Program.SystemConfig.getOptBoolean("notkillsteam"))
                            return 0;
                        else if (amazonLauncher != null)
                            amazonLauncher.Kill();
                    }
                    return 0;
                }
            }
        }
    }
}
