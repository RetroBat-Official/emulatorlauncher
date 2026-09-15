using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class GZDoomGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath(emulator);
            if (string.IsNullOrEmpty(path))
            {
                SimpleLogger.Instance.Error("[Generator] Emulator path not found for '" + emulator + "'.");
                return null;
            }

            bool isUzdoom = emulator == "uzdoom";
            
            string exe = Path.Combine(path, isUzdoom ? "uzdoom.exe" : "gzdoom.exe");

            if (!File.Exists(exe))
                return null;
			
            bool fullscreen = ShouldRunFullscreen();

            string configFile = Path.Combine(path, isUzdoom ? "uzdoom_portable.ini" : "gzdoom_portable.ini");
            string romPath = Path.GetDirectoryName(rom);

            var commandArray = new List<string>
            {
                "-config",
                "\"" + configFile + "\""
            };

            string romExt = Path.GetExtension(rom).ToLowerInvariant();

            if (_romExtensions.Contains(romExt) && File.Exists(rom))
            {
                var lines = File.ReadAllLines(rom)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 && !l.StartsWith("#"))
                    .ToArray();

                if (lines.Length == 0)
                    throw new ApplicationException("gzdoom or uzdoom file does not contain any launch argument.");

                // A single line file only holds the path of the game to run
                if (lines.Length == 1 && !lines[0].StartsWith("-") && !lines[0].StartsWith("+"))
                {
                    commandArray.Add("-iwad");
                    commandArray.Add("\"" + ResolveRomRelativePath(rom, lines[0]) + "\"");
                }
                else
                {
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("\\") || line.StartsWith("/"))
                            commandArray.Add("\"" + ResolveRomRelativePath(rom, line) + "\"");
                        else
                            commandArray.Add(line);
                    }
                }
            }
            
            else if (_romExtensions.Contains(romExt) && Directory.Exists(rom))
            {
                var files = Directory.GetFiles(rom)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Exact file name match, so that 'freedoom.wad' is not detected as 'doom.wad'
                string mainIwad = files.FirstOrDefault(f => iwadList.Contains(Path.GetFileName(f).ToLowerInvariant()));
                // Custom IWADs are identified by their extension instead of their name
                if (mainIwad == null)
                    mainIwad = files.FirstOrDefault(f => customIwadExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                if (mainIwad == null)
                    throw new ApplicationException("There is no iwad file in the folder");

                commandArray.Add("-iwad");
                commandArray.Add("\"" + mainIwad + "\"");

                // Only pass files the engine can load, readme or artwork files are skipped
                var modFiles = files
                    .Where(f => f != mainIwad && modExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                if (modFiles.Count > 0)
                {
                    commandArray.Add("-file");
                    foreach (var modFile in modFiles)
                        commandArray.Add("\"" + modFile + "\"");
                }
            }
            
            else
            {
                commandArray.Add("-iwad");
                commandArray.Add("\"" + rom + "\"");
            }

            string args = string.Join(" ", commandArray);

            SetupConfiguration(configFile, romPath, fullscreen, isUzdoom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        //Setup configuration file
        private void SetupConfiguration(string configFile, string romPath, bool fullscreen, bool isUzdoom)
        {
            if (!File.Exists(configFile))
                File.WriteAllText(configFile, "");

            try
            {
                using (var ini = IniFile.FromFile(configFile, IniOptions.KeepEmptyLines))
                {
                    ini.WriteValue("IWADSearch.Directories", "Path", romPath.Replace("\\", "/"));

                    if (fullscreen)
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "true");
                    else
                        ini.WriteValue("GlobalSettings", "vid_fullscreen", "false");

                    BindIniFeature(ini, "GlobalSettings", "vid_preferbackend", "gzdoom_renderer", "0");
                    BindIniFeature(ini, "GlobalSettings", "vid_aspect", "gzdoom_ratio", "0");
                    BindIniFeature(ini, "GlobalSettings", "vid_rendermode", "gzdoom_rendermode", "4");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter", "gzdoom_texture_filter", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_texture_filter_anisotropic", "gzdoom_anisotropic_filtering", "1");
                    BindIniFeature(ini, "GlobalSettings", "vid_scalemode", "gzdoom_scalemode", "0");
                    BindIniFeature(ini, "GlobalSettings", "gl_multisample", "gzdoom_msaa", "1");
                    BindIniFeature(ini, "GlobalSettings", "gl_fxaa", "gzdoom_fxaa", "0");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_vsync", "gzdoom_vsync", "true", "false");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "vid_cropaspect", "gzdoom_cropaspect", "true", "false");

                    BindIniFeature(ini, "GlobalSettings", "snd_mididevice", "gzdoom_mididevice", "-5");

                    BindIniFeature(ini, "GlobalSettings", "gl_ssao", "gzdoom_ssao", "0");
                    BindBoolIniFeature(ini, "GlobalSettings", "cl_capfps", "gzdoom_vanillafps", "true", "false");
                    BindBoolIniFeatureOn(ini, "GlobalSettings", "cl_run", "gzdoom_alwaysrun", "true", "false");

                    // Per game cvars, not stored in GlobalSettings
                    if (Features.IsSupported("gzdoom_compatmode"))
                        WriteGameValue(ini, "compatmode", SystemConfig.GetValueOrDefault("gzdoom_compatmode", "0"));

                    if (Features.IsSupported("gzdoom_lightmode"))
                        WriteGameValue(ini, "gl_lightmode", SystemConfig.GetValueOrDefault("gzdoom_lightmode", "1"));

                    // hdr
                    if (SystemConfig["gzdoom_renderer"] == "1" && SystemConfig.getOptBoolean("enable_hdr"))
                    {
                        ini.WriteValue("GlobalSettings", "vid_hdr", "false");
                        ini.WriteValue("GlobalSettings", "vk_hdr", "true");
                    }
                    else if (SystemConfig.getOptBoolean("enable_hdr"))
                    {
                        ini.WriteValue("GlobalSettings", "vid_hdr", "true");
                        ini.WriteValue("GlobalSettings", "vk_hdr", "false");
                    }
                    else
                    {
                        ini.WriteValue("GlobalSettings", "vid_hdr", "false");
                        ini.WriteValue("GlobalSettings", "vk_hdr", "false");
                    }

                    // crosshairs
                    if (SystemConfig.isOptSet("gzdoom_crosshair") && SystemConfig["gzdoom_crosshair"] == "false")
                    {
                        WriteGameValue(ini, "crosshairon", "false");
                        WriteGameValue(ini, "crosshairhealth", "0");
                    }
                    else if (SystemConfig.isOptSet("gzdoom_crosshair") && !string.IsNullOrEmpty(SystemConfig["gzdoom_crosshair"]))
                    {
                        WriteGameValue(ini, "crosshairon", "true");
                        WriteGameValue(ini, "crosshair", SystemConfig["gzdoom_crosshair"]);
                    }
                    else
                        WriteGameValue(ini, "crosshairon", "false");

                    if (SystemConfig.isOptSet("gzdoom_crosshair_color") && SystemConfig["gzdoom_crosshair_color"] == "health")
                    {
                        WriteGameValue(ini, "crosshairhealth", "1");
                    }
                    else if (SystemConfig.isOptSet("gzdoom_crosshair_color") && !string.IsNullOrEmpty(SystemConfig["gzdoom_crosshair_color"]))
                    {
                        WriteGameValue(ini, "crosshairhealth", "0");
                        WriteGameValue(ini, "crosshaircolor", SystemConfig["gzdoom_crosshair_color"].Replace("_", " "));
                    }
                    else
                        WriteGameValue(ini, "crosshairhealth", "1");



                    string savePath = isUzdoom ? Path.Combine(AppConfig.GetFullPath("saves"), "uzdoom") : Path.Combine(AppConfig.GetFullPath("saves"), "gzdoom");
                    if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                        catch { }
                    ini.WriteValue("GlobalSettings", "save_dir", savePath.Replace("\\", "/"));

                    string screenshotPath = isUzdoom ? Path.Combine(AppConfig.GetFullPath("screenshots"), "uzdoom") : Path.Combine(AppConfig.GetFullPath("screenshots"), "gzdoom");
                    if (!Directory.Exists(screenshotPath)) try { Directory.CreateDirectory(screenshotPath); }
                        catch { }
                    ini.WriteValue("GlobalSettings", "screenshot_dir", screenshotPath.Replace("\\", "/"));
                }
            }
            catch { }
        }

        // Write a per game cvar in every game section, so that the setting also applies to Heretic, Hexen, Strife...
        private static void WriteGameValue(IniFile ini, string key, string value)
        {
            foreach (var game in gameSections)
                ini.WriteValue(game + ".ConsoleVariables", key, value);
        }

        // Resolve a path written in a .gzdoom or .uzdoom file, relative paths are based on the rom folder
        private static string ResolveRomRelativePath(string rom, string value)
        {
            value = value.Replace("/", "\\");

            if (Path.IsPathRooted(value) && !value.StartsWith("\\"))
                return value;

            return Path.Combine(Path.GetDirectoryName(rom), value.TrimStart('\\'));
        }

        // Config sections used by the engine, cvars without CVAR_GLOBALCONFIG are stored once per game
        private readonly static List<string> gameSections = new List<string>()
        {
            "Doom", "Heretic", "Hexen", "Strife", "Chex",
            "Harmony", "Hacx", "Square", "UrbanBrawl", "Delaweare", "WoolBall"
        };

        // File types the engine is able to load through -file
        private readonly static List<string> modExtensions = new List<string>()
        {
            ".wad", ".pk3", ".pk7", ".ipk3", ".iwad", ".zip", ".pak", ".deh", ".bex", ".lmp"
        };

        // Extensions used by custom IWADs, which are not listed by name in iwadinfo.txt
        private readonly static List<string> customIwadExtensions = new List<string>()
        {
            ".iwad", ".ipk3"
        };

        private readonly static List<string> _romExtensions = new List<string>()
        {
            ".gzdoom", ".uzdoom"
        };

        // IWAD file names known by the engine, kept in sync with iwadinfo.txt (Names block)
        private readonly static List<string> iwadList = new List<string>()
        {
            // Doom
            "doom.wad",
            "doom1.wad",
            "doomu.wad",
            "doomunity.wad",
            "doomkex.wad",
            "doomxbox.wad",
            "bfgdoom.wad",
            "doombfg.wad",
            // Doom 2
            "doom2.wad",
            "doom2f.wad",
            "doom2unity.wad",
            "doom2kex.wad",
            "doom2xbox.wad",
            "bfgdoom2.wad",
            "doom2bfg.wad",
            // Final Doom
            "tnt.wad",
            "tntunity.wad",
            "tntkex.wad",
            "plutonia.wad",
            "plutoniaunity.wad",
            "plutoniakex.wad",
            // WadSmoosh
            "doom_complete.pk3",
            // Heretic
            "heretic.wad",
            "heretic1.wad",
            "hereticsr.wad",
            "blasphem.wad",
            "blasphemer.wad",
            // Hexen
            "hexen.wad",
            "hexdd.wad",
            "hexdemo.wad",
            "hexendemo.wad",
            // Strife
            "strife.wad",
            "strife0.wad",
            "strife1.wad",
            "sve.wad",
            // Chex Quest
            "chex.wad",
            "chex3.wad",
            // Freedoom
            "freedoom.wad",
            "freedoom1.wad",
            "freedoom2.wad",
            "freedoomu.wad",
            "freedm.wad",
            // Action Doom 2
            "action2.wad",
            // Harmony
            "harm1.wad",
            // Hacx
            "hacx.wad",
            "hacx2.wad",
            // The Adventures of Square
            "square1.pk3",
            // Delaweare
            "delaweare.wad",
            // Rise of the Wool Ball
            "rotwb.wad",
        };
    }
}
