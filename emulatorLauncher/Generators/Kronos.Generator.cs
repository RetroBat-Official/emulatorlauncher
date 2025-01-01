using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Windows.Forms;

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
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("kronos");

            string exe = Path.Combine(path, "kronos.exe");

            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            _startBios = SystemConfig.getOptBoolean("saturn_startbios");

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

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
                if (availableLanguages.TryGetValue(lang, out string ret))
                    return ret;
            }

            return "0";
        }

        private void SetupConfig(string path, string system, string exe, string rom, bool fullscreen = true)
        {
            string iniFile = Path.Combine(path, "kronos.ini");

            if (!File.Exists(iniFile))
            {
                try
                {
                    File.WriteAllText(iniFile, kronosIni);
                    System.Threading.Thread.Sleep(100);
                }
                catch { }
            }

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues))
                {
                    // Inject path loop
                    Dictionary<string, string> userPath = new Dictionary<string, string>
                    {
                        { "General\\SaveStates", Path.Combine(AppConfig.GetFullPath("saves"), system, "kronos", "sstates") },
                        { "General\\ScreenshotsDirectory", Path.Combine(AppConfig.GetFullPath("screenshots"), "kronos") },
                    };
                    
                    foreach (KeyValuePair<string, string> pair in userPath)
                    {
                        if (!Directory.Exists(pair.Value)) try { Directory.CreateDirectory(pair.Value); }
                            catch { }
                        if (!string.IsNullOrEmpty(pair.Value) && Directory.Exists(pair.Value))
                            ini.WriteValue("1.0", pair.Key, pair.Value.Replace("\\", "/"));
                    }

                    // bkram
                    string bkramPath = Path.Combine(AppConfig.GetFullPath("saves"), system, "kronos");
                    if (!Directory.Exists(bkramPath)) try { Directory.CreateDirectory(bkramPath); }
                        catch { }

                    string bkram = Path.Combine(bkramPath, "bkram.bin");

                    ini.WriteValue("1.0", "Memory\\Path", bkram.Replace("\\", "/"));

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
                    ini.AppendValue("1.0", "General\\CdRomISO", null);
                    BindBoolIniFeature(ini, "1.0", "General\\EnableEmulatedBios", "kronos_hle_bios", "true", "false");
                    ini.WriteValue("1.0", "Video\\OSDCore", "3");
                    ini.WriteValue("1.0", "Advanced\\SH2Interpreter", "8");
                    BindBoolIniFeatureOn(ini, "1.0", "General\\EnableVSync", "kronos_vsync", "true", "false");
                    BindBoolIniFeature(ini, "1.0", "General\\ShowFPS", "kronos_fps", "true", "false");
                    BindIniFeature(ini, "1.0", "Video\\AspectRatio", "kronos_ratio", "0");
                    BindIniFeature(ini, "1.0", "Video\\upscale_type", "kronos_scaler", "0");
                    BindIniFeature(ini, "1.0", "Video\\filter_type", "kronos_filtering", "0");
                    BindBoolIniFeature(ini, "1.0", "Video\\MeshMode", "kronos_mesh", "1", "0");
                    BindBoolIniFeature(ini, "1.0", "Video\\BandingMode", "kronos_bandingmode", "1", "0");
                    BindIniFeature(ini, "1.0", "Sound\\SoundCore", "kronos_audiocore", "2");
                    BindIniFeature(ini, "1.0", "Cartridge\\Type", "kronos_cartridge", "7");
                    BindIniFeature(ini, "1.0", "Video\\resolution_mode", "kronos_resolution", "1");

                    CreateControllerConfiguration(ini);
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

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }

        private readonly string kronosIni =
            @"[1.0]
            General\EnableVSync=true
            Input\Port\1\Id\1\Type=2
            General\Version=2.7.0
            General\Bios=./../../bios/sega_101.bin
            General\BiosSettings=
            General\EnableEmulatedBios=false
            General\SH2Cache=false
            General\CdRom=1
            General\CdRomISO=
            General\SaveStates=./../../saves/saturn/kronos
            General\ScreenshotsDirectory=./../../screenshots/kronos
            General\ScreenshotsFormat=bmp
            General\SystemLanguageID=0
            General\Translation=
            General\ShowFPS=false
            autostart=true
            Video\OSDCore=3
            Video\AspectRatio=0
            Video\Wireframe=0
            Video\MeshMode=0
            Video\BandingMode=0
            Video\Fullscreen=true
            Video\filter_type=0
            Video\upscale_type=0
            Video\resolution_mode=1
            General\ClockSync=false
            General\FixedBaseTime=1998-01-01T12:00:00
            Sound\SoundCore=1
            Cartridge\LastCart=0
            Cartridge\Type=7
            Cartridge\Path\32MbitDram=
            Cartridge\ModemIP=127.0.0.1
            Cartridge\ModemPort=1337
            Cartridge\STVGame=
            Cartridge\STVGameName=
            Memory\Path=./../../saves/saturn/kronos/bkram.bin
            MpegROM\Path=
            Memory\ExtendMemory=false
            Cartridge\Path=
            Input\PerCore=3
            Input\GunMouseSensitivity=100
            STV\Region=E
            Advanced\SH2Interpreter=8
            Advanced\68kCore=3
            Shortcuts\%26Quitter=Esc
            Shortcuts\Start=
            Shortcuts\Stop=
            Shortcuts\%26Param%E8tres...=Ctrl+S
            Shortcuts\L%26ancer=F1
            Shortcuts\%26Pause=F2
            Shortcuts\%26Red%E9marrer=F3
            Shortcuts\%26Transfert=Ctrl+T
            Shortcuts\Sc%26reenshot=F8
            Shortcuts\Synchronisation%20Verticale=F4
            Shortcuts\F%26PS=F12
            Shortcuts\Vdp1=1
            Shortcuts\%26Plein%20Ecran=Alt+Return
            Shortcuts\%26Log=Ctrl+L
            Shortcuts\SH2%20%26Ma%EEtre=
            Shortcuts\SH2%20E%26sclave=
            Shortcuts\VDP%261=
            Shortcuts\VDP%262=
            Shortcuts\M%2668K=
            Shortcuts\SCU-%26DSP=
            Shortcuts\S%26CSP=
            Shortcuts\Memory%20Transfer=
            Shortcuts\NBG0=2
            Shortcuts\NBG1=3
            Shortcuts\NBG2=4
            Shortcuts\NBG3=5
            Shortcuts\RGB0=6
            Shortcuts\RBG1=7
            Shortcuts\Sauvegarde1=Ctrl+1
            Shortcuts\Sauvegarde2=Ctrl+2
            Shortcuts\Sauvegarde3=Ctrl+3
            Shortcuts\Sauvegarde4=Ctrl+4
            Shortcuts\Sauvegarde5=Ctrl+5
            Shortcuts\Sauvegarde6=Ctrl+6
            Shortcuts\Sauvegarde7=Ctrl+7
            Shortcuts\Sauvegarde8=Ctrl+8
            Shortcuts\Sauvegarde9=Ctrl+9
            Shortcuts\Chargement1=Alt+1
            Shortcuts\Chargement2=Alt+2
            Shortcuts\Chargement3=Alt+3
            Shortcuts\Chargement4=Alt+4
            Shortcuts\Chargement5=Alt+5
            Shortcuts\Chargement6=Alt+6
            Shortcuts\Chargement7=Alt+7
            Shortcuts\Chargement8=Alt+8
            Shortcuts\Chargement9=Alt+9
            Shortcuts\Vers%20un%20Fichier...=F9
            Shortcuts\A%20Partir%20d%27un%20Fichier...=F5
            Shortcuts\Liste%20de%20%26Cheats...=Alt+L
            Shortcuts\%26Gestion%20des%20Sauvegardes...=Alt+B
            Shortcuts\Ejecter\Charger%20un%20ISO...=Ctrl+I
            Shortcuts\Ouvrir%20un%20Lecteur%20%26CD%20Rom...=Ctrl+C
            Shortcuts\Select%20Cartridge...=Ctrl+A
            Shortcuts\Sound...=
            Shortcuts\Sauvegarde0=Ctrl+0
            Shortcuts\Chargement0=Alt+0
            Shortcuts\%26Editeur%20de%20M%E9moire=
            Shortcuts\%26Rechercher%20un%20Cheat...=Alt+S
            Shortcuts\Canaux%20SCSP=
            Shortcuts\%26Fichier=
            Shortcuts\S%26auvegarder%20un%20Etat=F6
            Shortcuts\%26Charger%20un%20Etat=F7
            Shortcuts\%26Visualiser=
            Shortcuts\%26Couche=
            Shortcuts\%26Debug=
            Shortcuts\%26Aide=
            Shortcuts\Ou%26tils=
            Shortcuts\%26Emulation=
            Shortcuts\Barre%20d%27Outils=
            Shortcuts\Log=
            Debug\Addr2Line=
            General\Geometry=@ByteArray(\x1\xd9\xd0\xcb\0\x3\0\0\0\0\x2\x7f\0\0\0\xf5\0\0\x5\0\0\0\x2\xf4\0\0\x2\x80\0\0\x1\x14\0\0\x4\xff\0\0\x2\xf3\0\0\0\0\0\0\0\0\a\x80\0\0\x2\x80\0\0\x1\x14\0\0\x4\xff\0\0\x2\xf3)
            Shortcuts\1%20...%20=Alt+1
            Shortcuts\2%20...%20=Alt+2
            Shortcuts\3%20...%20=Alt+3
            Shortcuts\4%20...%20=Alt+4
            Shortcuts\5%20...%20=Alt+5
            Shortcuts\6%20...%20=Alt+6
            Shortcuts\7%20...%20=Alt+7
            Shortcuts\8%20...%20=Alt+8
            Shortcuts\9%20...%20=Alt+9
            Shortcuts\0%20...%20=Alt+0
            Sound\Volume=100";
    }
}
