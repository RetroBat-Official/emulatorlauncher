using System;
using System.Collections.Generic;
using System.Linq;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class AresGenerator : Generator
    {
        private void CreateControllerConfiguration(BmlFile bml)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            // clear existing pad sections of file
            for (int i = 1; i < 6; i++)
            {
                var vpad = bml.GetOrCreateContainer("VirtualPad" + i);

                foreach (string button in virtualPadButtons)
                {
                    vpad[button] = ";;";
                }

                var vmouse = bml.GetOrCreateContainer("VirtualMouse" + i);

                foreach (string button in mouseButtons)
                {
                    vmouse[button] = ";;";
                }
            }

            // Inject controllers                
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(5))
                ConfigureInput(bml, controller); // ini has one section for each pad (from Pad1 to Pad5)
        }

        private void ConfigureInput(BmlFile bml, Controller controller)
        {
            if (controller == null || controller.Config == null)
                return;

            if (controller.IsKeyboard)
                ConfigureKeyboard(bml, controller.Config, controller.PlayerIndex);
            else
                ConfigureJoystick(bml, controller, controller.PlayerIndex);
        }

        private void ConfigureKeyboard(BmlFile bml, InputConfig keyboard, int playerindex)
        {
            if (keyboard == null)
                return;

            var vpad = bml.GetOrCreateContainer("VirtualPad" + playerindex);

            vpad["Pad.Up"] = GetKeyName(InputKey.up, keyboard);
            vpad["Pad.Down"] = GetKeyName(InputKey.down, keyboard);
            vpad["Pad.Left"] = GetKeyName(InputKey.left, keyboard);
            vpad["Pad.Right"] = GetKeyName(InputKey.right, keyboard);
            vpad["Select"] = GetKeyName(InputKey.select, keyboard);
            vpad["Start"] = GetKeyName(InputKey.start, keyboard);
            vpad["A..South"] = GetKeyName(InputKey.a, keyboard);
            vpad["B..East"] = GetKeyName(InputKey.b, keyboard);
            vpad["X..West"] = GetKeyName(InputKey.y, keyboard);
            vpad["Y..North"] = GetKeyName(InputKey.x, keyboard);
            vpad["L-Bumper"] = GetKeyName(InputKey.pageup, keyboard);
            vpad["R-Bumper"] = GetKeyName(InputKey.pagedown, keyboard);
            vpad["L-Trigger"] = GetKeyName(InputKey.l2, keyboard);
            vpad["R-Trigger"] = GetKeyName(InputKey.r2, keyboard);
            vpad["L-Stick..Click"] = GetKeyName(InputKey.l3, keyboard);
            vpad["R-Stick..Click"] = GetKeyName(InputKey.r3, keyboard);
            vpad["L-Up"] = GetKeyName(InputKey.leftanalogup, keyboard);
            vpad["L-Down"] = GetKeyName(InputKey.leftanalogdown, keyboard);
            vpad["L-Left"] = GetKeyName(InputKey.leftanalogleft, keyboard);
            vpad["L-Right"] = GetKeyName(InputKey.leftanalogright, keyboard);
            vpad["R-Up"] = GetKeyName(InputKey.rightanalogup, keyboard);
            vpad["R-Down"] = GetKeyName(InputKey.rightanalogdown, keyboard);
            vpad["R-Left"] = GetKeyName(InputKey.rightanalogleft, keyboard);
            vpad["R-Right"] = GetKeyName(InputKey.rightanalogright, keyboard);
        }

        private void ConfigureJoystick(BmlFile bml, Controller ctrl, int playerindex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            string guid = ctrl.SdlController != null ? ctrl.SdlController.Guid.ToString().ToLower() : ctrl.Guid.ToString().ToLower();

            var vpad = bml.GetOrCreateContainer("VirtualPad" + playerindex);
            
            string prodID = ctrl.DirectInput.ProductId.ToString("X4");
            string vendorID = ctrl.DirectInput.VendorId.ToString("X");
            string padId = "0x";
            
            int index = ctrl.DeviceIndex;

            if (index == 0)
                padId = padId + vendorID + prodID + "/";
            else
                padId = padId + index + "0" + vendorID + prodID + "/";

            if (n64StyleControllers.ContainsKey(guid))
            {
                Dictionary<string, string> buttons = n64StyleControllers[guid];

                foreach (var button in buttons)
                    vpad[button.Key] = padId + button.Value + ";;";

                return;
            }

            vpad["Pad.Up"] = GetInputKeyName(ctrl, InputKey.up, padId);
            vpad["Pad.Down"] = GetInputKeyName(ctrl, InputKey.down, padId);
            vpad["Pad.Left"] = GetInputKeyName(ctrl, InputKey.left, padId);
            vpad["Pad.Right"] = GetInputKeyName(ctrl, InputKey.right, padId);
            vpad["Select"] = GetInputKeyName(ctrl, InputKey.select, padId);
            vpad["Start"] = GetInputKeyName(ctrl, InputKey.start, padId);
            vpad["A..South"] = GetInputKeyName(ctrl, InputKey.a, padId);
            vpad["B..East"] = GetInputKeyName(ctrl, InputKey.b, padId);
            vpad["X..West"] = GetInputKeyName(ctrl, InputKey.y, padId);
            vpad["Y..North"] = GetInputKeyName(ctrl, InputKey.x, padId);
            vpad["L-Bumper"] = GetInputKeyName(ctrl, InputKey.pageup, padId);
            vpad["R-Bumper"] = GetInputKeyName(ctrl, InputKey.pagedown, padId);
            vpad["L-Trigger"] = GetInputKeyName(ctrl, InputKey.l2, padId);
            vpad["R-Trigger"] = GetInputKeyName(ctrl, InputKey.r2, padId);
            vpad["L-Stick..Click"] = GetInputKeyName(ctrl, InputKey.l3, padId);
            vpad["R-Stick..Click"] = GetInputKeyName(ctrl, InputKey.r3, padId);
            vpad["L-Up"] = GetInputKeyName(ctrl, InputKey.leftanalogup, padId);
            vpad["L-Down"] = GetInputKeyName(ctrl, InputKey.leftanalogdown, padId);
            vpad["L-Left"] = GetInputKeyName(ctrl, InputKey.leftanalogleft, padId);
            vpad["L-Right"] = GetInputKeyName(ctrl, InputKey.leftanalogright, padId);
            vpad["R-Up"] = GetInputKeyName(ctrl, InputKey.rightanalogup, padId);
            vpad["R-Down"] = GetInputKeyName(ctrl, InputKey.rightanalogdown, padId);
            vpad["R-Left"] = GetInputKeyName(ctrl, InputKey.rightanalogleft, padId);
            vpad["R-Right"] = GetInputKeyName(ctrl, InputKey.rightanalogright, padId);
        }

        private static string GetInputKeyName(Controller c, InputKey key, string padId)
        {
            Int64 pid;

            // If controller is nintendo, A/B and X/Y are reversed
            //bool revertbuttons = (c.VendorID == VendorId.USB_VENDOR_NINTENDO);

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    return padId + "3/" + pid + ";;";
                }

                if (input.Type == "axis")
                {
                    if (input.Id < 4)
                    {
                        pid = input.Id;
                        string pval = revertAxis ? "Hi" : "Lo";
                        return padId + "0/" + pid + "/" + pval + ";;";
                    }
                    else if (input.Id == 4)
                        return padId + "0/4/Hi;;";
                    else if (input.Id == 5)
                        return padId + "0/5/Hi;;";
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return padId + "1/1/Lo;;";
                        case 2: return padId + "1/0/Hi;;";
                        case 4: return padId + "1/1/Hi;;";
                        case 8: return padId + "1/0/Lo;;";
                    }
                }
            }
            return ";;";
        }

        private static string GetKeyName(InputKey key, InputConfig keyboard)
        {
            var k = keyboard[key];
            if (k != null)
            {
                return "0x1/0/" + SdlToKeyCode(k.Id) + ";;";
            }
            return ";;";
        }

        private static string SdlToKeyCode(long sdlCode)
        {
            switch (sdlCode)
            {
                case 0x1B: return "0";          // Escape
                case 0x4000003A: return "1";    // F1
                case 0x4000003B: return "2";
                case 0x4000003C: return "3";
                case 0x4000003D: return "4";
                case 0x4000003E: return "5";
                case 0x4000003F: return "6";
                case 0x40000040: return "7";
                case 0x40000041: return "8";
                case 0x40000042: return "9";
                case 0x40000043: return "10";
                case 0x40000044: return "11";
                case 0x40000045: return "12";   // F12
                case 0x40000046: return "13";   // Printscreen
                case 0x40000047: return "14";   // Scrolllock
                case 0x31: return "16";          // 1
                case 0x32: return "17";
                case 0x33: return "18";
                case 0x34: return "19";
                case 0x35: return "20";
                case 0x36: return "21";
                case 0x37: return "22";
                case 0x38: return "23";
                case 0x39: return "24";
                case 0x30: return "25";         // 0
                case 0x2D: return "26";         // -
                case 0x3D: return "27";         // =
                case 0x08: return "28";         // Backspace
                case 0x40000049: return "29";   // Insert
                case 0x7F: return "30";         // Delete
                case 0x4000004A: return "31";   // Home
                case 0x4000004D: return "32";   // End
                case 0x4000004B: return "33";   // PageUp
                case 0x4000004E: return "34";   // PageDown
                case 0x61: return "35";         // A
                case 0x62: return "36";
                case 0x63: return "37";
                case 0x64: return "38";
                case 0x65: return "39";
                case 0x66: return "40";
                case 0x67: return "41";
                case 0x68: return "42";
                case 0x69: return "43";
                case 0x6A: return "44";
                case 0x6B: return "45";
                case 0x6C: return "46";
                case 0x6D: return "47";
                case 0x6E: return "48";
                case 0x6F: return "49";
                case 0x70: return "50";
                case 0x71: return "51";
                case 0x72: return "52";
                case 0x73: return "53";
                case 0x74: return "54";
                case 0x75: return "55";
                case 0x76: return "56";
                case 0x77: return "57";
                case 0x78: return "58";
                case 0x79: return "59";
                case 0x7A: return "60";         // Z
                case 0x5B: return "61";         // Left Bracket
                case 0x5D: return "62";         // Right Bracket
                case 0x5C: return "63";         // BackSlash
                case 0x3B: return "64";         // Semicolon
                case 0x27: return "65";         // Apostrophe
                case 0x2C: return "66";         // Comma
                case 0x2E: return "67";         // Period
                case 0x2F: return "68";         // Slash
                case 0x40000059: return "69";   // Keypad 1
                case 0x4000005A: return "70";
                case 0x4000005B: return "71";
                case 0x4000005C: return "72";
                case 0x4000005D: return "73";
                case 0x4000005E: return "74";
                case 0x4000005F: return "75";
                case 0x40000060: return "76";
                case 0x40000061: return "77";
                case 0x40000062: return "78";   // Keypad 0
                case 0x40000063: return "79";   // Point
                case 0x40000058: return "80";   // Enter
                case 0x40000057: return "81";   // Plus
                case 0x40000056: return "82";   // Minus
                case 0x40000055: return "83";   // Multiply
                case 0x40000054: return "84";   // Divide
                case 0x40000039: return "85";   // Capslock
                case 0x40000052: return "86";   // Up
                case 0x40000051: return "87";   // Down
                case 0x40000050: return "88";   // Left
                case 0x4000004F: return "89";   // Right
                case 0x09: return "90";         // Tab
                case 0x0D: return "91";         // Return
                case 0x20: return "92";         // Space
                case 0x400000E1: return "93";   // Left Shift
                case 0x400000E5: return "94";   // Right Shift
                case 0x400000E0: return "95";   // Left Control
                case 0x400000E4: return "96";   // Right Control
                case 0x400000E2: return "97";   // Left ALT
                case 0x400000E6: return "98";   // Right ALT
            }
            return ";;";
        }

        static readonly List<string> virtualPadButtons = new List<string>() 
        {
            "Pad.Up", "Pad.Down", "Pad.Left", "Pad.Right",
            "Select", "Start", "A..South", "B..East", "X..West", "Y..North",
            "L-Bumper", "R-Bumper", "L-Trigger", "R-Trigger", "L-Stick..Click", "R-Stick..Click",
            "L-Up", "L-Down", "L-Left", "L-Right", "R-Up", "R-Down", "R-Left", "R-Right"
        };

        static readonly List<string> mouseButtons = new List<string>()
        {
            "X", "Y", "Left", "Middle", "Right", "Extra"
        };

        static readonly Dictionary<string, Dictionary<string, string>> n64StyleControllers = new Dictionary<string, Dictionary<string, string>>()
        {
            {
                // Nintendo Switch Online N64 Controller
                "0300b7e67e050000192000000000680c",
                new Dictionary<string, string>()
                {
                    { "Pad.Up", "3/11" },
                    { "Pad.Down", "3/12" },
                    { "Pad.Left", "3/13" },
                    { "Pad.Right", "3/14" },
                    { "Select", "" },
                    { "Start", "3/6" },
                    { "A..South", "3/0" },
                    { "B..East", "" },
                    { "X..West", "3/1" },
                    { "Y..North", "" },
                    { "L-Bumper", "3/9" },
                    { "R-Bumper", "3/10" },
                    { "L-Trigger", "" },
                    { "R-Trigger", "0/4/Hi" },
                    { "L-Stick..Click", "" },
                    { "R-Stick..Click", "" },
                    { "L-Up", "0/1/Lo" },
                    { "L-Down", "0/1/Hi" },
                    { "L-Left", "0/0/Lo" },
                    { "L-Right", "0/0/Hi" },
                    { "R-Up", "3/3" },
                    { "R-Down", "0/5/Hi" },
                    { "R-Left", "3/2" },
                    { "R-Right", "3/4" },
                }
            },

            {
                // Raphnet 2x N64 Adapter
                "030000009b2800006300000000000000",
                new Dictionary<string, string>()
                {
                    { "Pad.Up", "3/10" },
                    { "Pad.Down", "3/11" },
                    { "Pad.Left", "3/12" },
                    { "Pad.Right", "3/13" },
                    { "Select", "" },
                    { "Start", "3/3" },
                    { "A..South", "3/0" },
                    { "B..East", "" },
                    { "X..West", "3/1" },
                    { "Y..North", "" },
                    { "L-Bumper", "3/4" },
                    { "R-Bumper", "3/5" },
                    { "L-Trigger", "" },
                    { "R-Trigger", "3/2" },
                    { "L-Stick..Click", "" },
                    { "R-Stick..Click", "" },
                    { "L-Up", "0/1/Lo" },
                    { "L-Down", "0/1/Hi" },
                    { "L-Left", "0/0/Lo" },
                    { "L-Right", "0/0/Hi" },
                    { "R-Up", "3/6" },
                    { "R-Down", "3/7" },
                    { "R-Left", "3/8" },
                    { "R-Right", "3/9" },
                }
            },

            {
                // Mayflash N64 Adapter
                "03000000d620000010a7000000000000",
                new Dictionary<string, string>()
                {
                    { "Pad.Up", "1/1/Lo" },
                    { "Pad.Down", "1/1/Hi" },
                    { "Pad.Left", "1/0/Lo" },
                    { "Pad.Right", "1/0/Hi" },
                    { "Select", "" },
                    { "Start", "3/9" },
                    { "A..South", "3/1" },
                    { "B..East", "" },
                    { "X..West", "3/2" },
                    { "Y..North", "" },
                    { "L-Bumper", "3/4" },
                    { "R-Bumper", "3/5" },
                    { "L-Trigger", "" },
                    { "R-Trigger", "3/6" },
                    { "L-Stick..Click", "" },
                    { "R-Stick..Click", "" },
                    { "L-Up", "0/1/Lo" },
                    { "L-Down", "0/1/Hi" },
                    { "L-Left", "0/0/Lo" },
                    { "L-Right", "0/0/Hi" },
                    { "R-Up", "0/3/Lo" },
                    { "R-Down", "0/3/Hi" },
                    { "R-Left", "0/2/Lo" },
                    { "R-Right", "0/2/Hi" },
                }
            },
        };
    }
}
