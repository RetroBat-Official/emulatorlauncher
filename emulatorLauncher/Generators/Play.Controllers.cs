using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class PlayGenerator : Generator
    {
        // Play! input provider ids ('xinp', 'dinp', 'QtMo', 'QtKy')
        private const string ProviderXInput = "2020175472";
        private const string ProviderDInput = "1684631152";
        private const string ProviderMouse = "1366576495";
        private const string ProviderKeyboard = "1366575993";

        private const string NoDevice = "0:0:0:0:0:0";

        /// <summary>
        /// Binding target of Play! (one input of a device)
        /// </summary>
        private class PlayTarget
        {
            public string DeviceId;
            public string KeyId;
            public string KeyType;      // 0 = button, 1 = axis, 2 = pov hat
            public string ProviderId;
            public string HatRef = "-1"; // pov hat direction (0 = up, 2 = right, 4 = down, 6 = left)
        }

        /// <summary>
        /// Create the RetroBat input profile (inputprofiles\Retrobat.xml)
        /// </summary>
        private void ConfigureControllers(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Play!");

            string controllerFile = Path.Combine(path, "Play Data Files", "inputprofiles", "Retrobat.xml");
            
            var controllers = Controllers.Where(c => !c.IsKeyboard && c.Config != null).OrderBy(i => i.PlayerIndex).Take(2).ToList();
            if (controllers.Count == 0)
                return;

            try
            {
                XDocument cfg = File.Exists(controllerFile) ? XDocument.Load(controllerFile) : new XDocument(new XElement("Config"));

                for (int pad = 1; pad <= 2; pad++)
                    CleanupPad(cfg, pad);

                var arcade = _isArcade ? GetArcadeMapping(path) : null;

                for (int i = 0; i < controllers.Count; i++)
                    ConfigurePad(cfg, controllers[i], i + 1, arcade);

                // Light guns: mouse and keyboard keys of the gun for player 1
                if (arcade != null && arcade.Gun.Count > 0)
                    ConfigureGun(cfg, arcade.Gun);

                cfg.Save(controllerFile);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[Play] Unable to write the controller configuration : " + ex.Message);
            }
        }

        private void ConfigurePad(XDocument cfg, Controller ctrl, int pad, ArcadeMapping arcade)
        {
            bool isXinput = ctrl.IsXInputDevice && ctrl.XInput != null;
            string pref = "input.pad" + pad + ".";

            // PS2 button -> controller input (gamecontrollerdb names)
            var map = new Dictionary<string, List<string>>();
            foreach (var entry in ps2DefaultMapping)
                map[entry.Key] = new List<string>() { entry.Value };

            string gas = null;
            string brake = null;

            if (arcade != null)
            {
                if (arcade.Buttons.Count > 0)
                {
                    foreach (var key in arcadeResetKeys)
                        map[key] = new List<string>();

                    foreach (var entry in arcade.Buttons)
                    {
                        foreach (var target in entry.Value)
                        {
                            if (target == "gas") gas = entry.Key;
                            else if (target == "brake") brake = entry.Key;
                            else map[target].Add(entry.Key);
                        }
                    }
                }

                // Test = L3 + R3 in Play! : both on the R3 button when the game does not use them
                if (map["l3"].Count == 0 || map["l3"].SequenceEqual(new[] { "leftstick" }))
                    map["l3"] = new List<string>() { "rightstick" };
                if (map["r3"].Count == 0)
                    map["r3"] = new List<string>() { "rightstick" };
            }

            SetPreference(cfg, pref + "analog.sensitivity", "float", "1.000000");

            foreach (var entry in map)
            {
                // Play! binds one input per PS2 button: the first one is used
                var target = entry.Value.Select(v => GetTarget(ctrl, v, isXinput)).FirstOrDefault(t => t != null);
                WriteBinding(cfg, pref + entry.Key, target);
            }

            // Driving games : pedals on buttons (left stick Y up = gas, right stick X right = brake)
            if (gas != null)
                WriteSimulatedAxis(cfg, pref + "analog_left_y", GetPedalTarget(ctrl, gas, isXinput), null);
            if (brake != null)
                WriteSimulatedAxis(cfg, pref + "analog_right_x", null, GetPedalTarget(ctrl, brake, isXinput));

            // Rumble
            bool rumble = !SystemConfig.getOptBoolean("play_rumble");
            SetPreference(cfg, pref + "motor.bindingtarget1.deviceId", "string", rumble ? GetDeviceID(ctrl, isXinput) : NoDevice);
            SetPreference(cfg, pref + "motor.bindingtarget1.keyId", "integer", rumble ? "-1" : "0");
            SetPreference(cfg, pref + "motor.bindingtarget1.keyType", "integer", rumble ? "3" : "0");
            SetPreference(cfg, pref + "motor.bindingtarget1.providerId", "integer", rumble ? (isXinput ? ProviderXInput : ProviderDInput) : "0");

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + pad);
        }

        private void ConfigureGun(XDocument cfg, Dictionary<string, List<string>> gun)
        {
            SimpleLogger.Instance.Info("[GUNS] Configuring light gun buttons for Play!");

            foreach (var entry in gun)
            {
                PlayTarget target = null;

                if (entry.Key == "mouseleft" || entry.Key == "mouseright")
                    target = new PlayTarget() { DeviceId = NoDevice, KeyId = entry.Key == "mouseleft" ? "1" : "2", KeyType = "0", ProviderId = ProviderMouse };
                else if (entry.Key.StartsWith("kb_") && entry.Key.Length == 4 && char.IsDigit(entry.Key[3]))
                    target = new PlayTarget() { DeviceId = NoDevice, KeyId = ((int)entry.Key[3]).ToString(), KeyType = "0", ProviderId = ProviderKeyboard };    // Qt key code of a digit is its ASCII code

                if (target == null)
                    continue;

                foreach (var ps2Key in entry.Value.Where(v => ps2DefaultMapping.ContainsKey(v)))
                    WriteBinding(cfg, "input.pad1." + ps2Key, target);
            }
        }

        #region bindings
        private static void WriteTargetPrefs(XDocument cfg, string prefix, PlayTarget target)
        {
            SetPreference(cfg, prefix + ".deviceId", "string", target == null ? NoDevice : target.DeviceId);
            SetPreference(cfg, prefix + ".keyId", "integer", target == null ? "0" : target.KeyId);
            SetPreference(cfg, prefix + ".keyType", "integer", target == null ? "0" : target.KeyType);
            SetPreference(cfg, prefix + ".providerId", "integer", target == null ? "0" : target.ProviderId);
        }

        private static void WriteBinding(XDocument cfg, string key, PlayTarget target)
        {
            // Binding type : 0 = unbound, 1 = simple, 3 = pov hat
            string bindingType = target == null ? "0" : (target.KeyType == "2" ? "3" : "1");

            SetPreference(cfg, key + ".bindingtype", "integer", bindingType);
            SetPreference(cfg, key + ".povhatbinding.refvalue", "integer", target == null ? "-1" : target.HatRef);
            WriteTargetPrefs(cfg, key + ".bindingtarget1", target);
        }

        private static void WriteSimulatedAxis(XDocument cfg, string key, PlayTarget toMin, PlayTarget toMax)
        {
            // Binding type 2 : bindingtarget1 pushes the axis to 0, bindingtarget2 to 255
            SetPreference(cfg, key + ".bindingtype", "integer", "2");
            SetPreference(cfg, key + ".povhatbinding.refvalue", "integer", "-1");
            WriteTargetPrefs(cfg, key + ".bindingtarget1", toMin);
            WriteTargetPrefs(cfg, key + ".bindingtarget2", toMax);
        }

        private static void CleanupPad(XDocument cfg, int pad)
        {
            string pref = "input.pad" + pad + ".";

            foreach (var key in ps2DefaultMapping.Keys)
            {
                SetPreference(cfg, pref + key + ".bindingtype", "integer", "0");
                SetPreference(cfg, pref + key + ".povhatbinding.refvalue", "integer", "-1");
                WriteTargetPrefs(cfg, pref + key + ".bindingtarget1", null);

                if (key.StartsWith("analog_"))
                    WriteTargetPrefs(cfg, pref + key + ".bindingtarget2", null);
            }

            WriteTargetPrefs(cfg, pref + "motor.bindingtarget1", null);
        }
        #endregion

        #region device inputs
        private PlayTarget GetTarget(Controller ctrl, string input, bool isXinput)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            if (isXinput)
            {
                if (!xinputKeyID.TryGetValue(input, out string keyId))
                    return null;

                return new PlayTarget() { DeviceId = GetDeviceID(ctrl, true), KeyId = keyId, KeyType = xinputKeyType[input], ProviderId = ProviderXInput };
            }

            // DirectInput : gamecontrollerdb mapping, or RetroBat HID mapping when the controller is not in gamecontrollerdb
            var inputKey = SdlToDirectInput.SdlToGameControllerDB.Where(k => k.Value == input && k.Key != InputKey.hotkey).Select(k => (InputKey?)k.Key).FirstOrDefault();
            if (inputKey == null)
                return null;

            var dinput = ctrl.GetDirectInputMapping(inputKey.Value);
            if (dinput == null)
            {
                SimpleLogger.Instance.Info("[INFO] No DirectInput mapping for " + input);
                return null;
            }

            var target = new PlayTarget() { DeviceId = GetDeviceID(ctrl, false), ProviderId = ProviderDInput };

            switch (dinput.Type)
            {
                case "button":
                    target.KeyType = "0";
                    target.KeyId = (48 + dinput.Id).ToString();     // DIJOFS_BUTTON0
                    break;
                case "hat":
                    target.KeyType = "2";
                    target.KeyId = (32 + 4 * dinput.Id).ToString(); // DIJOFS_POV(n)
                    target.HatRef = hatDirections.TryGetValue((int)dinput.Value, out string dir) ? dir : "-1";
                    break;
                case "axis":
                    target.KeyType = "1";
                    target.KeyId = GetAxisOffset(ctrl, (int)dinput.Id).ToString();
                    break;
                default:
                    return null;
            }

            return target;
        }

        /// <summary>
        /// Pedals use simulated axes : any non-zero value presses them.
        /// A full range trigger axis rests at 0 and can be used, a half axis (both triggers on one axis) rests at its center :
        /// the shoulder button of the same side is used instead.
        /// </summary>
        private PlayTarget GetPedalTarget(Controller ctrl, string input, bool isXinput)
        {
            if (!isXinput && IsHalfTriggerAxis(ctrl, input))
            {
                string shoulder = input == "lefttrigger" ? "leftshoulder" : "rightshoulder";
                SimpleLogger.Instance.Info("[INFO] Pedal on half axis replaced by " + shoulder);
                return GetTarget(ctrl, shoulder, false);
            }

            return GetTarget(ctrl, input, isXinput);
        }

        private static bool IsHalfTriggerAxis(Controller ctrl, string input)
        {
            if (input != "lefttrigger" && input != "righttrigger")
                return false;

            var trigger = ctrl.GetDirectInputMapping(input == "lefttrigger" ? InputKey.l2 : InputKey.r2);
            if (trigger == null || trigger.Type != "axis")
                return false;

            var other = ctrl.GetDirectInputMapping(input == "lefttrigger" ? InputKey.r2 : InputKey.l2);
            return trigger.Value == 1 || (other != null && other.Type == "axis" && other.Id == trigger.Id);
        }

        /// <summary>
        /// SDL numbers the DirectInput axes the device has (X, Y, Z, Rx, Ry, Rz, sliders), Play! uses their offset in DIJOYSTATE
        /// </summary>
        private static int GetAxisOffset(Controller ctrl, int axisIndex)
        {
            try
            {
                if (ctrl.DirectInput != null)
                {
                    var slots = ctrl.DirectInput.GetAxisSlots();
                    int n = 0;
                    for (int slot = 0; slot < slots.Length; slot++)
                    {
                        if (!slots[slot])
                            continue;

                        if (n == axisIndex)
                            return slot * 4;

                        n++;
                    }
                }
            }
            catch { }

            return axisIndex * 4;
        }

        private string GetDeviceID(Controller ctrl, bool isXinput)
        {
            if (isXinput)
                return ctrl.XInput.DeviceIndex + ":0:0:0:0:0";

            if (ctrl.DirectInput == null)
                return NoDevice;

            // Play! stores the instance GUID bytes as 4 little endian uint32 (hexadecimal)
            byte[] b = ctrl.DirectInput.InstanceGuid.ToByteArray();
            return string.Format("{0:x}:{1:x}:{2:x}:{3:x}:0:0", BitConverter.ToUInt32(b, 0), BitConverter.ToUInt32(b, 4), BitConverter.ToUInt32(b, 8), BitConverter.ToUInt32(b, 12));
        }
        #endregion

        #region arcade
        private class ArcadeMapping
        {
            public Dictionary<string, List<string>> Buttons = new Dictionary<string, List<string>>();
            public Dictionary<string, List<string>> Gun = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Read play.yml : &lt;rom&gt;_&lt;layout&gt;, &lt;rom&gt;, &lt;parent&gt;_&lt;layout&gt;, &lt;parent&gt;, default_&lt;layout&gt;, default
        /// </summary>
        private ArcadeMapping GetArcadeMapping(string path)
        {
            var ret = new ArcadeMapping();

            string ymlPath = mappingPaths
                .Select(p => Path.Combine(Program.AppConfig.GetFullPath("retrobat"), p.Replace("{systempath}", "system").Replace("{userpath}", "user")))
                .FirstOrDefault(File.Exists);

            if (ymlPath == null)
                return ret;

            YmlFile yml = YmlFile.Load(ymlPath);
            if (yml == null)
                return ret;

            string parent = null;
            string inputMode = "default";
            try
            {
                string arcadeDef = Path.Combine(path, "arcadedefs", _romName + ".arcadedef");
                if (File.Exists(arcadeDef))
                {
                    var def = JObject.Parse(File.ReadAllText(arcadeDef));
                    parent = (string)def["parent"];
                    inputMode = (string)def["inputMode"] ?? "default";
                }
            }
            catch { }

            string layout = SystemConfig["controller_layout"];
            if (layout == "classic8")
                layout = "6alternative";

            var names = new List<string>();
            foreach (var name in new[] { _romName, parent, "default" }.Where(n => !string.IsNullOrEmpty(n)))
            {
                if (!string.IsNullOrEmpty(layout))
                    names.Add(name + "_" + layout);
                names.Add(name);
            }

            var game = names.Select(n => yml.Elements.OfType<YmlContainer>().FirstOrDefault(c => c.Name == n)).FirstOrDefault(c => c != null);
            if (game != null)
            {
                SimpleLogger.Instance.Info("[INPUT] Play! arcade mapping : " + game.Name);
                ret.Buttons = ReadYmlMapping(game);
            }

            // Light guns
            if (inputMode == "lightgun" && SystemConfig.getOptBoolean("use_guns"))
            {
                var gun = new[] { _romName + "_gun", parent + "_gun", "lightgun_gun" }.Select(n => yml.Elements.OfType<YmlContainer>().FirstOrDefault(c => c.Name == n)).FirstOrDefault(c => c != null);
                if (gun != null)
                    ret.Gun = ReadYmlMapping(gun);
            }

            return ret;
        }

        private static Dictionary<string, List<string>> ReadYmlMapping(YmlContainer container)
        {
            var ret = new Dictionary<string, List<string>>();

            foreach (var element in container.Elements.OfType<YmlElement>())
            {
                string input;
                if (!ymlInputs.TryGetValue(element.Name, out input))
                    input = element.Name;   // gun containers : mouseleft, mouseright, kb_x

                var targets = new List<string>();
                foreach (var value in element.Value.Split(',').Select(v => v.Trim().ToLowerInvariant()).Where(v => v.Length > 0))
                {
                    if (jvsButtons.TryGetValue(value, out string ps2))
                        targets.Add(ps2);
                    else if (ps2DefaultMapping.ContainsKey(value) || value == "gas" || value == "brake")
                        targets.Add(value);
                    else
                        SimpleLogger.Instance.Warning("[INPUT] Unknown Play! arcade control : " + value);
                }

                ret[input] = targets;
            }

            return ret;
        }

        // JVS buttons of the Namco System 246 / 256 and the PS2 button that drives them in Play!
        static private readonly Dictionary<string, string> jvsButtons = new Dictionary<string, string>()
        {
            { "button1", "cross" }, { "button2", "circle" }, { "button3", "triangle" }, { "button4", "square" }, { "button5", "r3" },
            { "button6", "r2" }, { "button7", "r1" }, { "button8", "l3" }, { "button9", "l2" }, { "button10", "l1" },
        };

        // play.yml controller buttons -> gamecontrollerdb names
        static private readonly Dictionary<string, string> ymlInputs = new Dictionary<string, string>()
        {
            { "south", "a" }, { "east", "b" }, { "west", "x" }, { "north", "y" },
            { "l1", "leftshoulder" }, { "r1", "rightshoulder" }, { "l2", "lefttrigger" }, { "r2", "righttrigger" },
            { "l3", "leftstick" }, { "r3", "rightstick" }, { "select", "back" }, { "start", "start" },
            { "up", "dpup" }, { "down", "dpdown" }, { "left", "dpleft" }, { "right", "dpright" },
        };

        // PS2 buttons remapped by play.yml (directions, sticks, select and start keep their default input)
        static private readonly string[] arcadeResetKeys = { "cross", "circle", "triangle", "square", "l1", "l2", "l3", "r1", "r2", "r3" };

        static readonly string[] mappingPaths =
        {
            // User specific
            "{userpath}\\inputmapping\\play.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\play.yml",
        };
        #endregion

        // PS2 button -> controller input (gamecontrollerdb names)
        static private readonly Dictionary<string, string> ps2DefaultMapping = new Dictionary<string, string>()
        {
            { "analog_left_x", "leftx" },
            { "analog_left_y", "lefty" },
            { "analog_right_x", "rightx" },
            { "analog_right_y", "righty" },
            { "dpad_up", "dpup" },
            { "dpad_down", "dpdown" },
            { "dpad_left", "dpleft" },
            { "dpad_right", "dpright" },
            { "select", "back" },
            { "start", "start" },
            { "square", "x" },
            { "triangle", "y" },
            { "circle", "b" },
            { "cross", "a" },
            { "l1", "leftshoulder" },
            { "l2", "lefttrigger" },
            { "l3", "leftstick" },
            { "r1", "rightshoulder" },
            { "r2", "righttrigger" },
            { "r3", "rightstick" },
        };

        // DirectInput hat value -> Play! pov hat direction
        static private readonly Dictionary<int, string> hatDirections = new Dictionary<int, string>()
        {
            { 1, "0" }, { 2, "2" }, { 4, "4" }, { 8, "6" },
        };

        static private readonly Dictionary<string, string> xinputKeyID = new Dictionary<string, string>()
        {
            { "leftx", "0" },
            { "lefty", "1" },
            { "rightx", "2" },
            { "righty", "3" },
            { "dpdown", "7" },
            { "dpleft", "8" },
            { "dpright", "9" },
            { "dpup", "6" },
            { "b", "17" },
            { "a", "16" },
            { "leftshoulder", "14" },
            { "lefttrigger", "4" },
            { "leftstick", "12" },
            { "rightshoulder", "15" },
            { "righttrigger", "5" },
            { "rightstick", "13" },
            { "back", "11" },
            { "x", "18" },
            { "start", "10" },
            { "y", "19" },
        };

        static private readonly Dictionary<string, string> xinputKeyType = new Dictionary<string, string>()
        {
            { "leftx", "1" },
            { "lefty", "1" },
            { "rightx", "1" },
            { "righty", "1" },
            { "dpdown", "0" },
            { "dpleft", "0" },
            { "dpright", "0" },
            { "dpup", "0" },
            { "b", "0" },
            { "a", "0" },
            { "leftshoulder", "0" },
            { "lefttrigger", "0" },
            { "leftstick", "0" },
            { "rightshoulder", "0" },
            { "righttrigger", "0" },
            { "rightstick", "0" },
            { "back", "0" },
            { "x", "0" },
            { "start", "0" },
            { "y", "0" },
        };
    }
}