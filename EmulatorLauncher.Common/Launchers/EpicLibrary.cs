﻿using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Launchers.Epic;
using Microsoft.Win32;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EmulatorLauncher.Common.Launchers
{
    public class EpicLibrary
    {
        const string GameLaunchUrl = @"com.epicgames.launcher://apps/{0}?action=launch&silent=true";

        public static string GetEpicGameExecutableName(Uri uri)
        {
            string shorturl = Regex.Replace(uri.LocalPath, @"[^a-zA-Z0-9]", "");

            var modSdkMetadataDir = GetMetadataPath();
            if (modSdkMetadataDir != null)
            {
                string manifestPath = modSdkMetadataDir.ToString();

                string gameExecutable = null;

                if (Directory.Exists(manifestPath))
                {
                    foreach (var manifest in GetInstalledManifests())
                    {
                        if (shorturl.Equals(manifest.AppName))
                        {
                            gameExecutable = manifest.LaunchExecutable;
                            break;
                        }
                        else if (shorturl.Equals(manifest.MainGameAppName))
                        {
                            gameExecutable = manifest.LaunchExecutable;
                            break;
                        }
                    }
                }

                if (gameExecutable == null)
                    throw new ApplicationException("There is a problem: The Game is not installed");

                return Path.GetFileNameWithoutExtension(gameExecutable);
            }

            throw new ApplicationException("There is a problem: Epic Launcher is not installed");
        }

        public static LauncherGameInfo[] GetInstalledGames()
        {
            if (!IsInstalled)
                return new LauncherGameInfo[0];

            var appList = GetInstalledAppList();
            var manifests = GetInstalledManifests();

            if (appList == null || manifests == null)
                return new LauncherGameInfo[0];

            // Index manifests for O(1) lookup
            var manifestDict = manifests
                .Where(m => !string.IsNullOrEmpty(m.AppName))
                .ToDictionary(m => m.AppName, m => m);

            var games = new ConcurrentBag<LauncherGameInfo>();

            Parallel.ForEach(appList, app =>
            {
                try
                {
                    if (app.AppName.StartsWith("UE_"))
                        return;

                    if (!manifestDict.TryGetValue(app.AppName, out var manifest))
                        return;

                    // Skip DLCs
                    if (manifest.AppName != manifest.MainGameAppName)
                        return;

                    // Skip Plugins
                    if (manifest.AppCategories?.Any(a => a == "plugins" || a == "plugins/engine") == true)
                        return;

                    var installLocation = manifest.InstallLocation ?? app.InstallLocation;
                    if (string.IsNullOrEmpty(installLocation))
                        return;

                    var fullInstallPath = Path.GetFullPath(installLocation);
                    var exePath = Path.Combine(fullInstallPath, manifest.LaunchExecutable);

                    var game = new LauncherGameInfo()
                    {
                        Id = app.AppName,
                        Name = manifest.DisplayName ?? Path.GetFileName(installLocation),
                        LauncherUrl = string.Format(GameLaunchUrl, manifest.AppName),
                        InstallDirectory = fullInstallPath,
                        ExecutableName = manifest.LaunchExecutable,
                        Launcher = GameLauncherType.Epic,
                        IconPath = File.Exists(exePath) ? exePath : null
                    };

                    games.Add(game);
                }
                catch
                {
                    // Ignore individual failures to keep the loop robust
                }
            });

            return games.ToArray();
        }


        static string AllUsersPath { get { return Path.Combine(Environment.ExpandEnvironmentVariables("%PROGRAMDATA%"), "Epic"); } }

        public static bool IsInstalled
        {
            get
            {
                return File.Exists(GetExecutablePath());
            }
        }

        static string GetExecutablePath()
        {
            var modSdkMetadataDir = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Epic Games\\EOS", "ModSdkCommand", null);
            if (modSdkMetadataDir != null)
                return modSdkMetadataDir.ToString();

            return null;
        }

        static string GetMetadataPath()
        {
            var modSdkMetadataDir = Registry.GetValue("HKEY_CURRENT_USER\\Software\\Epic Games\\EOS", "ModSdkMetadataDir", null);
            if (modSdkMetadataDir != null)
                return modSdkMetadataDir.ToString();

            return null;
        }

        static List<LauncherInstalled.InstalledApp> GetInstalledAppList()
        {
            var installListPath = Path.Combine(AllUsersPath, "UnrealEngineLauncher", "LauncherInstalled.dat");
            if (!File.Exists(installListPath))
                return new List<LauncherInstalled.InstalledApp>();

            var list = JsonSerializer.DeserializeString<LauncherInstalled>(File.ReadAllText(installListPath));
            return list.InstallationList;
        }

        static IEnumerable<EpicGame> GetInstalledManifests()
        {
            var installListPath = GetMetadataPath();
            if (Directory.Exists(installListPath))
            {
                foreach (var manFile in Directory.GetFiles(installListPath, "*.item"))
                {
                    EpicGame manifest = null;

                    try { manifest = JsonSerializer.DeserializeString<EpicGame>(File.ReadAllText(manFile)); }
                    catch { }

                    if (manifest != null)
                        yield return manifest;
                }
            }
        }

    }    
}

namespace EmulatorLauncher.Common.Launchers.Epic
{
    [DataContract]
    public class LauncherInstalled
    {
        [DataContract]
        public class InstalledApp
        {
            [DataMember]
            public string InstallLocation { get; set; }
            [DataMember]
            public string AppName { get; set; }
            [DataMember]
            public long AppID { get; set; }
            [DataMember]
            public string AppVersion { get; set; }
        }

        [DataMember]
        public List<InstalledApp> InstallationList { get; set; }
    }

    [DataContract]
    public class EpicGame
    {
        [DataMember]
        public string AppName { get; set; }

        [DataMember]
        public string CatalogNamespace { get; set; }

        [DataMember]
        public string LaunchExecutable { get; set; }

        [DataMember]
        public string InstallLocation;

        [DataMember]
        public string MainGameAppName;

        [DataMember]
        public string DisplayName;

        [DataMember]
        public List<string> AppCategories { get; set; }
    }
}
