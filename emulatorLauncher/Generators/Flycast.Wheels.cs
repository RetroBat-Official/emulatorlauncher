using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class FlycastGenerator
    {
        private void ConfigureFlycastWheels(IniFile ini, string mappingPath, Dictionary<string, string> hotkeyMapping)
        {
            if (!SystemConfig.getOptBoolean("use_wheel"))
                return;

            SimpleLogger.Instance.Info("[WHEELS] Configuring wheels.");

            Dictionary<string, int> same_wheel = new Dictionary<string, int>();
            int nsameWheel = 0;

            bool serviceMenu = SystemConfig.isOptSet("flycast_service_menu") && SystemConfig.getOptBoolean("flycast_service_menu");
            bool racingController = SystemConfig.isOptSet("flycast_racing_controller") && SystemConfig.getOptBoolean("flycast_racing_controller");
            List<Wheel> usableWheels = new List<Wheel>();
            string wheelTech1 = "sdl";
            string wheelTech2 = "sdl";
            int wheelNb = 0;
            int wheelIndex1 = -1;
            int wheelIndex2 = -1;
            Wheel wheel1 = null;
            Wheel wheel2 = null;
            string wheeltype1 = "default";
            string wheeltype2 = "default";

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

            // Setup first wheel
            usableWheels.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));

            wheel1 = usableWheels[0];
            wheelIndex1 = wheel1.DinputIndex;

            var c1 = this.Controllers.FirstOrDefault(c => c.DeviceIndex == wheel1.ControllerIndex);

            wheeltype1 = wheel1.Type.ToString();
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1, wheeltype identified : " + wheeltype1);
            wheelIndex1 = wheel1.DinputIndex;
            SimpleLogger.Instance.Info("[WHEELS] Wheel 1 directinput index : " + wheelIndex1);

            // Get mapping from yml file in retrobat\system\resources\inputmapping\wheels and retrieve mapping
            YmlFile ymlFile = null;
            YmlContainer wheel1Mapping = null;
            Dictionary<string, string> wheel1buttonMap = new Dictionary<string, string>();
            string flycastWheelMapping = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "flycast_wheels.yml");
            if (File.Exists(flycastWheelMapping))
            {
                ymlFile = YmlFile.Load(flycastWheelMapping);

                wheel1Mapping = ymlFile.Elements.Where(c => c.Name == wheeltype1).FirstOrDefault() as YmlContainer;

                if (wheel1Mapping == null)
                {
                    wheel1Mapping = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;
                    if (wheel1Mapping == null)
                    {
                        SimpleLogger.Instance.Info("[WHEELS] No mapping exists for the wheel and Flycast emulator in yml file.");
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
                        if (button.Value == null || button.Value == "nul" )
                            continue;
                        wheel1buttonMap.Add(button.Name, button.Value);
                    }
                }
            }
            else
            {
                SimpleLogger.Instance.Info("[WHEELS] Mapping file for Flycast does not exist.");
                return;
            }

            // Change driver if needed
            if (wheel1buttonMap.ContainsKey("driver"))
                wheelTech1 = wheel1buttonMap["driver"];

            string deviceName = c1.Name;
            if (wheel1buttonMap.ContainsKey("name"))
                deviceName = wheel1buttonMap["name"];

            string mappingFile = Path.Combine(mappingPath, "SDL_" + deviceName + ".cfg");
            if (_isArcade)
                mappingFile = Path.Combine(mappingPath, "SDL_" + deviceName + "_arcade.cfg");

            // Do not generate twice the same mapping file
            if (same_wheel.ContainsKey(mappingFile))
                nsameWheel = same_wheel[mappingFile];
            else
                nsameWheel = 0;

            same_wheel[mappingFile] = nsameWheel + 1;

            if (nsameWheel > 0)
                goto BypassCtrlConfig;

            // Delete potential existing file
            if (File.Exists(mappingFile))
                File.Delete(mappingFile);

            using (var ctrlini = new IniFile(mappingFile, IniOptions.UseSpaces))
            {
                SimpleLogger.Instance.Info("[WHEELS] Writing wheel 1 mapping to " + mappingFile);

                ctrlini.ClearSection("analog");
                ctrlini.ClearSection("digital");
                ctrlini.ClearSection("emulator");

                List<string> analogBinds = new List<string>();
                List<string> digitalBinds = new List<string>();

                if (!_isArcade)
                {
                    BuildMapping(analogBinds, wheel1buttonMap, "SteerLeft", "btn_analog_left");
                    BuildMapping(analogBinds, wheel1buttonMap, "SteerRight", "btn_analog_right");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Up", "btn_dpad1_up");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Down", "btn_dpad1_down");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Left", "btn_dpad1_left");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Right", "btn_dpad1_right");

                    if (SystemConfig.isOptSet("flycast_wheel_layout") && !string.IsNullOrEmpty(SystemConfig["flycast_wheel_layout"]))
                    {
                        string layout = SystemConfig["flycast_wheel_layout"];

                        switch (layout)
                        {
                            case "triggers_ax":
                                BuildMapping(analogBinds, wheel1buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                                BuildMapping(analogBinds, wheel1buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_a");
                                BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                                break;

                            case "triggers_manual":
                                BuildMapping(analogBinds, wheel1buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                                BuildMapping(analogBinds, wheel1buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                                if (SystemConfig.getOptBoolean("wheel_nogearstick"))
                                {
                                    BuildMapping(digitalBinds, wheel1buttonMap, "StickGear4", "btn_b");
                                    BuildMapping(digitalBinds, wheel1buttonMap, "StickGear2", "btn_a");
                                    BuildMapping(digitalBinds, wheel1buttonMap, "StickGear1", "btn_x");
                                    BuildMapping(digitalBinds, wheel1buttonMap, "StickGear3", "btn_y");
                                }
                                else
                                {
                                    BuildMapping(digitalBinds, wheel1buttonMap, "Gear4", "btn_b");
                                    BuildMapping(digitalBinds, wheel1buttonMap, "Gear2", "btn_a");
                                    BuildMapping(digitalBinds, wheel1buttonMap, "Gear1", "btn_x");
                                    BuildMapping(digitalBinds, wheel1buttonMap, "Gear3", "btn_y");
                                }
                                break;

                            case "ax_triggers":
                                BuildMapping(analogBinds, wheel1buttonMap, "Throttle", "btn_a");
                                BuildMapping(analogBinds, wheel1buttonMap, "Brake", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_trigger_left");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_trigger_right");
                                BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                                break;
                            case "ax_by":
                                BuildMapping(analogBinds, wheel1buttonMap, "Throttle", "btn_a");
                                BuildMapping(analogBinds, wheel1buttonMap, "Brake", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_y");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "LeftTrigger", "btn_trigger_left");
                                BuildMapping(digitalBinds, wheel1buttonMap, "RightTrigger", "btn_trigger_right");
                                break;
                        }
                    }

                    else
                    {
                        BuildMapping(analogBinds, wheel1buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                        BuildMapping(analogBinds, wheel1buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                        BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                        BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                        BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_x");
                        BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                    }
                    
                    BuildMapping(digitalBinds, wheel1buttonMap, "Select", "btn_menu");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Start", "btn_start");
                    BuildMapping(digitalBinds, wheel1buttonMap, "RightStick", "btn_d");
                }
                else
                {
                    BuildMapping(analogBinds, wheel1buttonMap, "SteerLeft", "btn_analog_left");
                    BuildMapping(analogBinds, wheel1buttonMap, "SteerRight", "btn_analog_right");
                    BuildMapping(analogBinds, wheel1buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                    BuildMapping(analogBinds, wheel1buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Left", "btn_dpad1_left");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Right", "btn_dpad1_right");

                    if (!SystemConfig.isOptSet("flycast_arcadewheel_layout"))
                    {
                        if (_romName.StartsWith("initd"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_b");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_a");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "West", "insert_card");
                        }
                        else if (_romName.StartsWith("18wheel"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_c");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_b");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_a");
                        }
                        else if (_romName.StartsWith("crzyt"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_dpad1_up");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_c");
                        }
                        else if (_romName.StartsWith("f355"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_a");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_b");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_c");
                        }
                        else if (_romName == "tokyobus")
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_b");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_c");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                            BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_y");
                            BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_z");
                        }
                        else if (_romName.StartsWith("wrungp"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "Up", "axis2_up");
                            BuildMapping(digitalBinds, wheel1buttonMap, "Down", "axis2_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "axis2_left");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "axis2_right");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_dpad1_up");
                        }
                        else if (_romName == "ftspeed")
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_dpad1_up");
                            BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_a");
                        }
                        else if (_romName == "maxspeed")
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_dpad1_up");
                        }
                        else if (_romName.StartsWith("clubk"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_b");
                            BuildMapping(digitalBinds, wheel1buttonMap, "West", "insert_card");
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_dpad1_down");
                        }
                        else if (_romName.StartsWith("kingrt66"))
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_c");
                            BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_x");
                            BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                            BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                        }
                        else if (_romName == "wldrider")
                        {
                            BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_dpad1_up");
                            BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                            BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_b");
                        }
                    }

                    else if (SystemConfig.isOptSet("flycast_arcadewheel_layout") && !string.IsNullOrEmpty(SystemConfig["flycast_arcadewheel_layout"]))
                    {
                        string layout = SystemConfig["flycast_arcadewheel_layout"];

                        switch (layout)
                        {
                            case "b_reverse":
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                                BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                                BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "Up", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel1buttonMap, "Down", "btn_dpad1_down");
                                break;
                            case "x_reverse":
                                BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                                BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "Up", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel1buttonMap, "Down", "btn_dpad1_down");
                                break;
                            case "ab_gearup":
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_a");
                                BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                                BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "Up", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel1buttonMap, "Down", "btn_dpad1_down");
                                break;
                            case "dpadup_gearup":
                                BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                                BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                                BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                                BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_x");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearUp", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel1buttonMap, "GearDown", "btn_dpad1_down");
                                break;
                        }
                    }

                    else
                    {
                        BuildMapping(digitalBinds, wheel1buttonMap, "Up", "btn_dpad1_up");
                        BuildMapping(digitalBinds, wheel1buttonMap, "Down", "btn_dpad1_down");
                        BuildMapping(digitalBinds, wheel1buttonMap, "East", "btn_b");
                        BuildMapping(digitalBinds, wheel1buttonMap, "South", "btn_a");
                        BuildMapping(digitalBinds, wheel1buttonMap, "North", "btn_y");
                        BuildMapping(digitalBinds, wheel1buttonMap, "West", "btn_x");
                    }
                    BuildMapping(digitalBinds, wheel1buttonMap, "Select", "btn_d");
                    BuildMapping(digitalBinds, wheel1buttonMap, "Start", "btn_start");

                    if (serviceMenu)
                    {
                        BuildMapping(digitalBinds, wheel1buttonMap, "RightStick", "btn_dpad2_down");    // service menu
                        BuildMapping(digitalBinds, wheel1buttonMap, "LeftStick", "btn_dpad2_up");       // test
                    }
                }

                for (int i = 0; i < analogBinds.Count; i++)
                    ctrlini.WriteValue("analog", "bind" + i, analogBinds[i]);

                for (int i = 0; i < digitalBinds.Count; i++)
                    ctrlini.WriteValue("digital", "bind" + i, digitalBinds[i]);

                ctrlini.WriteValue("emulator", "dead_zone", "1");
                ctrlini.WriteValue("emulator", "mapping_name", "Default");
                ctrlini.WriteValue("emulator", "rumble_power", "100");
                ctrlini.WriteValue("emulator", "version", "3");

                ctrlini.Save();
            }
            
            BypassCtrlConfig:
            // Write information in ini file
            ini.WriteValue("input", "maple_sdl_joystick_" + wheelIndex1, "0");

            if (racingController == false && SystemConfig["flycast_controller1"] == "15")
                racingController = true;

            ini.WriteValue("input", "device1", racingController ? "15" : "0");
            ini.WriteValue("input", "device1.1", "1");

            if (SystemConfig.isOptSet("flycast_extension1") && !string.IsNullOrEmpty(SystemConfig["flycast_extension1"]))
                ini.WriteValue("input", "device1.2", SystemConfig["flycast_extension1"]);
            else
                ini.WriteValue("input", "device1.2", "10");

            // Setup second wheel
            if (wheelNb > 1)
            {
                wheel2 = usableWheels[1];
                wheeltype2 = wheel2.Type.ToString();
                SimpleLogger.Instance.Info("[WHEELS] Wheel 2, wheeltype identified : " + wheeltype2);
                wheelIndex2 = wheel2.DinputIndex;
                SimpleLogger.Instance.Info("[WHEELS] Wheel 2 directinput index : " + wheelIndex2);

                // Get mapping from yml file in retrobat\system\resources\inputmapping\wheels and retrieve mapping
                YmlContainer wheel2Mapping = null;
                Dictionary<string, string> wheel2buttonMap = new Dictionary<string, string>();

                if (File.Exists(flycastWheelMapping))
                {
                    ymlFile = YmlFile.Load(flycastWheelMapping);

                    wheel2Mapping = ymlFile.Elements.Where(c => c.Name == wheeltype2).FirstOrDefault() as YmlContainer;

                    if (wheel2Mapping == null)
                    {
                        wheel2Mapping = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;
                        if (wheel2Mapping == null)
                        {
                            SimpleLogger.Instance.Info("[WHEELS] No mapping exists for the wheel and Flycast emulator in yml file.");
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
                    SimpleLogger.Instance.Info("[WHEELS] Mapping file for Flycast does not exist.");
                    return;
                }

                var c2 = this.Controllers.FirstOrDefault(c => c.DeviceIndex == wheel2.ControllerIndex);

                // Change driver if needed
                if (wheel2buttonMap.ContainsKey("driver"))
                    wheelTech2 = wheel2buttonMap["driver"];

                string deviceName2 = c2.Name;
                if (wheel2buttonMap.ContainsKey("name"))
                    deviceName2 = wheel2buttonMap["name"];

                string mappingFile2 = Path.Combine(mappingPath, "SDL_" + deviceName2 + ".cfg");
                if (_isArcade)
                    mappingFile2 = Path.Combine(mappingPath, "SDL_" + deviceName2 + "_arcade.cfg");

                // Do not generate twice the same mapping file
                if (same_wheel.ContainsKey(mappingFile2))
                    nsameWheel = same_wheel[mappingFile2];
                else
                    nsameWheel = 0;

                same_wheel[mappingFile2] = nsameWheel + 1;

                if (nsameWheel > 0)
                    goto BypassC2Cfg;

                // Delete potential existing file
                if (File.Exists(mappingFile2))
                    File.Delete(mappingFile2);

                using (var ctrlini = new IniFile(mappingFile2, IniOptions.UseSpaces))
                {
                    SimpleLogger.Instance.Info("[WHEELS] Writing wheel 2 mapping to " + mappingFile2);

                    ctrlini.ClearSection("analog");
                    ctrlini.ClearSection("digital");
                    ctrlini.ClearSection("emulator");

                    List<string> analogBinds = new List<string>();
                    List<string> digitalBinds = new List<string>();

                    if (!_isArcade)
                    {
                        BuildMapping(analogBinds, wheel2buttonMap, "SteerLeft", "btn_analog_left");
                        BuildMapping(analogBinds, wheel2buttonMap, "SteerRight", "btn_analog_right");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Up", "btn_dpad1_up");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Down", "btn_dpad1_down");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Left", "btn_dpad1_left");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Right", "btn_dpad1_right");

                        if (SystemConfig.isOptSet("flycast_wheel_layout") && !string.IsNullOrEmpty(SystemConfig["flycast_wheel_layout"]))
                        {
                            string layout = SystemConfig["flycast_wheel_layout"];

                            switch (layout)
                            {
                                case "triggers_ax":
                                    BuildMapping(analogBinds, wheel2buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                                    BuildMapping(analogBinds, wheel2buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_a");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                                    break;

                                case "triggers_manual":
                                    BuildMapping(analogBinds, wheel2buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                                    BuildMapping(analogBinds, wheel2buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                                    if (SystemConfig.getOptBoolean("wheel_nogearstick"))
                                    {
                                        BuildMapping(digitalBinds, wheel2buttonMap, "StickGear4", "btn_b");
                                        BuildMapping(digitalBinds, wheel2buttonMap, "StickGear2", "btn_a");
                                        BuildMapping(digitalBinds, wheel2buttonMap, "StickGear1", "btn_x");
                                        BuildMapping(digitalBinds, wheel2buttonMap, "StickGear3", "btn_y");
                                    }
                                    else
                                    {
                                        BuildMapping(digitalBinds, wheel2buttonMap, "Gear4", "btn_b");
                                        BuildMapping(digitalBinds, wheel2buttonMap, "Gear2", "btn_a");
                                        BuildMapping(digitalBinds, wheel2buttonMap, "Gear1", "btn_x");
                                        BuildMapping(digitalBinds, wheel2buttonMap, "Gear3", "btn_y");
                                    }
                                    break;

                                case "ax_triggers":
                                    BuildMapping(analogBinds, wheel2buttonMap, "Throttle", "btn_a");
                                    BuildMapping(analogBinds, wheel2buttonMap, "Brake", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_trigger_left");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_trigger_right");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                                    break;
                                case "ax_by":
                                    BuildMapping(analogBinds, wheel2buttonMap, "Throttle", "btn_a");
                                    BuildMapping(analogBinds, wheel2buttonMap, "Brake", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_y");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "LeftTrigger", "btn_trigger_left");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "RightTrigger", "btn_trigger_right");
                                    break;
                            }
                        }

                        else
                        {
                            BuildMapping(analogBinds, wheel2buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                            BuildMapping(analogBinds, wheel2buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                            BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                            BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                            BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_x");
                            BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                        }
                        BuildMapping(digitalBinds, wheel2buttonMap, "Select", "btn_menu");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Start", "btn_start");
                        BuildMapping(digitalBinds, wheel2buttonMap, "RightStick", "btn_d");
                    }
                    else
                    {
                        BuildMapping(analogBinds, wheel2buttonMap, "SteerLeft", "btn_analog_left");
                        BuildMapping(analogBinds, wheel2buttonMap, "SteerRight", "btn_analog_right");
                        BuildMapping(analogBinds, wheel2buttonMap, "Throttle", racingController ? "btn_trigger2_right" : "btn_trigger_right");
                        BuildMapping(analogBinds, wheel2buttonMap, "Brake", racingController ? "btn_trigger2_left" : "btn_trigger_left");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Left", "btn_dpad1_left");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Right", "btn_dpad1_right");

                        if (!SystemConfig.isOptSet("flycast_arcadewheel_layout"))
                        {
                            if (_romName.StartsWith("initd"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_b");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_a");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "West", "insert_card");
                            }
                            else if (_romName.StartsWith("18wheel"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_c");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_b");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_a");
                            }
                            else if (_romName.StartsWith("crzyt"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_c");
                            }
                            else if (_romName.StartsWith("f355"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_a");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_b");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_c");
                            }
                            else if (_romName == "tokyobus")
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_b");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_c");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                                BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_y");
                                BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_z");
                            }
                            else if (_romName.StartsWith("wrungp"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "Up", "axis2_up");
                                BuildMapping(digitalBinds, wheel2buttonMap, "Down", "axis2_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "axis2_left");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "axis2_right");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_dpad1_up");
                            }
                            else if (_romName == "ftspeed")
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_a");
                            }
                            else if (_romName == "maxspeed")
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_dpad1_up");
                            }
                            else if (_romName.StartsWith("clubk"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_b");
                                BuildMapping(digitalBinds, wheel2buttonMap, "West", "insert_card");
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_dpad1_down");
                            }
                            else if (_romName.StartsWith("kingrt66"))
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_c");
                                BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_x");
                                BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                                BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                            }
                            else if (_romName == "wldrider")
                            {
                                BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_dpad1_down");
                                BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_dpad1_up");
                                BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                                BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_b");
                            }
                        }

                        else if (SystemConfig.isOptSet("flycast_arcadewheel_layout") && !string.IsNullOrEmpty(SystemConfig["flycast_arcadewheel_layout"]))
                        {
                            string layout = SystemConfig["flycast_arcadewheel_layout"];

                            switch (layout)
                            {
                                case "b_reverse":
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "Up", "btn_dpad1_up");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "Down", "btn_dpad1_down");
                                    break;
                                case "x_reverse":
                                    BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "Up", "btn_dpad1_up");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "Down", "btn_dpad1_down");
                                    break;
                                case "ab_gearup":
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_a");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "Up", "btn_dpad1_up");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "Down", "btn_dpad1_down");
                                    break;
                                case "dpadup_gearup":
                                    BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_x");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearUp", "btn_dpad1_up");
                                    BuildMapping(digitalBinds, wheel2buttonMap, "GearDown", "btn_dpad1_down");
                                    break;
                            }
                        }

                        else
                        {
                            BuildMapping(digitalBinds, wheel2buttonMap, "Up", "btn_dpad1_up");
                            BuildMapping(digitalBinds, wheel2buttonMap, "Down", "btn_dpad1_down");
                            BuildMapping(digitalBinds, wheel2buttonMap, "East", "btn_b");
                            BuildMapping(digitalBinds, wheel2buttonMap, "South", "btn_a");
                            BuildMapping(digitalBinds, wheel2buttonMap, "North", "btn_y");
                            BuildMapping(digitalBinds, wheel2buttonMap, "West", "btn_x");
                        }
                        BuildMapping(digitalBinds, wheel2buttonMap, "Select", "btn_d");
                        BuildMapping(digitalBinds, wheel2buttonMap, "Start", "btn_start");

                        if (serviceMenu)
                        {
                            BuildMapping(digitalBinds, wheel2buttonMap, "RightStick", "btn_dpad2_down");    // service menu
                            BuildMapping(digitalBinds, wheel2buttonMap, "LeftStick", "btn_dpad2_up");       // test
                        }
                    }

                    for (int i = 0; i < analogBinds.Count; i++)
                        ctrlini.WriteValue("analog", "bind" + i, analogBinds[i]);

                    for (int i = 0; i < digitalBinds.Count; i++)
                        ctrlini.WriteValue("digital", "bind" + i, digitalBinds[i]);

                    ctrlini.WriteValue("emulator", "dead_zone", "1");
                    ctrlini.WriteValue("emulator", "mapping_name", "Default");
                    ctrlini.WriteValue("emulator", "rumble_power", "100");
                    ctrlini.WriteValue("emulator", "version", "3");

                    ctrlini.Save();
                }

                BypassC2Cfg:
                if (racingController == false && SystemConfig["flycast_controller2"] == "15")
                    racingController = true;
                else if (racingController == false && SystemConfig["flycast_controller2"] != "15")
                    racingController = false;

                ini.WriteValue("input", "maple_sdl_joystick_" + wheelIndex2, "1");
                ini.WriteValue("input", "device2", racingController ? "15" : "0");
                ini.WriteValue("input", "device2.1", "1");

                if (SystemConfig.isOptSet("flycast_extension2") && !string.IsNullOrEmpty(SystemConfig["flycast_extension2"]))
                    ini.WriteValue("input", "device2.2", SystemConfig["flycast_extension2"]);
                else
                    ini.WriteValue("input", "device2.2", "10");
            }
        }

        private static void BuildMapping(List<string> Binds, Dictionary<string,string> mapping, string buttonkey, string target)
        {
            if (buttonkey.StartsWith("StickGear"))
            {
                if (!mapping.ContainsKey(buttonkey) || mapping[buttonkey] == "nul")
                    buttonkey = buttonkey.Substring(5);
            }

            if (mapping.ContainsKey(buttonkey))
                    Binds.Add(mapping[buttonkey] + ":" + target);
        }
    }
}
