using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Linq;

namespace EmulatorLauncher
{
    partial class Gopher64Generator : Generator
    {
        public Gopher64Generator()
        {
            DependsOnDesktopResolution = true;
        }

        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "gopher64-windows-x86_64.exe");
            if (!File.Exists(exe))
                return null;

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            // Ensure portable folder exists
            string portableFolder = Path.Combine(path, "portable_data");
            if (!Directory.Exists(portableFolder)) try { Directory.CreateDirectory(portableFolder); }
                catch { }

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // Bezels
            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            string setupPath = Path.Combine(path, "portable_data", "config");

            SetupConfiguration(setupPath, fullscreen);

            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("--fullscreen");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        //Manage Config.json file settings
        private void SetupConfiguration(string setupPath, bool fullscreen)
        {
            var json = DynamicJson.Load(Path.Combine(setupPath, "config.json"));
            var input = json.GetOrCreateContainer("input");
            var transfer_pak = input.GetOrCreateContainer("transfer_pak");
            var video = json.GetOrCreateContainer("video");
            var emulations = json.GetOrCreateContainer("emulation");

            ConfigureControls(input);

            // Set fullscreen
            video["fullscreen"] = fullscreen ? "true" : "false";

            BindBoolFeature(video, "integer_scaling", "integerscale", "true", "false");
            BindBoolFeature(video, "widescreen", "gopher64_widescreen", "true", "false");
            //BindBoolFeature(video, "crt", "gopher64_crt", "true", "false");
            
            if (SystemConfig.isOptSet("gopher64_resolution") && !string.IsNullOrEmpty(SystemConfig["gopher64_resolution"]))
            {
                string res = SystemConfig["gopher64_resolution"].ToIntegerString();
                video["upscale"] = res;
            }
            else
                video["upscale"] = "1";

            //save config file
            json.Save();
        }

        private void ConfigureControls(DynamicJson input)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // Set default input profiles
            List<string> profiles = new List<string>
            {
                "default",
                "default",
                "default",
                "default"
            };
            input.SetObject("input_profile_binding", profiles);

            int padCount = this.Controllers.Where(c => !c.IsKeyboard).Count();

            // Initiate HID list
            List<string> controllersHID = new List<string>
            {
                null,
                null,
                null,
                null
            };

            // Enable controllers
            List<bool> controllersEnabled = new List<bool>
            {
                true,
                false,
                false,
                false
            };

            int i = 0;

            // Loop through controllers and assign them to the first 4 slots
            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).Take(4))
            {
                string cPath = controller.DirectInput.DevicePath
                    .Replace("hid#", "HID#")
                    .Replace("_vid", "_VID")
                    .Replace("_pid", "_PID")
                    .Replace("vid_", "VID_")
                    .Replace("pid_", "PID_")
                    .Replace("ig_", "IG_")
                    ;
                controllersHID[i] = cPath;
                controllersEnabled[i] = true;
            }
            input.SetObject("controller_assignment", controllersHID);
            input.SetObject("controller_enabled", controllersEnabled);
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
            {
                return 0;
            }

            return ret;
        }
    }
}
