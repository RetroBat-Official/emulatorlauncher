using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Snes9xGenerator : Generator
    {
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;


            // clear existing pad sections of file
            for (int i = 1; i <= 8; i++)
            {
                ini.WriteValue("Controls\\Win", "Joypad" + i + ":Enabled", "FALSE");
            }

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(8))
                ConfigureInput(ini, controller);

            // Some other stuff for background input
            ini.WriteValue("Controls\\Win", "Input:Background", "OFF");
            ini.WriteValue("Controls\\Win", "Input:BackgroundKeyHotkeys", "ON");
        }

        private void ConfigureInput(IniFile ini, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(ini, controller.Config, controller.PlayerIndex);
            else
                ConfigureJoystick(ini, controller, controller.PlayerIndex);
        }

        private void ConfigureKeyboard(IniFile ini, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            return; 
            // TODO
        }

        private void ConfigureJoystick(IniFile ini, Controller ctrl, int playerindex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            // Initializing controller information
            string guid = (ctrl.Guid.ToString()).Substring(0, 27) + "00000";
            SdlToDirectInput controller = null;
            int index = ctrl.DirectInput != null ? ctrl.DirectInput.JoystickID : ctrl.DeviceIndex;
            string joyNb = "Joypad" + playerindex;
            bool isxinput = ctrl.IsXInputDevice;
            bool allowdiagonals = true;

            if (SystemConfig.isOptSet("snes9x_allowdiagonals") && SystemConfig.getOptBoolean("snes9x_allowdiagonals"))
                allowdiagonals = false;

            // Looking for gamecontrollerdb.txt file
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                gamecontrollerDB = null;
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

            // Fetching controller mapping from gamecontrollerdb.txt file
            controller = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

            if (controller == null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid);
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ": " + guid + " found in gamecontrollerDB file.");

            if (controller.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return;
            }

            ini.WriteValue("Controls\\Win", joyNb + ":Enabled", "TRUE");

            if (SystemConfig.isOptSet("snes9x_analog") && SystemConfig.getOptBoolean("snes9x_analog"))
            {
                allowdiagonals = false;
                ini.WriteValue("Controls\\Win", joyNb + ":Up", "(J" + index + ")Up");
                ini.WriteValue("Controls\\Win", joyNb + ":Down", "(J" + index + ")Down");
                ini.WriteValue("Controls\\Win", joyNb + ":Left", "(J" + index + ")Left");
                ini.WriteValue("Controls\\Win", joyNb + ":Right", "(J" + index + ")Right");
            }

            else
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Up", GetDinputMapping(index, controller, "dpup", isxinput));
                ini.WriteValue("Controls\\Win", joyNb + ":Down", GetDinputMapping(index, controller, "dpdown", isxinput));
                ini.WriteValue("Controls\\Win", joyNb + ":Left", GetDinputMapping(index, controller, "dpleft", isxinput));
                ini.WriteValue("Controls\\Win", joyNb + ":Right", GetDinputMapping(index, controller, "dpright", isxinput));
            }

            ini.WriteValue("Controls\\Win", joyNb + ":A", GetDinputMapping(index, controller, "b", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":B", GetDinputMapping(index, controller, "a", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":Y", GetDinputMapping(index, controller, "x", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":X", GetDinputMapping(index, controller, "y", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":L", GetDinputMapping(index, controller, "leftshoulder", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":R", GetDinputMapping(index, controller, "rightshoulder", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":Start", GetDinputMapping(index, controller, "start", isxinput));
            ini.WriteValue("Controls\\Win", joyNb + ":Select", GetDinputMapping(index, controller, "back", isxinput));

            if (allowdiagonals)
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Up", GetDinputMapping(index, controller, "diag_dpup_dpleft", isxinput));
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Up", GetDinputMapping(index, controller, "diag_dpup_dpright", isxinput));
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Down", GetDinputMapping(index, controller, "diag_dpdown_dpright", isxinput));
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Down", GetDinputMapping(index, controller, "diag_dpdown_dpleft", isxinput));
            }
            else
            {
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Up", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Up", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Right+Down", "Unassigned");
                ini.WriteValue("Controls\\Win", joyNb + ":Left+Down", "Unassigned");
            }

            // Unassigned keys
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:AutoFire", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:AutoHold", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:TempTurbo", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:ClearAll", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:A", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:B", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:Y", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:X", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:L", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:R", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:Start", "Unassigned");
            ini.WriteValue("Controls\\Win", joyNb + "Turbo:Select", "Unassigned");
        }

        private string GetDinputMapping(int index, SdlToDirectInput c, string buttonkey, bool isxinput, int plus = 0)
        {
            if (c == null)
                return "Unassigned";

            if (!c.ButtonMappings.ContainsKey(buttonkey) && !buttonkey.StartsWith("diag"))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "Unassigned";
            }

            if (buttonkey.StartsWith("diag_"))
            {
                string [] buttonlist = buttonkey.Split('_');

                if (!c.ButtonMappings.ContainsKey(buttonlist[1]))
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonlist[1] + " in gamecontrollerdb file");
                    return "Unassigned";
                }
                string button1 = c.ButtonMappings[buttonlist[1]];

                if (!c.ButtonMappings.ContainsKey(buttonlist[2]))
                {
                    SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonlist[2] + " in gamecontrollerdb file");
                    return "Unassigned";
                }
                string button2 = c.ButtonMappings[buttonlist[2]];

                if (button1.StartsWith("b"))
                {
                    int button1ID = (button1.Substring(1).ToInteger());
                    int button2ID = (button2.Substring(1).ToInteger());
                    return "(J" + index + ")Button " + button1ID + " " + button2ID;
                }

                else if (button1.StartsWith("h"))
                {
                    int hat1ID = (button1.Substring(3).ToInteger());
                    int hat2ID = (button2.Substring(3).ToInteger());
                    string povIndex = "(J" + index + ")POV ";

                    switch (hat1ID)
                    {
                        case 1:
                            switch (hat2ID)
                            {
                                case 2:
                                    return povIndex + "Up Right";
                                case 8:
                                    return povIndex + "Up Left";
                            }
                            return "Unassigned";
                        case 2:
                            switch (hat2ID)
                            {
                                case 1:
                                    return povIndex + "Up Right";
                                case 4:
                                    return povIndex + "Dn Right";
                            }
                            return "Unassigned";
                        case 4:
                            switch (hat2ID)
                            {
                                case 2:
                                    return povIndex + "Dn Right";
                                case 8:
                                    return povIndex + "Dn Left";
                            }
                            return "Unassigned";
                        case 8:
                            switch (hat2ID)
                            {
                                case 1:
                                    return povIndex + "Up Left";
                                case 4:
                                    return povIndex + "Dn Left";
                            }
                            return "Unassigned";
                    }
                }
            }

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("-a"))
                plus = -1;

            if (button.StartsWith("+a"))
                plus = 1;

            if (isxinput)
            {
                if (button == "a5")
                    return "(J" + index + ")Z Up";
            }

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger());
                return "(J" + index + ")Button " + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "(J" + index + ")POV Up";
                    case 2:
                        return "(J" + index + ")POV Right";
                    case 4:
                        return "(J" + index + ")POV Down";
                    case 8:
                        return "(J" + index + ")POV Left";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                switch (axisID)
                {
                    case 0:
                        if (plus == 1) return "(J" + index + ")Right";
                        else return "(J" + index + ")Left";
                    case 1:
                        if (plus == 1) return "(J" + index + ")Down";
                        else return "(J" + index + ")Up";
                    case 2:
                        if (plus == 1) return "(J" + index + ")Z Up";
                        else return "(J" + index + ")Z Down";
                    case 3:
                        if (plus == 1) return "(J" + index + ")V Down";
                        else return "(J" + index + ")V Up";
                    case 4:
                        if (plus == 1) return "(J" + index + ")U Down";
                        else return "(J" + index + ")U Up";
                    case 5:
                        if (plus == 1) return "(J" + index + ")R Up";
                        else return "(J" + index + ")R Down";
                }
            }

            return "Unassigned";
        }
    }
}
