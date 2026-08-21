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

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                UpdateSdlControllersWithHints();

            SimpleLogger.Instance.Info("[WHEELS] Configuring wheels.");

            string wheelTech1 = "dinput";
            string wheelTech2 = "dinput";
            int wheelIndex1 = -1;
            int wheelIndex2 = -1;
            int sdlIndex1 = -1;
            int sdlIndex2 = -1;
            Wheel wheel1 = null;
            Wheel wheel2 = null;
            string wheeltype1 = "default";
            string wheeltype2 = "default";
            string forceWheelType = null;
            string forceWheelType2 = null;

            // Retrieve wheels
            var usableWheels = Wheel.GetConnectedWheels(this.Controllers);

            if (usableWheels.Count < 1)
                return;

            string pcsx2WheelMapping = _isArcade ? Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "pcsx2x6_wheels.yml") : Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "pcsx2_wheels.yml");
            if (!File.Exists(pcsx2WheelMapping))
            {
                SimpleLogger.Instance.Info("[WHEELS] Mapping file " + pcsx2WheelMapping + " does not exist.");
                return;
            }

            if (SystemConfig.isOptSet("pcsx2_wheeltype") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheeltype"]))
                forceWheelType = SystemConfig["pcsx2_wheeltype"];

            if (SystemConfig.isOptSet("pcsx2_wheeltype2") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheeltype2"]))
                forceWheelType2 = SystemConfig["pcsx2_wheeltype2"];

            // Setup first wheel
            wheel1 = usableWheels[0];
            wheeltype1 = wheel1.Type.ToString();
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1, wheeltype identified : " + wheeltype1);
            wheelIndex1 = wheel1.DinputIndex;
            sdlIndex1 = GetWheelSdlIndex(wheel1);
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1 directinput index : " + wheelIndex1 + ", SDL index : " + sdlIndex1);

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

            // Override index
            if (SystemConfig.isOptSet("pcsx2_wheel1_index") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheel1_index"]))
            {
                wheelIndex1 = SystemConfig["pcsx2_wheel1_index"].ToInteger();
                sdlIndex1 = wheelIndex1;
                SimpleLogger.Instance.Info("[WHEELS] Wheel 1 index forced by user to : " + wheelIndex1);
            }

            wheelTech1 = GetWheelTech(wheel1buttonMap);

            if (wheelTech1 == "sdl")
            {
                pcsx2ini.WriteValue("InputSources", "SDL", "true");
                pcsx2ini.WriteValue("InputSources", "SDLRawInput", "true");
            }

            pcsx2ini.WriteValue(usbSection1, "Type", "Pad");
            string padSubtype1;

            if (wheel1buttonMap.ContainsKey("wheeltype") && wheel1buttonMap["wheeltype"] != null && wheel1buttonMap["wheeltype"] != "nul")
                padSubtype1 = wheel1buttonMap["wheeltype"];
            else
                padSubtype1 = forceWheelType ?? "2";

            pcsx2ini.WriteValue(usbSection1, "Pad_subtype", padSubtype1);

            string DevicePrefix = "DInput-" + wheelIndex1 + "/";

            SimpleLogger.Instance.Info("[WHEEL] Wheel 1. Configuring with " + wheelTech1 + " ids.");

            if (wheelTech1 == "sdl")
                DevicePrefix = "SDL-" + sdlIndex1 + "/";

            if (_isArcade)
            {
                pcsx2ini.WriteValue("JVS", "TestMode", "false");

                foreach (var entry in wheel1buttonMap)
                {
                    if (entry.Key == "wheeltype" || entry.Key == "driver" || entry.Key == "name" || entry.Key == "FFDevice" || entry.Key == "Gear3" || entry.Key == "Gear4")
                        continue;

                    if (SystemConfig.getOptBoolean("gearupdown_to_gear34") && wheel1buttonMap.ContainsKey("Gear3") && wheel1buttonMap.ContainsKey("Gear4"))
                    {
                        if (entry.Key == "Racing_ShiftDown_P1" || entry.Key == "BG3_ShiftDown_P1")
                        {
                            pcsx2ini.WriteValue("JVS", entry.Key, DevicePrefix + GetWheelButton(wheel1buttonMap, "Gear3"));
                            continue;
                        }
                        if (entry.Key == "Racing_ShiftUp_P1" || entry.Key == "BG3_ShiftUp_P1")
                        {
                            pcsx2ini.WriteValue("JVS", entry.Key, DevicePrefix + GetWheelButton(wheel1buttonMap, "Gear4"));
                            continue;
                        }
                    }

                    pcsx2ini.WriteValue("JVS", entry.Key, DevicePrefix + entry.Value);
                }

                return;
            }

            BindIniFeatureSlider(pcsx2ini, usbSection1, "Pad_SteeringSmoothing", "pcsx2_steering_smoothing", "0");
            BindIniFeatureSlider(pcsx2ini, usbSection1, "Pad_SteeringDeadzone", "pcsx2_steering_deadzone", "0");
            BindIniFeature(pcsx2ini, usbSection1, "Pad_SteeringCurveExponent", "pcsx2_steering_damping", "Off");
            BindBoolIniFeature(pcsx2ini, usbSection1, "Pad_FfbDropoutWorkaround", "pcsx2_ffb_dropout", "true", "false");

            pcsx2ini.WriteValue(usbSection1, "Pad_FFDevice", GetWheelFFDevice(wheel1buttonMap, wheelTech1, wheelIndex1, sdlIndex1));

            pcsx2ini.WriteValue(usbSection1, "Pad_SteeringLeft", DevicePrefix + GetWheelButton(wheel1buttonMap, "SteerLeft"));
            pcsx2ini.WriteValue(usbSection1, "Pad_SteeringRight", DevicePrefix + GetWheelButton(wheel1buttonMap, "SteerRight"));
            pcsx2ini.WriteValue(usbSection1, "Pad_Throttle", DevicePrefix + GetWheelButton(wheel1buttonMap, "Throttle"));
            pcsx2ini.WriteValue(usbSection1, "Pad_Brake", DevicePrefix + GetWheelButton(wheel1buttonMap, "Brake"));

            if (padSubtype1 == "3")
            {
                // GT Force
                pcsx2ini.WriteValue(usbSection1, "Pad_MenuUp", DevicePrefix + GetWheelButton(wheel1buttonMap, "Up"));
                pcsx2ini.WriteValue(usbSection1, "Pad_MenuDown", DevicePrefix + GetWheelButton(wheel1buttonMap, "Down"));
                pcsx2ini.WriteValue(usbSection1, "Pad_X", DevicePrefix + GetWheelButton(wheel1buttonMap, "West"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Y", DevicePrefix + GetWheelButton(wheel1buttonMap, "North"));
                pcsx2ini.WriteValue(usbSection1, "Pad_A", DevicePrefix + GetWheelButton(wheel1buttonMap, "South"));
                pcsx2ini.WriteValue(usbSection1, "Pad_B", DevicePrefix + GetWheelButton(wheel1buttonMap, "East"));
            }
            else
            {
                pcsx2ini.WriteValue(usbSection1, "Pad_Start", DevicePrefix + GetWheelButton(wheel1buttonMap, "Start"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Select", DevicePrefix + GetWheelButton(wheel1buttonMap, "Select"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadUp", DevicePrefix + GetWheelButton(wheel1buttonMap, "Up"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadDown", DevicePrefix + GetWheelButton(wheel1buttonMap, "Down"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadLeft", DevicePrefix + GetWheelButton(wheel1buttonMap, "Left"));
                pcsx2ini.WriteValue(usbSection1, "Pad_DPadRight", DevicePrefix + GetWheelButton(wheel1buttonMap, "Right"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Cross", DevicePrefix + GetWheelButton(wheel1buttonMap, "South"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Circle", DevicePrefix + GetWheelButton(wheel1buttonMap, "East"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Square", DevicePrefix + GetWheelButton(wheel1buttonMap, "West"));
                pcsx2ini.WriteValue(usbSection1, "Pad_Triangle", DevicePrefix + GetWheelButton(wheel1buttonMap, "North"));

                if (SystemConfig.getOptBoolean("gearupdown_to_gear34") && wheel1buttonMap.ContainsKey("Gear3") && wheel1buttonMap.ContainsKey("Gear4"))
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_L1", DevicePrefix + GetWheelButton(wheel1buttonMap, "Gear3"));
                    pcsx2ini.WriteValue(usbSection1, "Pad_R1", DevicePrefix + GetWheelButton(wheel1buttonMap, "Gear4"));
                }
                else
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_L1", DevicePrefix + GetWheelButton(wheel1buttonMap, "LeftShoulder"));
                    pcsx2ini.WriteValue(usbSection1, "Pad_R1", DevicePrefix + GetWheelButton(wheel1buttonMap, "RightShoulder"));
                }

                pcsx2ini.WriteValue(usbSection1, "Pad_L2", DevicePrefix + GetWheelButton(wheel1buttonMap, "LeftTrigger"));
                pcsx2ini.WriteValue(usbSection1, "Pad_R2", DevicePrefix + GetWheelButton(wheel1buttonMap, "RightTrigger"));

                if (padSubtype1 != "0")   // L3/R3 not in subtype 0 (Driving Force)
                {
                    pcsx2ini.WriteValue(usbSection1, "Pad_L3", DevicePrefix + GetWheelButton(wheel1buttonMap, "LeftStick"));
                    pcsx2ini.WriteValue(usbSection1, "Pad_R3", DevicePrefix + GetWheelButton(wheel1buttonMap, "RightStick"));
                }
            }

            // Setup second wheel
            if (usableWheels.Count > 1)
            {
                string usbSection2 = "USB2";
                if (usbSection1 == "USB2")
                    usbSection2 = "USB1";

                wheel2 = usableWheels[1];
                wheeltype2 = wheel2.Type.ToString();
                wheelIndex2 = wheel2.DinputIndex;
                sdlIndex2 = GetWheelSdlIndex(wheel2);
                SimpleLogger.Instance.Info("[WHEELS] Wheel 2, wheeltype identified : " + wheeltype2);
                SimpleLogger.Instance.Info("[WHEELS] Wheel 2 directinput index : " + wheelIndex2 + ", SDL index : " + sdlIndex2);

                YmlContainer wheel2Mapping = null;
                Dictionary<string, string> wheel2buttonMap = new Dictionary<string, string>();

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

                // Override index
                if (SystemConfig.isOptSet("pcsx2_wheel2_index") && !string.IsNullOrEmpty(SystemConfig["pcsx2_wheel2_index"]))
                {
                    wheelIndex2 = SystemConfig["pcsx2_wheel2_index"].ToInteger();
                    sdlIndex2 = wheelIndex2;
                    SimpleLogger.Instance.Info("[WHEELS] Wheel 2 index forced by user to : " + wheelIndex2);
                }

                wheelTech2 = GetWheelTech(wheel2buttonMap);

                if (wheelTech2 == "sdl")
                {
                    pcsx2ini.WriteValue("InputSources", "SDL", "true");
                    pcsx2ini.WriteValue("InputSources", "SDLRawInput", "true");
                }

                string DevicePrefix2 = "DInput-" + wheelIndex2 + "/";

                pcsx2ini.WriteValue(usbSection2, "Type", "Pad");
                string padSubtype2;

                if (wheel2buttonMap.ContainsKey("wheeltype") && wheel2buttonMap["wheeltype"] != null && wheel2buttonMap["wheeltype"] != "nul")
                    padSubtype2 = wheel2buttonMap["wheeltype"];
                else
                    padSubtype2 = forceWheelType ?? "2";

                pcsx2ini.WriteValue(usbSection2, "Pad_subtype", padSubtype2);
                SimpleLogger.Instance.Info("[WHEEL] Wheel 2. Configuring with " + wheelTech2 + " ids.");

                if (wheelTech2 == "sdl")
                    DevicePrefix2 = "SDL-" + sdlIndex2 + "/";

                BindIniFeatureSlider(pcsx2ini, usbSection2, "Pad_SteeringSmoothing", "pcsx2_steering_smoothing", "0");
                BindIniFeatureSlider(pcsx2ini, usbSection2, "Pad_SteeringDeadzone", "pcsx2_steering_deadzone", "0");
                BindIniFeature(pcsx2ini, usbSection2, "Pad_SteeringCurveExponent", "pcsx2_steering_damping", "Off");
                BindBoolIniFeature(pcsx2ini, usbSection2, "Pad_FfbDropoutWorkaround", "pcsx2_ffb_dropout", "true", "false");

                pcsx2ini.WriteValue(usbSection2, "Pad_FFDevice", GetWheelFFDevice(wheel2buttonMap, wheelTech2, wheelIndex2, sdlIndex2));

                pcsx2ini.WriteValue(usbSection2, "Pad_SteeringLeft", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "SteerLeft"));
                pcsx2ini.WriteValue(usbSection2, "Pad_SteeringRight", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "SteerRight"));

                if (padSubtype1 == "3")
                {
                    // GT Force : different binding
                    pcsx2ini.WriteValue(usbSection2, "Pad_MenuUp", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Up"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_MenuDown", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Down"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_X", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "West"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Y", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "North"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_A", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "South"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_B", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "East"));
                }

                else
                {
                    pcsx2ini.WriteValue(usbSection2, "Pad_Start", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Start"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Select", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Select"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadUp", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Up"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadDown", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Down"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadLeft", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Left"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_DPadRight", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Right"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Cross", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "South"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Circle", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "East"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Square", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "West"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_Triangle", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "North"));

                    if (SystemConfig.getOptBoolean("gearupdown_to_gear34") && wheel2buttonMap.ContainsKey("Gear3") && wheel2buttonMap.ContainsKey("Gear4"))
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_L1", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Gear3"));
                        pcsx2ini.WriteValue(usbSection2, "Pad_R1", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "Gear4"));
                    }
                    else
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_L1", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "LeftShoulder"));
                        pcsx2ini.WriteValue(usbSection2, "Pad_R1", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "RightShoulder"));
                    }

                    pcsx2ini.WriteValue(usbSection2, "Pad_L2", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "LeftTrigger"));
                    pcsx2ini.WriteValue(usbSection2, "Pad_R2", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "RightTrigger"));

                    if (padSubtype1 != "0")   // L3/R3 not for subtype 0 (Driving Force)
                    {
                        pcsx2ini.WriteValue(usbSection2, "Pad_L3", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "LeftStick"));
                        pcsx2ini.WriteValue(usbSection2, "Pad_R3", DevicePrefix2 + GetWheelButton(wheel2buttonMap, "RightStick"));
                    }
                }
            }
        }

        private static string GetWheelButton(Dictionary<string,string> mapping, string buttonkey)
        {
            if (mapping.ContainsKey(buttonkey) && !string.IsNullOrEmpty(mapping[buttonkey]))
                return mapping[buttonkey];
            else
                return "";
        }

        private int GetWheelSdlIndex(Wheel wheel)
        {
            var ctrl = this.Controllers.FirstOrDefault(c => !c.IsKeyboard
                && c.DevicePath != null
                && c.DevicePath.ToLowerInvariant() == wheel.DevicePath);

            int sdlIndex = -1;

            if (ctrl != null && ctrl.Sdl3Controller != null)
                sdlIndex = ctrl.Sdl3Controller.PlayerSlot;

            if (sdlIndex == -1)
                sdlIndex = (ctrl != null && ctrl.SdlController != null) ? ctrl.SdlController.Index : wheel.SDLIndex;

            EnsureDolphinBarDetection();

            if (_dolphinbar)
                sdlIndex += 4;

            return sdlIndex;
        }

        private string GetWheelTech(Dictionary<string, string> buttonMap)
        {
            // The driver declared in the yml file is authoritative
            if (buttonMap.ContainsKey("driver") && !string.IsNullOrEmpty(buttonMap["driver"]))
                return buttonMap["driver"].Contains("sdl") ? "sdl" : "dinput";

            // No driver declared in the yml file : fallback to the emulator feature
            if (SystemConfig.isOptSet("pcsx2_input_driver_force") && !string.IsNullOrEmpty(SystemConfig["pcsx2_input_driver_force"]))
                return SystemConfig["pcsx2_input_driver_force"] == "dinput" ? "dinput" : "sdl";

            return "dinput";
        }

        private string GetWheelFFDevice(Dictionary<string, string> buttonMap, string wheelTech, int dinputIndex, int sdlIndex)
        {
            if (SystemConfig.isOptSet("pcsx2_force_feedback") && !SystemConfig.getOptBoolean("pcsx2_force_feedback"))
                return "None";

            if (buttonMap.ContainsKey("FFDevice") && !string.IsNullOrEmpty(buttonMap["FFDevice"]))
            {
                string ffDevice = buttonMap["FFDevice"];
                return ffDevice + "-" + (ffDevice.Contains("DInput") ? dinputIndex : sdlIndex);
            }

            return wheelTech == "sdl" ? "SDL-" + sdlIndex : "DInput-" + dinputIndex;
        }
    }
}