using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class MagicEngineGenerator : Generator
    {
        private ScreenResolution _resolution;
        private bool _fullscreen;

        public MagicEngineGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("magicengine");

            string exe = Path.Combine(path, "pce.exe");
            if (!File.Exists(exe))
                return null;

            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            var commandArray = new List<string>
            {
                "\"" + rom + "\""
            };

            string args = string.Join(" ", commandArray);

            _resolution = resolution;

            SetupConfig(path, system);
            SetupControllers(path);

            //Applying bezels
            if (_fullscreen && SystemConfig["magicengine_renderer"] != "0")
                ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, emulator);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        private void SetupConfig(string path, string system)
        {
            string iniFile = Path.Combine(path, "pce.ini");

            if (!File.Exists(iniFile))
                try { File.WriteAllText(iniFile, ""); }
                catch { }

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
                {
                    // Write paths
                    string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "magicengine", system);
                    if (!Directory.Exists(cheatsPath))
                        try { Directory.CreateDirectory(cheatsPath); }
                        catch { }
                    string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "magicengine");
                    if (!Directory.Exists(screenshotsPath))
                        try { Directory.CreateDirectory(screenshotsPath); }
                        catch { }
                    string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system, "magicengine");
                    if (!Directory.Exists(savePath))
                        try { Directory.CreateDirectory(savePath); }
                        catch { }

                    ini.WriteValue("path", "cheats", cheatsPath);
                    ini.WriteValue("path", "screenshots", screenshotsPath);
                    ini.WriteValue("path", "saved_games", savePath);

                    // Emulator settings
                    ini.WriteValue("setup", "show", "n");
                    BindIniFeature(ini, "video", "driver", "magicengine_renderer", "1");
                    ini.WriteValue("video", "windowed", _fullscreen ? "n" : "y");
                    ini.WriteValue("video", "screen_width", _resolution != null ? _resolution.Width.ToString() : Screen.PrimaryScreen.Bounds.Width.ToString());
                    ini.WriteValue("video", "screen_height", _resolution != null ? _resolution.Height.ToString() : Screen.PrimaryScreen.Bounds.Height.ToString());
                    ini.WriteValue("video", "screen_depth", _resolution != null ? _resolution.BitsPerPel.ToString() : Screen.PrimaryScreen.BitsPerPixel.ToString());
                    BindIniFeature(ini, "video", "screen_ratio", "magicengine_ratio", "1,1");
                    ini.WriteValue("setup", "desktop", "n");
                    BindIniFeature(ini, "video", "vsync", "magicengine_vsync", "0,1");
                    BindIniFeatureSlider(ini, "video", "frameskip", "magicengine_frameskip", "0");
                    ini.WriteValue("video", "fullscreen", _fullscreen ? "0,1" : "0,0");
                    BindBoolIniFeature(ini, "video", "high_res", "magicengine_hires", "0,1", "0,0");
                    BindBoolIniFeature(ini, "video", "filter", "smooth", "0,1", "0,0");
                    BindIniFeature(ini, "video", "scanlines", "magicengine_scanlines", "0,0");
                    BindIniFeature(ini, "misc", "language", "magicengine_language", "0");
                    BindBoolIniFeature(ini, "misc", "fps_counter", "magicengine_showfps", "y", "n");
                    ini.WriteValue("misc", "background_scrolling", "n");
                    ini.WriteValue("misc", "esc_key_mode", "2");
                    
                    ini.Save();
                }
            }
            catch { }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
            {
                ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);
                return 0;
            }

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, path.WorkingDirectory);

            return ret;
        }
    }
}
