using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class Lime3dsGenerator : Generator
    {
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Lime3DS");

            var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

            if (c1.IsKeyboard)
                ConfigureKeyboard(c1, ini);
            else
                ConfigureJoystick(c1, ini);
        }

        private void ConfigureJoystick(Controller controller, IniFile ini)
        {
            if (controller == null)
                return;

            var cfg = controller.Config;
            if (cfg == null)
                return;

            var guid = controller.GetSdlGuid(_sdlVersion, true);
            var limeGuid = guid.ToString().ToLowerInvariant();
            string newGuidPath = Path.Combine(AppConfig.GetFullPath("tools"), "controllerinfo.yml");
            string newGuid = SdlJoystickGuid.GetGuidFromFile(newGuidPath, controller.Guid, "lime3ds");
            if (newGuid != null)
                limeGuid = newGuid;

            //only 1 player so profile is fixed to 1
            ini.WriteValue("Controls", "profile\\default", "true");
            ini.WriteValue("Controls", "profile", "0");
            ini.WriteValue("Controls", "profiles\\1\\name\\default", "true");
            ini.WriteValue("Controls", "profiles\\1\\name", "default");

            string profile = "profiles\\1\\";

            //manage buttons and directions
            foreach (var map in Mapping)
            {
                string name = profile + map.Value;

                string cvalue = FromInput(controller, cfg[map.Key], limeGuid);

                if (string.IsNullOrEmpty(cvalue))
                {
                    ini.WriteValue("Controls", name + "\\default", "false");
                    ini.WriteValue("Controls", name, "[empty]");
                }
                else
                {
                    ini.WriteValue("Controls", name + "\\default", "false");
                    ini.WriteValue("Controls", name, "\"" + cvalue + "\"");
                }
            }

            //Keep default keyboard buttons for debug and gpio14
            ini.WriteValue("Controls", profile + "button_debug" + "\\default", "true");
            ini.WriteValue("Controls", profile + "button_debug", "\"" + "code:79,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_gpio14" + "\\default", "true");
            ini.WriteValue("Controls", profile + "button_gpio14", "\"" + "code:80,engine:keyboard" + "\"");

            //Manage mapping of triggers ZL and ZR
            foreach (var map in Zmapping)
            {
                string name = profile + map.Value;

                string cvalue = FromInput(controller, cfg[map.Key], limeGuid);

                if (string.IsNullOrEmpty(cvalue))
                {
                    ini.WriteValue("Controls", name + "\\default", "false");
                    ini.WriteValue("Controls", name, "[empty]");
                }
                else
                {
                    ini.WriteValue("Controls", name + "\\default", "false");
                    ini.WriteValue("Controls", name, "\"" + cvalue + "\"");
                }
            }

            //Home button - empty
            ini.WriteValue("Controls", profile + "button_home" + "\\default", "false");
            ini.WriteValue("Controls", profile + "button_home", "[empty]");

            //Manage sticks
            //left stick = circle pad
            //right stick = c-stick
            ProcessStick(controller, profile, "circle_pad", ini, limeGuid);
            ProcessStick(controller, profile, "c_stick", ini, limeGuid);
            
            //motion
            if (SystemConfig.isOptSet("n3ds_motion") && !string.IsNullOrEmpty(SystemConfig["n3ds_motion"]))
            {
                switch (SystemConfig["n3ds_motion"])
                {
                    case "cemuhook":
                        ini.WriteValue("Controls", profile + "motion_device" + "\\default", "false");
                        ini.WriteValue("Controls", profile + "motion_device", "\"engine:cemuhookudp\"");
                        break;
                    case "mouse":
                        ini.WriteValue("Controls", profile + "motion_device" + "\\default", "false");
                        ini.WriteValue("Controls", profile + "motion_device", "\"engine:motion_emu,update_period:100,sensitivity:0.01,tilt_clamp:90.0\"");
                        break;
                    case "sdl":
                        ini.WriteValue("Controls", profile + "motion_device" + "\\default", "false");
                        ini.WriteValue("Controls", profile + "motion_device", "\"engine:sdl,guid:" + limeGuid + ",port:0\"");
                        break;
                }
            }
            else
            {
                ini.WriteValue("Controls", profile + "motion_device" + "\\default", "true");
                ini.WriteValue("Controls", profile + "motion_device", "\"engine:motion_emu,update_period:100,sensitivity:0.01,tilt_clamp:90.0\"");
            }
            ini.WriteValue("Controls", profile + "touch_device" + "\\default", "true");
            ini.WriteValue("Controls", profile + "touch_device", "engine:emu_window");
            
            //touch from button
            ini.WriteValue("Controls", profile + "use_touch_from_button" + "\\default", "true");
            ini.WriteValue("Controls", profile + "use_touch_from_button", "false");
            ini.WriteValue("Controls", profile + "touch_from_button_map" + "\\default", "true");
            ini.WriteValue("Controls", profile + "touch_from_button_map", "0");

            //udp information
            ini.WriteValue("Controls", profile + "udp_input_address" + "\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_address", "127.0.0.1");
            ini.WriteValue("Controls", profile + "udp_input_port" + "\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_port", "26760");
            ini.WriteValue("Controls", profile + "udp_pad_index" + "\\default", "true");
            ini.WriteValue("Controls", profile + "udp_pad_index", "0");

            ini.WriteValue("Controls", "profiles\\size", "1");

            //in the future we can add profiles per game to use touch from button in the touchscreen (via features)
            ini.WriteValue("Controls", "touch_from_button_maps\\1\\name\\default", "true");
            ini.WriteValue("Controls", "touch_from_button_maps\\1\\name", "default");
            ini.WriteValue("Controls", "touch_from_button_maps\\1\\entries\\size", "0");
            ini.WriteValue("Controls", "touch_from_button_maps\\size", "1");

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());
        }

        private string FromInput(Controller controller, Input input, string guid)
        {
            if (input == null)
                return null;

            string value = "";

            if (input.Type == "button")
            { 
                if (controller.IsXInputDevice && Program.SystemConfig.isOptSet("gamepadbuttons") && Program.SystemConfig.getOptBoolean("gamepadbuttons"))
                {
                    long newID = input.Id;
                    if (input.Id == 0)
                        newID = 1;
                    else if (input.Id == 1)
                        newID = 0;
                    else if (input.Id == 2)
                        newID = 3;
                    else if (input.Id == 3)
                        newID = 2;

                    value = "button:" + newID + ",engine:sdl,guid:" + guid + ",port:0";
                }
                else
                    value = "button:" + input.Id + ",engine:sdl,guid:" + guid + ",port:0";
            }
            
            else if (input.Type == "hat")
                value = "direction:" + input.Name.ToString() + ",engine:sdl,guid:" + guid + ",hat:0,port:0";
            else if (input.Type == "axis")
                value = "axis:" + input.Id + ",direction:+,engine:sdl,guid:" + guid + ",port:0,threshold:0.5";

            return value;
        }

        private void ProcessStick(Controller controller, string profile, string stickName, IniFile ini, string guid)
        {
            var cfg = controller.Config;

            string name = profile + stickName;

            var leftVal = cfg[stickName == "circle_pad" ? InputKey.joystick1left : InputKey.joystick2left];
            var topVal = cfg[stickName == "circle_pad" ? InputKey.joystick1up : InputKey.joystick2up];

            if (leftVal != null && topVal != null && leftVal.Type == topVal.Type && leftVal.Type == "axis")
            {
                long limeleftval = leftVal.Id;
                long limetopval = topVal.Id;

                string value = "axis_x:" + limeleftval + ",axis_y:" + limetopval + ",deadzone:0.100000,engine:sdl,guid:" + guid + ",port:0";

                ini.WriteValue("Controls", name + "\\default", "false");
                ini.WriteValue("Controls", name, "\"" + value + "\"");
            }

            else
            {
                ini.WriteValue("Controls", name + "\\default", "false");
                ini.WriteValue("Controls", name, "[empty]");
            }
        }

        private static void ConfigureKeyboard(Controller controller, IniFile ini)
        {
            if (controller == null)
                return;

            InputConfig keyboard = controller.Config;
            if (keyboard == null)
                return;

            string profile = "profiles\\1\\";

            ini.WriteValue("Controls", "profile\\default", "true");
            ini.WriteValue("Controls", "profile", "0");
            ini.WriteValue("Controls", profile + "button_a\\default", "true");
            ini.WriteValue("Controls", profile + "button_a", "\"" + "code:65,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_b\\default", "true");
            ini.WriteValue("Controls", profile + "button_b", "\"" + "code:83,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_x\\default", "true");
            ini.WriteValue("Controls", profile + "button_x", "\"" + "code:90,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_y\\default", "true");
            ini.WriteValue("Controls", profile + "button_y", "\"" + "code:88,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_up\\default", "true");
            ini.WriteValue("Controls", profile + "button_up", "\"" + "code:84,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_down\\default", "true");
            ini.WriteValue("Controls", profile + "button_down", "\"" + "code:71,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_left\\default", "true");
            ini.WriteValue("Controls", profile + "button_left", "\"" + "code:70,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_right\\default", "true");
            ini.WriteValue("Controls", profile + "button_right", "\"" + "code:72,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_l\\default", "true");
            ini.WriteValue("Controls", profile + "button_l", "\"" + "code:81,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_r\\default", "true");
            ini.WriteValue("Controls", profile + "button_r", "\"" + "code:87,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_start\\default", "true");
            ini.WriteValue("Controls", profile + "button_start", "\"" + "code:77,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_select\\default", "true");
            ini.WriteValue("Controls", profile + "button_select", "\"" + "code:78,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_debug\\default", "true");
            ini.WriteValue("Controls", profile + "button_debug", "\"" + "code:79,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_gpio14\\default", "true");
            ini.WriteValue("Controls", profile + "button_gpio14", "\"" + "code:80,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_zl\\default", "true");
            ini.WriteValue("Controls", profile + "button_zl", "\"" + "code:49,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_zr\\default", "true");
            ini.WriteValue("Controls", profile + "button_zr", "\"" + "code:50,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_home\\default", "true");
            ini.WriteValue("Controls", profile + "button_home", "\"" + "code:66,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "circle_pad\\default", "true");
            ini.WriteValue("Controls", profile + "circle_pad", "\"" + "down:code$016777237$1engine$0keyboard,engine:analog_from_button,left:code$016777234$1engine$0keyboard,modifier:code$068$1engine$0keyboard,modifier_scale:0.500000,right:code$016777236$1engine$0keyboard,up:code$016777235$1engine$0keyboard" + "\"");
            ini.WriteValue("Controls", profile + "c_stick\\default", "true");
            ini.WriteValue("Controls", profile + "c_stick", "\"" + "down:code$075$1engine$0keyboard,engine:analog_from_button,left:code$074$1engine$0keyboard,modifier:code$068$1engine$0keyboard,modifier_scale:0.500000,right:code$076$1engine$0keyboard,up:code$073$1engine$0keyboard" + "\"");
            ini.WriteValue("Controls", profile + "motion_device\\default", "true");
            ini.WriteValue("Controls", profile + "motion_device", "\"" + "engine:motion_emu,update_period:100,sensitivity:0.01,tilt_clamp:90.0" + "\"");
            ini.WriteValue("Controls", profile + "touch_device\\default", "true");
            ini.WriteValue("Controls", profile + "touch_device", "\"" + "engine:emu_window" + "\"");
            ini.WriteValue("Controls", profile + "use_touch_from_button\\default", "true");
            ini.WriteValue("Controls", profile + "use_touch_from_button", "false");
            ini.WriteValue("Controls", profile + "touch_from_button_map\\default", "true");
            ini.WriteValue("Controls", profile + "touch_from_button_map", "0");
            ini.WriteValue("Controls", profile + "udp_input_address\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_address", "127.0.0.1");
            ini.WriteValue("Controls", profile + "udp_input_port\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_port", "26760");
            ini.WriteValue("Controls", profile + "udp_pad_index\\default", "true");
            ini.WriteValue("Controls", profile + "udp_pad_index", "0");
            ini.WriteValue("Controls", "profiles\\size", "1");
            ini.WriteValue("Controls", "touch_from_button_maps\\1\\name\\default", "true");
            ini.WriteValue("Controls", "touch_from_button_maps\\1\\name", "default");
            ini.WriteValue("Controls", "touch_from_button_maps\\1\\entries\\size", "0");
            ini.WriteValue("Controls", "touch_from_button_maps\\size", "1");
        }

        /*static Dictionary<string, string> DefKeys = new Dictionary<string, string>()
        {
            { "button_a", "code:65,engine:keyboard" },
            { "button_b","code:83,engine:keyboard" },
            { "button_x","code:90,engine:keyboard" },
            { "button_y","code:88,engine:keyboard" },
            { "button_up","code:84,engine:keyboard" },
            { "button_down","code:71,engine:keyboard" },
            { "button_left","code:70,engine:keyboard" },
            { "button_right","code:72,engine:keyboard" },
            { "button_l","code:81,engine:keyboard" },
            { "button_r","code:87,engine:keyboard" },
            { "button_start","code:77,engine:keyboard" },
            { "button_select","code:78,engine:keyboard" },
            { "button_debug","code:79,engine:keyboard" },
            { "button_gpio14","code:80,engine:keyboard" },
            { "button_zl","code:49,engine:keyboard" },
            { "button_zr","code:50,engine:keyboard" },
            { "button_home","code:66,engine:keyboard" },
            { "circle_pad","down:code$016777237$1engine$0keyboard,engine:analog_from_button,left:code$016777234$1engine$0keyboard,modifier:code$068$1engine$0keyboard,modifier_scale:0.500000,right:code$016777236$1engine$0keyboard,up:code$016777235$1engine$0keyboard" },
            { "c_stick","down:code$075$1engine$0keyboard,engine:analog_from_button,left:code$074$1engine$0keyboard,modifier:code$068$1engine$0keyboard,modifier_scale:0.500000,right:code$076$1engine$0keyboard,up:code$073$1engine$0keyboard" }, 
        };*/

        static readonly InputKeyMapping Mapping = new InputKeyMapping()
        {
            { InputKey.b,               "button_a" },
            { InputKey.a,               "button_b" },
            { InputKey.x,               "button_x" },
            { InputKey.y,               "button_y" },
            { InputKey.up,              "button_up" },
            { InputKey.down,            "button_down" },
            { InputKey.left,            "button_left" },
            { InputKey.right,           "button_right" },
            { InputKey.pageup,          "button_l" },
            { InputKey.pagedown,        "button_r" },
            { InputKey.start,           "button_start" },
            { InputKey.select,          "button_select" },
        };

        static readonly InputKeyMapping Zmapping = new InputKeyMapping()
        {
            { InputKey.l2,               "button_zl" },
            { InputKey.r2,               "button_zr" },
        };
    }
}
