using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using emulatorLauncher.Tools;
using System.Text.RegularExpressions;
using System.Drawing;

namespace emulatorLauncher
{
    class BigPEmuGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("bigpemu");

            string exe = Path.Combine(path, "BigPEmu.exe");
            if (!File.Exists(exe))
                return null;

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

            List<string> commandArray = new List<string>();

            //arguments:
            //first argument must always be the rom
            //-localdata : specify to use the config file stored in "userdata" folder within emulator folder instead of the one in %APPDATA%
            commandArray.Add("\"" + rom + "\"");
            commandArray.Add("-localdata");

            string args = string.Join(" ", commandArray);

            SetupConfiguration(path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
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

            if (ret == 1)
                return 0;

            return ret;
        }

        //Configuration file in json format "BigPEmuConfig.bigpcfg"
        private void SetupConfiguration(string path)
        {
            //open userdata config file
            string folder = Path.Combine(path, "userdata");
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); }
                catch { }
            }

            string configfile = Path.Combine(folder, "BigPEmuConfig.bigpcfg");

            var json = DynamicJson.Load(configfile);
            var bigpemucore = json.GetOrCreateContainer("BigPEmuConfig");

            //system part
            var system = bigpemucore.GetOrCreateContainer("System");
            BindFeature(system, "PALMode", "pal_mode", "0");
            
            //video part
            var video = bigpemucore.GetOrCreateContainer("Video");
            BindFeature(video, "DisplayMode", "displaymode", "0");      //0 for borderless windows, 1 for windowed, 2 for fullscreen
            BindFeature(video, "VSync", "vsync", "1");                  // vsync on as default setting
            BindFeature(video, "HDROutput", "enable_hdr", "0");

            //save
            json.Save();
        }
    }

}
