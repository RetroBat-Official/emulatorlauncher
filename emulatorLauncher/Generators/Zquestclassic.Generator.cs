using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class ZQuestClassicGenerator : Generator
    {
        public ZQuestClassicGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("zquestclassic");
            if (string.IsNullOrEmpty(path))
                return null;

            string exe = Path.Combine(path, "zplayer.exe");
            if (!File.Exists(exe))
                return null;

            // The save file is named after the game entry (zip name when compressed)
            string romName = Path.GetFileNameWithoutExtension(rom);
            string originalRom = rom;

            // ZC cannot read compressed quests: extract and look for the .qst file
            string ext = Path.GetExtension(rom).ToLowerInvariant();
            if (ext == ".zip" || ext == ".7z")
            {
                string uncompressedPath = TryUnZipGameIfNeeded(system, rom, true, false);
                if (Directory.Exists(uncompressedPath))
                    rom = Directory.GetFiles(uncompressedPath, "*.qst", SearchOption.AllDirectories).FirstOrDefault();

                if (string.IsNullOrEmpty(rom) || !File.Exists(rom))
                    throw new ApplicationException("No .qst file found in " + Path.GetFileName(originalRom));
            }

            bool fullscreen = ShouldRunFullscreen();

            // Stretching the game area is not compatible with decorations
            if (SystemConfig.getOptBoolean("zc_stretch"))
                SystemConfig["forceNoBezel"] = "1";

            if (fullscreen)
            {
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, originalRom, resolution, emulator);
                _resolution = resolution;

                if (_bezelFileInfo != null && _bezelFileInfo.PngFile != null)
                    SimpleLogger.Instance.Info("[Generator] Bezel file selected : " + _bezelFileInfo.PngFile);
            }

            SetupConfiguration(path, system, _bezelFileInfo != null);
            SetupControllers(path);

            // -standalone : direct quest launch, single save slot, no file select screen (zelda.cpp)
            // Quest path must be absolute: ZC resolves it as current_path / qstdir / arg
            // Second argument is the save file, resolved as save_folder / arg
            var commandArray = new List<string>
            {
                fullscreen ? "-fullscreen" : "-windowed",
                "-standalone",
                "\"" + rom + "\"",
                "\"" + romName + ".sav\""
            };

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = string.Join(" ", commandArray),
            };
        }

        private void SetupConfiguration(string path, string system, bool bezelEnabled)
        {
            string cfgFile = Path.Combine(path, "zc.cfg");

            try
            {
                using (var ini = IniFile.FromFile(cfgFile, IniOptions.KeepEmptyLines))
                {
                    // Saves : one .sav per quest in RetroBat saves folder
                    string savesPath = AppConfig.GetFullPath("saves");
                    if (!string.IsNullOrEmpty(savesPath))
                    {
                        string savePath = Path.Combine(savesPath, system, "zquestclassic");
                        FileTools.TryCreateDirectory(savePath);
                        ini.WriteValue("zeldadx", "save_folder", savePath.Replace("\\", "/"));
                    }

                    // Frontend friendly behaviour
                    ini.WriteValue("zeldadx", "clicktofreeze", "0");        // a mouse click would freeze the game
                    ini.WriteValue("zeldadx", "quickload_slot", "0");
                    ini.WriteValue("zeldadx", "quickload_last", "0");
                    ini.WriteValue("zeldadx", "replay_new_saves", "0");

                    // Video : the game is always centered with aspect ratio kept (render.cpp)
                    if (bezelEnabled)
                    {
                        // Fill the screen height so the game matches the bezel viewport
                        ini.WriteValue("zeldadx", "scaling_force_integer", "0");
                        ini.WriteValue("zeldadx", "stretch_game_area", "0");
                    }
                    else
                    {
                        BindBoolIniFeature(ini, "zeldadx", "scaling_force_integer", "zc_integerscale", "1", "0");
                        BindBoolIniFeature(ini, "zeldadx", "stretch_game_area", "zc_stretch", "1", "0");
                    }

                    BindBoolIniFeature(ini, "zeldadx", "scaling_mode", "zc_smooth", "1", "0");
                    BindBoolIniFeatureOn(ini, "zeldadx", "vsync", "zc_vsync", "1", "0");

                    // Audio
                    BindBoolIniFeature(ini, "zeldadx", "heart_beep", "zc_heart_beep", "1", "0");

                    // FPS
                    BindBoolIniFeature(ini, "zeldadx", "showfps", "zc_fps", "1", "0");
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Unable to write " + cfgFile + " : " + ex.Message);
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            // ZC fullscreen is a borderless window (ALLEGRO_FULLSCREEN_WINDOW), compatible with the fake bezel
            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            return ret;
        }
    }
}