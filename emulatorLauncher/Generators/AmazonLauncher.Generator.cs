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
                LauncherExe = GetAmazonGameExecutableName(uri.ToString());
                InstallSQLiteInteropDll();
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
                        string gameInstallPath = null;

                        using (var db = new SQLiteConnection("Data Source = " + amazonDB))
                        {
                            db.Open();

                            var cmd = db.CreateCommand();                            
                            cmd.CommandText = "SELECT installDirectory FROM DbSet where Id = '" + shorturl + "'";

                            var sqlite_datareader = cmd.ExecuteReader();

                            if (!sqlite_datareader.HasRows)
                            {
                                db.Close();
                                throw new ApplicationException("There is a problem: the Game is not installed in Amazon Launcher");
                            }

                            while (sqlite_datareader.Read())
                                gameInstallPath = sqlite_datareader.GetString(0);

                            db.Close();
                        }

                        return GetAmazonGameExecutable(gameInstallPath);
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
                    throw new ApplicationException("There is a problem: game executable cannot be found");

                var json = DynamicJson.Load(fuelFile);

                var jsonMain = json.GetObject("Main");
                if (jsonMain == null)
                    return null;
                                
                gameexe = jsonMain["Command"];

                string amazonGameExecutable = Path.GetFileNameWithoutExtension(gameexe);
                if (!string.IsNullOrEmpty(amazonGameExecutable))
                    return amazonGameExecutable;

                return null;
            }

            private static void InstallSQLiteInteropDll()
            {
                string dllName = Path.Combine(Path.GetDirectoryName(typeof(ConfigFile).Assembly.Location), "SQLite.Interop.dll");
                int platform = IntPtr.Size;

                if (File.Exists(dllName) && Kernel32.IsX64(dllName) == (IntPtr.Size == 8))
                    return;

                if (File.Exists(dllName))
                {
                    try { File.Delete(dllName); }
                    catch { }
                }

                FileTools.ExtractGZipBytes(IntPtr.Size == 8 ? Properties.Resources.SQLite_Interop_x64_gz : Properties.Resources.SQLite_Interop_x86_gz, dllName);
            }

            public override int RunAndWait(ProcessStartInfo path)
            {
                Process process = Process.Start(path);

                int i = 1;
                Process[] game = Process.GetProcessesByName(LauncherExe);

                while (i <= 5 && game.Length == 0)
                {
                    game = Process.GetProcessesByName(LauncherExe);
                    Thread.Sleep(4000);
                    i++;
                }

                Process[] amazon = Process.GetProcessesByName("Amazon Games UI");

                if (game.Length == 0)
                    return 0;

                Process amazonGame = game.OrderBy(p => p.StartTime).FirstOrDefault();
                Process amazonLauncher = null;

                if (amazon.Length > 0)
                    amazonLauncher = amazon.OrderBy(p => p.StartTime).FirstOrDefault();

                amazonGame.WaitForExit();

                if (Program.SystemConfig.isOptSet("notkillsteam") && Program.SystemConfig.getOptBoolean("notkillsteam"))
                    return 0;
                
                if (amazonLauncher != null)
                    amazonLauncher.Kill();                

                return 0;
            }
        }
    }
}
