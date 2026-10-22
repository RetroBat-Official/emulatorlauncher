using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class GemRBGenerator : Generator
    {
        protected override bool UseGenericScreenPlacement { get { return true; } }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        // Game types known by GemRB (GemRB.cfg GameType), any other rom extension falls back to auto detection
        private static readonly string[] gameTypes = { "auto", "bg1", "bg2", "bgee", "bg2ee", "how", "iwd", "iwd2", "pst", "demo" };

        // Encodings shipped with GemRB 0.9.5 (unhardcoded\shared\*.ini)
        private static readonly string[] encodings = { "default", "japanese", "korean", "chinese" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("gemrb");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "gemrb.exe");
            if (!File.Exists(exe))
                return null;

            _resolution = resolution;

            // GemRB.cfg next to the executable, passed with -c (portable, nothing in the user profile)
            string conf = Path.Combine(path, "GemRB.cfg");

            bool fullscreen = ShouldRunFullscreen();

            SetupConfiguration(rom, conf, system, emulator, fullscreen, resolution);

            var psi = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-c \"" + conf + "\"",
                UseShellExecute = false
            };

            ConfigureControllerFilter(psi);

            return psi;
        }

        private void SetupConfiguration(string rom, string conf, string system, string emulator, bool fullscreen, ScreenResolution resolution)
        {
            string gamePath = AppConfig.GetFullPath(rom);
            string gameExtension = Path.GetExtension(rom).Replace(".", "").ToLowerInvariant();
            string gameName = Path.GetFileNameWithoutExtension(rom.TrimEnd('\\', '/'));

            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, gameName);
            if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                catch { }

            // Game resolution (logical size, the picture is scaled to the window keeping the aspect ratio)
            Screen screen = Program.TargetScreen ?? Screen.PrimaryScreen;
            int width = 640, height = 480;

            string res = SystemConfig.GetValueOrDefault("gemrb_resolution", SystemConfig["resolution"]);
            if (!string.IsNullOrEmpty(res))
            {
                res = res.ToLowerInvariant();
                if (res == "desktop")
                {
                    width = resolution == null ? screen.Bounds.Width : resolution.Width;
                    height = resolution == null ? screen.Bounds.Height : resolution.Height;
                }
                else if (res.Contains("x"))
                {
                    string[] parts = res.Split('x');
                    int w, h;
                    if (parts.Length == 2 && int.TryParse(parts[0], out w) && int.TryParse(parts[1], out h) && w > 0 && h > 0)
                    {
                        width = w;
                        height = h;
                    }
                }
            }

            // Bezels only with a 4:3 picture in fullscreen (black borders on the sides)
            if (fullscreen && width * 3 == height * 4)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            try
            {
                using (var ini = IniFile.FromFile(conf))
                {
                    ini.WriteValue("", "GameType", gameTypes.Contains(gameExtension) ? gameExtension : "auto");
                    ini.WriteValue("", "GamePath", gamePath);
                    ini.WriteValue("", "SavePath", savesPath);

                    // EE games : text language follows RetroBat when the language folder exists
                    string langPath = GetEELanguagePath(gamePath);
                    if (langPath != null)
                        ini.WriteValue("", "GameLanguagePath", langPath);
                    else
                        ini.Remove("", "GameLanguagePath");

                    // Video
                    // "FullScreen" is the GemRB setting, "Full Screen" the game option saved in gem-*.ini : both must follow RetroBat
                    ini.Remove("", "Fullscreen"); // old key written by previous RetroBat versions
                    ini.WriteValue("", "FullScreen", fullscreen ? "1" : "0");
                    ini.WriteValue("", "Full Screen", fullscreen ? "1" : "0");
                    ini.WriteValue("", "Width", width.ToString());
                    ini.WriteValue("", "Height", height.ToString());
                    ini.WriteValue("", "Bpp", "32");

                    // CapFPS : 0 = vertical sync, otherwise a frame rate cap (avoids an unlimited frame rate)
                    BindBoolIniFeatureOn(ini, "", "CapFPS", "gemrb_vsync", "0", "60");
                    BindIniFeature(ini, "", "ScaleQuality", "gemrb_scalequality", "best");
                    BindIniFeature(ini, "", "SpriteFogOfWar", "SpriteFogOfWar", "0");

                    // In-game zoom (mouse wheel), only forced when set, otherwise the in-game option is kept
                    if (SystemConfig.isOptSet("gemrb_zoom"))
                        ini.WriteValue("", "Zoom Lock", SystemConfig.getOptBoolean("gemrb_zoom") ? "0" : "1");
                    else
                        ini.Remove("", "Zoom Lock");

                    // Audio
                    ini.WriteValue("", "AudioDriver", "openal");

                    // Text encoding of the game data (asian versions), older values (german, polish...) no longer exist
                    string encoding = SystemConfig.GetValueOrDefault("Encoding", "default");
                    if (!encodings.Contains(encoding))
                        encoding = "default";
                    ini.WriteValue("", "Encoding", encoding);

                    // Gamepad : left stick moves the pointer, right stick scrolls the map
                    ini.WriteValue("", "GamepadSupport", "1");
                    BindIniFeature(ini, "", "GamepadPointerSpeed", "gemrb_pointerspeed", "10");
                    BindIniFeature(ini, "", "GamepadLDeadZone", "gemrb_deadzone", "5000");
                    BindIniFeature(ini, "", "GamepadRDeadZone", "gemrb_deadzone", "5000");

                    // Misc
                    BindBoolIniFeature(ini, "", "SkipIntroVideos", "SkipIntroVideos", "1", "0");
                    BindBoolIniFeature(ini, "", "DrawFPS", "DrawFPS", "1", "0");
                    BindBoolIniFeature(ini, "", "EnableCheatKeys", "EnableCheatKeys", "1", "0");
                    BindBoolIniFeature(ini, "", "Logging", "Logging", "1", "0");
                    BindIniFeature(ini, "", "DebugMode", "DebugMode", "0");

                    // Removed in 0.9.5 : GUI enhancements are now set from the in-game options and saved in gem-*.ini
                    ini.Remove("", "GUIEnhancements");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[GEMRB] Unable to write GemRB.cfg : " + ex.Message);
            }
        }

        // Enhanced Edition games have a lang folder (lang\en_US, lang\fr_FR...)
        private string GetEELanguagePath(string gamePath)
        {
            string langRoot = Path.Combine(gamePath, "lang");
            if (!Directory.Exists(langRoot))
                return null;

            string lang = SystemConfig["Language"];
            if (string.IsNullOrEmpty(lang))
                return null;

            string[] folders;
            try { folders = Directory.GetDirectories(langRoot).Select(d => Path.GetFileName(d)).ToArray(); }
            catch { return null; }

            // Full code first (fr_FR), then the language only (fr)
            string match = folders.FirstOrDefault(f => f.Equals(lang, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                string shortLang = GetCurrentLanguage();
                if (!string.IsNullOrEmpty(shortLang))
                    match = folders.FirstOrDefault(f => f.StartsWith(shortLang + "_", StringComparison.OrdinalIgnoreCase));
            }

            if (match == null)
                return null;

            return "lang/" + match;
        }

        // GemRB only uses one gamepad : the first one in SDL order, which is not always the RetroBat player 1
        // When several gamepads are connected, only the player 1 gamepad is made visible to SDL
        private void ConfigureControllerFilter(ProcessStartInfo psi)
        {
            if (SystemConfig.getOptBoolean("disableautocontrollers"))
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
            SimpleLogger.Instance.Info("[GEMRB] Only controllers " + vidPid + " are visible to the game.");
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
    }
}