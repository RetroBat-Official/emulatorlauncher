using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;
using System.Reflection;

namespace EmulatorLauncher.Libretro
{
    partial class LibRetroGenerator : Generator
    {
        /// <summary>
        /// Injects wheel settings
        /// </summary>
        /// <param name="retroarchConfig"></param>
        /// <param name="wheelDeviceType"></param>
        /// <param name="standardDeviceType"></param>
        /// <param name="core"></param>
        private void SetupWheels(ConfigFile retroarchConfig, string wheelDeviceType, string standardDeviceType, string core)
        {
            if (!SystemConfig.getOptBoolean("use_wheel"))
                return;

            SimpleLogger.Instance.Info("[WHEELS] Wheels enabled, searching for wheels.");

            List<Wheel> usableWheels = new List<Wheel>();

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard))
            {
                var drivingWheel = Wheel.GetWheelType(controller.DevicePath.ToUpperInvariant());

                if (drivingWheel != WheelType.Default)
                {
                    SimpleLogger.Instance.Info("[WHEELS] Wheel model found : " + drivingWheel.ToString());
                    
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
            }

            int wheelNb = usableWheels.Count;
            SimpleLogger.Instance.Info("[WHEELS] Found : " + wheelNb.ToString() + " usable wheels.");

            usableWheels.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));

            if (wheelNb < 1)
                return;

            var filteredWheels = new List<Wheel>();

            foreach (var wheel in usableWheels)
            {
                string wheeltype = wheel.Type.ToString();
                SimpleLogger.Instance.Info("[WHEELS] Looking for mapping for " + wheeltype);

                Dictionary<string, string> wheelbuttonMap = new Dictionary<string, string>();

                string coreWheelMapping = Path.Combine(
                    AppConfig.GetFullPath("retrobat"),
                    "system",
                    "resources",
                    "inputmapping",
                    "wheels",
                    "libretro_" + core + "_wheels.yml"
                );

                if (!File.Exists(coreWheelMapping))
                {
                    SimpleLogger.Instance.Info("[WHEELS] No wheel mapping file found for core libretro_" + core);
                    continue;
                }

                var ymlFile = YmlFile.Load(coreWheelMapping);
                var wheelMapping = ymlFile.Elements
                                          .OfType<YmlContainer>()
                                          .FirstOrDefault(c => c.Name == wheeltype);

                if (wheelMapping == null)
                {
                    SimpleLogger.Instance.Info("[WHEELS] No mapping exists for wheel " + wheeltype);
                    continue;
                }

                foreach (var mapEntry in wheelMapping.Elements.OfType<YmlElement>())
                {
                    if (string.IsNullOrEmpty(mapEntry.Value) || mapEntry.Value == "nul")
                        continue;

                    wheelbuttonMap[mapEntry.Name] = mapEntry.Value;
                }

                if (wheelbuttonMap.Count == 0)
                {
                    SimpleLogger.Instance.Info("[WHEELS] Wheel mapping is empty for " + wheeltype);
                    continue;
                }

                wheel.ButtonMapping = wheelbuttonMap;
                filteredWheels.Add(wheel);
            }
            
            usableWheels = filteredWheels;

            if (usableWheels.Count < 1)
            {
                SimpleLogger.Instance.Info("[WHEELS] No mapping found for any wheel in yml files for the core " + core);
                return;
            }

            // Clean up unused mapping after last wheel used
            for (int i = 1; i < 17; i++)
            {
                foreach (string button in RAButtons)
                {
                    retroarchConfig["input_player" + i + "_" + button + "_axis"] = "nul";
                    retroarchConfig["input_player" + i + "_" + button + "_btn"] = "nul";
                }
            }

            // Check if first wheel supports inputdriver defined, else change driver
            string inputDriver = retroarchConfig["input_joypad_driver"];
            if (inputDriver != null)
                SimpleLogger.Instance.Info("[WHEELS] Current input driver : " + inputDriver + ", checking if compatible with wheel.");

            if (inputDriver != null)
            {
                var wheel1 = usableWheels[0];
                if (wheel1.ButtonMapping.ContainsKey("driver"))
                {
                    if (!wheel1.ButtonMapping["driver"].Contains(inputDriver))
                    {
                        string newDriver = (wheel1.ButtonMapping["driver"].Split(','))[0];
                        retroarchConfig["input_joypad_driver"] = newDriver;
                        SimpleLogger.Instance.Info("[WHEELS] Input driver switched to " + newDriver);
                    }
                    else
                        SimpleLogger.Instance.Info("[WHEELS] No need to change input driver.");
                }
            }

            inputDriver = retroarchConfig["input_joypad_driver"];

            int playerIndex = 1;
            foreach (var wheel in usableWheels)
            {
                if (inputDriver == "sdl2" && wheel.SDLIndex > -1)
                    retroarchConfig[string.Format("input_player{0}_joypad_index", playerIndex)] = wheel.SDLIndex.ToString();
                else if (inputDriver == "dinput" && wheel.DinputIndex > -1)
                    retroarchConfig[string.Format("input_player{0}_joypad_index", playerIndex)] = wheel.DinputIndex.ToString();
                else if (inputDriver == "xinput" && wheel.XInputIndex > -1)
                    retroarchConfig[string.Format("input_player{0}_joypad_index", playerIndex)] = wheel.XInputIndex.ToString();

                if (SystemConfig.getOptBoolean("wheel_use_standard_gamepad"))
                    retroarchConfig["input_libretro_device_p" + playerIndex] = standardDeviceType;
                else
                    retroarchConfig["input_libretro_device_p" + playerIndex] = wheelDeviceType;

                foreach (var buttonmap in wheel.ButtonMapping)
                {
                    if (!RAButtons.Contains(buttonmap.Key))
                        continue;
                    else
                    {
                        string value = buttonmap.Value;
                        string key = buttonmap.Key;
                        if (value.StartsWith("+") || value.StartsWith("-"))
                            retroarchConfig["input_player" + playerIndex + "_" + key + "_axis"] = value;
                        else
                            retroarchConfig["input_player" + playerIndex + "_" + key + "_btn"] = value;
                    }
                }

                playerIndex++;
            }
        }

        private readonly List<string> RAButtons = new List<string>()
        { 
            "a", "b", "down", "l", "l2", "l3", "l_x_minus", "l_x_plus", "l_y_minus", "l_y_plus", "left", "r", "r2", "r3", 
            "r_x_minus", "r_x_plus", "r_y_minus", "r_y_plus", "right", "select", "start", "turbo", "up", "x", "y"
        };

    }
}