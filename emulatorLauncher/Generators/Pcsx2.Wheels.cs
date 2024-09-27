using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Pcsx2Generator : Generator
    {
        private void SetupWheelQT(IniFile pcsx2ini)
        {
            if (!SystemConfig.getOptBoolean("use_wheel"))
                return;

            SimpleLogger.Instance.Info("[WHEELS] Configuring wheels.");

            List<Wheel> usableWheels = new List<Wheel>();
            string wheelTech1 = "dinput";
            string wheelTech2 = "dinput";
            int wheelNb = 0;
            int wheelIndex1 = -1;
            int wheelIndex2 = -1;
            Wheel wheel1 = null;
            Wheel wheel2 = null;
            WheelMappingInfo wheelmapping1 = null;
            WheelSDLMappingInfo wheelSDLmapping1 = null;
            WheelMappingInfo wheelmapping2 = null;
            WheelSDLMappingInfo wheelSDLmapping2 = null;
            string wheelGuid1 = "nul";
            string wheelGuid2 = "nul";
            string wheeltype1 = "default";
            string wheeltype2 = "default";
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            SdlToDirectInput sdlWheel1 = null;
            SdlToDirectInput sdlWheel2 = null;
            string forceWheelType = null;
            string forceWheelType2 = null;

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[WHEELS] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                gamecontrollerDB = null;
            }

            // Retrieve wheels
            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard))
            {
                var drivingWheel = Wheel.GetWheelType(controller.DevicePath.ToUpperInvariant());

                if (drivingWheel != WheelType.Default)
                    usableWheels.Add(new Wheel()
                    {
                        Name = controller.Name,
                        VendorID = controller.VendorID.ToString(),
                        ProductID = controller.ProductID.ToString(),
                        DevicePath = controller.DevicePath.ToLowerInvariant(),
                        DinputIndex = controller.DirectInput != null ? controller.DirectInput.DeviceIndex : controller.DeviceIndex,
                        SDLIndex = controller.SdlController != null ? controller.SdlController.Index : controller.DeviceIndex,
                        XInputIndex = controller.XInput != null ? controller.XInput.DeviceIndex : controller.DeviceIndex,
                        ControllerIndex = controller.DeviceIndex,
                        Type = drivingWheel
                    });
            }

            wheelNb = usableWheels.Count;
            SimpleLogger.Instance.Info("[WHEELS] Found " + wheelNb + " usable wheels.");
            if (wheelNb < 1)
                return;

            if (SystemConfig.isOptSet("pcsx2_wheeltype") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheeltype"]))
                forceWheelType = SystemConfig["pcsx2_wheeltype"];

            if (SystemConfig.isOptSet("pcsx2_wheeltype2") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheeltype2"]))
                forceWheelType2 = SystemConfig["pcsx2_wheeltype2"];

            // Enable Dinput (needed for ForceFeedback)
            pcsx2ini.WriteValue("InputSources", "DInput", "true");

            // Initialize USB sections
            string usbSection1 = "USB1";
            if (SystemConfig.isOptSet("pcsx2_wheel") && SystemConfig["pcsx2_wheel"] == "USB2")
                usbSection1 = "USB2";

            pcsx2ini.ClearSection("USB1");
            pcsx2ini.ClearSection("USB2");

            // Setup first wheel
            usableWheels.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));

            wheel1 = usableWheels[0];
            wheeltype1 = wheel1.Type.ToString();
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1, wheeltype identified : " + wheeltype1);
            wheelIndex1 = wheel1.DinputIndex;
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1 directinput index : " + wheelIndex1);

            if (SystemConfig.isOptSet("pcsx2_wheel1_index") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheel1_index"]))
                wheelIndex1 = SystemConfig["pcsx2_wheel1_index"].ToInteger();

            try 
            {
                if (!WheelMappingInfo.InstanceW.TryGetValue(wheeltype1, out wheelmapping1))
                    WheelMappingInfo.InstanceW.TryGetValue("default", out wheelmapping1);
            }
            catch { SimpleLogger.Instance.Info("[WHEELS] No DInput mapping in yml file four wheel 1."); }

            try
            {
                if (!WheelSDLMappingInfo.InstanceWSDL.TryGetValue(wheeltype1, out wheelSDLmapping1))
                    SimpleLogger.Instance.Info("[WHEELS] No SDL mapping found to configure first wheel.");
            }
            catch { SimpleLogger.Instance.Info("[WHEELS] No SDL mapping in yml file four wheel 1."); }

            if (wheelSDLmapping1 != null)
            {
                wheelTech1 = "sdl";
                pcsx2ini.WriteValue("InputSources", "SDL", "true");
            }

            if (wheelTech1 == "sdl")
                wheelGuid1 = wheelSDLmapping1.WheelGuid;
            else
                wheelGuid1 = wheelmapping1.WheelGuid;

            if (gamecontrollerDB != null && wheelTech1 == "dinput")
            {
                SimpleLogger.Instance.Info("[WHEEL] Wheel 1. Fetching gamecontrollerdb.txt file with guid : " + wheelGuid1);
                sdlWheel1 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, wheelGuid1);

                if (sdlWheel1 == null)
                    SimpleLogger.Instance.Info("[WHEEL] Wheel 1. No Dinput mapping found for : " + wheelGuid1 + " " + wheel1.Name);

                pcsx2ini.WriteValue(usbSection1, "Type", "Pad");
                pcsx2ini.WriteValue(usbSection1, "Pad_subtype", forceWheelType ?? wheelmapping1.Pcsx2_Type);

                if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "None");
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringLeft", GetWheelMapping(sdlWheel1, wheelmapping1.Steer, wheelIndex1, -1));
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringRight", GetWheelMapping(sdlWheel1, wheelmapping1.Steer, wheelIndex1, 1));
                }
                else
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "DInput-" + wheel1.DinputIndex);
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringLeft", GetWheelMapping(sdlWheel1, wheelmapping1.Steer, wheelIndex1, -1));
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringRight", GetWheelMapping(sdlWheel1, wheelmapping1.Steer, wheelIndex1, 1));
                }

                pcsx2ini.WriteValue(usbSection1, "Pad_Start", GetDInputKeyName(wheelIndex1, sdlWheel1, "start"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Select", GetDInputKeyName(wheelIndex1, sdlWheel1, "back"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadUp", GetDInputKeyName(wheelIndex1, sdlWheel1, "dpup"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadDown", GetDInputKeyName(wheelIndex1, sdlWheel1, "dpdown"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadLeft", GetDInputKeyName(wheelIndex1, sdlWheel1, "dpleft"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadRight", GetDInputKeyName(wheelIndex1, sdlWheel1, "dpright"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Throttle", GetWheelMapping(sdlWheel1, wheelmapping1.Throttle, wheelIndex1, -1));
                pcsx2ini.WriteValue(usbSection1, "Pad_Brake", GetWheelMapping(sdlWheel1, wheelmapping1.Brake, wheelIndex1, -1));
                pcsx2ini.WriteValue(usbSection1, "Pad_Cross", GetDInputKeyName(wheelIndex1, sdlWheel1, "a"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Circle", GetDInputKeyName(wheelIndex1, sdlWheel1, "b"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Square", GetDInputKeyName(wheelIndex1, sdlWheel1, "x"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Triangle", GetDInputKeyName(wheelIndex1, sdlWheel1, "y"));
                pcsx2ini.WriteValue(usbSection1, "Pad_L1", GetDInputKeyName(wheelIndex1, sdlWheel1, "leftshoulder"));
                pcsx2ini.WriteValue(usbSection1, "Pad_R1", GetDInputKeyName(wheelIndex1, sdlWheel1, "rightshoulder"));
                pcsx2ini.WriteValue(usbSection1, "Pad_L2", GetDInputKeyName(wheelIndex1, sdlWheel1, "lefttrigger"));
                pcsx2ini.WriteValue(usbSection1, "Pad_R2", GetDInputKeyName(wheelIndex1, sdlWheel1, "rightrigger"));
            }

            else if (wheelTech1 == "sdl")
            {
                SimpleLogger.Instance.Info("[WHEEL] Wheel 1. Configuring SDL mapping for wheel " + wheelGuid1);

                SimpleLogger.Instance.Info("[WHEEL] Wheel 1. Fetching gamecontrollerdb.txt file with guid : " + wheelGuid1);
                sdlWheel1 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, wheelGuid1);

                if (sdlWheel1 == null)
                    SimpleLogger.Instance.Info("[WHEEL] Wheel 1. No Dinput mapping found for : " + wheelGuid1 + " " + wheel1.Name);

                string sdlDevice = "SDL-" + wheel1.SDLIndex + "/";

                pcsx2ini.WriteValue(usbSection1, "Type", "Pad");
                pcsx2ini.WriteValue(usbSection1, "Pad_subtype", forceWheelType ?? wheelSDLmapping1.Pcsx2_Type);

                if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "None");
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringLeft", sdlDevice + "-" + wheelSDLmapping1.Steer);
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringRight", sdlDevice + "+" + wheelSDLmapping1.Steer);
                }
                else
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "SDL-" + wheel1.SDLIndex);
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringLeft", sdlDevice + "-" + wheelSDLmapping1.Steer);
                    pcsx2ini.WriteValue(usbSection1, "Pad_SteeringRight", sdlDevice + "+" + wheelSDLmapping1.Steer);
                }

                pcsx2ini.WriteValue(usbSection1, "Pad_Start", sdlDevice + wheelSDLmapping1.Start);
                pcsx2ini.WriteValue(usbSection1, "Pad_Select", sdlDevice + wheelSDLmapping1.Select);
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadUp", sdlDevice + wheelSDLmapping1.Dpad + "North");
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadDown", sdlDevice + wheelSDLmapping1.Dpad + "South");
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadLeft", sdlDevice + wheelSDLmapping1.Dpad + "West");
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadRight", sdlDevice + wheelSDLmapping1.Dpad + "East");
                pcsx2ini.WriteValue(usbSection1, "Pad_Throttle", sdlDevice + "+" +  wheelSDLmapping1.Throttle);
                pcsx2ini.WriteValue(usbSection1, "Pad_Brake", sdlDevice + "+" + wheelSDLmapping1.Brake);
                pcsx2ini.WriteValue(usbSection1, "Pad_Cross", sdlDevice + wheelSDLmapping1.South);
                pcsx2ini.WriteValue(usbSection1, "Pad_Circle", sdlDevice + wheelSDLmapping1.East);
                pcsx2ini.WriteValue(usbSection1, "Pad_Square", sdlDevice + wheelSDLmapping1.West);
                pcsx2ini.WriteValue(usbSection1, "Pad_Triangle", sdlDevice + wheelSDLmapping1.North);
                pcsx2ini.WriteValue(usbSection1, "Pad_L1", sdlDevice + wheelSDLmapping1.L1);
                pcsx2ini.WriteValue(usbSection1, "Pad_R1", sdlDevice + wheelSDLmapping1.R1);
                pcsx2ini.WriteValue(usbSection1, "Pad_L2", sdlDevice + wheelSDLmapping1.L2);
                pcsx2ini.WriteValue(usbSection1, "Pad_R2", sdlDevice + wheelSDLmapping1.R2);
            }
            
            // Setup second wheel
            if (wheelNb > 1)
            {
                string usbSection2 = "USB2";
                if (usbSection1 == "USB2")
                    usbSection2 = "USB1";

                wheel2 = usableWheels[1];
                wheeltype2 = wheel2.Type.ToString();
                SimpleLogger.Instance.Info("[WHEELS] Wheel 2, wheeltype identified : " + wheeltype2);

                try
                {
                    if (!WheelMappingInfo.InstanceW.TryGetValue(wheeltype2, out wheelmapping2))
                        WheelMappingInfo.InstanceW.TryGetValue("default", out wheelmapping2);
                }
                catch { SimpleLogger.Instance.Info("[WHEELS] No DInput mapping in yml file four wheel 2."); }

                try
                {
                    if (!WheelSDLMappingInfo.InstanceWSDL.TryGetValue(wheeltype2, out wheelSDLmapping2))
                        SimpleLogger.Instance.Info("[WHEELS] No SDL mapping found to configure second wheel.");
                }
                catch { SimpleLogger.Instance.Info("[WHEELS] No SDL mapping in yml file four wheel 2."); }

                if (wheelSDLmapping2 == null)
                    pcsx2ini.WriteValue("InputSources", "DInput", "true");
                else
                    wheelTech2 = "sdl";

                if (wheelTech2 == "sdl")
                {
                    pcsx2ini.WriteValue("InputSources", "SDL", "true");
                    wheelGuid2 = wheelSDLmapping2.WheelGuid;
                }
                else
                    wheelGuid2 = wheelmapping2.WheelGuid;

                if (wheelTech2 == "dinput" && gamecontrollerDB != null)
                {
                    wheelIndex2 = wheel2.DinputIndex;
                    SimpleLogger.Instance.Info("[WHEELS] Wheel 2 directinput index : " + wheelIndex2);

                    if (SystemConfig.isOptSet("pcsx2_wheel2_index") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheel2_index"]))
                        wheelIndex2 = SystemConfig["pcsx2_wheel2_index"].ToInteger();

                    SimpleLogger.Instance.Info("[WHEEL] Wheel 2. Fetching gamecontrollerdb.txt file with guid : " + wheelGuid2);
                    sdlWheel2 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, wheelGuid2);

                    if (sdlWheel2 == null)
                        SimpleLogger.Instance.Info("[WHEEL] Wheel 2. No Dinput mapping found for : " + wheelGuid2 + " " + wheel2.Name);

                    pcsx2ini.WriteValue(usbSection2, "Type", "Pad");
                    pcsx2ini.WriteValue(usbSection2, "Pad_subtype", forceWheelType2 ?? wheelmapping2.Pcsx2_Type);

                    if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "None");
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringLeft", GetWheelMapping(sdlWheel2, wheelmapping2.Steer, wheelIndex2, -1));
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringRight", GetWheelMapping(sdlWheel2, wheelmapping2.Steer, wheelIndex2, 1));
                    }
                    else
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "DInput-" + wheelIndex2);
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringLeft", GetWheelMapping(sdlWheel2, wheelmapping2.Steer, wheelIndex2, -1));
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringRight", GetWheelMapping(sdlWheel2, wheelmapping2.Steer, wheelIndex2, 1));
                    }

                    pcsx2ini.WriteValue(usbSection2, "Pad_Start", GetDInputKeyName(wheelIndex2, sdlWheel2, "start"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Select", GetDInputKeyName(wheelIndex2, sdlWheel2, "back"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadUp", GetWheelMapping(sdlWheel2, wheelmapping2.DpadUp, wheelIndex2));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadDown", GetWheelMapping(sdlWheel2, wheelmapping2.DpadDown, wheelIndex2));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadLeft", GetWheelMapping(sdlWheel2, wheelmapping2.DpadDown, wheelIndex2));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadRight", GetWheelMapping(sdlWheel2, wheelmapping2.DpadRight, wheelIndex2));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Throttle", GetWheelMapping(sdlWheel2, wheelmapping2.Throttle, wheelIndex2, -1));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Brake", GetWheelMapping(sdlWheel2, wheelmapping2.Brake, wheelIndex2, -1));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Cross", GetDInputKeyName(wheelIndex2, sdlWheel2, "a"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Circle", GetDInputKeyName(wheelIndex2, sdlWheel2, "b"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Square", GetDInputKeyName(wheelIndex2, sdlWheel2, "x"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Triangle", GetDInputKeyName(wheelIndex2, sdlWheel2, "y"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_L1", GetDInputKeyName(wheelIndex2, sdlWheel2, "leftshoulder"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_R1", GetDInputKeyName(wheelIndex2, sdlWheel2, "rightshoulder"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_L2", GetDInputKeyName(wheelIndex2, sdlWheel2, "lefttrigger"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_R2", GetDInputKeyName(wheelIndex2, sdlWheel2, "righttrigger"));
                }

                else if (wheelTech2 == "sdl")
                {
                    SimpleLogger.Instance.Info("[WHEEL] Wheel 2. Configuring SDL mapping for wheel " + wheelGuid2);

                    SimpleLogger.Instance.Info("[WHEEL] Wheel 2. Fetching gamecontrollerdb.txt file with guid : " + wheelGuid2);
                    sdlWheel2 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, wheelGuid2);

                    if (sdlWheel2 == null)
                        SimpleLogger.Instance.Info("[WHEEL] Wheel 2. No Dinput mapping found for : " + wheelGuid2 + " " + wheel2.Name);

                    string sdlDevice2 = "SDL-" + wheel2.SDLIndex + "/";

                    pcsx2ini.WriteValue(usbSection2, "Type", "Pad");
                    pcsx2ini.WriteValue(usbSection2, "Pad_subtype", forceWheelType2 ?? wheelSDLmapping2.Pcsx2_Type);
                    
                    if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "Null");
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringLeft", sdlDevice2 + "-" + wheelSDLmapping2.Steer);
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringRight", sdlDevice2 + "+" + wheelSDLmapping2.Steer);
                    }
                    else
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "SDL-" + wheel2.SDLIndex);
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringLeft", sdlDevice2 + "-" + wheelSDLmapping2.Steer);
                        pcsx2ini.WriteValue(usbSection2, "Pad_SteeringRight", sdlDevice2 + "+" + wheelSDLmapping2.Steer);
                    }

                    pcsx2ini.WriteValue(usbSection2, "Pad_Start", sdlDevice2 + wheelSDLmapping2.Start);
                    pcsx2ini.WriteValue(usbSection2, "Pad_Select", sdlDevice2 + wheelSDLmapping2.Select);
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadUp", sdlDevice2 + wheelSDLmapping2.Dpad + "North");
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadDown", sdlDevice2 + wheelSDLmapping2.Dpad + "South");
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadLeft", sdlDevice2 + wheelSDLmapping2.Dpad + "West");
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadRight", sdlDevice2 + wheelSDLmapping2.Dpad + "East");
                    pcsx2ini.WriteValue(usbSection2, "Pad_Throttle", sdlDevice2 + "+" + wheelSDLmapping2.Throttle);
                    pcsx2ini.WriteValue(usbSection2, "Pad_Brake", sdlDevice2 + "+" + wheelSDLmapping2.Brake);
                    pcsx2ini.WriteValue(usbSection2, "Pad_Cross", sdlDevice2 + wheelSDLmapping2.South);
                    pcsx2ini.WriteValue(usbSection2, "Pad_Circle", sdlDevice2 + wheelSDLmapping2.East);
                    pcsx2ini.WriteValue(usbSection2, "Pad_Square", sdlDevice2 + wheelSDLmapping2.West);
                    pcsx2ini.WriteValue(usbSection2, "Pad_Triangle", sdlDevice2 + wheelSDLmapping2.North);
                    pcsx2ini.WriteValue(usbSection2, "Pad_L1", sdlDevice2 + wheelSDLmapping2.L1);
                    pcsx2ini.WriteValue(usbSection2, "Pad_R1", sdlDevice2 + wheelSDLmapping2.R1);
                    pcsx2ini.WriteValue(usbSection2, "Pad_L2", sdlDevice2 + wheelSDLmapping2.L2);
                    pcsx2ini.WriteValue(usbSection2, "Pad_R2", sdlDevice2 + wheelSDLmapping2.R2);
                }
            }
        }

        private static string GetDInputKeyName(int index, SdlToDirectInput c, string buttonkey, int plus = 0)
        {
            if (c == null)
                return "None";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "None";
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "None";
            }

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("b"))
            {
                string buttonID = button.Substring(1);
                return "DInput-" + index + "/Button" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "DInput-" + index + "/DPadUp";
                    case 2:
                        return "DInput-" + index + "/DPadRight";
                    case 4:
                        return "DInput-" + index + "/DPadDown";
                    case 8:
                        return "DInput-" + index + "/DPadLeft";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                switch (plus)
                {
                    case -1: 
                        return "DInput-" + index + "/-Axis" + axisID;
                    case 1:
                        return "DInput-" + index + "/+Axis" + axisID;
                    default:
                        return "DInput-" + index + "/Axis" + axisID;
                }
            }

            return "None";
        }

        private static string GetWheelMapping(SdlToDirectInput wheel, string button, int index, int direction = 0)
        {
            if (wheel == null)
                return "None";

            if (button.StartsWith("button_"))
            {
                int buttonID = (button.Substring(7).ToInteger());
                return "DInput-" + index + "/Button" + buttonID;
            }
            else
            {
                switch (button)
                {
                    case "throttle":
                    case "brake":
                    case "lefttrigger":
                    case "righttrigger":
                        return GetDInputKeyName(index, wheel, button, -1);
                    case "leftx":
                        if (direction == -1)
                            return GetDInputKeyName(index, wheel, button, -1);
                        else if (direction == 1)
                            return GetDInputKeyName(index, wheel, button, 1);
                        else
                            return GetDInputKeyName(index, wheel, button, 0);
                    case "rightshoulder":
                    case "leftshoulder":
                        return GetDInputKeyName(index, wheel, button);
                }
            }
            SimpleLogger.Instance.Info("[INFO] No mapping found for " + button + " in wheel database.");

            return "None";
        }
    }
}