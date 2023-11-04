using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Drawing;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
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

            SetupConfiguration(path, system, resolution);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        //Configuration file in json format "BigPEmuConfig.bigpcfg"
        private void SetupConfiguration(string path, string system, ScreenResolution resolution = null)
        {
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //open userdata config file
            string folder = Path.Combine(path, "userdata");
            if (!Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); }
                catch { }
            }

            string configfile = Path.Combine(folder, "BigPEmuConfig.bigpcfg");

            if (File.Exists(configfile))
            {
                var json = DynamicJson.Load(configfile);
                var bigpemucore = json.GetOrCreateContainer("BigPEmuConfig");

                //system part
                var jsonSystem = bigpemucore.GetOrCreateContainer("System");
                BindFeature(jsonSystem, "PALMode", "pal_mode", "0");
                jsonSystem["PerGameSlots"] = "1";
                jsonSystem["SaveAutoIncr"] = "1";

                if (system == "jaguarcd")
                {
                    jsonSystem["AttachButch"] = "1";
                    jsonSystem["AttachMT"] = "1";
                }
                else
                {
                    jsonSystem["AttachButch"] = "0";
                    jsonSystem["AttachMT"] = "0";
                }
                BindBoolFeature(jsonSystem, "ForceJGD", "bigpemu_jgd", "1", "0");

                //video part
                var video = bigpemucore.GetOrCreateContainer("Video");
                BindFeature(video, "DisplayMode", "displaymode", fullscreen ? "0" : "1");      //0 for borderless windows, 1 for windowed, 2 for fullscreen
                BindFeature(video, "VSync", "vsync", "1");                  // vsync on as default setting
                BindFeature(video, "HDROutput", "enable_hdr", "0");
                video["ShittyFreqWarn"] = "0";

                if (resolution != null)
                {
                    video["DisplayWidth"] = (resolution.Width).ToString();
                    video["DisplayHeight"] = (resolution.Height).ToString();
                }
                else
                {
                    video.Remove("DisplayWidth");
                    video.Remove("DisplayHeight");
                }

                if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                {
                    string emuMonitor = "\\\\" + ".\\" + "DISPLAY" + SystemConfig["MonitorIndex"];
                    video["Display"] = emuMonitor;
                }

                BindFeature(video, "MSAAPref", "bigpemu_antialiasing", "0");
                BindFeature(video, "ScreenFilter", "smooth", "0");

                if (_bezelFileInfo == null && SystemConfig.isOptSet("bigpemu_ratio") && !string.IsNullOrEmpty(SystemConfig["bigpemu_ratio"]))
                {
                    if (SystemConfig["bigpemu_ratio"] == "stretch")                 // Stretch only if bezels are empty and ratio = STRETCH
                    {
                        video["ScreenScaling"] = "6";
                    }
                    else
                    {
                        video["ScreenScaling"] = "0";                       // else do not stretch and apply selected ratio
                        video["ScreenAspect"] = SystemConfig["bigpemu_ratio"];
                    }

                }
                else                                                        // set aspect ratio to 4/3 and no stretch (this is the standard case but also with bezels)
                {
                    video["ScreenScaling"] = "0";
                    video["ScreenAspect"] = "2";
                }

                // Allow use of internal effects if shaders are set to none
                if (SystemConfig["shaderset"] == "none" && SystemConfig.isOptSet("bigpemu_shader") && !string.IsNullOrEmpty(SystemConfig["bigpemu_shader"]))
                    bigpemucore["ScreenEffect"] = SystemConfig["bigpemu_shader"];
                else
                    bigpemucore["ScreenEffect"] = "";

                //save
                json.Save();
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
