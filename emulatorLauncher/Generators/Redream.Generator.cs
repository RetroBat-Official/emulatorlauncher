using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class RedreamGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("redream");

            string exe = Path.Combine(path, "redream.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            bool wideScreen = SystemConfig["redream_aspect"] == "16:9" || SystemConfig["redream_aspect"] == "stretch";
            if (wideScreen)
                SystemConfig["forceNoBezel"] = "1";

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            SetupConfiguration(path, fullscreen);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "\"" + rom + "\"",
            };
        }

        private void SetupConfiguration(string path, bool fullscreen)
        {
            string conf = Path.Combine(path, "redream.cfg");

            using (var ini = IniFile.FromFile(conf))
            {
                ini.WriteValue("", "fullmode", fullscreen ? "borderless fullscreen" : "windowed");
                ini.WriteValue("", "mode", fullscreen ? "borderless fullscreen" : "windowed");
                ini.WriteValue("", "gamedir", "./../../roms/dreamcast");

                BindIniFeature(ini, "", "cable", "redream_cable", "rgb");
                BindIniFeature(ini, "", "broadcast", "redream_broadcast", "ntsc");
                BindIniFeature(ini, "", "language", "redream_language", "english");
                BindIniFeature(ini, "", "region", "redream_region", "japan");
                BindBoolIniFeature(ini, "", "vsync", "redream_vsync", "0", "1");
                BindBoolIniFeature(ini, "", "frameskip", "redream_frameskip", "1", "0");
                BindIniFeature(ini, "", "aspect", "redream_aspect", "4:3");

                ConfigureControllers(ini);
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
