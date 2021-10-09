using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class GsPlusGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("gsplus-win-sdl");
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("gsplus");
            
            string exe = Path.Combine(path, "gsplus.exe");
            if (!File.Exists(exe))
                return null;

            IniFile conf = new IniFile(Path.Combine(path, "config.txt"), true);

            conf.WriteValue(null, "s5d1", "");
            conf.WriteValue(null, "s5d2", "");
            conf.WriteValue(null, "s6d1", "");
            conf.WriteValue(null, "s6d2", "");
            conf.WriteValue(null, "s7d1", "");

            if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig["bios"]))
                conf.WriteValue(null, "g_cfg_rom_path", Path.Combine(AppConfig.GetFullPath("bios"), "APPLE2GS.ROM"));

            if (Path.GetExtension(rom).ToLowerInvariant() == ".2mg")
                conf.WriteValue(null, "s7d1", rom);

            conf.Save();

            _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            _resolution = resolution;

            string screenShots = "";
            if (!string.IsNullOrEmpty(AppConfig["thumbnails"]) && Directory.Exists(AppConfig["thumbnails"]))
                screenShots = " -ssdir \"" + AppConfig.GetFullPath("thumbnails") + "\"";

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-fullscreen -borderless -sw 1920 -sh 1080"+screenShots,
            };
        }

        public override void RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();
        }

    }
}
