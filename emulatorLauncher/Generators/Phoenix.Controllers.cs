using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;

namespace EmulatorLauncher
{
    partial class PhoenixGenerator : Generator
    {
        private void ConfigureControllers(XElement settingsPlatform, string system)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!this.Controllers.Any(c => !c.IsKeyboard))
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Phoenix");

            var xmlInput = settingsPlatform.Element("Input");
            xmlInput.RemoveAll();
            xmlInput.SetAttributeValue("hide-cursor", "true");

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available");
                return;
            }

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
                ConfigureInput(xmlInput, controller, gamecontrollerDB, system);
        }

        private void ConfigureInput(XElement xml, Controller controller, string gamecontrollerDB, string system)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(xml, controller, gamecontrollerDB, system);
        }

        private void ConfigureJoystick(XElement xml, Controller ctrl, string gamecontrollerdb, string system)
        {
            if (ctrl == null)
                return;

            SdlToDirectInput controller = null;
            bool isXinput = ctrl.IsXInputDevice;
            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";

            try { controller = GameControllerDBParser.ParseByGuid(gamecontrollerdb, guid); }
            catch { }

            if (controller == null || controller.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt does not contain mapping for the controller " + guid + ". Controller mapping will not be available");
                return;
            }

            int index = ctrl.dinputCtrl != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex;

            List<string> buttonMapping = new List<string>();

            if (system == "3do")
            {
                if (SystemConfig.isOptSet("phoenix_dpad") && SystemConfig.getOptBoolean("phoenix_dpad"))
                {
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpup", isXinput) + "@-1");
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpdown", isXinput) + "@1");
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpleft", isXinput) + "@-1");
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpright", isXinput) + "@1");
                }
                else
                {
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "lefty", isXinput) + "@-1");
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "lefty", isXinput) + "@1");
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftx", isXinput) + "@-1");
                    buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftx", isXinput) + "@1");
                }

                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "x", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "a", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "b", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "back", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "start", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftshoulder", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "rightshoulder", isXinput));
            }
            else
            {
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpup", isXinput) + "@-1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpdown", isXinput) + "@1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpleft", isXinput) + "@-1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpright", isXinput) + "@1");

                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "b", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "a", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "x", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "back", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "start", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "y", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftshoulder", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "rightshoulder", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "lefttrigger", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "righttrigger", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftstick", isXinput));
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "rightstick", isXinput));
            }

            string input = string.Join(",", buttonMapping.Where(i => !string.IsNullOrEmpty(i)));

            XElement device = new XElement("Device", new XAttribute("name", "Joy"), input);
            xml.Add(device);

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private string GetInputCode(SdlToDirectInput ctrl, Controller c, string buttonkey, bool isXinput)
        {
            if (!ctrl.ButtonMappings.ContainsKey(buttonkey))
                return "";

            string button = ctrl.ButtonMappings[buttonkey];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1:
                    case 4:
                        if (c.VendorID == USB_VENDOR.SONY)
                            return "a7@0";
                        else
                            return "a6@0";
                    case 2:
                    case 8:
                        if (c.VendorID == USB_VENDOR.SONY)
                            return "a6@0";
                        else
                            return "a5@0";
                };
            }

            else if (button.StartsWith("+a") || button.StartsWith("-a"))
                return button.Substring(1) + "@0";

            else if (button.StartsWith("a"))
            {
                if (isXinput && button == "a2")
                    return "a2@0@1";
                else if (isXinput && button == "a5")
                    return "a2@0@-1";
                else
                    return button + "@0";
            }

            else
                return button;

            return button;
        }
    }
}
