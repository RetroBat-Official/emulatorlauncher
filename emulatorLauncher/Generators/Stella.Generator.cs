﻿using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Data.SQLite;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class StellaGenerator : Generator
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

            var commandArray = new List<string>
            {
                "-baseinappdir"
            };
            ConfigureCommandLineArguments(commandArray);
            commandArray.Add("\"" + rom + "\"");
            string args = string.Join(" ", commandArray);

            ConfigureSQL(path);

            bool stretch = SystemConfig.isOptSet("stella_stretch") && SystemConfig.getOptBoolean("stella_stretch");

            if (!stretch)
            {
                if (SystemConfig["stella_renderer"] != "opengl")
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                else
                    ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator);
            }
            else
                SystemConfig["forceNoBezel"] = "1";

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
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            commandArray.Add("-fullscreen");
            commandArray.Add(fullscreen ? "1" : "0");

        }

        private void ConfigureSQL(string path)
        {
            string configdb = Path.Combine(path, "stella.sqlite3");

            if (File.Exists(configdb))
            {
                SQLiteInteropManager.InstallSQLiteInteropDll();

                using (var db = new SQLiteConnection("Data Source = " + configdb))
                {
                    db.Open();

                    ForceStellaSetting(db, "grabmouse", "1");
                    ForceStellaSetting(db, "confirmexit", "0");

                    string snapDir = Path.Combine(AppConfig.GetFullPath("screenshots"), "stella");
                    if (!Directory.Exists(snapDir)) try { Directory.CreateDirectory(snapDir); }
                        catch { }
                    ForceStellaSetting(db, "snapsavedir", snapDir);
                    ForceStellaSetting(db, "snapname", "rom");
                    ForceStellaSetting(db, "autoslot", "1");

                    SetStellaBoolSetting(db, "tia.correct_aspect", "stella_correct_aspect", "1", "0", "1");
                    SetStellaBoolSetting(db, "tia.fs_stretch", "stella_stretch", "1", "0", "0");
                    SetStellaBoolSetting(db, "tia.fs_refresh", "stella_adapt_refresh", "1", "0", "0");
                    SetStellaSetting(db, "tia.fs_overscan", "stella_overscan", "Off");
                    SetStellaBoolSetting(db, "tia.inter", "stella_interpolation", "1", "0", "0");
                    SetStellaSetting(db, "tv.filter", "stella_tvfilter", "0");
                    SetStellaSetting(db, "tv.scanlines", "stella_scanlines", "Off");
                    SetStellaSetting(db, "video", "stella_renderer", "direct3d");
                    SetStellaBoolSetting(db, "vsync", "stella_vsync", "1", "0", "1");
                    SetStellaSetting(db, "display", "stella_monitor", "0");
                    SetStellaSetting(db, "audio.preset", "stella_audio_quality", "3");
                    SetStellaBoolSetting(db, "audio.stereo", "stella_force_stereo", "1", "0", "0");
                    SetStellaBoolSetting(db, "threads", "stella_multithread", "1", "0", "0");
                    SetStellaBoolSetting(db, "fastscbios", "stella_fastload", "1", "0", "1");
                    SetStellaBoolSetting(db, "uimessages", "stella_uimessages", "1", "0", "1");
                    SetStellaBoolSetting(db, "saveonexit", "stella_autosave", "current", "none", "none");
                    SetStellaSetting(db, "usemouse", "stella_mouse", "analog");
                    SetStellaSetting(db, "cursor", "stella_cursor", "2");
                    SetStellaSetting(db, "adeadzone", "stella_deadzone", "3");
                    SetStellaBoolSetting(db, "joyallow4", "stella_fourway", "1", "0", "1");

                    if (!SystemConfig.isOptSet("stella_autofire") || SystemConfig["stella_autofire"] == "0")
                    {
                        ForceStellaSetting(db, "autofire", "0");
                        ForceStellaSetting(db, "autofirerate", "0");
                    }
                    else
                    {
                        ForceStellaSetting(db, "autofire", "1");
                        ForceStellaSetting(db, "autofirerate", SystemConfig["stella_autofire"]);
                    }

                    CreateControllerConfiguration(db);

                    db.Close();
                }
            }
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

        private static void SetStellaBoolSetting(SQLiteConnection db, string setting, string feature, string trueValue, string falseValue, string defaultValue)
        {
            var cmd = db.CreateCommand();

            if (Program.SystemConfig.isOptSet(feature))
            {
                if (Program.SystemConfig.getOptBoolean(feature))
                    cmd.CommandText = "UPDATE settings SET value = '" + trueValue + "' where setting = '" + setting + "'";
                else
                    cmd.CommandText = "UPDATE settings SET value = '" + falseValue + "' where setting = '" + setting + "'";
            }
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

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
        }

    }
}
