using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using System;
using System.Collections.Generic;

namespace EmulatorLauncher.Common.Joysticks
{
    public static class GameControllerDBParser
    {
        public static SdlToDirectInput ParseByGuid(string gamecontrollerDBpath, string targetGuid)
        {
            try
            {
                string[] lines = System.IO.File.ReadAllLines(gamecontrollerDBpath);
                foreach (var line in lines)
                {
                    // skip comment
                    if (line.StartsWith("#"))
                        continue;

                    // split the line into components
                    string[] parts = line.Split(',');

                    // check if the guid matches the target guid
                    if (parts.Length > 0 && parts[0].Trim().Equals(targetGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        // Create SdlToDirectInput object and return
                        return SdlToDirectInput.FromString(parts);
                    }
                }
            }
            catch
            { }

            return null;
        }
    }

    public class SdlToDirectInput
    {
        public string Name { get; set; }
        public Dictionary<string, string> ButtonMappings { get; set; }

        public static SdlToDirectInput FromString(string[] parts)
        {
            if (parts.Length < 2)
                return null;

            var controller = new SdlToDirectInput
            {
                Name = parts[0].Trim(),
                ButtonMappings = new Dictionary<string, string>()
            };

            for (int i = 1; i < parts.Length; i++)
            {
                string[] buttonParts = parts[i].Split(':');
                if (buttonParts.Length == 2)
                {
                    string buttonName = buttonParts[0].Trim();
                    string buttonValue = buttonParts[1].Trim();

                    controller.ButtonMappings[buttonName] = buttonValue;
                }
            }

            return controller;
        }

        public static Input GetDinputInput(SdlToDirectInput ctrl, InputKey key, Input input, bool isXinput)
        {
            if (ctrl.ButtonMappings == null)
                return input;

            // Get Name
            string buttonDinputName = SdlToGameControllerDB[key];

            if (!ctrl.ButtonMappings.ContainsKey(buttonDinputName))
                return input;

            string buttonName = ctrl.ButtonMappings[buttonDinputName];

            if (isXinput)
            {
                if (buttonName == "a2")
                    buttonName = buttonName.Replace("a", "+a");
                else if (buttonName == "a5")
                    buttonName = "-a2";
            }

            // Get type
            string type;

            if (buttonName.StartsWith("a") || buttonName.StartsWith("+a") || buttonName.StartsWith("-a"))
                type = "axis";
            else if (buttonName.StartsWith("h"))
                type = "hat";
            else
                type = "button";

            // Get id
            int id = -1;
            if (type == "axis")
            {
                if (buttonName.StartsWith("+a") || buttonName.StartsWith("-a"))
                    id = buttonName.Substring(2).ToInteger();
                else
                    id = buttonName.Substring(1).ToInteger();
            }

            else if (type == "hat")
                id = 0;
            
            else
                id = buttonName.Substring(1).ToInteger();
            
            // Get value
            int value = -1;

            if (type == "axis")
            {
                if (buttonName.StartsWith("+a"))
                    value = 1;
                else
                    value = -1;
            }

            else if (type == "hat")
                value = buttonName.Substring(3).ToInteger();

            else
                value = 1;

            // Build input
            Input ret = new Input();
            ret.Name = key;
            ret.Type = type;
            ret.Id = id;
            ret.Value = value;
            return ret;
        }

        public static readonly Dictionary<InputKey, string> SdlToGameControllerDB = new Dictionary<InputKey, string>()
        {
            { InputKey.a,               "a" },
            { InputKey.b,               "b"},
            { InputKey.select,          "back" },
            { InputKey.down,            "dpdown" },
            { InputKey.left,            "dpleft" },
            { InputKey.right,           "dpright" },
            { InputKey.up,              "dpup" },
            { InputKey.l1,              "leftshoulder" },
            { InputKey.l3,              "leftstick" },
            { InputKey.l2,              "lefttrigger" },
            { InputKey.joystick1left,   "leftx" },
            { InputKey.joystick1up,     "lefty" },
            { InputKey.r1,              "rightshoulder" },
            { InputKey.r3,              "rightstick" },
            { InputKey.r2,              "righttrigger" },
            { InputKey.joystick2left,   "rightx" },
            { InputKey.joystick2up,     "righty" },
            { InputKey.start,           "start" },
            { InputKey.x,               "x" },
            { InputKey.y,               "y" },
            { InputKey.hotkey,          "back" },
        };

        public static readonly Dictionary<string, string> axisNameMapping = new Dictionary<string, string>()
        {
            { "a0", "X" },
            { "a1", "Y"},
            { "a2", "Z"},
            { "a3", "RX"},
            { "a4", "RY"},
            { "a5", "RZ"},
        };
    }

}