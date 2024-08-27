using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.IO;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;
using System.Text;

namespace EmulatorLauncher
{
    partial class CapriceForeverGenerator : Generator
    {
        private void ConfigureControllers(IniFile ini, string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // Define profile as retrobat
            ini.WriteValue("Inputs", "DefaultProfileFilename", "Retrobat.prfl");

            // clear existing pad sections of ini file
            string profileFile = Path.Combine(path, "Profiles", "Retrobat.prfl");
            StringBuilder contentBuilder = new StringBuilder();

            // Inject controllers                
            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(2))
                ConfigureJoystick(contentBuilder, controller, controller.PlayerIndex);

            // Write to file
            using (StreamWriter writer = new StreamWriter(profileFile, false, Encoding.UTF8))
            {
                writer.Write(contentBuilder.ToString());
            }
        }

        private void ConfigureJoystick(StringBuilder contentBuilder, Controller ctrl, int playerIndex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            if (ctrl.DirectInput == null)
                return;

            bool isXinput = ctrl.IsXInputDevice;
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid1 = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput sdlController = null;

            SimpleLogger.Instance.Info("[INFO] Player " + ctrl.PlayerIndex + ". Fetching gamecontrollerdb.txt file with guid : " + guid1);

            try { sdlController = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1); }
            catch { }

            if (sdlController == null)
            {
                SimpleLogger.Instance.Info("[INFO] Controller " + playerIndex + " with GUID : " + guid1 + " not available in GamesControllerDB, controller will not be configured.");
                return;
            }

            string instanceGuid = ctrl.DirectInput.InstanceGuid.ToString().ToUpperInvariant().Substring(0,18);
            string name = ctrl.DirectInput.Name;
            string fire1 = GetButton(sdlController, "b", isXinput);
            string fire2 = GetButton(sdlController, "a", isXinput);
            string pov_up = GetButton(sdlController, "dpup", isXinput);
            string pov_down = GetButton(sdlController, "dpdown", isXinput);
            string pov_right = GetButton(sdlController, "dpright", isXinput);
            string pov_left = GetButton(sdlController, "dpleft", isXinput);
            string anal_up = GetButton(sdlController, "lefty", isXinput, "ButtonMin");
            string anal_down = GetButton(sdlController, "lefty", isXinput, "ButtonMax");
            string anal_right = GetButton(sdlController, "leftx", isXinput, "ButtonMax");
            string anal_left = GetButton(sdlController, "leftx", isXinput, "ButtonMin");

            contentBuilder.AppendLine("DeviceGUID=" + instanceGuid);
            contentBuilder.AppendLine("Device=" + name);

