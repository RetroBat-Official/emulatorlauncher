using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class AzaharGenerator : Generator
    {
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled, just configuring shortcuts.");
                ConfigureShortcuts(ini);
                ini.WriteValue("Controls", "profiles\\1\\button_gpio14\\default", "false");
                ini.WriteValue("Controls", "profiles\\1\\button_gpio14", "\"" + "code:16777233,engine:keyboard" + "\"");
                ini.WriteValue("Controls", "profiles\\1\\button_home\\default", "false");
                ini.WriteValue("Controls", "profiles\\1\\button_home", "\"" + "code:16777232,engine:keyboard" + "\"");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Azahar");

            var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

            if (c1.IsKeyboard)
            {
                ConfigureKeyboard(c1, ini);
                ConfigureShortcuts(ini);
            }
            else
            {
                string azaharGuid = ConfigureJoystick(c1, ini);
                ConfigureShortcuts(ini, c1, azaharGuid);
            }
        }

        private string ConfigureJoystick(Controller controller, IniFile ini)
        {
            if (controller == null)
                return null;

            var cfg = controller.Config;
            if (cfg == null)
                return null;

            var guid = controller.GetSdlGuid(_sdlVersion, true);
            var azaharGuid = guid.ToString().ToLowerInvariant();
            string newGuidPath = Path.Combine(AppConfig.GetFullPath("tools"), "controllerinfo.yml");
            string batPath = Path.Combine(AppConfig.GetFullPath("retrobat"));
            string newGuid = SdlJoystickGuid.GetGuidFromFile(newGuidPath, controller.SdlController, controller.Guid, "azahar", 0, batPath);
            if (newGuid != null)
                azaharGuid = newGuid;

            //only 1 player so profile is fixed to 1
            ini.WriteValue("Controls", "profile\\default", "true");
            ini.WriteValue("Controls", "profile", "0");
            ini.WriteValue("Controls", "profiles\\1\\name\\default", "true");
            ini.WriteValue("Controls", "profiles\\1\\name", "default");
            ini.WriteValue("Controls", "profiles\\1\\input_maptype\\default", "false");
            ini.WriteValue("Controls", "profiles\\1\\input_maptype", "0");

            string profile = "profiles\\1\\";

            if (Program.SystemConfig.isOptSet("gamepadbuttons") && Program.SystemConfig.getOptBoolean("gamepadbuttons"))
            {
                Mapping[InputKey.b] = "button_b";
                Mapping[InputKey.a] = "button_a";
                Mapping[InputKey.x] = "button_y";
                Mapping[InputKey.y] = "button_x";
            }

            //manage buttons and directions
            foreach (var map in Mapping)
            {
                string name = profile + map.Value;

                string cvalue = FromInput(controller, cfg[map.Key], azaharGuid);

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
            ini.WriteValue("Controls", profile + "button_gpio14" + "\\default", "false");
            ini.WriteValue("Controls", profile + "button_gpio14", "\"" + "code:16777233,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_home" + "\\default", "false");
            ini.WriteValue("Controls", profile + "button_home", "\"" + "code:16777232,engine:keyboard" + "\"");

            //Manage mapping of triggers ZL and ZR
            foreach (var map in Zmapping)
            {
                string name = profile + map.Value;

                string cvalue = FromInput(controller, cfg[map.Key], azaharGuid);

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

            //Manage sticks
            //left stick = circle pad
            //right stick = c-stick
            ProcessStick(controller, profile, "circle_pad", ini, azaharGuid);
            ProcessStick(controller, profile, "c_stick", ini, azaharGuid);
            
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
                        ini.WriteValue("Controls", profile + "motion_device" + "\\default", "true");
                        ini.WriteValue("Controls", profile + "motion_device", "\"engine:motion_emu,update_period:100,sensitivity:0.01,tilt_clamp:90.0\"");
                        break;
                    case "sdl":
                        ini.WriteValue("Controls", profile + "motion_device" + "\\default", "false");
                        ini.WriteValue("Controls", profile + "motion_device", "\"engine:sdl,guid:" + azaharGuid + ",port:0\"");
                        break;
                }
            }
            else
            {
                ini.WriteValue("Controls", profile + "motion_device" + "\\default", "true");
                ini.WriteValue("Controls", profile + "motion_device", "\"engine:motion_emu,update_period:100,sensitivity:0.01,tilt_clamp:90.0\"");
            }

            if (SystemConfig.isOptSet("n3ds_touchdevice") && SystemConfig["n3ds_touchdevice"] == "cemuhook")
            {
                ini.WriteValue("Controls", profile + "touch_device" + "\\default", "false");
                ini.WriteValue("Controls", profile + "touch_device", "\"engine:cemuhookudp,max_x:1800,max_y:850,min_x:100,min_y:50\"");
            }
            else
            {
                ini.WriteValue("Controls", profile + "touch_device" + "\\default", "true");
                ini.WriteValue("Controls", profile + "touch_device", "\"engine:emu_window\"");
            }

            // Use custom touch from button
            if (Program.SystemConfig.isOptSet("azahar_touchprofile") && !string.IsNullOrEmpty(Program.SystemConfig["azahar_touchprofile"]))
            {
                string touch_profile = Program.SystemConfig["azahar_touchprofile"];
                Dictionary<string, int> touchProfile = new Dictionary<string, int>();
                var touchProfileStr = ini.EnumerateKeys("Controls").Where(k => k.StartsWith("touch_from_button_maps") && k.EndsWith("name")).ToList();
                foreach (var tp in touchProfileStr)
                {
                    string[] parts = tp.Split('\\');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int extractedNumber))
                        touchProfile.Add(ini.GetValue("Controls", tp), extractedNumber);
                }
                int profileToUse = 0;
                if (touchProfile.ContainsKey(touch_profile))
                    profileToUse = touchProfile[touch_profile] - 1;

                ini.WriteValue("Controls", profile + "use_touch_from_button\\default", "false");
                ini.WriteValue("Controls", profile + "use_touch_from_button", "true");

                if (profileToUse == 0)
                {
                    ini.WriteValue("Controls", profile + "touch_from_button_map" + "\\default", "true");
                    ini.WriteValue("Controls", profile + "touch_from_button_map", "0");
                }
                else
                {
                    ini.WriteValue("Controls", profile + "touch_from_button_map\\default", "false");
                    ini.WriteValue("Controls", profile + "touch_from_button_map", profileToUse.ToString());
                }
            }
            else
            {
                ini.WriteValue("Controls", profile + "use_touch_from_button" + "\\default", "true");
                ini.WriteValue("Controls", profile + "use_touch_from_button", "false");
                ini.WriteValue("Controls", profile + "touch_from_button_map" + "\\default", "true");
                ini.WriteValue("Controls", profile + "touch_from_button_map", "0");
            }

            //udp information
            ini.WriteValue("Controls", profile + "udp_input_address" + "\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_address", "127.0.0.1");
            ini.WriteValue("Controls", profile + "udp_input_port" + "\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_port", "26760");
            ini.WriteValue("Controls", profile + "udp_pad_index" + "\\default", "true");
            ini.WriteValue("Controls", profile + "udp_pad_index", "0");

            ini.WriteValue("Controls", "profiles\\size", "1");

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex.ToString());

            return azaharGuid;
        }

        private string FromInput(Controller controller, Input input, string guid, bool shortcut = false)
        {
            if (input == null)
                return null;

            string value = "";

            if (input.Type == "button")
            {
                if (shortcut)
                    value = "button:" + input.Id + ",down:1" + ",engine:sdl,guid:" + guid + ",maptype:all,port:0";
                else
                    value = "button:" + input.Id + ",engine:sdl,guid:" + guid + ",maptype:all,port:0";
            }

            else if (input.Type == "hat")
            {
                if (shortcut)
                    value = "direction:" + input.Name.ToString() + ",down:1" + ",engine:sdl,guid:" + guid + ",maptype:all,hat:0,port:0";
                else
                    value = "direction:" + input.Name.ToString() + ",engine:sdl,guid:" + guid + ",maptype:all,hat:0,port:0";
            }

            else if (input.Type == "axis")
                value = "axis:" + input.Id + ",direction:+,engine:sdl,guid:" + guid + ",maptype:all,port:0,threshold:0.5";

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
                long azleftval = leftVal.Id;
                long aztopval = topVal.Id;

                string value = "axis_x:" + azleftval + ",axis_y:" + aztopval + ",deadzone:0.100000,engine:sdl,guid:" + guid + ",port:0";

                ini.WriteValue("Controls", name + "\\default", "false");
                ini.WriteValue("Controls", name, "\"" + value + "\"");
            }

            else
            {
                // Log why the stick could not be mapped: this leaves the stick unusable in Azahar
                SimpleLogger.Instance.Warning("[WARNING] " + stickName + " not mapped - left axis: "
                    + (leftVal == null ? "missing" : leftVal.Type + " id " + leftVal.Id)
                    + ", up axis: " + (topVal == null ? "missing" : topVal.Type + " id " + topVal.Id));

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
            ini.WriteValue("Controls", profile + "button_gpio14\\default", "false");
            ini.WriteValue("Controls", profile + "button_gpio14", "\"" + "code:16777233,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_zl\\default", "true");
            ini.WriteValue("Controls", profile + "button_zl", "\"" + "code:49,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_zr\\default", "true");
            ini.WriteValue("Controls", profile + "button_zr", "\"" + "code:50,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "button_home\\default", "false");
            ini.WriteValue("Controls", profile + "button_home", "\"" + "code:16777232,engine:keyboard" + "\"");
            ini.WriteValue("Controls", profile + "circle_pad\\default", "true");
            ini.WriteValue("Controls", profile + "circle_pad", "\"" + "down:code$016777237$1engine$0keyboard,engine:analog_from_button,left:code$016777234$1engine$0keyboard,modifier:code$068$1engine$0keyboard,modifier_scale:0.500000,right:code$016777236$1engine$0keyboard,up:code$016777235$1engine$0keyboard" + "\"");
            ini.WriteValue("Controls", profile + "c_stick\\default", "true");
            ini.WriteValue("Controls", profile + "c_stick", "\"" + "down:code$075$1engine$0keyboard,engine:analog_from_button,left:code$074$1engine$0keyboard,modifier:code$068$1engine$0keyboard,modifier_scale:0.500000,right:code$076$1engine$0keyboard,up:code$073$1engine$0keyboard" + "\"");
            ini.WriteValue("Controls", profile + "motion_device\\default", "true");
            ini.WriteValue("Controls", profile + "motion_device", "\"" + "engine:motion_emu,update_period:100,sensitivity:0.01,tilt_clamp:90.0" + "\"");
            ini.WriteValue("Controls", profile + "touch_device\\default", "true");
            ini.WriteValue("Controls", profile + "touch_device", "\"" + "engine:emu_window" + "\"");

            // Use custom touch from button
            if (Program.SystemConfig.isOptSet("azahar_touchprofile") && !string.IsNullOrEmpty(Program.SystemConfig["azahar_touchprofile"]))
            {
                string touch_profile = Program.SystemConfig["azahar_touchprofile"];
                Dictionary<string, int> touchProfile = new Dictionary<string, int>();
                var touchProfileStr = ini.EnumerateKeys("Controls").Where(k => k.StartsWith("touch_from_button_maps") && k.EndsWith("name")).ToList();
                foreach (var tp in touchProfileStr)
                {
                    string[] parts = tp.Split('\\');
                    if (parts.Length >= 3 && int.TryParse(parts[1], out int extractedNumber))
                        touchProfile.Add(ini.GetValue("Controls", tp), extractedNumber);
                }
                int profileToUse = 0;
                if (touchProfile.ContainsKey(touch_profile))
                    profileToUse = touchProfile[touch_profile] - 1;

                ini.WriteValue("Controls", profile + "use_touch_from_button" + "\\default", "false");
                ini.WriteValue("Controls", profile + "use_touch_from_button", "true");

                if (profileToUse == 0)
                {
                    ini.WriteValue("Controls", profile + "touch_from_button_map" + "\\default", "true");
                    ini.WriteValue("Controls", profile + "touch_from_button_map", "0");
                }
                else
                {
                    ini.WriteValue("Controls", profile + "touch_from_button_map\\default", "false");
                    ini.WriteValue("Controls", profile + "touch_from_button_map", profileToUse.ToString());
                }
            }
            else
            {
                ini.WriteValue("Controls", profile + "use_touch_from_button" + "\\default", "true");
                ini.WriteValue("Controls", profile + "use_touch_from_button", "false");
                ini.WriteValue("Controls", profile + "touch_from_button_map" + "\\default", "true");
                ini.WriteValue("Controls", profile + "touch_from_button_map", "0");
            }

            ini.WriteValue("Controls", profile + "udp_input_address\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_address", "127.0.0.1");
            ini.WriteValue("Controls", profile + "udp_input_port\\default", "true");
            ini.WriteValue("Controls", profile + "udp_input_port", "26760");
            ini.WriteValue("Controls", profile + "udp_pad_index\\default", "true");
            ini.WriteValue("Controls", profile + "udp_pad_index", "0");
            ini.WriteValue("Controls", "profiles\\size", "1");
        }

        private void ConfigureShortcuts(IniFile ini, Controller controller = null, string azaharGuid = null)
        {
            // Keyboard shortcuts: RetroBat defaults, overwritten by kb_hotkeys.yml
            var kbShortcuts = new Dictionary<string, string>(KeyboardShortcuts);

            if (Hotkeys.GetHotKeysFromFile("azahar", "", out Dictionary<string, HotkeyResult> custoHotkeys))
            {
                foreach (var hotkey in custoHotkeys.Values)
                    kbShortcuts[hotkey.EmulatorKey] = hotkey.EmulatorValue;
            }

            foreach (var shortcut in kbShortcuts)
            {
                string key = "Shortcuts\\Main%20Window\\" + shortcut.Key + "\\KeySeq";
                ini.WriteValue("UI", key + "\\default", "false");
                ini.WriteValue("UI", key, shortcut.Value);
            }

            // Key sent to load a save state launched from ES
            _quickLoadShortcut = kbShortcuts["Quick%20Load"];

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // Controller shortcuts: RetroBat defaults, replaced by controller_hotkeys.yml
            var padShortcuts = ControllerShortcuts.Concat(ScreenShortcuts).ToDictionary(s => s.Key, s => s.Value);

            if (Hotkeys.GetPadHKFromFile("azahar", "", out Dictionary<string, string> padHKDic))
            {
                padShortcuts = padHKDic;

                // Screen shortcuts have no RetroArch equivalent: kept when their button is free
                foreach (var screen in ScreenShortcuts)
                {
                    if (!padShortcuts.ContainsValue(screen.Value))
                        padShortcuts[screen.Key] = screen.Value;
                }
            }

            if (SystemConfig.getOptBoolean("fastforward_toggle") && padShortcuts.ContainsKey(TurboHold))
            {
                padShortcuts[TurboToggle] = padShortcuts[TurboHold];
                padShortcuts.Remove(TurboHold);
            }

            string hotkeyValue = null;

            if (controller != null && controller.Config != null && !string.IsNullOrEmpty(azaharGuid))
            {
                var hotkeyInput = controller.Config[InputKey.hotkey] ?? controller.Config[InputKey.select];
                if (hotkeyInput != null)
                    hotkeyValue = "api:controller," + FromInput(controller, hotkeyInput, azaharGuid, true);
            }

            // Every shortcut managed by RetroBat is written, so a combo removed from the yml does not persist
            foreach (string action in KeyboardShortcuts.Keys.Union(ScreenShortcuts.Keys))
            {
                string key = "Shortcuts\\Main%20Window\\" + action + "\\controller_keyseq";
                string buttonValue = null;

                if (!string.IsNullOrEmpty(hotkeyValue) && padShortcuts.TryGetValue(action, out string button) && Enum.TryParse<InputKey>(button, true, out var inputKey))
                {
                    string input = FromInput(controller, controller.Config[inputKey], azaharGuid, true);
                    if (!string.IsNullOrEmpty(input))
                        buttonValue = "api:controller," + input;
                }

                if (!string.IsNullOrEmpty(buttonValue))
                {
                    ini.WriteValue("UI", key + "\\default", "false");
                    ini.WriteValue("UI", key, "\"" + hotkeyValue + "||" + buttonValue + "\"");
                }
                else
                {
                    // Reset to upstream default, otherwise a previously written combo persists
                    ini.WriteValue("UI", key + "\\default", "true");
                    ini.WriteValue("UI", key, "");
                }
            }
        }

        static InputKeyMapping Mapping = new InputKeyMapping()
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

        private const string TurboHold = "Turbo%20Mode%20%28Hold%29";
        private const string TurboToggle = "Toggle%20Turbo%20Mode";

        // Keyboard shortcuts written by RetroBat (Qt key names, ini names as written by QSettings).
        // RetroArch hotkeys of kb_hotkeys.yml replace them (azahar entry in HotkeysDictionaries.cs)
        static readonly Dictionary<string, string> KeyboardShortcuts = new Dictionary<string, string>()
        {
            { "Audio%20Mute\\Unmute",           "Ctrl+M" },
            { "Capture%20Screenshot",           "F8" },
            { "Continue\\Pause%20Emulation",    "P" },
            { "Exit%20Azahar",                  "Esc" },
            { "Exit%20Fullscreen",              "Tab" },
            { "Fullscreen",                     "F1" },
            { "Load%20Amiibo",                  "Ctrl+F2" },
            { "Quick%20Load",                   "F4" },
            { "Quick%20Save",                   "F2" },
            { "Remove%20Amiibo",                "Ctrl+F3" },
            { "Restart%20Emulation",            "Ctrl+F6" },
            { "Rotate%20Screens%20Upright",     "Ctrl+F8" },
            { TurboToggle,                      "" },
            { TurboHold,                        "" }
        };

        // Controller shortcuts: each entry is combined with the "hotkey" button (yml names: a = SOUTH, x = NORTH, y = WEST).
        // Azahar evaluates the two bindings as an AND combo (both held at once), see
        // ControllerHotkeyMonitor::checkAllButtons in src/citra_qt/hotkey_monitor.cpp
        static readonly Dictionary<string, string> ControllerShortcuts = new Dictionary<string, string>()
        {
            { "Exit%20Azahar",                  "start" },
            { "Quick%20Save",                   "y" },
            { "Quick%20Load",                   "x" },
            { "Capture%20Screenshot",           "r3" },
            { "Continue\\Pause%20Emulation",    "a" },
            { TurboHold,                        "right" }
        };

        // No RetroArch equivalent: not part of controller_hotkeys.yml
        static readonly Dictionary<string, string> ScreenShortcuts = new Dictionary<string, string>()
        {
            { "Swap%20Screens",                 "pageup" },
            { "Toggle%20Screen%20Layout",       "pagedown" }
        };
    }
}
