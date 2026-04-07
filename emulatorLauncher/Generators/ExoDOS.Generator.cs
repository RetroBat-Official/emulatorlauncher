using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    class exoDOSGenerator : Generator
    {
        public exoDOSGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            return new ProcessStartInfo()
            {
                FileName = rom
            };
        }

        public static void UpdateGames()
        {
            try
            {
                string exoDOSPath = Path.GetDirectoryName(Program.SystemConfig["exodosPath"]);
                if (!Directory.Exists(exoDOSPath))
                {
                    SimpleLogger.Instance.Error("[ExoDOS] Invalid ExoDOS path.");
                    return;
                }

                CleanExoDOSScripts(exoDOSPath);

                string baseInstalledGamesPath = Path.Combine(exoDOSPath, "eXo", "eXoDOS");

                if (!Directory.Exists(baseInstalledGamesPath))
                    return;

                var games = GetGames(baseInstalledGamesPath);

                if (games.Count == 0)
                    return;

                CreateShortcuts(games);
            }
            catch { }
        }

        private static List<ExoDosGame> GetGames(string baseInstalledGamesPath)
        {
            var games = new List<ExoDosGame>();

            var folders = Directory.EnumerateDirectories(baseInstalledGamesPath)
                .Where(d => !Path.GetFileName(d).StartsWith("!"));

            foreach (var dir in folders)
            {
                string dirName = Path.GetFileName(dir);
                var gameBatPath = Path.Combine(baseInstalledGamesPath, "!dos", dirName);

                if (!Directory.Exists(gameBatPath))
                    continue;

                var batFile = Directory.EnumerateFiles(gameBatPath, "*.bat", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => !string.Equals(Path.GetFileName(f), "install.bat", StringComparison.OrdinalIgnoreCase));

                if (batFile == null)
                    continue;

                games.Add(new ExoDosGame
                {
                    Name = Path.GetFileNameWithoutExtension(batFile),
                    BatPath = batFile,
                    WorkingDirectory = gameBatPath
                });
            }

            return games;
        }

        private static void CreateShortcuts(List<ExoDosGame> games)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));

            foreach (var game in games)
            {
                try
                {
                    dynamic shortcut = shell.CreateShortcut(
                        Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "roms", "exodos", game.Name + ".lnk"));

                    shortcut.TargetPath = game.BatPath;
                    shortcut.WorkingDirectory = game.WorkingDirectory;
                    shortcut.Save();

                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }
                catch { }
            }

            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }

        class ExoDosGame
        {
            public string Name { get; set; }
            public string BatPath { get; set; }
            public string WorkingDirectory { get; set; }
        }

        private static void CleanExoDOSScripts(string exoDOSPath)
        {
            string exoDosScriptsPath = Path.Combine(exoDOSPath, "eXo", "util");
            if (!Directory.Exists(exoDosScriptsPath))
                return;

            var ipScript = Path.Combine(exoDosScriptsPath, "ip.bat");
            if (File.Exists(ipScript))
            {
                var content = File.ReadAllText(ipScript);

                // Add -UseBasicParsing to any Invoke-WebRequest missing it
                content = Regex.Replace(
                    content,
                    @"(Invoke-WebRequest\b(?![^)]*-UseBasicParsing)[^)]*)\)",
                    "$1 -UseBasicParsing)",
                    RegexOptions.IgnoreCase
                );

                File.WriteAllText(ipScript, content);
            }
        }
    }
}
