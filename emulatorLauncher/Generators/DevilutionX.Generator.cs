using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class DevilutionXGenerator : Generator
    {
        public DevilutionXGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "devilutionx.exe"))
                return null;

            _resolution = resolution;

            // The game "rom" is the folder (or file) next to the game data : diabdat.mpq / hellfire.mpq
            string gamePath = Directory.Exists(rom) ? rom : Path.GetDirectoryName(rom);
            string gameName = Path.GetFileNameWithoutExtension(rom.TrimEnd('\\', '/'));

            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "devilutionx", gameName);
            if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                catch { }

            bool fullscreen = ShouldRunFullscreen();

            // Bezels only when widescreen (Fit to Screen) is not forced
            if (fullscreen && !SystemConfig.getOptBoolean("devilutionx_widescreen"))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            // diablo.ini is stored with the game data (portable, one configuration per game folder)
            SetupConfiguration(Path.Combine(gamePath, "diablo.ini"), fullscreen);

            var commandArray = new List<string>
            {
                "--data-dir", "\"" + gamePath + "\"",
                "--config-dir", "\"" + gamePath + "\"",
                "--save-dir", "\"" + savesPath + "\""
            };

            // Game : Diablo or Hellfire (without option, the game asks when both are present)
            string gameMode = SystemConfig["devilutionx_game"];
            if (string.IsNullOrEmpty(gameMode))
            {
                if (gameName.IndexOf("hellfire", StringComparison.OrdinalIgnoreCase) >= 0)
                    gameMode = "hellfire";
                else if (File.Exists(Path.Combine(gamePath, "diabdat.mpq")) && File.Exists(Path.Combine(gamePath, "hellfire.mpq")))
                    gameMode = "diablo";
            }

            if (gameMode == "hellfire")
                commandArray.Add("--hellfire");
            else if (gameMode == "diablo")
                commandArray.Add("--diablo");

            // Language
            string lang = GetDevilutionLanguage();
            if (lang != null)
            {
                commandArray.Add("--lang");
                commandArray.Add(lang);
            }

            if (SystemConfig.getOptBoolean("devilutionx_skipintro"))
                commandArray.Add("-n");

            var psi = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
                UseShellExecute = false
            };

            ConfigureControllerFilter(psi);

            return psi;
        }

        // DevilutionX opens every gamepad and all of them control the hero,
        // optionally only the player 1 gamepad is made visible to SDL, for setups where another gamepad's sticks drift
        private void ConfigureControllerFilter(ProcessStartInfo psi)
        {
            if (!SystemConfig.getOptBoolean("devilutionx_p1only"))
                return;

            var pads = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.PlayerIndex).ToList();
            if (pads.Count < 2)
                return;

            var p1 = pads[0];
            ushort vid = (ushort)p1.VendorID;
            ushort pid = (ushort)p1.ProductID;
            if (vid == 0 || pid == 0)
                return;

            string vidPid = string.Format("0x{0:X4}/0x{1:X4}", vid, pid);
            psi.EnvironmentVariables["SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT"] = vidPid;
            SimpleLogger.Instance.Info("[DEVILUTIONX] Only controllers " + vidPid + " are visible to the game.");
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            return ret;
        }

        private void SetupConfiguration(string iniFile, bool fullscreen)
        {
            try
            {
                using (var ini = IniFile.FromFile(iniFile, IniOptions.KeepEmptyValues | IniOptions.AllowDuplicateValues | IniOptions.KeepEmptyLines))
                {
                    // Graphics
                    ini.WriteValue("Graphics", "Fullscreen", fullscreen ? "1" : "0");
                    ini.WriteValue("Graphics", "Upscale", "1");

                    if (_bezelFileInfo != null)
                    {
                        // Original 4:3 picture, the bezel covers the black borders
                        ini.WriteValue("Graphics", "Fit to Screen", "0");
                    }
                    else
                        BindBoolIniFeatureOn(ini, "Graphics", "Fit to Screen", "devilutionx_widescreen", "1", "0");

                    string res = SystemConfig.GetValueOrDefault("devilutionx_resolution", "640x480");
                    string[] parts = res.Split('x');
                    if (parts.Length == 2)
                    {
                        ini.WriteValue("Graphics", "Width", parts[0]);
                        ini.WriteValue("Graphics", "Height", parts[1]);
                    }

                    BindIniFeature(ini, "Graphics", "Scaling Quality", "devilutionx_scaling", "2");
                    BindBoolIniFeature(ini, "Graphics", "Integer Scaling", "devilutionx_integerscaling", "1", "0");
                    BindBoolIniFeatureOn(ini, "Graphics", "Frame Rate Control", "devilutionx_vsync", "1", "2");
                    BindBoolIniFeature(ini, "Graphics", "Show FPS", "devilutionx_showfps", "1", "0");

                    // Intro : only written when the option is set, the game itself changes "Once" to "Off" after the first launch
                    if (SystemConfig.isOptSet("devilutionx_skipintro"))
                    {
                        string intro = SystemConfig.getOptBoolean("devilutionx_skipintro") ? "0" : "2";
                        ini.WriteValue("StartUp", "Diablo Intro", intro);
                        ini.WriteValue("StartUp", "Hellfire Intro", intro);
                        ini.WriteValue("StartUp", "Splash", SystemConfig.getOptBoolean("devilutionx_skipintro") ? "1" : "2");
                    }

                    // User interface
                    BindBoolIniFeature(ini, "Game", "Experience Bar", "devilutionx_xpbar", "1", "0");
                    BindBoolIniFeature(ini, "Game", "Enemy Health Bar", "devilutionx_enemyhealth", "1", "0");
                    BindBoolIniFeature(ini, "Game", "Show health values", "devilutionx_globevalues", "1", "0");
                    BindBoolIniFeature(ini, "Game", "Show mana values", "devilutionx_globevalues", "1", "0");
                    BindBoolIniFeature(ini, "Game", "Show Item Labels", "devilutionx_itemlabels", "1", "0");
                    BindIniFeature(ini, "Game", "Enable floating numbers", "devilutionx_floatingnumbers", "0");

                    // Gameplay
                    BindBoolIniFeature(ini, "Game", "Run in Town", "devilutionx_runintown", "1", "0");
                    BindBoolIniFeature(ini, "Game", "Auto Gold Pickup", "devilutionx_autogold", "1", "0");
                    BindBoolIniFeature(ini, "Game", "Auto Refill Belt", "devilutionx_autobelt", "1", "0");

                    // Controller stick dead zone (0 to 1, default 0.07)
                    BindIniFeature(ini, "Controller", "deadzone", "devilutionx_deadzone", "0.07");

                    // Only combos that are free by default to avoid sending spells ! Select+LS (quick save), Start+LS (quick load), Start+RS (screenshot)
                    if (!SystemConfig.isOptSet("devilutionx_padshortcuts") || SystemConfig.getOptBoolean("devilutionx_padshortcuts"))
                    {
                        ini.WriteValue("Padmapping", "QuickSave", "Select+LS");
                        ini.WriteValue("Padmapping", "QuickLoad", "Start+LS");
                        ini.WriteValue("Padmapping", "Screenshot", "Start+RS");
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[DEVILUTIONX] Unable to write diablo.ini : " + ex.Message);
            }
        }

        private static readonly string[] devilutionLanguages = { "bg", "cs", "da", "de", "el", "es", "fr", "hr", "hu", "it", "ja", "ko", "pl", "pt_BR", "ro", "ru", "sv", "tr", "uk", "zh_CN", "zh_TW" };

        private string GetDevilutionLanguage()
        {
            if (!SystemConfig.isOptSet("Language") || string.IsNullOrEmpty(SystemConfig["Language"]))
                return null;

            string full = SystemConfig["Language"];
            var match = devilutionLanguages.FirstOrDefault(l => l.Equals(full, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;

            string shortLang = GetCurrentLanguage();
            if (shortLang == "en")
                return "en";

            match = devilutionLanguages.FirstOrDefault(l => l.Equals(shortLang, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;

            if (shortLang == "pt")
                return "pt_BR";
            if (shortLang == "zh")
                return "zh_CN";

            return null;
        }
    }
}