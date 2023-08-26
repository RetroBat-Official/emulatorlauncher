using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;
using System.Globalization;
using System.IO;
using SharpDX.DirectInput;
using System.Windows.Documents;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        static Dictionary<string, int> inputPortNb = new Dictionary<string, int>()
        {
            { "QuickNes", 2 },
            { "NesHawk", 4 },
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

            if (controller.IsKeyboard && this.Controllers.Count(i => !i.IsKeyboard) == 0)
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
                foreach (var c in this.Controllers.OrderBy(i => i.DirectInput.DeviceIndex))
                {
                    if (!c.IsXInputDevice)
                        list.Add(c);
                }
                index = list.IndexOf(controller) + 1;
            }
            else
                index = controller.XInput.DeviceIndex + 1;

            var controllerConfig = json.GetOrCreateContainer("AllTrollers");

            if (system == "nes")
            {
                var nesControllerConfig = controllerConfig.GetOrCreateContainer("NES Controller");

                foreach (var x in nesMapping)
                {
                    string value = x.Value;
                    InputKey key = x.Key;

                    if (isXInput)
                    {
                        nesControllerConfig["P" + playerIndex + " " + value] = "X" + index + " " + GetXInputKeyName(controller, key);
                    }
                    else
                    {
                        nesControllerConfig["P" + playerIndex + " " + value] = "J" + index + " " + GetInputKeyName(controller, key);
                    }
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
            { InputKey.a,               "A" },
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
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "U+";
                        else return "U-";
                    case 3:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "R+";
                        else return "R-";
                }
            }
            return "";
        }
    }
}