using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using System;
using System.Collections.Generic;

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

    public static Dictionary<InputKey, string> SdlToGameControllerDB = new Dictionary<InputKey, string>()
    {
        { InputKey.a,               "a" },
        { InputKey.b,               "b"},
        { InputKey.select,          "back" },
        { InputKey.down,            "dpdown" },
        { InputKey.left,            "dpleft" },
        { InputKey.right,           "dpright" },
        { InputKey.up,              "dpup" },
        { InputKey.pageup,          "leftshoulder" },
        { InputKey.l3,              "leftstick" },
        { InputKey.l2,              "lefttrigger" },
        { InputKey.joystick1left,   "leftx" },
        { InputKey.joystick1up,     "lefty" },
        { InputKey.pagedown,        "rightshoulder" },
        { InputKey.r3,              "rightstick" },
        { InputKey.r2,              "righttrigger" },
        { InputKey.joystick2left,   "rightx" },
        { InputKey.joystick2up,     "righty" },
        { InputKey.start,           "start" },
        { InputKey.x,               "x" },
        { InputKey.y,               "y" },
    };
}