using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    class RetrobatLauncherGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!File.Exists(rom))
                return null;

            if (Path.GetExtension(rom).ToLower() != ".menu")
                return null;

            string[] lines = File.ReadAllLines(rom);
            if (lines.Length == 0)
                return null;

            rom = lines[0];
            string folder = rom.ExtractString("\\", "\\");
            if (string.IsNullOrEmpty(folder))
                return null;
            
            Installer installer = Installer.GetInstaller(folder);
            if (installer != null && !installer.IsInstalled() && installer.CanInstall())
            {
                using (InstallerFrm frm = new InstallerFrm(installer))
                    if (frm.ShowDialog() != DialogResult.OK)
                        return null;            
            }

            bool updatesEnabled = !SystemConfig.isOptSet("updates.enabled") || SystemConfig.getOptBoolean("updates.enabled");

            if (installer != null)
            {
                if (updatesEnabled && installer.HasUpdateAvailable() && installer.CanInstall())
                {
                    SimpleLogger.Instance.Info("[Startup] Emulator update found : proposing to update.");
                    using (InstallerFrm frm = new InstallerFrm(installer))
                        if (frm.ShowDialog() != DialogResult.OK)
                            return null;
                }
            }

            string fullPath = AppConfig.GetFullPath(folder);
            if (string.IsNullOrEmpty(fullPath))
                return null;

            string path = Path.GetDirectoryName(fullPath) + rom;
            if (!File.Exists(path))
                return null;

            DisableEmulatorUpdateCheck(folder, Path.GetDirectoryName(path));

            var ret = new ProcessStartInfo()
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path),
                UseShellExecute = false
            };
            
            if (lines.Length > 1)
                ret.Arguments = lines[1];

            // gopher64 checks GitHub at each GUI start without option (gui.rs, check_latest_version) : the check is skipped for itch.io builds only
            if (folder.Equals("gopher64", StringComparison.InvariantCultureIgnoreCase))
                ret.EnvironmentVariables["ITCHIO_APP"] = "1";

            return ret;
        }

        #region Emulator update check
        private enum ConfigFormat { Ini, Toml, Conf, Yml, Json, Xml }

        private class UpdateSetting
        {
            public ConfigFormat Format;
            public string File;                     // Relative to the emulator folder, "{saves}\..." for the RetroBat saves folder
            public string Section;                  // Ini / toml section, json container (null : root)
            public string Key;
            public string Value;
            public IniOptions Options;
            public bool CreateFile = true;          // false : the emulator resets a partial file (settings version, full json schema)
            public bool OnlyIfMissing;
        }

        private static UpdateSetting Ini(string file, string section, string key, string value, IniOptions options = 0, bool createFile = true, bool onlyIfMissing = false)
        {
            return new UpdateSetting { Format = ConfigFormat.Ini, File = file, Section = section, Key = key, Value = value, Options = options, CreateFile = createFile, OnlyIfMissing = onlyIfMissing };
        }

        private static UpdateSetting Other(ConfigFormat format, string file, string section, string key, string value, bool createFile = true)
        {
            return new UpdateSetting { Format = format, File = file, Section = section, Key = key, Value = value, CreateFile = createFile };
        }

        private const IniOptions SpacesDuplicates = IniOptions.UseSpaces | IniOptions.AllowDuplicateValues;

        /// <summary>
        /// Emulators started from the RetroBat menu don't go through their generator, which disables their own update check at each game launch.
        /// Same settings as the generators : templates can be missing (removed by the user) or the emulator never launched from a game yet.
        /// </summary>
        private static readonly Dictionary<string, UpdateSetting[]> _updateSettings = new Dictionary<string, UpdateSetting[]>(StringComparer.InvariantCultureIgnoreCase)
        {
            // ArcadeDuck resets a settings.ini without SettingsVersion and runs its setup wizard (SETTINGS_VERSION in qthost.cpp)
            { "arcadeduck", new[] { Ini("settings.ini", "Main", "SettingsVersion", "3", SpacesDuplicates, onlyIfMissing: true),
                                    Ini("settings.ini", "AutoUpdater", "CheckAtStartup", "false", SpacesDuplicates) } },
            { "amiberry", new[] { Other(ConfigFormat.Conf, "Settings\\amiberry.conf", null, "update_check", "0") } },
            { "azahar", new[] { Ini("user\\config\\qt-config.ini", "Miscellaneous", "check_for_update_on_start\\default", "false"),
                                Ini("user\\config\\qt-config.ini", "Miscellaneous", "check_for_update_on_start", "false") } },
            { "azahar-plus", new[] { Ini("user\\config\\qt-config.ini", "Miscellaneous", "check_for_update_on_start\\default", "false"),
                                     Ini("user\\config\\qt-config.ini", "Miscellaneous", "check_for_update_on_start", "false") } },
            { "bizhawk", new[] { Other(ConfigFormat.Json, "config.ini", null, "UpdateAutoCheckEnabled", "false", createFile: false) } },
            { "cemu", new[] { Other(ConfigFormat.Xml, "portable\\settings.xml", null, "check_update", "false") } },
            { "citra", new[] { Ini("user\\config\\qt-config.ini", "UI", "Updater\\check_for_update_on_start\\default", "false"),
                               Ini("user\\config\\qt-config.ini", "UI", "Updater\\check_for_update_on_start", "false") } },
            { "lime3ds", new[] { Ini("user\\config\\qt-config.ini", "UI", "Updater\\check_for_update_on_start\\default", "false"),
                                 Ini("user\\config\\qt-config.ini", "UI", "Updater\\check_for_update_on_start", "false") } },
            { "mandarine", new[] { Ini("user\\config\\qt-config.ini", "UI", "Updater\\check_for_update_on_start\\default", "false"),
                                   Ini("user\\config\\qt-config.ini", "UI", "Updater\\check_for_update_on_start", "false") } },
            { "dolphin-emu", new[] { Ini("User\\Config\\Dolphin.ini", "AutoUpdate", "UpdateTrack", "''", IniOptions.UseSpaces | IniOptions.KeepEmptyValues) } },
            { "dolphin-triforce", new[] { Ini("User\\Config\\Dolphin.ini", "AutoUpdate", "UpdateTrack", "''", IniOptions.UseSpaces | IniOptions.KeepEmptyValues) } },
            { "duckstation", new[] { Ini("settings.ini", "AutoUpdater", "CheckAtStartup", "false", IniOptions.UseSpaces, createFile: false) } },
            { "eden", new[] { Ini("user\\config\\qt-config.ini", "UI", "check_for_updates\\default", "false", IniOptions.KeepEmptyValues),
                              Ini("user\\config\\qt-config.ini", "UI", "check_for_updates", "false", IniOptions.KeepEmptyValues) } },
            { "mesen", new[] { Other(ConfigFormat.Json, "settings.json", "Preferences", "AutomaticallyCheckForUpdates", "false") } },
            { "mgba", new[] { Ini("config.ini", "ports.qt", "updateAutoCheck", "0") } },
            { "mupen64", new[] { Ini("Config\\mupen64plus.cfg", "Rosalie's Mupen GUI", "CheckForUpdates", "False", IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines) } },
            { "pcsx2", new[] { Ini("inis\\PCSX2.ini", "AutoUpdater", "CheckAtStartup", "false", SpacesDuplicates, createFile: false) } },
            { "pcsx2x6", new[] { Ini("inis\\PCSX2.ini", "AutoUpdater", "CheckAtStartup", "false", SpacesDuplicates, createFile: false) } },
            { "ppsspp", new[] { Ini("{saves}\\psp\\SYSTEM\\ppsspp.ini", "General", "CheckForNewVersion", "False", IniOptions.UseSpaces, createFile: false) } },
            { "rpcs3", new[] { Ini("GuiConfigs\\CurrentSettings.ini", "Meta", "checkUpdateStart", "false") } },
            { "ryujinx", new[] { Other(ConfigFormat.Json, "portable\\Config.json", null, "check_updates_on_start", "false", createFile: false),
                                 Other(ConfigFormat.Json, "portable\\Config.json", null, "update_checker_type", "Off", createFile: false) } },
            { "scummvm", new[] { Ini("scummvm.ini", "scummvm", "updates_check", "0") } },
            { "shadps4", new[] { Ini("launcher\\qt_ui.ini", "general_settings", "checkForUpdates", "false") } },
            { "vita3k", new[] { Other(ConfigFormat.Yml, "config.yml", null, "check-for-updates-mode", "0") } },     // UPDATE_STARTUP_OFF
            { "xemu", new[] { Other(ConfigFormat.Toml, "xemu.toml", "general.updates", "check", "false") } },
            { "xemu-chihiro", new[] { Other(ConfigFormat.Toml, "xemu.toml", "general.updates", "check", "false") } },
            // Also disables the update check of the Xenia builds (same option, MainWindow.Control_OnLoaded)
            { "xenia-manager", new[] { Other(ConfigFormat.Json, "Config\\config.json", "update_checks", "check_for_updates_on_startup", "false") } },
            { "ymir", new[] { Ini("Ymir.toml", "General", "CheckForUpdates", "false") } },
        };

        private void DisableEmulatorUpdateCheck(string folder, string emulatorPath)
        {
            UpdateSetting[] settings;
            if (!_updateSettings.TryGetValue(folder, out settings))
                return;

            foreach (var group in settings.GroupBy(s => s.File))
            {
                string file = GetUpdateConfigFile(folder, emulatorPath, group.Key);

                try
                {
                    if (!File.Exists(file) && !group.First().CreateFile)
                    {
                        SimpleLogger.Instance.Info("[RetrobatLauncher] " + file + " not found, " + folder + " update check left unchanged.");
                        continue;
                    }

                    FileTools.TryCreateDirectory(Path.GetDirectoryName(file));
                    WriteUpdateSettings(file, group.ToList());
                    SimpleLogger.Instance.Info("[RetrobatLauncher] " + folder + " update check disabled in " + file);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[RetrobatLauncher] Unable to disable the " + folder + " update check in " + file + " : " + ex.Message);
                }
            }
        }

        private string GetUpdateConfigFile(string folder, string emulatorPath, string file)
        {
            if (file.StartsWith("{saves}\\"))
                return Path.Combine(AppConfig.GetFullPath("saves"), file.Substring("{saves}\\".Length));

            // Vita3K portable mode : a 'portable' folder next to Vita3K.exe holds config.yml
            if (folder.Equals("vita3k", StringComparison.InvariantCultureIgnoreCase) && Directory.Exists(Path.Combine(emulatorPath, "portable")))
                return Path.Combine(emulatorPath, "portable", file);

            return Path.Combine(emulatorPath, file);
        }

        private static void WriteUpdateSettings(string file, List<UpdateSetting> settings)
        {
            switch (settings[0].Format)
            {
                case ConfigFormat.Ini:
                    using (var ini = new IniFile(file, settings[0].Options))
                    {
                        foreach (var s in settings)
                        {
                            if (!s.OnlyIfMissing || string.IsNullOrEmpty(ini.GetValue(s.Section, s.Key)))
                                ini.WriteValue(s.Section, s.Key, s.Value);
                        }
                    }
                    break;

                case ConfigFormat.Toml:
                    using (var toml = new IniTomlFile(file, IniTomlOptions.KeepEmptyLines | IniTomlOptions.UseSpaces))
                    {
                        foreach (var s in settings)
                            toml.WriteValue(s.Section, s.Key, s.Value);
                    }
                    break;

                case ConfigFormat.Conf:
                    foreach (var s in settings)
                        WriteConfValue(file, s.Key, s.Value);
                    break;

                case ConfigFormat.Yml:
                    {
                        // Missing keys are set to their default value by the emulator
                        var yml = YmlFile.Load(file);
                        foreach (var s in settings)
                            yml[s.Key] = s.Value;
                        yml.Save();
                    }
                    break;

                case ConfigFormat.Json:
                    {
                        var json = DynamicJson.Load(file);
                        foreach (var s in settings)
                        {
                            var container = string.IsNullOrEmpty(s.Section) ? json : json.GetOrCreateContainer(s.Section);
                            container[s.Key] = s.Value;
                        }
                        json.Save();
                    }
                    break;

                case ConfigFormat.Xml:
                    {
                        var xdoc = File.Exists(file) ? XElement.Load(file) : new XElement("content");
                        foreach (var s in settings)
                            xdoc.SetElementValue(s.Key, s.Value);
                        xdoc.Save(file);
                    }
                    break;
            }
        }

        /// <summary>
        /// Writes a "key=value" line, replacing the existing one (Amiberry .conf format).
        /// </summary>
        private static void WriteConfValue(string file, string key, string value)
        {
            var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : new List<string>();

            int index = lines.FindIndex(l =>
            {
                int sep = l.IndexOf('=');
                return sep > 0 && l.Substring(0, sep).Trim().Equals(key, StringComparison.InvariantCultureIgnoreCase);
            });

            if (index >= 0)
                lines[index] = key + "=" + value;
            else
                lines.Add(key + "=" + value);

            File.WriteAllLines(file, lines);
        }
        #endregion
    }
}
