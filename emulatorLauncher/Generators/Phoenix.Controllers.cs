using System;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class PhoenixGenerator : Generator
    {
        private void ConfigureControllers(XElement settingsPlatform)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!this.Controllers.Any(c => !c.IsKeyboard))
                return;

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
                ConfigureInput(xmlInput, controller, gamecontrollerDB);
        }

        private void ConfigureInput(XElement xml, Controller controller, string gamecontrollerDB)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                return;
            else
                ConfigureJoystick(xml, controller, controller.PlayerIndex, gamecontrollerDB);
        }

        private void ConfigureJoystick(XElement xml, Controller ctrl, int playerIndex, string gamecontrollerdb)
        {
            if (ctrl == null)
                return;

            SdlToDirectInput controller = null;
            string guid = (ctrl.Guid.ToString()).Substring(0, 27) + "00000";

            try { controller = GameControllerDBParser.ParseByGuid(gamecontrollerdb, guid); }
            catch { }

            if (controller == null || controller.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[WARNING] gamecontrollerdb.txt does not contain mapping for the controller " + guid + ". Controller mapping will not be available");
                return;
            }

            int index = ctrl.dinputCtrl != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex;

            List<string> buttonMapping = new List<string>();

            if (SystemConfig.isOptSet("phoenix_dpad") && SystemConfig.getOptBoolean("phoenix_dpad"))
            {
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpup") + "@-1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpdown") + "@1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpleft") + "@-1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "dpright") + "@1");
            }
            else
            {
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "lefty") + "@-1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "lefty") + "@1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftx") + "@-1");
                buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftx") + "@1");
            }

            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "x"));
            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "a"));
            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "b"));
            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "back"));
            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "start"));
            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "leftshoulder"));
            buttonMapping.Add("j" + index + GetInputCode(controller, ctrl, "rightshoulder"));

            string input = string.Join(",", buttonMapping.Where(i => !string.IsNullOrEmpty(i)));

            XElement device = new XElement("Device", new XAttribute("name", "Joy"), input);
            xml.Add(device);
        }

        private string GetInputCode(SdlToDirectInput ctrl, Controller c, string buttonkey)
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
                return button + "@0";

            else
                return button;

            return button;
        }
    }
}
