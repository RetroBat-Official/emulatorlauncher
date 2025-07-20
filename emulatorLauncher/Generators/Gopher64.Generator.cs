using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

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
            // Read and parse JSON
            string configFile = Path.Combine(setupPath, "config.json");

            JObject root;
            
            if (File.Exists(configFile))
            {
                string jsonText = File.ReadAllText(configFile);
                root = JObject.Parse(jsonText);
            }
            else
            {
                root = new JObject
                {
                    ["input"] = new JObject
                    {
                        ["input_profiles"] = new JObject
                        {
                            ["default"] = new JObject()
                        }
                    }
                };
            }

            if (root["input"] == null || root["input"].Type != JTokenType.Object)
                root["input"] = new JObject();
            var input = (JObject)root["input"];

            if (input["input_profiles"] == null || input["input_profiles"].Type != JTokenType.Object)
                input["input_profiles"] = new JObject();
            var profiles = (JObject)input["input_profiles"];

            ConfigureControls(input, profiles);

            if (root["video"] == null || root["video"].Type != JTokenType.Object)
                root["video"] = new JObject();
            var video = (JObject)root["video"];

            if (root["emulation"] == null || root["emulation"].Type != JTokenType.Object)
                root["emulation"] = new JObject();
            var emulation = (JObject)root["emulation"];

            // Set fullscreen
            video["fullscreen"] = fullscreen ? true : false;

            BindBoolFeature(video, "integer_scaling", "integerscale");
            BindBoolFeature(video, "widescreen", "gopher64_widescreen");
            
            if (SystemConfig.isOptSet("gopher64_resolution") && !string.IsNullOrEmpty(SystemConfig["gopher64_resolution"]))
            {
                string res = SystemConfig["gopher64_resolution"].ToIntegerString();
                video["upscale"] = res.ToInteger();
            }
            else
                video["upscale"] = 1;

            // Emulation
            BindBoolFeature(emulation, "disable_expansion_pak", "gopher64_disable_expansion_pak");

            //save config file
            string jsonString = root.ToString(Newtonsoft.Json.Formatting.Indented);

            File.WriteAllText(configFile, jsonString);
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
