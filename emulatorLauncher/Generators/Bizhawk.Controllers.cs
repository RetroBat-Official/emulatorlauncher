using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Management;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private static List<string> systemMonoPlayer = new List<string>() { "gb", "gbc", "gba", "lynx", "nds" };

        private static Dictionary<string, int> inputPortNb = new Dictionary<string, int>()
        {
            { "Ares64", 4 },
            { "BSNES", 8 },
            { "Faust", 8 },
            { "Gambatte", 1 },
            { "GBHawk", 1 },
            { "Genplus-gx", 8 },
            { "HyperNyma", 5 },
            { "mGBA", 1 },
            { "melonDS", 1 },
            { "Mupen64Plus", 4 },
            { "NeoPop", 1 },
            { "NesHawk", 4 },
            { "PCEHawk", 5 },
            { "QuickNes", 2 },
            { "SameBoy", 1 },
            { "Saturnus", 12 },
            { "SMSHawk", 2 },
            { "Snes9x", 5 },
            { "TurboNyma", 5 }, 
        };

        private void CreateControllerConfiguration(DynamicJson json, string system, string core)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            int maxPad = inputPortNb[core];

            ResetControllerConfiguration(json, maxPad, system, core);

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
            bool monoplayer = systemMonoPlayer.Contains(system);
            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);
            InputKeyMapping mapping = mappingToUse[system];

            // Perform clean up of previous config to avoid having a controller in 2 positions
            int maxPad = inputPortNb[core];

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

            foreach (var x in mapping)
            {
                string value = x.Value;
                InputKey key = x.Key;

                if (!monoplayer)
                {
                    if (isXInput)
                    {
                        controllerConfig["P" + playerIndex + " " + value] = "X" + index + " " + GetXInputKeyName(controller, key);
                    }
                    else
                    {
                        controllerConfig["P" + playerIndex + " " + value] = "J" + index + " " + GetInputKeyName(controller, key);
                    }
                }
                else
                {
                    if (isXInput)
                    {
                        controllerConfig[value] = "X" + index + " " + GetXInputKeyName(controller, key);
                    }
                    else
                    {
                        controllerConfig[value] = "J" + index + " " + GetInputKeyName(controller, key);
                    }
                }
            }

            // Configure analog part of .ini
            var analog = json.GetOrCreateContainer("AllTrollersAnalog");
            var analogConfig = analog.GetOrCreateContainer(systemController[system]);

            if (system == "n64")
            {
                var xAxis = analogConfig.GetOrCreateContainer("P" + playerIndex + " X Axis");
                var yAxis = analogConfig.GetOrCreateContainer("P" + playerIndex + " Y Axis");

                xAxis["Value"] = isXInput ? "X" + index + " LeftThumbX" : "J" + index + " X";
                xAxis.SetObject("Mult", 1.0);
                xAxis.SetObject("Deadzone", 0.1);

                yAxis["Value"] = isXInput ? "X" + index + " LeftThumbY" : "J" + index + " Y";
                yAxis.SetObject("Mult", 1.0);
                yAxis.SetObject("Deadzone", 0.1);
            }

            if (system == "nds")
            {
                var xAxis = analogConfig.GetOrCreateContainer("Touch X");
                var yAxis = analogConfig.GetOrCreateContainer("Touch Y");

                xAxis["Value"] = "WMouse X";
                xAxis.SetObject("Mult", 1.0);
                xAxis.SetObject("Deadzone", 0.0);

                yAxis["Value"] = "WMouse Y";
                yAxis.SetObject("Mult", 1.0);
                yAxis.SetObject("Deadzone", 0.0);

                controllerConfig["Touch"] = "WMouse L";
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

        private static InputKeyMapping gbMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.a,               "B" },
            { InputKey.b,               "A" }
        };

        private static InputKeyMapping gbaMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.start,           "Start" },
            { InputKey.select,          "Select" },
            { InputKey.a,               "B" },
            { InputKey.b,               "A" },
            { InputKey.pageup,          "L" },
            { InputKey.pagedown,        "R" }
        };

        private static InputKeyMapping mdMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.y,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.b,                   "C" },
            { InputKey.start,               "Start" },
            { InputKey.pageup,              "X" },
            { InputKey.x,                   "Y" },
            { InputKey.pagedown,            "Z" },
            { InputKey.select,              "Mode" },
        };

        private static InputKeyMapping n64Mapping = new InputKeyMapping()
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

        private static InputKeyMapping ndsMapping = new InputKeyMapping()
        {
            { InputKey.b,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.x,                   "X" },
            { InputKey.y,                   "Y" },
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.pageup,              "L" },
            { InputKey.pagedown,            "R" },
            { InputKey.select,              "Select" },
            { InputKey.start,               "Start" },
        };

        private static InputKeyMapping nesMapping = new InputKeyMapping()
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

        private static InputKeyMapping ngpMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.b,               "B" },
            { InputKey.a,               "A" },
            { InputKey.start,           "Option"}
        };

        private static InputKeyMapping pceMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "I" },
            { InputKey.b,               "II" },
            { InputKey.y,               "III" },
            { InputKey.x,               "IV" },
            { InputKey.pageup,          "V" },
            { InputKey.pagedown,        "VI" },
            { InputKey.select,          "Select" },
            { InputKey.start,           "Run" },
            { InputKey.l2,              "Mode: Set 2-button" },
            { InputKey.r2,              "Mode: Set 6-button" },

        };

        private static InputKeyMapping saturnMapping = new InputKeyMapping()
        {
            { InputKey.up,                  "Up"},
            { InputKey.down,                "Down"},
            { InputKey.left,                "Left" },
            { InputKey.right,               "Right"},
            { InputKey.start,               "Start" },
            { InputKey.pageup,              "X" },
            { InputKey.x,                   "Y" },
            { InputKey.pagedown,            "Z" },
            { InputKey.y,                   "A" },
            { InputKey.a,                   "B" },
            { InputKey.b,                   "C" },
            { InputKey.l2,                  "L" },
            { InputKey.r2,                  "R" },
        };

        private static InputKeyMapping smsMapping = new InputKeyMapping()
        {
            { InputKey.up,              "Up"},
            { InputKey.down,            "Down"},
            { InputKey.left,            "Left" },
            { InputKey.right,           "Right"},
            { InputKey.a,               "B1" },
            { InputKey.b,               "B2" }
        };

        private static InputKeyMapping snesMapping = new InputKeyMapping()
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

        private static Dictionary<string, string> systemController = new Dictionary<string, string>()
        {
            { "c64", "Commodore 64 Controller" },
            { "gamegear", "GG Controller" },
            { "gb", "Gameboy Controller" },
            { "gba", "GBA Controller" },
            { "jaguar", "Jaguar Controller" },
            { "lynx", "Lynx Controller" },
            { "mastersystem", "SMS Controller" },
            { "megadrive", "GPGX Genesis Controller" },
            { "n64", "Nintendo 64 Controller" },
            { "nds", "NDS Controller" },
            { "nes", "NES Controller" },
            { "ngp", "NeoGeo Portable Controller" },
            { "pcengine", "PC Engine Controller" },
            { "pcfx", "PC-FX Controller" },
            { "psx", "PSX Front Panel" },
            { "saturn", "Saturn Controller" },
            { "snes", "SNES Controller" },
            { "wswan", "WonderSwan Controller" },
            { "zxspectrum", "ZXSpectrum Controller" }, 
        };

        private static Dictionary<string, InputKeyMapping> mappingToUse = new Dictionary<string, InputKeyMapping>()
        {
            { "gb", gbMapping },
            { "gba", gbaMapping },
            { "gbc", gbMapping },
            { "mastersystem", smsMapping },
            { "megadrive", mdMapping },
            { "n64", n64Mapping },
            { "nds", ndsMapping },
            { "nes", nesMapping },
            { "ngp", ngpMapping },
            { "pcengine", pceMapping },
            { "saturn", saturnMapping },
            { "snes", snesMapping },  
        };

        private void ResetControllerConfiguration(DynamicJson json, int totalNB, string system, string core)
        {
            bool monoplayer = systemMonoPlayer.Contains(system);
            InputKeyMapping mapping = mappingToUse[system];

            var trollers = json.GetOrCreateContainer("AllTrollers");
            var controllerConfig = trollers.GetOrCreateContainer(systemController[system]);

            if (monoplayer)
            {
                foreach (var x in mapping)
                {
                    string value = x.Value;
                    InputKey key = x.Key;
                    controllerConfig[value] = "";
                }
            }

            else
            {
                for (int i = 1; i < totalNB; i++)
                {
                    foreach (var x in mapping)
                    {
                        string value = x.Value;
                        InputKey key = x.Key;
                        controllerConfig["P" + i + " " + value] = "";
                    }
                }
            }
        }
    }
}