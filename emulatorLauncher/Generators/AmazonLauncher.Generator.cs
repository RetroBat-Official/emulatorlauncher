using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Data.SQLite;
using System.IO.Compression;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        class AmazonGameLauncher : GameLauncher
        {
            public AmazonGameLauncher(Uri uri)
            {                
                LauncherExe = GetAmazonGameExecutableName(uri);
            }

            private string GetAmazonGameExecutableName(Uri uri)
            {
                string shorturl = uri.AbsolutePath.Substring(1);

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string amazonDB = Path.Combine(appData, "Amazon Games", "Data", "Games", "Sql", "GameInstallInfo.sqlite");

                if (File.Exists(amazonDB))
                {
                    string gameInstallPath = null;

                    InstallSQLiteInteropDll();

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

                    var exe = GetAmazonGameExecutable(gameInstallPath);
                    if (string.IsNullOrEmpty(exe))
                        throw new ApplicationException("There is a problem: Game is not installed");

                    return exe;
                }

                throw new ApplicationException("There is a problem: Amazon Launcher is not installed or the Game is not installed");
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
                bool uiExists = Process.GetProcessesByName("Amazon Games UI").Any();

                KillExistingLauncherExes();

                Process.Start(path);

                var amazonGame = GetLauncherExeProcess();
                if (amazonGame != null)
                {
                    amazonGame.WaitForExit();

                    if (!uiExists || (Program.SystemConfig.isOptSet("killsteam") && Program.SystemConfig.getOptBoolean("killsteam")))
                    {
                        foreach (var ui in Process.GetProcessesByName("Amazon Games UI"))
                        {
                            try { ui.Kill(); }
                            catch { }
                        }
                    }                
                }

                return 0;
            }
        }
    }
}
