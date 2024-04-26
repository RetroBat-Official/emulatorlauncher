using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class PlayGenerator : Generator
    {
        /// <summary>
        /// Configure emulator features (config.xml)
        /// </summary>
        /// <param name="path"></param>
        private void ConfigureControllers(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string controllerFile = Path.Combine(path, "Play Data Files", "inputprofiles", "Retrobat.xml");
            string templateControllerFile = Path.Combine(AppConfig.GetFullPath("templates"), "play", "inputprofiles", "Retrobat.xml");

            if (!File.Exists(controllerFile) && !File.Exists(templateControllerFile))
            {
                SimpleLogger.Instance.Info("[ERROR]: Controller file does not exist for autoconfiguration.");
                return;
            }

            else if (!File.Exists(controllerFile) && File.Exists(templateControllerFile))
                File.Copy(templateControllerFile, controllerFile);

            var controllers = Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).ToList();
            Controller c1 = null;
            Controller c2 = null;

            if (controllers.Count == 0)
                return;

            c1 = controllers[0];

            if (controllers.Count > 1)
                c2 = controllers[1];

            try
            {
                XDocument controllerConfigfile = XDocument.Load(controllerFile);

                CleanupControllerConfig(controllerConfigfile);
                ConfigureControllers(controllerConfigfile, c1, c1.IsXInputDevice);

                if (c2 != null)
                    ConfigureControllers(controllerConfigfile, c2, c2.IsXInputDevice);

                //save file
                controllerConfigfile.Save(controllerFile);
            }

            catch { SimpleLogger.Instance.Info("ERROR: An error happened when saving the controller configuration file."); }
        }

        private void ConfigureControllers(XDocument cfg, Controller ctrl, bool isXinput)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput dinputCtrl = null;
            string inputPad = "input.pad" + (ctrl.PlayerIndex).ToString() + ".";
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            bool rumble = (!SystemConfig.isOptSet("play_rumble") || !SystemConfig.getOptBoolean("play_rumble"));

            if (!isXinput && !File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                gamecontrollerDB = null;
                return;
            }

            if (!isXinput && gamecontrollerDB != null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);
                dinputCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

                if (dinputCtrl == null)
                {
                    SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid);
                    return;
                }
            }

            XElement analogsensitivity = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog.sensitivity").FirstOrDefault();
            analogsensitivity.SetAttributeValue("Value", "1.000000");

            // Left analog horizontal
            XElement leftx1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_x.bindingtarget1.deviceId").FirstOrDefault();
            leftx1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement leftx1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_x.bindingtarget1.keyId").FirstOrDefault();
            leftx1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "leftx", isXinput));

            XElement leftx1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_x.bindingtarget1.keyType").FirstOrDefault();
            leftx1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "leftx", isXinput));

            XElement leftx1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_x.bindingtarget1.providerId").FirstOrDefault();
            leftx1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // Left analog vertical
            XElement lefty1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_y.bindingtarget1.deviceId").FirstOrDefault();
            lefty1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement lefty1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_y.bindingtarget1.keyId").FirstOrDefault();
            lefty1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "lefty", isXinput));

            XElement lefty1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_y.bindingtarget1.keyType").FirstOrDefault();
            lefty1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "lefty", isXinput));

            XElement lefty1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_left_y.bindingtarget1.providerId").FirstOrDefault();
            lefty1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // Right analog horizontal
            XElement rightx1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_x.bindingtarget1.deviceId").FirstOrDefault();
            rightx1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement rightx1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_x.bindingtarget1.keyId").FirstOrDefault();
            rightx1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "rightx", isXinput));

            XElement rightx1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_x.bindingtarget1.keyType").FirstOrDefault();
            rightx1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "rightx", isXinput));

            XElement rightx1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_x.bindingtarget1.providerId").FirstOrDefault();
            rightx1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // Right analog vertical
            XElement righty1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_y.bindingtarget1.deviceId").FirstOrDefault();
            righty1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement righty1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_y.bindingtarget1.keyId").FirstOrDefault();
            righty1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "righty", isXinput));

            XElement righty1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_y.bindingtarget1.keyType").FirstOrDefault();
            righty1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "righty", isXinput));

            XElement righty1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "analog_right_y.bindingtarget1.providerId").FirstOrDefault();
            righty1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // Other for analog axis
            foreach (string key in inputBindings2Keys)
            {
                foreach (string inputBinding in inputBindings2)
                {
                    XElement analog2clean = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + key + inputBinding).FirstOrDefault();
                    analog2clean.SetAttributeValue("Value", "0");
                }

                XElement stickBindType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + key + ".bindingtype").FirstOrDefault();
                stickBindType.SetAttributeValue("Value", "1");

                XElement povHat = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + key + ".povhatbinding.refvalue").FirstOrDefault();
                povHat.SetAttributeValue("Value", "-1");
            }

            // D-Pad down
            XElement dpdown1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_down.bindingtarget1.deviceId").FirstOrDefault();
            dpdown1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement dpdown1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_down.bindingtarget1.keyId").FirstOrDefault();
            dpdown1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "dpdown", isXinput));

            XElement dpdown1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_down.bindingtarget1.keyType").FirstOrDefault();
            dpdown1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "dpdown", isXinput));

            XElement dpdown1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_down.bindingtarget1.providerId").FirstOrDefault();
            dpdown1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // D-Pad left
            XElement dpleft1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_left.bindingtarget1.deviceId").FirstOrDefault();
            dpleft1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement dpleft1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_left.bindingtarget1.keyId").FirstOrDefault();
            dpleft1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "dpleft", isXinput));

            XElement dpleft1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_left.bindingtarget1.keyType").FirstOrDefault();
            dpleft1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "dpleft", isXinput));

            XElement dpleft1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_left.bindingtarget1.providerId").FirstOrDefault();
            dpleft1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // D-Pad right
            XElement dpright1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_right.bindingtarget1.deviceId").FirstOrDefault();
            dpright1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement dpright1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_right.bindingtarget1.keyId").FirstOrDefault();
            dpright1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "dpright", isXinput));

            XElement dpright1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_right.bindingtarget1.keyType").FirstOrDefault();
            dpright1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "dpright", isXinput));

            XElement dpright1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_right.bindingtarget1.providerId").FirstOrDefault();
            dpright1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // D-Pad up
            XElement dpup1DeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_up.bindingtarget1.deviceId").FirstOrDefault();
            dpup1DeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

            XElement dpup1KeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_up.bindingtarget1.keyId").FirstOrDefault();
            dpup1KeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, "dpup", isXinput));

            XElement dpup1KeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_up.bindingtarget1.keyType").FirstOrDefault();
            dpup1KeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, "dpup", isXinput));

            XElement dpup1ProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "dpad_up.bindingtarget1.providerId").FirstOrDefault();
            dpup1ProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

            // Other for dpad
            foreach (string key in dpadKeys)
            {
                XElement dpadBindType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + key + ".bindingtype").FirstOrDefault();
                dpadBindType.SetAttributeValue("Value", isXinput ? "1" : "3");

                foreach (KeyValuePair<string, string> entry in povHatDpad)
                {
                    XElement povHat = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + entry.Key + ".povhatbinding.refvalue").FirstOrDefault();
                    povHat.SetAttributeValue("Value", isXinput ? "-1" : entry.Value);
                }
            }

            // Buttons
            foreach (KeyValuePair<string, string> button in ps2buttons)
            {
                XElement buttonDeviceID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + button.Key + ".bindingtarget1.deviceId").FirstOrDefault();
                buttonDeviceID.SetAttributeValue("Value", GetDeviceID(ctrl, isXinput));

                XElement buttonKeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + button.Key + ".bindingtarget1.keyId").FirstOrDefault();
                buttonKeyID.SetAttributeValue("Value", GetKeyID(ctrl, dinputCtrl, button.Value, isXinput));

                XElement buttonKeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + button.Key + ".bindingtarget1.keyType").FirstOrDefault();
                buttonKeyType.SetAttributeValue("Value", GetDeviceKeyType(ctrl, dinputCtrl, button.Value, isXinput));

                XElement buttonProvID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + button.Key + ".bindingtarget1.providerId").FirstOrDefault();
                buttonProvID.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");

                XElement buttonBindType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + button.Key + ".bindingtype").FirstOrDefault();
                buttonBindType.SetAttributeValue("Value", "1");

                XElement buttonPovHat = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + button.Key + ".povhatbinding.refvalue").FirstOrDefault();
                buttonPovHat.SetAttributeValue("Value", "-1");
            }

            // Motor
            XElement rumbleDevID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "motor.bindingtarget1.deviceId").FirstOrDefault();
            rumbleDevID.SetAttributeValue("Value", rumble ? GetDeviceID(ctrl, isXinput) : "0:0:0:0:0:0");

            XElement rumbleKeyID = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "motor.bindingtarget1.keyId").FirstOrDefault();
            rumbleKeyID.SetAttributeValue("Value", rumble ? "-1" : "0");

            XElement rumbleKeyType = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "motor.bindingtarget1.keyType").FirstOrDefault();
            rumbleKeyType.SetAttributeValue("Value", rumble ? "3" : "0");

            if (rumble)
            {
                XElement rumbleProvider = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "motor.bindingtarget1.providerId").FirstOrDefault();
                rumbleProvider.SetAttributeValue("Value", isXinput ? "2020175472" : "1684631152");
            }
            else
            {
                XElement rumbleProvider = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == inputPad + "motor.bindingtarget1.providerId").FirstOrDefault();
                rumbleProvider.SetAttributeValue("Value", "0");
            }
        }

        private string GetDeviceID(Controller ctrl, bool isXinput)
        {
            // dinput
            // Retrobat :   fc6f3e60-8fa8-11ed-8002-444553540000
            // Play :       fc6f3e60:11ed8fa8:45440280:5453:0:0

            if (isXinput)
                return ctrl.XInput.DeviceIndex + ":0:0:0:0:0";
            else
            {
                string instanceID = ctrl.DirectInput.InstanceGuid.ToString();
                string[] parts = instanceID.Split('-');
                string targetPart1 = parts[0];
                string targetPart2 = parts[2] + parts[1];
                string targetPart3 = parts[4].Substring(2, 2) + parts[4].Substring(0, 2) + parts[3].Substring(2, 2) + parts[3].Substring(0, 2);
                string targetPart4 = parts[4].Substring(6, 2) + parts[4].Substring(4, 2);
                string target = targetPart1 + ":" + targetPart2 + ":" + targetPart3 + ":" + targetPart4 + ":0:0";
                return target;
            }
        }

        private string GetDeviceKeyType(Controller ctrl, SdlToDirectInput dinputCtrl, string buttonkey, bool isXinput)
        {
            if (ctrl == null)
                return "0";

            if (!isXinput)
            {
                if (dinputCtrl == null)
                    return "0";

                if (dinputCtrl.ButtonMappings == null)
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found in gamecontrollerdb for the controller.");
                    return "0";
                }

                if (!dinputCtrl.ButtonMappings.ContainsKey(buttonkey))
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                    return "0";
                }

                string button = dinputCtrl.ButtonMappings[buttonkey];

                if (button.StartsWith("b"))
                    return "0";

                if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
                    return "1";

                if (button.StartsWith("h"))
                    return "2";
            }

            else
                return xinputKeyType[buttonkey];

            return "0";
        }

        private string GetKeyID(Controller ctrl, SdlToDirectInput dinputCtrl, string buttonkey, bool isXinput)
        {
            if (ctrl == null)
                return "0";

            if (!isXinput)
            {
                if (dinputCtrl == null)
                    return "0";

                if (dinputCtrl.ButtonMappings == null)
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found in gamecontrollerdb for the controller.");
                    return "0";
                }

                if (!dinputCtrl.ButtonMappings.ContainsKey(buttonkey))
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                    return "0";
                }

                string button = dinputCtrl.ButtonMappings[buttonkey];

                if (button.StartsWith("b"))
                {
                    int buttonID = (button.Substring(1).ToInteger());
                    return (buttonID + 48).ToString();
                }

                if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
                {
                    int axisID = button.Substring(1).ToInteger();

                    if (button.StartsWith("-a") || button.StartsWith("+a"))
                        axisID = button.Substring(2).ToInteger();

                    switch (axisID)
                    {
                        case 0:
                            return "0";
                        case 1:
                            return "4";
                        case 2:
                            return "8";
                        case 3:
                            return "12";
                        case 4:
                            return "16";
                        case 5:
                            return "20";
                    }
                }

                if (button.StartsWith("h"))
                    return "32";
            }

            else
                return xinputKeyID[buttonkey];

            return "0";
        }

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

        static private readonly List<string> inputBindings2Keys = new List<string>()
        {
            { "analog_left_x" },
            { "analog_left_y" },
            { "analog_right_x" },
            { "analog_right_y" },
        };

        static private readonly List<string> inputBindings2 = new List<string>()
        {
            { ".bindingtarget2.deviceId" },
            { ".bindingtarget2.keyId" },
            { ".bindingtarget2.keyType" },
            { ".bindingtarget2.providerId" },
        };

        static private readonly List<string> inputBindings1 = new List<string>()
        {
            { ".bindingtarget1.deviceId" },
            { ".bindingtarget1.keyId" },
            { ".bindingtarget1.keyType" },
            { ".bindingtarget1.providerId" },
        };

        static private readonly List<string> inputBindingsSpecial = new List<string>()
        {
            { ".bindingtype" },
            { ".povhatbinding.refvalue" },
        };

        static private readonly List<string> dpadKeys = new List<string>()
        {
            { "dpad_down" },
            { "dpad_left" },
            { "dpad_right" },
            { "dpad_up" },
        };

        static private readonly Dictionary<string, string> povHatDpad = new Dictionary<string, string>()
        {
            { "dpad_down", "4"},
            { "dpad_left", "6" },
            { "dpad_right", "2" },
            { "dpad_up", "0" },
        };

        static private readonly Dictionary<string, string> ps2buttons = new Dictionary<string, string>()
        {
            { "circle", "b" },
            { "cross", "a" },
            { "l1", "leftshoulder" },
            { "l2", "lefttrigger" },
            { "l3", "leftstick" },
            { "r1", "rightshoulder" },
            { "r2", "righttrigger" },
            { "r3", "rightstick" },
            { "select", "back" },
            { "square", "x" },
            { "start", "start" },
            { "triangle", "y" },
        };

        private void CleanupControllerConfig(XDocument cfg)
        {
            for (int i = 1; i < 3; i++)
            {
                string pad = "input.pad" + i + ".";

                foreach (string key in inputBindings2Keys)
                {
                    foreach (string binding in inputBindings1)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }

                    foreach (string binding in inputBindings2)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }

                    foreach (string binding in inputBindingsSpecial)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }
                }

                foreach (KeyValuePair<string, string> button in ps2buttons)
                {
                    foreach (string binding in inputBindings1)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + button.Key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }

                    foreach (string binding in inputBindingsSpecial)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + button.Key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }
                }

                foreach (string key in dpadKeys)
                {
                    foreach (string binding in inputBindings1)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }

                    foreach (string binding in inputBindingsSpecial)
                    {
                        XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + key + binding).FirstOrDefault();
                        toDelete.SetAttributeValue("Value", "0");
                    }
                }

                foreach (string binding in inputBindings1)
                {
                    XElement toDelete = cfg.Descendants("Preference").Where(x => (string)x.Attribute("Name") == pad + "motor" + binding).FirstOrDefault();
                    toDelete.SetAttributeValue("Value", "0");
                }
            }
        }
    }
}
