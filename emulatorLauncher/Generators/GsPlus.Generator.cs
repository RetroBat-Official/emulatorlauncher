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
        public GsPlusGenerator()
        {
            DependsOnDesktopResolution = true;
        }

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

            using (var conf = new IniFile(Path.Combine(path, "config.txt"), IniOptions.UseSpaces))
            {
                conf.WriteValue(null, "s5d1", "");
                conf.WriteValue(null, "s5d2", "");
                conf.WriteValue(null, "s6d1", "");
                conf.WriteValue(null, "s6d2", "");
                conf.WriteValue(null, "s7d1", "");

                if (!string.IsNullOrEmpty(AppConfig["bios"]) && Directory.Exists(AppConfig["bios"]))
                    conf.WriteValue(null, "g_cfg_rom_path", Path.Combine(AppConfig.GetFullPath("bios"), "APPLE2GS.ROM"));

                if (Path.GetExtension(rom).ToLowerInvariant() == ".2mg")
                    conf.WriteValue(null, "s7d1", rom);
            }

            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            List<string> commandArray = new List<string>();
            commandArray.Add("-borderless");         
            commandArray.AddRange(new string[] { "-sw", (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString() });
            commandArray.AddRange(new string[] { "-sh", (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString() });

            if (!string.IsNullOrEmpty(AppConfig["thumbnails"]) && Directory.Exists(AppConfig["thumbnails"]))
                commandArray.AddRange(new string[] { "-ssdir", AppConfig.GetFullPath("thumbnails") });

            var args = string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            // GsPlus always returns 1
            if (ret == 1)
                return 0;

            return ret;
        }

    }
}
