using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

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
            string wheeltype1 = "default";
            string wheeltype2 = "default";
            string forceWheelType = null;
            string forceWheelType2 = null;

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

            // Setup first wheel
            usableWheels.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));
            wheel1 = usableWheels[0];
            wheeltype1 = wheel1.Type.ToString();
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1, wheeltype identified : " + wheeltype1);
            wheelIndex1 = wheel1.DinputIndex;
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1 directinput index : " + wheelIndex1);

            // Enable Dinput (needed for ForceFeedback)
            pcsx2ini.WriteValue("InputSources", "DInput", "true");

            // Initialize USB sections
            string usbSection1 = "USB1";
            if (SystemConfig.isOptSet("pcsx2_wheel") && SystemConfig["pcsx2_wheel"] == "USB2")
                usbSection1 = "USB2";

            pcsx2ini.ClearSection("USB1");
            pcsx2ini.ClearSection("USB2");

            // Get mapping from yml file in retrobat\system\resources\inputmapping\wheels and retrieve mapping
            YmlFile ymlFile = null;
            YmlContainer wheel1Mapping = null;
            Dictionary<string, string> wheel1buttonMap = new Dictionary<string, string>();
            string pcsx2WheelMapping = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "pcsx2_wheels.yml");
            if (File.Exists(pcsx2WheelMapping))
            {
                ymlFile = YmlFile.Load(pcsx2WheelMapping);

                wheel1Mapping = ymlFile.Elements.Where(c => c.Name == wheeltype1).FirstOrDefault() as YmlContainer;

                if (wheel1Mapping == null)
                {
                    wheel1Mapping = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;
                    if (wheel1Mapping == null)
                    {
                        SimpleLogger.Instance.Info("[WHEELS] No mapping exists for the wheel and PCSX2 emulator in yml file.");
                        return;
                    }
                    else
                        SimpleLogger.Instance.Info("[WHEELS] Using default wheel mapping in yml file.");
                }

                SimpleLogger.Instance.Info("[WHEELS] Retrieving wheel mapping from yml file.");

                foreach (var mapEntry in wheel1Mapping.Elements)
                {

                    if (mapEntry is YmlElement button)
                    {
                        if (button.Value == null || button.Value == "nul")
                            continue;
                        wheel1buttonMap.Add(button.Name, button.Value);
                    }
                }
            }
            else
            {
                SimpleLogger.Instance.Info("[WHEELS] Mapping file for PCSX2 does not exist.");
                return;
            }

            // Override index
            if (SystemConfig.isOptSet("pcsx2_wheel1_index") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheel1_index"]))
                wheelIndex1 = SystemConfig["pcsx2_wheel1_index"].ToInteger();

            if (wheel1buttonMap.ContainsKey("driver") && wheel1buttonMap["driver"].Contains("sdl"))
            {
                wheelTech1 = "sdl";
                pcsx2ini.WriteValue("InputSources", "SDL", "true");
            }

            pcsx2ini.WriteValue(usbSection1, "Type", "Pad");

            if (wheel1buttonMap.ContainsKey("wheeltype") && wheel1buttonMap["wheeltype"] != null && wheel1buttonMap["wheeltype"] != "nul")
                pcsx2ini.WriteValue(usbSection1, "Pad_subtype", wheel1buttonMap["wheeltype"]);
            else
                pcsx2ini.WriteValue(usbSection1, "Pad_subtype", forceWheelType ?? "2");

            string DevicePrefix = "DInput-" + wheelIndex1 + "/";

            if (wheelTech1 == "dinput")
            {
                SimpleLogger.Instance.Info("[WHEEL] Wheel 1. Configuring with dinput ids");

                if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "None");
                else if (wheel1buttonMap.ContainsKey("FFDevice") && !string.IsNullOrEmpty(wheel1buttonMap["FFDevice"]))
                {
                    if (wheel1buttonMap["FFDevice"].Contains("DInput"))
                        pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheelIndex1);
                    else
                        pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheel1.SDLIndex);
                }
                else
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "DInput-" + wheel1.DinputIndex);
            }

            else if (wheelTech1 == "sdl")
            {
                SimpleLogger.Instance.Info("[WHEEL] Wheel 1. Configuring with SDL ids.");

                DevicePrefix = "SDL-" + wheel1.SDLIndex + "/";

                if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "None");
                else if (wheel1buttonMap.ContainsKey("FFDevice") && !string.IsNullOrEmpty(wheel1buttonMap["FFDevice"]))
                {
                    if (wheel1buttonMap["FFDevice"].Contains("DInput"))
                        pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheelIndex1);
                    else
                        pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheel1.SDLIndex);
                }
                else
                    pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", "SDL-" + wheel1.SDLIndex);  
            }

            pcsx2ini.WriteValue(usbSection1, "Pad_SteeringLeft", DevicePrefix + GetWheelButton(wheel1buttonMap, "SteerLeft", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_SteeringRight", DevicePrefix + GetWheelButton(wheel1buttonMap, "SteerRight", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Start", DevicePrefix + GetWheelButton(wheel1buttonMap, "Start", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Select", DevicePrefix + GetWheelButton(wheel1buttonMap, "Select", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_DPadUp", DevicePrefix + GetWheelButton(wheel1buttonMap, "Up", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_DPadDown", DevicePrefix + GetWheelButton(wheel1buttonMap, "Down", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_DPadLeft", DevicePrefix + GetWheelButton(wheel1buttonMap, "Left", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_DPadRight", DevicePrefix + GetWheelButton(wheel1buttonMap, "Right", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Throttle", DevicePrefix + GetWheelButton(wheel1buttonMap, "Throttle", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Brake", DevicePrefix + GetWheelButton(wheel1buttonMap, "Brake", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Cross", DevicePrefix + GetWheelButton(wheel1buttonMap, "South", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Circle", DevicePrefix + GetWheelButton(wheel1buttonMap, "East", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Square", DevicePrefix + GetWheelButton(wheel1buttonMap, "West", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_Triangle", DevicePrefix + GetWheelButton(wheel1buttonMap, "North", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_L1", DevicePrefix + GetWheelButton(wheel1buttonMap, "LeftShoulder", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_R1", DevicePrefix + GetWheelButton(wheel1buttonMap, "RightShoulder", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_L2", DevicePrefix + GetWheelButton(wheel1buttonMap, "LeftTrigger", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_R2", DevicePrefix + GetWheelButton(wheel1buttonMap, "RightTrigger", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_L3", DevicePrefix + GetWheelButton(wheel1buttonMap, "LeftStick", wheelTech1));
            pcsx2ini.WriteValue(usbSection1, "Pad_R3", DevicePrefix + GetWheelButton(wheel1buttonMap, "RightStick", wheelTech1));

            // Setup second wheel
            if (wheelNb > 1)
            {
                string usbSection2 = "USB2";
                if (usbSection1 == "USB2")
                    usbSection2 = "USB1";

                wheel2 = usableWheels[1];
                wheeltype2 = wheel2.Type.ToString();
                wheelIndex2 = wheel2.DinputIndex;
                SimpleLogger.Instance.Info("[WHEELS] Wheel 2, wheeltype identified : " + wheeltype2);

                YmlContainer wheel2Mapping = null;
                Dictionary<string, string> wheel2buttonMap = new Dictionary<string, string>();

                if (File.Exists(pcsx2WheelMapping))
                {
                    ymlFile = YmlFile.Load(pcsx2WheelMapping);

                    wheel2Mapping = ymlFile.Elements.Where(c => c.Name == wheeltype2).FirstOrDefault() as YmlContainer;

                    if (wheel2Mapping == null)
                    {
                        wheel2Mapping = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;
                        if (wheel2Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[WHEELS] No mapping exists for the wheel and PCSX2 emulator in yml file.");
                            return;
                        }
                        else
                            SimpleLogger.Instance.Info("[WHEELS] Using default wheel mapping in yml file.");
                    }

                    SimpleLogger.Instance.Info("[WHEELS] Retrieving wheel mapping from yml file.");

                    foreach (var mapEntry in wheel2Mapping.Elements)
                    {

                        if (mapEntry is YmlElement button)
                        {
                            if (button.Value == null || button.Value == "nul")
                                continue;
                            wheel2buttonMap.Add(button.Name, button.Value);
                        }
                    }
                }
                else
                {
                    SimpleLogger.Instance.Info("[WHEELS] Mapping file for PCSX2 does not exist.");
                    return;
                }

                // Override index
                if (SystemConfig.isOptSet("pcsx2_wheel2_index") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheel2_index"]))
                    wheelIndex2 = SystemConfig["pcsx2_wheel2_index"].ToInteger();

                if (wheel2buttonMap.ContainsKey("driver") && wheel2buttonMap["driver"].Contains("sdl"))
                {
                    wheelTech2 = "sdl";
                    pcsx2ini.WriteValue("InputSources", "SDL", "true");
                }

                string DevicePrefix2 = "DInput-" + wheelIndex2 + "/";

                pcsx2ini.WriteValue(usbSection2, "Type", "Pad");

                if (wheel2buttonMap.ContainsKey("wheeltype") && wheel2buttonMap["wheeltype"] != null && wheel2buttonMap["wheeltype"] != "nul")
                    pcsx2ini.WriteValue(usbSection2, "Pad_subtype", wheel2buttonMap["wheeltype"]);
                else
                    pcsx2ini.WriteValue(usbSection2, "Pad_subtype", forceWheelType2 ?? "2");

                if (wheelTech2 == "dinput")
                {
                    SimpleLogger.Instance.Info("[WHEEL] Wheel 2. Configuring with dinput ids");

                    if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "None");
                    else if (wheel1buttonMap.ContainsKey("FFDevice") && !string.IsNullOrEmpty(wheel1buttonMap["FFDevice"]))
                    {
                        if (wheel1buttonMap["FFDevice"].Contains("DInput"))
                            pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheelIndex1);
                        else
                            pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheel1.SDLIndex);
                    }
                    else
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "DInput-" + wheel2.DinputIndex);
                }

                else if (wheelTech2 == "sdl")
                {
                    SimpleLogger.Instance.Info("[WHEEL] Wheel 2. Configuring with SDL ids.");

                    DevicePrefix2 = "SDL-" + wheel2.SDLIndex + "/";

                    if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "None");
                    else if (wheel1buttonMap.ContainsKey("FFDevice") && !string.IsNullOrEmpty(wheel1buttonMap["FFDevice"]))
                    {
                        if (wheel1buttonMap["FFDevice"].Contains("DInput"))
                            pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheelIndex1);
                        else
                            pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", wheel1buttonMap["FFDevice"] + "-" + wheel1.SDLIndex);
                    }
                    else
                        pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", "SDL-" + wheel2.SDLIndex);
                }

                pcsx2ini.WriteValue(usbSection2, "Pad_SteeringLeft", DevicePrefix + GetWheelButton(wheel2buttonMap, "SteerLeft", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_SteeringRight", DevicePrefix + GetWheelButton(wheel2buttonMap, "SteerRight", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Start", DevicePrefix + GetWheelButton(wheel2buttonMap, "Start", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Select", DevicePrefix + GetWheelButton(wheel2buttonMap, "Select", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_DPadUp", DevicePrefix + GetWheelButton(wheel2buttonMap, "Up", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_DPadDown", DevicePrefix + GetWheelButton(wheel2buttonMap, "Down", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_DPadLeft", DevicePrefix + GetWheelButton(wheel2buttonMap, "Left", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_DPadRight", DevicePrefix + GetWheelButton(wheel2buttonMap, "Right", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Throttle", DevicePrefix + GetWheelButton(wheel2buttonMap, "Throttle", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Brake", DevicePrefix + GetWheelButton(wheel2buttonMap, "Brake", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Cross", DevicePrefix + GetWheelButton(wheel2buttonMap, "South", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Circle", DevicePrefix + GetWheelButton(wheel2buttonMap, "East", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Square", DevicePrefix + GetWheelButton(wheel2buttonMap, "West", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_Triangle", DevicePrefix + GetWheelButton(wheel2buttonMap, "North", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_L1", DevicePrefix + GetWheelButton(wheel2buttonMap, "LeftShoulder", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_R1", DevicePrefix + GetWheelButton(wheel2buttonMap, "RightShoulder", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_L2", DevicePrefix + GetWheelButton(wheel2buttonMap, "LeftTrigger", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_R2", DevicePrefix + GetWheelButton(wheel2buttonMap, "RightTrigger", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_L3", DevicePrefix + GetWheelButton(wheel2buttonMap, "LeftStick", wheelTech2));
                pcsx2ini.WriteValue(usbSection2, "Pad_R3", DevicePrefix + GetWheelButton(wheel2buttonMap, "RightStick", wheelTech2));
            }
        }

        private static string GetWheelButton(Dictionary<string,string> mapping, string buttonkey, string tech)
        {
            if (mapping.ContainsKey(buttonkey) && !string.IsNullOrEmpty(mapping[buttonkey]))
                return mapping[buttonkey];
            else
                return "";
        }
    }
}