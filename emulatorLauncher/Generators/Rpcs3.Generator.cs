using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class Rpcs3Generator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("rpcs3");

            string exe = Path.Combine(path, "rpcs3.exe");
            if (!File.Exists(exe))
                return null;

            rom = this.TryUnZipGameIfNeeded(system, rom);

            if (Directory.Exists(rom))
            {
                string eboot = Path.Combine(rom, "PS3_GAME\\USRDIR\\EBOOT.BIN");
                if (!File.Exists(eboot))
                    eboot = Path.Combine(rom, "USRDIR\\EBOOT.BIN");

                if (!File.Exists(eboot))
                    throw new ApplicationException("Unable to find any game in the provided folder");

                rom = eboot;
            }
            else if (Path.GetExtension(rom).ToLower() == ".m3u")
            {
                string romPath = Path.GetDirectoryName(rom);
                rom = File.ReadAllText(rom);

                if (rom.StartsWith(".\\") || rom.StartsWith("./"))
                    rom = Path.Combine(romPath, rom.Substring(2));
                else if (rom.StartsWith("\\") || rom.StartsWith("/"))
                    rom = Path.Combine(path, rom.Substring(1));
            }

            List<string> commandArray = new List<string>();
            commandArray.Add("\"" + rom + "\"");

            if (SystemConfig.isOptSet("gui") && !SystemConfig.getOptBoolean("gui"))
                commandArray.Add("--no-gui");

            string args = string.Join(" ", commandArray);

            // If game was uncompressed, say we are going to launch, so the deletion will not be silent
            ValidateUncompressedGame();

            SetupGuiConfiguration(path);
            SetupConfiguration(path);
            CreateControllerConfiguration(path);

            // Check if firmware is installed in emulator, if not and if firmware is available in \bios path then install it instead of running the game
            string firmware = Path.Combine(path, "dev_flash", "vsh", "etc", "version.txt");
            string biosPath = AppConfig.GetFullPath("bios");
            string biosPs3 = Path.Combine(biosPath, "PS3UPDAT.PUP");
            if (!File.Exists(firmware) && File.Exists(biosPs3))
            {
                List<string> commandArrayfirmware = new List<string>();
                commandArrayfirmware.Add("--installfw");
                commandArrayfirmware.Add(biosPs3);
                string argsfirmware = string.Join(" ", commandArrayfirmware);
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path,
                    Arguments = argsfirmware,
                };
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
                WindowStyle = ProcessWindowStyle.Minimized
            };
        }

        #region features
        /// Set 6 options in rpcs3 GUI settings to disable prompts (updates, exit, launching game...)
        private void SetupGuiConfiguration(string path)
        {
            string guiSettings = Path.Combine(path, "GuiConfigs", "CurrentSettings.ini");
            using (var ini = new IniFile(guiSettings))
            {
                ini.WriteValue("main_window", "confirmationBoxExitGame", "false");
                ini.WriteValue("main_window", "infoBoxEnabledInstallPUP", "false");
                ini.WriteValue("main_window", "infoBoxEnabledWelcome", "false");
                ini.WriteValue("main_window", "confirmationBoxBootGame", "false");
                ini.WriteValue("main_window", "infoBoxEnabledInstallPKG", "false");
                ini.WriteValue("Meta", "checkUpdateStart", "false");
            }
        }

        /// Setup config.yml file
        private void SetupConfiguration(string path)
        {
            var yml = YmlFile.Load(Path.Combine(path, "config.yml"));

            // Handle Core part of yml file
            var core = yml.GetOrCreateContainer("Core");
            BindFeature(core, "PPU Decoder", "ppudecoder", "Recompiler (LLVM)"); //this option changes in the latest version of RCPS3 (es_features only)
            BindFeature(core, "PPU LLVM Precompilation", "lvmprecomp", "true");
            BindFeature(core, "SPU Decoder", "spudecoder", "Recompiler (LLVM)"); //this option changes in the latest version of RCPS3 (es_features only)
            BindFeature(core, "Lower SPU thread priority", "lowerspuprio", "false");
            BindFeature(core, "Preferred SPU Threads", "sputhreads", "0");
            BindFeature(core, "SPU loop detection", "spuloopdetect", "false");
            BindFeature(core, "SPU Block Size", "spublocksize", "Safe");
            BindFeature(core, "Accurate RSX reservation access", "accuratersx", "false");
            BindFeature(core, "PPU LLVM Accurate Vector NaN values", "vectornan", "false");
            BindFeature(core, "Full Width AVX-512", "fullavx", "false");

            //xfloat is managed through 3 options now in latest release
            if (SystemConfig.isOptSet("xfloat") && (SystemConfig["xfloat"] == "Accurate"))
            {
                core["Accurate xfloat"] = "true";
                core["Approximate xfloat"] = "false";
                core["Relaxed xfloat"] = "false";
            }
            else if (SystemConfig.isOptSet("xfloat") && (SystemConfig["xfloat"] == "Relaxed"))
            {
                core["Accurate xfloat"] = "false";
                core["Approximate xfloat"] = "false";
                core["Relaxed xfloat"] = "true";
            }
            else if (Features.IsSupported("xfloat"))
            {
                core["Accurate xfloat"] = "false";
                core["Approximate xfloat"] = "true";
                core["Relaxed xfloat"] = "false";
            }

            // Handle Video part of yml file
            var video = yml.GetOrCreateContainer("Video");
            BindFeature(video, "Renderer", "gfxbackend", "Vulkan");
            BindFeature(video, "Resolution", "rpcs3_internal_resolution", "1280x720");
            BindFeature(video, "Aspect ratio", "ratio", "16:9");
            BindFeature(video, "Frame limit", "framelimit", "Auto");
            BindFeature(video, "MSAA", "msaa", "Auto");
            BindFeature(video, "Shader Mode", "shadermode", "Async Shader Recompiler");
            BindFeature(video, "Write Color Buffers", "writecolorbuffers", "false");
            BindFeature(video, "Write Depth Buffer", "writedepthbuffers", "false");
            BindFeature(video, "Read Color Buffers", "readcolorbuffers", "false");
            BindFeature(video, "Read Depth Buffer", "readdepthbuffers", "false");
            BindFeature(video, "VSync", "vsync", "false");
            BindFeature(video, "Stretch To Display Area", "stretchtodisplay", "false");
            BindFeature(video, "Strict Rendering Mode", "strict_rendering", "false");
            BindFeature(video, "Disable Vertex Cache", "disablevertex", "false");
            BindFeature(video, "Multithreaded RSX", "multithreadedrsx", "false");
            BindFeature(video, "Enable 3D", "enable3d", "false");
            BindFeature(video, "Anisotropic Filter Override", "anisotropicfilter", "0");
            BindFeature(video, "Shader Precision", "shader_quality", "Auto");

            //ZCULL Accuracy
            if (SystemConfig.isOptSet("zcull_accuracy") && (SystemConfig["zcull_accuracy"] == "Approximate"))
            {
                core["Relaxed ZCULL Sync"] = "false";
                core["Accurate ZCULL stats"] = "false";
            }
            else if (SystemConfig.isOptSet("zcull_accuracy") && (SystemConfig["zcull_accuracy"] == "Relaxed"))
            {
                core["Relaxed ZCULL Sync"] = "true";
                core["Accurate ZCULL stats"] = "false";
            }
            else if (Features.IsSupported("zcull_accuracy"))
            {
                core["Relaxed ZCULL Sync"] = "false";
                core["Accurate ZCULL stats"] = "true";
            }

            // Handle Vulkan part of yml file
            var vulkan = video.GetOrCreateContainer("Vulkan");
            BindFeature(vulkan, "Asynchronous Texture Streaming 2", "asynctexturestream", "false");
            BindFeature(vulkan, "Enable FidelityFX Super Resolution Upscaling", "fsr_upscaling", "false");

            // Handle Performance Overlay part of yml file
            var performance = video.GetOrCreateContainer("Performance Overlay");
            if (SystemConfig.isOptSet("performance_overlay") && (SystemConfig["performance_overlay"] == "detailed"))
            {
                performance["Enabled"] = "true";
                performance["Enable Framerate Graph"] = "true";
                performance["Enable Frametime Graph"] = "true";
            }
            else if (SystemConfig.isOptSet("performance_overlay") && (SystemConfig["performance_overlay"] == "simple"))
            {
                performance["Enabled"] = "true";
                performance["Enable Framerate Graph"] = "false";
                performance["Enable Frametime Graph"] = "false";
            }
            else if (Features.IsSupported("performance_overlay"))
            {
                performance["Enabled"] = "false";
                performance["Enable Framerate Graph"] = "false";
                performance["Enable Frametime Graph"] = "false";
            }

            // Handle Audio part of yml file
            var audio = yml.GetOrCreateContainer("Audio");
            BindFeature(audio, "Renderer", "audiobackend", "Cubeb");
            BindFeature(audio, "Audio Format", "audiochannels", "Stereo");

            // Handle System part of yml file
            var system_region = yml.GetOrCreateContainer("System");
            BindFeature(system_region, "License Area", "ps3_region", "SCEE");

            // Handle Miscellaneous part of yml file
            var misc = yml.GetOrCreateContainer("Miscellaneous");
            BindFeature(misc, "Start games in fullscreen mode", "startfullscreen", "true");

            // Save to yml file
            yml.Save();
        }
        #endregion features

        #region controller
        //Controller configuration
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            //Path does not exist by default so create it if inexistent
            string subpath = Path.Combine(path, "config", "input_configs", "global");
            System.IO.Directory.CreateDirectory(subpath);

            //Check if config file already exists or not and create it if not
            string controllerSettings = Path.Combine(path, "config", "input_configs", "global", "Default.yml");
            if (!File.Exists(controllerSettings))
            {
                StreamWriter tw = new StreamWriter(File.Open(controllerSettings, FileMode.OpenOrCreate, FileAccess.Write), Encoding.UTF8);
                tw.Close();
            }

            var yml = YmlFile.Load(controllerSettings);

            //Create a single Player block in the file for each player
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
            {
                if (controller.Config == null)
                    continue;

                ConfigureInputYml(controllerSettings, controller, yml);
            }
        }

        //Keyboard or joysticks ?
        private void ConfigureInputYml(string controllerSettings, Controller controller, YmlFile yml)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.Config.Type == "joystick")
                ConfigureJoystickYml(controllerSettings, controller, controller.PlayerIndex, yml);
            else
                ConfigureKeyboardYml(controllerSettings, controller.Config, yml);
        }

        #region keyboard
        //Configuration of keyboard
        private void ConfigureKeyboardYml(string controllerSettings, InputConfig keyboard, YmlFile yml)
        {
            if (keyboard == null)
                return;

            //Create player section (only 1 player with keyboard)
            var player = yml.GetOrCreateContainer("Player 1 Input");
            player["Handler"] = "Keyboard";
            player["Device"] = "Keyboard";

            var config = player.GetOrCreateContainer("Config");

            //Define action to generate key mappings based on SdlToKeyCode
            Action<string, InputKey> writemapping = (v, k) =>
            {
                var a = keyboard[k];
                if (a != null)
                {
                    string value = SdlToKeyCode(a.Id);
                    config[v] = value;
                }
                else
                    return;
            };

            //Perform mappings based on es_input
            writemapping("Left Stick Left", InputKey.leftanalogleft);
            writemapping("Left Stick Down", InputKey.leftanalogdown);
            writemapping("Left Stick Right", InputKey.leftanalogright);
            writemapping("Left Stick Up", InputKey.leftanalogup);
            writemapping("Right Stick Left", InputKey.rightanalogleft);
            writemapping("Right Stick Down", InputKey.rightanalogdown);
            writemapping("Right Stick Right", InputKey.rightanalogright);
            writemapping("Right Stick Up", InputKey.rightanalogup);
            writemapping("Start", InputKey.start);
            writemapping("Select", InputKey.select);
            writemapping("PS Button", InputKey.hotkey);
            writemapping("Square", InputKey.y);
            writemapping("Cross", InputKey.b);
            writemapping("Circle", InputKey.a);
            writemapping("Triangle", InputKey.x);
            writemapping("Left", InputKey.left);
            writemapping("Down", InputKey.down);
            writemapping("Right", InputKey.right);
            writemapping("Up", InputKey.up);
            writemapping("R1", InputKey.pagedown);
            writemapping("R2", InputKey.r2);
            writemapping("R3", InputKey.r3);
            writemapping("L1", InputKey.pageup);
            writemapping("L2", InputKey.l2);
            writemapping("L3", InputKey.l3);

            var motionx = config.GetOrCreateContainer("Motion Sensor X");
            motionx["Axis"] = "\"\"";
            motionx["Mirrored"] = "false";
            motionx["Shift"] = "0";

            var motiony = config.GetOrCreateContainer("Motion Sensor Y");
            motiony["Axis"] = "\"\"";
            motiony["Mirrored"] = "false";
            motiony["Shift"] = "0";

            var motionz = config.GetOrCreateContainer("Motion Sensor Z");
            motionz["Axis"] = "\"\"";
            motionz["Mirrored"] = "false";
            motionz["Shift"] = "0";

            var motiong = config.GetOrCreateContainer("Motion Sensor G");
            motiong["Axis"] = "\"\"";
            motiong["Mirrored"] = "false";
            motiong["Shift"] = "0";

            config["Pressure Intensity Button"] = "\"\"";
            config["Pressure Intensity Percent"] = "50";
            config["Left Stick Multiplier"] = "100";
            config["Right Stick Multiplier"] = "100";
            config["Left Stick Deadzone"] = "0";
            config["Right Stick Deadzone"] = "0";
            config["Left Trigger Threshold"] = "0";
            config["Right Trigger Threshold"] = "0";
            config["Left Pad Squircling Factor"] = "0";
            config["Right Pad Squircling Factor"] = "0";
            config["Color Value R"] = "0";
            config["Color Value G"] = "0";
            config["Color Value B"] = "0";
            config["Blink LED when battery is below 20%"] = "true";
            config["Use LED as a battery indicator"] = "false";
            config["LED battery indicator brightness"] = "50";
            config["Player LED enabled"] = "true";
            config["Enable Large Vibration Motor"] = "true";
            config["Enable Small Vibration Motor"] = "true";
            config["Switch Vibration Motors"] = "false";
            config["Mouse Movement Mode"] = "Relative";
            config["Mouse Deadzone X Axis"] = "60";
            config["Mouse Deadzone Y Axis"] = "60";
            config["Mouse Acceleration X Axis"] = "200";
            config["Mouse Acceleration Y Axis"] = "250";
            config["Left Stick Lerp Factor"] = "100";
            config["Right Stick Lerp Factor"] = "100";
            config["Analog Button Lerp Factor"] = "100";
            config["Trigger Lerp Factor"] = "100";
            config["Device Class Type"] = "0";
            config["Vendor ID"] = "1356";
            config["Product ID"] = "616";
            player["Buddy Device"] = "\"\"";

            // Save to yml file
            yml.Save();

        }
        #endregion keyboard

        #region joysticks
        //Configuration of Joysticks
        private void ConfigureJoystickYml(string controllerSettings, Controller ctrl, int playerIndex, YmlFile yml)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            string handler = "MMJoystick";

            //check controller type
            string devicename = joy.DeviceName;
            int device;

            //Check if controller is a XInput device
            bool xinput = false;
            if (ctrl.IsXInputDevice)
            {
                xinput = true;
                handler = "XInput";
            }

            var vendor = ctrl.Guid.FromSdlGuidString().GetVendorID();
            var product = ctrl.Guid.FromSdlGuidString().GetProductID();

            bool isdualsense = (vendor == VendorIds.USB_VENDOR_SONY && product == ProductIds.USB_PRODUCT_SONY_DS5);
            bool isds3 = (vendor == VendorIds.USB_VENDOR_SONY && (product == ProductIds.USB_PRODUCT_SONY_DS3 || product == ProductIds.USB_PRODUCT_SHANWAN_DS3));
            bool isds4 = (vendor == VendorIds.USB_VENDOR_SONY && (product == ProductIds.USB_PRODUCT_SONY_DS4 || product == ProductIds.USB_PRODUCT_SONY_DS4_SLIM || product == ProductIds.USB_PRODUCT_SONY_DS4_DONGLE));

            //Create Player block titles
            string playerBlockTitle = "Player" + " " + playerIndex + " " + "Input";
            var player = yml.GetOrCreateContainer(playerBlockTitle);
            player["Handler"] = handler;

            /*Logic to fill Device tag of the file, composed of type of handler + #
                Logic for #:
                    DS3 Pad # - - incremental based on # of DS devices (from 1 to i)
                    DS4 Pad # - incremental based on # of DS4 devices (from 1 to i)
                    DualSense Pad # - incremental based on # of DS devices (from 1 to i)
                    XInput Pad # - incremental based on number of XInput devices (from 1 to i)
                    Joystick # - based on joystick index
             */

            List<string> ControllerList = new List<string>();

            //Sort PS5 pads by index
            if (isdualsense)
            {
                int index = Program.Controllers
                    .Where(c => c.Name == "PS5 Controller")
                    .OrderBy(c => c.PlayerIndex)
                    .Select(c => c)
                    .ToList()
                    .IndexOf(ctrl);
                device = index + 1;
                player["Device"] = "\"" + "DualSense Pad #" + device + "\"";
            }

            //Sort PS4 pads by index
            else if (isds4)
            {
                int index = Program.Controllers
                    .Where(c => c.Name == "PS4 Controller")
                    .OrderBy(c => c.PlayerIndex)
                    .Select(c => c)
                    .ToList()
                    .IndexOf(ctrl);
                device = index + 1;
                player["Device"] = "\"" + "DS4 Pad #" + device + "\"";
            }
            //Sort PS3 pads by index
            else if (isds3)
            {
                int index = Program.Controllers
                    .Where(c => c.Name == ("PS3 Controller") || c.Name == ("PLAYSTATION(R)3 Controller"))
                    .OrderBy(c => c.PlayerIndex)
                    .Select(c => c)
                    .ToList()
                    .IndexOf(ctrl);
                device = index + 1;
                player["Device"] = "\"" + "DS3 Pad #" + device + "\"";
            }
            //Sort XInput pads by index
            else if (xinput)
            {
                int index = ctrl.XInput.DeviceIndex;
                device = index + 1;
                player["Device"] = "\"" + "XInput Pad #" + device + "\"";
            }
            //Sort WinMM pads by index
            else
            {
                int index = ctrl.WinmmJoystick.Index;
                device = index + 1;
                player["Device"] = "\"" + "Joystick #" + device + "\"";
            }

            //player["Device"] = device;
            var config = player.GetOrCreateContainer("Config");

            //Mapping for buttons
            if (isdualsense || isds4)
            {
                config["Left Stick Left"] = "LS X-";
                config["Left Stick Down"] = "LS Y-";
                config["Left Stick Right"] = "LS X+";
                config["Left Stick Up"] = "LS Y+";
                config["Right Stick Left"] = "RS X-";
                config["Right Stick Down"] = "RS Y-";
                config["Right Stick Right"] = "RS X+";
                config["Right Stick Up"] = "RS Y+";
                config["Start"] = "Options";
                config["Select"] = "Share";
                config["PS Button"] = "PS Button";
                config["Square"] = "Square";
                config["Cross"] = "Cross";
                config["Circle"] = "Circle";
                config["Triangle"] = "Triangle";
                config["Left"] = "Left";
                config["Down"] = "Down";
                config["Right"] = "Right";
                config["Up"] = "Up";
                config["R1"] = "R1";
                config["R2"] = "R2";
                config["R3"] = "R3";
                config["L1"] = "L1";
                config["L2"] = "L2";
                config["L3"] = "L3";
            }
            else if (isds3)
            {
                config["Left Stick Left"] = "LS X-";
                config["Left Stick Down"] = "LS Y-";
                config["Left Stick Right"] = "LS X+";
                config["Left Stick Up"] = "LS Y+";
                config["Right Stick Left"] = "RS X-";
                config["Right Stick Down"] = "RS Y-";
                config["Right Stick Right"] = "RS X+";
                config["Right Stick Up"] = "RS Y+";
                config["Start"] = "Start";
                config["Select"] = "Select";
                config["PS Button"] = "PS Button";
                config["Square"] = "Square";
                config["Cross"] = "Cross";
                config["Circle"] = "Circle";
                config["Triangle"] = "Triangle";
                config["Left"] = "Left";
                config["Down"] = "Down";
                config["Right"] = "Right";
                config["Up"] = "Up";
                config["R1"] = "R1";
                config["R2"] = "R2";
                config["R3"] = "R3";
                config["L1"] = "L1";
                config["L2"] = "L2";
                config["L3"] = "L3";
            }
         /*   else if (isswitchpro)
            {
                config["Left Stick Left"] = "X-";
                config["Left Stick Down"] = "Y+";
                config["Left Stick Right"] = "X+";
                config["Left Stick Up"] = "Y-";
                config["Right Stick Left"] = "U-";
                config["Right Stick Down"] = "R+";
                config["Right Stick Right"] = "U+";
                config["Right Stick Up"] = "R-";
                config["Start"] = "Button 10";
                config["Select"] = "Button 9";
                config["PS Button"] = "Button 13";
                config["Square"] = "Button 3";
                config["Cross"] = "Button 1";
                config["Circle"] = "Button 2";
                config["Triangle"] = "Button 4";
                config["Left"] = "POV Left";
                config["Down"] = "POV Down";
                config["Right"] = "POV Right";
                config["Up"] = "POV Up";
                config["R1"] = "Button 6";
                config["R2"] = "Button 8";
                config["R3"] = "Button 12";
                config["L1"] = "Button 5";
                config["L2"] = "Button 7";
                config["L3"] = "Button 11";
            }*/
            else if (xinput)
            {                
                config["Left Stick Left"] = "LS X-";
                config["Left Stick Down"] = "LS Y-";
                config["Left Stick Right"] = "LS X+";
                config["Left Stick Up"] = "LS Y+";
                config["Right Stick Left"] = "RS X-";
                config["Right Stick Down"] = "RS Y-";
                config["Right Stick Right"] = "RS X+";
                config["Right Stick Up"] = "RS Y+";
                config["Start"] = "Start";
                config["Select"] = "Back";
                config["PS Button"] = "Guide";
                config["Square"] = "X";
                config["Cross"] = "A";
                config["Circle"] = "B";
                config["Triangle"] = "Y";
                config["Left"] = "Left";
                config["Down"] = "Down";
                config["Right"] = "Right";
                config["Up"] = "Up";
                config["R1"] = "RB";
                config["R2"] = "RT";
                config["R3"] = "RS";
                config["L1"] = "LB";
                config["L2"] = "LT";
                config["L3"] = "LS";
            }
            else
            {
                config["Left Stick Left"] = GetInputKeyName(ctrl, InputKey.joystick1left);  
                config["Left Stick Down"] = GetInputKeyName(ctrl, InputKey.joystick1down);  
                config["Left Stick Right"] = GetInputKeyName(ctrl, InputKey.joystick1right);
                config["Left Stick Up"] = GetInputKeyName(ctrl, InputKey.joystick1up);
                config["Right Stick Left"] = GetInputKeyName(ctrl, InputKey.joystick2left);
                config["Right Stick Down"] = GetInputKeyName(ctrl, InputKey.joystick2down);
                config["Right Stick Right"] = GetInputKeyName(ctrl, InputKey.joystick2right);
                config["Right Stick Up"] = GetInputKeyName(ctrl, InputKey.joystick2up);
                config["Start"] = GetInputKeyName(ctrl, InputKey.start);
                config["Select"] = GetInputKeyName(ctrl, InputKey.select);
                config["Square"] = GetInputKeyName(ctrl, InputKey.y); 
                config["Cross"] = GetInputKeyName(ctrl, InputKey.a);
                config["Circle"] = GetInputKeyName(ctrl, InputKey.b);
                config["Triangle"] = GetInputKeyName(ctrl, InputKey.x);
                config["Left"] = GetInputKeyName(ctrl, InputKey.left);
                config["Down"] = GetInputKeyName(ctrl, InputKey.down);
                config["Right"] = GetInputKeyName(ctrl, InputKey.right); 
                config["Up"] = GetInputKeyName(ctrl, InputKey.up);
                config["R1"] = GetInputKeyName(ctrl, InputKey.r1);
                config["R2"] = GetInputKeyName(ctrl, InputKey.r2);
                config["R3"] = GetInputKeyName(ctrl, InputKey.r3);
                config["L1"] = GetInputKeyName(ctrl, InputKey.l1);
                config["L2"] = GetInputKeyName(ctrl, InputKey.l2);
                config["L3"] = GetInputKeyName(ctrl, InputKey.l3);

                if (GetInputKeyName(ctrl, InputKey.hotkey) == GetInputKeyName(ctrl, InputKey.select))
                    config["PS Button"] = null;
                else
                    config["PS Button"] = GetInputKeyName(ctrl, InputKey.hotkey);

            }

            //Motion setting
            var motionx = config.GetOrCreateContainer("Motion Sensor X");
            motionx["Axis"] = "\"\"";
            motionx["Mirrored"] = "false";
            motionx["Shift"] = "0";
            var motiony = config.GetOrCreateContainer("Motion Sensor Y");
            motiony["Axis"] = "\"\"";
            motiony["Mirrored"] = "false";
            motiony["Shift"] = "0";
            var motionz = config.GetOrCreateContainer("Motion Sensor Z");
            motionz["Axis"] = "\"\"";
            motionz["Mirrored"] = "false";
            motionz["Shift"] = "0";
            var motiong = config.GetOrCreateContainer("Motion Sensor G");
            motiong["Axis"] = "\"\"";
            motiong["Mirrored"] = "false";
            motiong["Shift"] = "0";

            //other settings
            config["Pressure Intensity Button"] = "\"\"";
            config["Pressure Intensity Percent"] = "50";
            config["Left Stick Multiplier"] = "100";
            config["Right Stick Multiplier"] = "100";

            if (isdualsense || isds3 || isds4)
            {
                config["Left Stick Deadzone"] = "50";
                config["Right Stick Deadzone"] = "50";
                config["Left Trigger Threshold"] = "0";
                config["Right Trigger Threshold"] = "0";
            }
            else
            {
                config["Left Stick Deadzone"] = "0";
                config["Right Stick Deadzone"] = "0";
                config["Left Trigger Threshold"] = "0";
                config["Right Trigger Threshold"] = "0";
            }
            if (xinput)
            {
                config["Left Stick Deadzone"] = "7849";
                config["Right Stick Deadzone"] = "8689";
                config["Left Trigger Threshold"] = "30";
                config["Right Trigger Threshold"] = "30";
            }

            config["Left Pad Squircling Factor"] = "8000";
            config["Right Pad Squircling Factor"] = "8000";

            if (isdualsense || isds4)
            {
                config["Color Value R"] = "0";
                config["Color Value G"] = "0";
                config["Color Value B"] = "20";
            }
            else
            {
                config["Color Value R"] = "0";
                config["Color Value G"] = "0";
                config["Color Value B"] = "0";
            }

            config["Blink LED when battery is below 20%"] = "true";
            config["Use LED as a battery indicator"] = "false";

            if (isds4)
                config["LED battery indicator brightness"] = "10";
            else
                config["LED battery indicator brightness"] = "50";

            config["Player LED enabled"] = "true";
            config["Enable Large Vibration Motor"] = "true";
            config["Enable Small Vibration Motor"] = "true";
            config["Switch Vibration Motors"] = "false";
            config["Mouse Movement Mode"] = "Relative";
            config["Mouse Deadzone X Axis"] = "60";
            config["Mouse Deadzone Y Axis"] = "60";
            config["Mouse Acceleration X Axis"] = "200";
            config["Mouse Acceleration Y Axis"] = "250";
            config["Left Stick Lerp Factor"] = "100";
            config["Right Stick Lerp Factor"] = "100";
            config["Analog Button Lerp Factor"] = "100";
            config["Trigger Lerp Factor"] = "100";
            config["Device Class Type"] = "0";
            config["Vendor ID"] = "1356";
            config["Product ID"] = "616";
            player["Buddy Device"] = "\"\"";

            // Save to yml file
            yml.Save();
        }
        #endregion joysticks

        private static Dictionary<InputKey, InputKey> revertedAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.joystick1right, InputKey.joystick1left },
            { InputKey.joystick1down, InputKey.joystick1up },
            { InputKey.joystick2right, InputKey.joystick2left },
            { InputKey.joystick2down, InputKey.joystick2up },
        };

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            bool revertAxis = false;
            
            InputKey revertedKey;
            if (revertedAxis.TryGetValue(key, out revertedKey))
            {
                key = revertedKey;
                revertAxis = true;
            }

            var input = c.Config[key];
            if (input != null)
            {

                if (input.Type == "button")
                    return "Button " + (input.Id + 1);

                if (input.Type == "hat")
                {
                    string hat = "POV Up";

                    if (input.Value == 2)
                        hat = "POV Right";
                    else if (input.Value == 4)
                        hat = "POV Down";
                    else if (input.Value == 8)
                        hat = "POV Left";

                    return hat;
                }

                if (input.Type == "axis")
                {
                    string[] axisNames = new string[] { "X", "Y", "Z", "R", "U" };

                    if (input.Id >= 0 && input.Id < axisNames.Length)
                    {
                        string axis = axisNames[input.Id];
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            axis += "+";
                        else
                            axis += "-";

                        return axis;
                    }
                }
            }

            return "\"\"";
        }

        //Search keyboard keycode
        private static string SdlToKeyCode(long sdlCode)
        {            
            switch (sdlCode)
            {
                case 0x0D: return "Enter";
                case 0x00: return "\"\"";
                case 0x08: return "Backspace";
                case 0x09: return "Tab";
                case 0x1B: return "Esc";
                case 0x20: return "Space";
                case 0x21: return "\"!\"";
                case 0x22: return "\"" + @"\" + "\"" + "\"";
                case 0x23: return "\"#\"";
                case 0x24: return "$";
                case 0x25: return "\"%\"";
                case 0x26: return "\"&\"";
                case 0x27: return @"\";
                case 0x28: return "(";
                case 0x29: return ")";
                case 0x2A: return "\"*\"";
                case 0x2B: return "\"+\"";
                case 0x2C: return "\",\"";
                case 0x2D: return "\"-\"";
                case 0x2E: return "\".\"";
                case 0x2F: return "/";
                case 0x30: return "0";
                case 0x31: return "1";
                case 0x32: return "2";
                case 0x33: return "3";
                case 0x34: return "4";
                case 0x35: return "5";
                case 0x36: return "6";
                case 0x37: return "7";
                case 0x38: return "8";
                case 0x39: return "9";
                case 0x3A: return "\":\"";
                case 0x3B: return ";";
                case 0x3C: return "<";
                case 0x3D: return "=";
                case 0x3F: return ">";
                case 0x40: return "\"" + "@" + "\"";
                case 0x5B: return "\"[\"";
                case 0x5C: return @"\";
                case 0x5D: return "\"]\"";
                case 0x5E: return "^";
                case 0x5F: return "_";
                case 0x60: return "\"'\"";
                case 0x61: return "A";
                case 0x62: return "B";
                case 0x63: return "C";
                case 0x64: return "D";
                case 0x65: return "E";
                case 0x66: return "F";
                case 0x67: return "G";
                case 0x68: return "H";
                case 0x69: return "I";
                case 0x6A: return "J";
                case 0x6B: return "K";
                case 0x6C: return "L";
                case 0x6D: return "M";
                case 0x6E: return "N";
                case 0x6F: return "O";
                case 0x70: return "P";
                case 0x71: return "Q";
                case 0x72: return "R";
                case 0x73: return "S";
                case 0x74: return "T";
                case 0x75: return "U";
                case 0x76: return "V";
                case 0x77: return "W";
                case 0x78: return "X";
                case 0x79: return "Y";
                case 0x7A: return "Z";
                case 0x7F: return "Del";
                case 0x40000039: return "CapsLock";
                case 0x4000003A: return "F1";
                case 0x4000003B: return "F2";
                case 0x4000003C: return "F3";
                case 0x4000003D: return "F4";
                case 0x4000003E: return "F5";
                case 0x4000003F: return "F6";
                case 0x40000040: return "F7";
                case 0x40000041: return "F8";
                case 0x40000042: return "F9";
                case 0x40000043: return "F10";
                case 0x40000044: return "F11";
                case 0x40000045: return "F12";
                case 0x40000046: return "PrintScreen";
                case 0x40000047: return "ScrollLock";
                case 0x40000048: return "Pause";
                case 0x40000049: return "Ins";
                case 0x4000004A: return "Home";
                case 0x4000004B: return "PgUp";
                case 0x4000004D: return "End";
                case 0x4000004E: return "PgDown";
                case 0x4000004F: return "Right";
                case 0x40000050: return "Left";
                case 0x40000051: return "Down";
                case 0x40000052: return "Up";
                case 0x40000053: return "NumLock";
                case 0x40000054: return "Num+/";
                case 0x40000055: return "Num+*";
                case 0x40000056: return "Num+-";
                case 0x40000057: return "Num++";
                case 0x40000058: return "Num+Enter";
                case 0x40000059: return "Num+1";
                case 0x4000005A: return "Num+2";
                case 0x4000005B: return "Num+3";
                case 0x4000005C: return "Num+4";
                case 0x4000005D: return "Num+5";
                case 0x4000005E: return "Num+6";
                case 0x4000005F: return "Num+7";
                case 0x40000060: return "Num+8";
                case 0x40000061: return "Num+9";
                case 0x40000062: return "Num+0";
                case 0x40000063: return "Num+.";
                case 0x40000067: return "Num+=";
                case 0x40000068: return "F13";
                case 0x40000069: return "F14";
                case 0x4000006A: return "F15";
                case 0x4000006B: return "F16";
                case 0x4000006C: return "F17";
                case 0x4000006D: return "F18";
                case 0x4000006E: return "F19";
                case 0x4000006F: return "F20";
                case 0x40000070: return "F21";
                case 0x40000071: return "F22";
                case 0x40000072: return "F23";
                case 0x40000073: return "F24";
                case 0x40000074: return "Execute";
                case 0x40000075: return "Help";
                case 0x40000076: return "Menu";
                case 0x40000077: return "Select";
                case 0x40000078: return "Stop";
                case 0x40000079: return "Again";
                case 0x4000007A: return "Undo";
                case 0x4000007B: return "Cut";
                case 0x4000007C: return "Copy";
                case 0x4000007D: return "Paste";
                case 0x4000007E: return "Menu";
                case 0x4000007F: return "Volume Mute";
                case 0x40000080: return "Volume Up";
                case 0x40000081: return "Volume Down";
                case 0x40000085: return "Num+,";
                case 0x400000E0: return "Ctrl Left";
                case 0x400000E1: return "Shift Left";
                case 0x400000E2: return "Alt";
                case 0x400000E4: return "Ctrl Right";
                case 0x400000E5: return "Shift Right";
                case 0x40000101: return "Mode";
                case 0x40000102: return "Media Next";
                case 0x40000103: return "Media Previous";
                case 0x40000105: return "Media Play";
            }
            return "\"\"";
        }
        #endregion controller
    }

}
