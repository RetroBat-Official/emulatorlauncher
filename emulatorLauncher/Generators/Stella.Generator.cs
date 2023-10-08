using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Data.SQLite;
using System.Diagnostics.Eventing.Reader;

namespace emulatorLauncher
{
    class StellaGenerator : Generator
    {
        public StellaGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("stella");

            string exe = Path.Combine(path, "Stella.exe");
            if (!File.Exists(exe))
                return null;

            var commandArray = new List<string>();
            commandArray.Add("-baseinappdir");
            ConfigureCommandLineArguments(commandArray);
            commandArray.Add("\"" + rom + "\"");
            string args = string.Join(" ", commandArray);

            ConfigureSQL(path);

            if (SystemConfig["stella_renderer"] != "opengl")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            else
                ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution);

            _resolution = resolution;

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void ConfigureCommandLineArguments(List<string> commandArray)
        {
            commandArray.Add("-fullscreen");
            commandArray.Add("1");
        }

        private void ConfigureSQL(string path)
        {
            string configdb = Path.Combine(path, "stella.sqlite3");

            if (File.Exists(configdb))
            {
                InstallSQLiteInteropDll();

                using (var db = new SQLiteConnection("Data Source = " + configdb))
                {
                    db.Open();

                    ForceStellaSetting(db, "grabmouse", "1");
                    ForceStellaSetting(db, "usemouse", "analog");

                    string snapDir = Path.Combine(AppConfig.GetFullPath("screenshots"), "stella");
                    if (!Directory.Exists(snapDir)) try { Directory.CreateDirectory(snapDir); }
                        catch { }
                    ForceStellaSetting(db, "snapsavedir", snapDir);
                    ForceStellaSetting(db, "snapname", "rom");

                    SetStellaSetting(db, "video", "stella_renderer", "direct3d");
                    SetStellaSetting(db, "vsync", "stella_vsync", "1");
                    SetStellaSetting(db, "display", "stella_monitor", "0");
                    
                    db.Close();
                }
            }
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

        private static void SetStellaSetting(SQLiteConnection db, string setting, string feature, string defaultValue)
        {
            var cmd = db.CreateCommand();

            if (Program.SystemConfig.isOptSet(feature) && !string.IsNullOrEmpty(Program.SystemConfig[feature]))
                cmd.CommandText = "UPDATE settings SET value = '" + Program.SystemConfig[feature] + "' where setting = '" + setting + "'";
            else
                cmd.CommandText = "UPDATE settings SET value = '" + defaultValue + "' where setting = '" + setting + "'";
            cmd.ExecuteNonQuery();
        }

        private static void ForceStellaSetting(SQLiteConnection db, string setting, string value)
        {
            var cmd = db.CreateCommand();
            cmd.CommandText = "UPDATE settings SET value = '" + value + "' where setting = '" + setting + "'";
            cmd.ExecuteNonQuery();
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            return ret;
        }

    }
}