            if (playerIndex == 1)
            {
                if (fire1 != null)
                    contentBuilder.AppendLine(fire1 + "=197");
                if (fire2 != null)
                    contentBuilder.AppendLine(fire2 + "=198");
                if (pov_up != null)
                    contentBuilder.AppendLine(pov_up + "=193");
                if (pov_down != null)
                    contentBuilder.AppendLine(pov_down + "=194");
                if (pov_right != null)
                    contentBuilder.AppendLine(pov_right + "=196");
                if (pov_left != null)
                    contentBuilder.AppendLine(pov_left + "=195");
                if (anal_up != null)
                    contentBuilder.AppendLine(anal_up + "=193");
                if (anal_down != null)
                    contentBuilder.AppendLine(anal_down + "=194");
                if (anal_right != null)
                    contentBuilder.AppendLine(anal_right + "=196");
                if (anal_left != null)
                    contentBuilder.AppendLine(anal_left + "=195");

                if (_xAxis)
                {
                    contentBuilder.AppendLine("AnalogXAxis_PressThreshold=50");
                    contentBuilder.AppendLine("AnalogXAxis_DeadZone=4");
                    contentBuilder.AppendLine("AnalogXAxis_MouseX=0");
                    contentBuilder.AppendLine("AnalogXAxis_MouseY=0");
                }

                if (_yAxis)
                {
                    contentBuilder.AppendLine("AnalogYAxis_PressThreshold=50");
                    contentBuilder.AppendLine("AnalogYAxis_DeadZone=4");
                    contentBuilder.AppendLine("AnalogYAxis_MouseX=0");
                    contentBuilder.AppendLine("AnalogYAxis_MouseY=0");
                }

                if (_zAxis)
                {
                    contentBuilder.AppendLine("AnalogZAxis_PressThreshold=50");
                    contentBuilder.AppendLine("AnalogZAxis_DeadZone=4");
                    contentBuilder.AppendLine("AnalogZAxis_MouseX=0");
                    contentBuilder.AppendLine("AnalogZAxis_MouseY=0");
                }

                if (_rxAxis)
                {
                    contentBuilder.AppendLine("AnalogRxAxis_PressThreshold=50");
                    contentBuilder.AppendLine("AnalogRxAxis_DeadZone=4");
                    contentBuilder.AppendLine("AnalogRxAxis_MouseX=0");
                    contentBuilder.AppendLine("AnalogRxAxis_MouseY=0");
                }

                if (_ryAxis)
                {
                    contentBuilder.AppendLine("AnalogRyAxis_PressThreshold=50");
                    contentBuilder.AppendLine("AnalogRyAxis_DeadZone=4");
                    contentBuilder.AppendLine("AnalogRyAxis_MouseX=0");
                    contentBuilder.AppendLine("AnalogRyAxis_MouseY=0");
                }

                if (_rzAxis)
                {
                    contentBuilder.AppendLine("AnalogRzAxis_PressThreshold=50");
                    contentBuilder.AppendLine("AnalogRzAxis_DeadZone=4");
                    contentBuilder.AppendLine("AnalogRzAxis_MouseX=0");
                    contentBuilder.AppendLine("AnalogRzAxis_MouseY=0");
                }

                contentBuilder.AppendLine();

            }
            else if (playerIndex == 2)
            {
                if (fire1 != null)
                    contentBuilder.AppendLine(fire1 + "=204");
                if (fire2 != null)
                    contentBuilder.AppendLine(fire2 + "=205");
                if (pov_up != null)
                    contentBuilder.AppendLine(pov_up + "=200");
                if (pov_down != null)
                    contentBuilder.AppendLine(pov_down + "=201");
                if (pov_right != null)
                    contentBuilder.AppendLine(pov_right + "=203");
                if (pov_left != null)
                    contentBuilder.AppendLine(pov_left + "=202");
                if (anal_up != null)
                    contentBuilder.AppendLine(anal_up + "=200");
                if (anal_down != null)
                    contentBuilder.AppendLine(anal_down + "=201");
                if (anal_right != null)
                    contentBuilder.AppendLine(anal_right + "=203");
                if (anal_left != null)
                    contentBuilder.AppendLine(anal_left + "=202");
            }
        }

        private string GetButton(SdlToDirectInput c, string button, bool isXinput, string direction = "ButtonMin")
        {
            if (!c.ButtonMappings.ContainsKey(button))
                return null;

            string value = c.ButtonMappings[button];

            if (value.StartsWith("b"))
            {
                int buttonID = (value.Substring(1).ToInteger()) + 1;
                return "Button" + buttonID;
            }

            else if (value.StartsWith("h"))
            {
                int hatID = value.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "POV1_ButtonUP";
                    case 2:
                        return "POV1_ButtonRIGHT";
                    case 4:
                        return "POV1_ButtonDOWN";
                    case 8:
                        return "POV1_ButtonLEFT";
                };
            }

            else if (value.StartsWith("+a") || value.StartsWith("a") || value.StartsWith("-a"))
            {
                switch (value.Substring(0,1))
                {
                    case "+":
                        direction = "ButtonMax";
                        break;
                    case "-":
                        direction = "ButtonMin";
                        break;
                }

                int axisID = value.Substring(1).ToInteger();
                
                if (value.StartsWith("+a") || value.StartsWith("-a"))
                    axisID = value.Substring(2).ToInteger();

                if (isXinput && axisID == 5)
                {
                    axisID = 2;
                    direction = "ButtonMin";
                }
                else if (isXinput && axisID == 2)
                {
                    direction = "ButtonMax";
                }

                switch (axisID)
                {
                    case 0:
                        _xAxis = true;
                        return "AnalogXAxis_" + direction;
                    case 1:
                        _yAxis = true;
                        return "AnalogYAxis_" + direction;
                    case 2:
                        _zAxis = true;
                        return "AnalogZAxis_" + direction;
                    case 3:
                        _rxAxis = true;
                        return "AnalogRxAxis_" + direction;
                    case 4:
                        _ryAxis = true;
                        return "AnalogRyAxis_" + direction;
                    case 5:
                        _rzAxis = true;
                        return "AnalogRzAxis_" + direction;
                };
            }

            return null;
        }

        ///Keycodes
        /*
            8 - Backspace
            9 - Tab
            12 - 5 in the numeric keypad when Num Lock is off
            13 - Enter
            16 - Shift
            17 - Ctrl
            18 - Alt
            19 - Pause/Break
            20 - Caps Lock
            27 - Esc
            32 - Space
            33 - Page Up
            34 - Page Down
            35 - End
            36 - Home
            37 - Left arrow
            38 - Up arrow
            39 - Right arrow
            40 - Down arrow
            44 - Print Screen
            45 - Insert
            46 - Delete

            48 - 0
            49 - 1
            50 - 2
            51 - 3
            52 - 4
            53 - 5
            54 - 6
            55 - 7
            56 - 8
            57 - 9

            65 - A
            66 - B
            67 - C
            68 - D
            69 - E
            70 - F
            71 - G
            72 - H
            73 - I
            74 - J
            75 - K
            76 - L
            77 - M
            78 - N
            79 - O
            80 - P
            81 - Q
            82 - R
            83 - S
            84 - T
            85 - U
            86 - V
            87 - W
            88 - X
            89 - Y
            90 - Z

            91 - left Win
            92 - right Win
            93 - Popup

            96 - 0 in the numeric keypad
            97 - 1 in the numeric keypad
            98 - 2 in the numeric keypad
            99 - 3 in the numeric keypad
            100 - 4 in the numeric keypad
            101 - 5 in the numeric keypad
            102 - 6 in the numeric keypad
            103 - 7 in the numeric keypad
            104 - 8 in the numeric keypad
            105 - 9 in the numeric keypad
            106 - * in the numeric keypad
            107 - + in the numeric keypad
            109 - - in the numeric keypad
            110 - . in the numeric keypad
            111 - / in the numeric keypad

            112 - F1
            113 - F2
            114 - F3
            115 - F4
            116 - F5
            117 - F6
            118 - F7
            119 - F8
            120 - F9
            121 - F10
            122 - F11
            123 - F12

            144 - Num Lock
            145 - Scroll Lock
            160 - left Shift
            161 - right Shift
            162 - left Ctrl
            163 - right Ctrl
        */
    }
}
