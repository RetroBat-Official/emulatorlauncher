using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class KronosGenerator : Generator
    {
        public KronosGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        private bool _startBios;
        private string _multitap;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("kronos");

            string exe = Path.Combine(path, "kronos.exe");

            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            _startBios = SystemConfig.getOptBoolean("saturn_startbios");

            var commandArray = new List<string>();

            // Manage .m3u files
            if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                var fromm3u = MultiDiskImageFile.FromFile(rom);

                if (fromm3u.Files.Length == 0)
                    throw new ApplicationException("m3u file does not contain any game file.");

                else if (fromm3u.Files.Length == 1)
                    rom = fromm3u.Files[0];

                else
                {
                    if (SystemConfig.isOptSet("saturn_discnumber") && !string.IsNullOrEmpty(SystemConfig["saturn_discnumber"]))
                    {
                        int discNumber = SystemConfig["saturn_discnumber"].ToInteger();
                        if (discNumber >= 0 && discNumber <= fromm3u.Files.Length)
                            rom = fromm3u.Files[discNumber];
                        else
                            rom = fromm3u.Files[0];
                    }
                    else
                        rom = fromm3u.Files[0];
                }

                if (!File.Exists(rom))
                    throw new ApplicationException("File '" + rom + "' does not exist");
            }

            SetupConfig(path, system, exe, rom, fullscreen);

            if (!_startBios)
            {
                commandArray.Add("-i");
                commandArray.Add("\"" + rom + "\"");
            }
            
            commandArray.Add("-a");                 // autostart
            if (fullscreen)
                commandArray.Add("-f");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private string GetDefaultsaturnLanguage()
        {
            Dictionary<string, string> availableLanguages = new Dictionary<string, string>()
            {
                { "en", "0" },
                { "de", "1" },
                { "fr", "2" },
                { "es", "3" },
                { "it", "4" },
                { "jp", "5" },
                { "ja", "5" },
            };

            string lang = GetCurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                string ret;
                if (availableLanguages.TryGetValue(lang, out ret))
                    return ret;
            }

            return "0";
        }

        private void SetupConfig(string path, string system, string exe, string rom, bool fullscreen = true)
        {
            string iniFile = Path.Combine(path, "kronos.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues))
                {
                    // Inject path loop
                    Dictionary<string, string> userPath = new Dictionary<string, string>
                    {
                        { "General\\SaveStates", Path.Combine(AppConfig.GetFullPath("saves"), system, "kronos", "sstates") },
                        { "General\\ScreenshotsDirectory", Path.Combine(AppConfig.GetFullPath("screenshots"), "kronos") },
                        { "Memory\\Path", Path.Combine(AppConfig.GetFullPath("saves"), system,  "kronos", "bkram.bin") }
                    };
                    
                    foreach (KeyValuePair<string, string> pair in userPath)
                    {
                        if (!Directory.Exists(pair.Value)) try { Directory.CreateDirectory(pair.Value); }
                            catch { }
                        if (!string.IsNullOrEmpty(pair.Value) && Directory.Exists(pair.Value))
                            ini.WriteValue("1.0", pair.Key, pair.Value.Replace("\\", "/"));
                    }

                    // Bios
                    string bios = Path.Combine(AppConfig.GetFullPath("bios"), "saturn_bios.bin");
                    if (File.Exists(bios))
                        ini.WriteValue("1.0", "General\\Bios", bios.Replace("\\", "/"));

                    // disable fullscreen if windowed mode
                    if (!fullscreen)
                        ini.WriteValue("1.0", "Video\\Fullscreen", "false");

                    // Language
                    string lang = GetDefaultsaturnLanguage();
                    if (SystemConfig.isOptSet("saturn_language") && !string.IsNullOrEmpty(SystemConfig["saturn_language"]))
                        lang = SystemConfig["saturn_language"];
                    
                    if (!string.IsNullOrEmpty(lang))
                        ini.WriteValue("1.0", "General\\SystemLanguageID", lang);
                    else
                        ini.WriteValue("1.0", "General\\SystemLanguageID", "0");

                    // Get version
                    var output = ProcessExtensions.RunWithOutput(exe, "-v");
                    output = FormatKronosVersionString(output.ExtractString("", "\r"));
                    if (output != null)
                        ini.WriteValue("1.0", "General\\Version", output.ToString());
                    
                    // Features
                    ini.WriteValue("1.0", "General\\CdRom", "1");
                    ini.AppendValue("1.0", "General\\CdRomISO", rom.Replace("\\", "/"));
                    ini.WriteValue("1.0", "View\\Toolbar", "1");
                    ini.WriteValue("1.0", "General\\EnableEmulatedBios", "false");
                    ini.WriteValue("1.0", "Video\\VideoCore", "2");
                    ini.WriteValue("1.0", "Video\\OSDCore", "3");
                    ini.WriteValue("1.0", "Advanced\\SH2Interpreter", "8");
                    BindIniFeature(ini, "1.0", "View\\Menubar", "kronos_menubar", "1");
                    BindBoolIniFeature(ini, "1.0", "General\\EnableVSync", "kronos_vsync", "false", "true");
                    BindBoolIniFeature(ini, "1.0", "General\\ShowFPS", "kronos_fps", "true", "false");
                    BindIniFeature(ini, "1.0", "Video\\AspectRatio", "kronos_ratio", "0");
                    BindIniFeature(ini, "1.0", "Video\\upscale_type", "kronos_scaler", "0");
                    BindIniFeature(ini, "1.0", "Video\\filter_type", "kronos_filtering", "0");
                    BindBoolIniFeature(ini, "1.0", "Video\\MeshMode", "kronos_mesh", "1", "0");
                    BindBoolIniFeature(ini, "1.0", "Video\\BandingMode", "kronos_bandingmode", "1", "0");
                    BindIniFeature(ini, "1.0", "Sound\\SoundCore", "kronos_audiocore", "2");
                    BindIniFeature(ini, "1.0", "Cartridge\\Type", "kronos_cartridge", "7");

                    CreateControllerConfiguration(path, ini);
                    ConfigureGun(path, ini);
                }
            }
            catch { }
        }

        private static string FormatKronosVersionString(string version)
        {
            var numbers = version.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (numbers.Count < 3)
                numbers.Add("0");

            return string.Join(".", numbers.Take(3).ToArray());
        }

        private void ConfigureGun(string path, IniFile ini)
        {
            if (!SystemConfig.isOptSet("use_guns") || string.IsNullOrEmpty(SystemConfig["use_guns"]) || !SystemConfig.getOptBoolean("use_guns"))
                return;

            string gunport = "2";
            if (SystemConfig.isOptSet("kronos_gunport") && !string.IsNullOrEmpty(SystemConfig["kronos_gunport"]))
                gunport = SystemConfig["kronos_gunport"];

            bool gunInvert = SystemConfig.getOptBoolean("gun_invert");

            ini.WriteValue("1.0", "Input\\Port\\" + gunport + "\\Id\\1\\Type", "37");
            ini.WriteValue("1.0", "Input\\Port\\" + gunport + "\\Id\\1\\Controller\\37\\Key\\25", gunInvert ? "2147483650" : "2147483649");
            ini.WriteValue("1.0", "Input\\Port\\" + gunport + "\\Id\\1\\Controller\\37\\Key\\27", gunInvert ? "2147483649" : "2147483650");
            ini.WriteValue("1.0", "Input\\GunMouseSensitivity", "100");
        }
    }
}
