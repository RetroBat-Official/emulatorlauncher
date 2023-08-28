using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;
using System.Globalization;
using System.IO;
using SharpDX.DirectInput;
using System.Windows.Documents;
using System.Data;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        static Dictionary<string, int> inputPortNb = new Dictionary<string, int>()
        {
            { "QuickNes", 2 },
            { "NesHawk", 4 },
            { "Faust", 8 },
            { "Snes9x", 5 },
            { "BSNES", 8 },
            { "Mupen64Plus", 4 },
        };

        private void CreateControllerConfiguration(DynamicJson json, string system, string core)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            int maxPad = inputPortNb[core];

            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(maxPad))
                ConfigureInput(controller, json, system, core);
        }

        private void ConfigureInput(Controller controller, DynamicJson json, string system, string core)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(controller, json, system, core);
            else
                ConfigureJoystick(controller, json, system, core);
        }

        private void ConfigureJoystick(Controller controller, DynamicJson json, string system, string core)
        {
            if (controller == null)
                return;

            var ctrlrCfg = controller.Config;
            if (ctrlrCfg == null)
                return;

            if (controller.DirectInput == null && controller.XInput == null)
                return;

            bool isXInput = controller.IsXInputDevice;
            int playerIndex = controller.PlayerIndex;
            int index = 1;

            if (!isXInput)
            {
                var list = new List<Controller>();
                foreach (var c in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.DirectInput != null ? c.DirectInput.DeviceIndex : c.DeviceIndex))
                {
                    if (!c.IsXInputDevice)
                        list.Add(c);
                }
                index = list.IndexOf(controller) + 1;
            }
            else
            {
                var list = new List<Controller>();
                foreach (var c in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.WinmmJoystick != null ? c.WinmmJoystick.Index : c.DeviceIndex))
                {
                    if (c.IsXInputDevice)
                        list.Add(c);
                }
                index = list.IndexOf(controller) + 1;
            }

            //int testindex = controller.DirectInput.DeviceIndex;

            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);
            InputKeyMapping mapping = mappingToUse[system];
            
            foreach (var x in mapping)
            {
                string value = x.Value;
                InputKey key = x.Key;

                if (isXInput)
                {
                    controllerConfig["P" + playerIndex + " " + value] = "X" + index + " " + GetXInputKeyName(controller, key);
                }
                else
                {
                    controllerConfig["P" + playerIndex + " " + value] = "J" + index + " " + GetInputKeyName(controller, key);
                }
            }
        }

        private static void ConfigureKeyboard(Controller controller, DynamicJson json, string system, string core)
        {
            if (controller == null)
                return;

            InputConfig keyboard = controller.Config;
            if (keyboard == null)
                return;
        }

        static InputKeyMapping nesMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.x,               "B" },
            { InputKey.a,               "A" }
        };

        static InputKeyMapping snesMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.a,               "B" },
            { InputKey.b,               "A" },
            { InputKey.x,               "X" },
            { InputKey.y,               "Y" },
            { InputKey.pageup,          "L" },
            { InputKey.pagedown,        "R" }
        };

        static InputKeyMapping n64Mapping = new InputKeyMapping()
        {
            { InputKey.leftanalogup,        "A Up" },
            { InputKey.leftanalogdown,      "A Down" },
            { InputKey.leftanalogleft,      "A Left" },
            { InputKey.leftanalogright,     "A Right" },
            { InputKey.up,                  "DPad U"},
            { InputKey.down,                "DPad D"},
            { InputKey.left,                "DPad L" },
            { InputKey.right,               "DPad R"},
            { InputKey.start,               "Start" },
            { InputKey.r2,                  "Z" },
            { InputKey.y,                   "B" },
            { InputKey.a,                   "A" },
            { InputKey.rightanalogup,       "C Up" },
            { InputKey.rightanalogdown,     "C Down" },
            { InputKey.rightanalogleft,     "C Left" },
            { InputKey.rightanalogright,    "C Right" },
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" }
        };

        private static string GetXInputKeyName(Controller c, InputKey key)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0: return "A";
                        case 1: return "B";
                        case 2: return "Y";
                        case 3: return "X";
                        case 4: return "LeftShoulder";
                        case 5: return "RightShoulder";
                        case 6: return "Back";
                        case 7: return "Start";
                        case 8: return "LeftThumb";
                        case 9: return "RightThumb";
                        case 10: return "Guide";
                    }
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LStickRight";
                            else return "LStickLeft";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "LStickDown";
                            else return "LStickUp";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RStickRight";
                            else return "RStickLeft";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RStickDown";
                            else return "RStickUp";
                        case 4: return "LeftTrigger";
                        case 5: return "RightTrigger";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "DpadUp";
                        case 2: return "DpadRight";
                        case 4: return "DpadDown";
                        case 8: return "DpadLeft";
                    }
                }
            }
            return "";
        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var input = c.GetDirectInputMapping(key);
            if (input == null)
                return "\"\"";

            long nb = input.Id + 1;


            if (input.Type == "button")
                return ("B" + nb);

            if (input.Type == "hat")
            {
                pid = input.Value;
                switch (pid)
                {
                    case 1: return "POV1U";
                    case 2: return "POV1R";
                    case 4: return "POV1D";
                    case 8: return "POV1L";
                }
            }

            if (input.Type == "axis")
            {
                pid = input.Id;
                switch (pid)
                {
                    case 0:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "X+";
                        else return "X-";
                    case 1:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "Y+";
                        else return "Y-";
                    case 2:
                        if (c.VendorID == USB_VENDOR.SONY && ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))) return "Z+";
                        else if (c.VendorID == USB_VENDOR.SONY) return "Z-";
                        else if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RotationX+";
                        else return "RotationX-";
                    case 3:
                        if (c.VendorID == USB_VENDOR.SONY && ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))) return "RotationX+";
                        else if (c.VendorID == USB_VENDOR.SONY) return "RotationX-";
                        else if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RotationY+";
                        else return "RotationY-";
                    case 4:
                        if (c.VendorID == USB_VENDOR.SONY && ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))) return "RotationY+";
                        else if (c.VendorID == USB_VENDOR.SONY) return "RotationY-";
                        else if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "Z+";
                        else return "Z-";
                    case 5:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "RotationZ+";
                        else return "RotationZ-";
                }
            }
            return "";
        }

        static Dictionary<string, string> systemController = new Dictionary<string, string>()
        {
            { "nes", "NES Controller" },
            { "snes", "SNES Controller" },
            { "n64", "Nintendo 64 Controller" }
        };

        static Dictionary<string, InputKeyMapping> mappingToUse = new Dictionary<string, InputKeyMapping>()
        {
            { "nes", nesMapping },
            { "snes", snesMapping },
            { "n64", n64Mapping }
        };
    }
}