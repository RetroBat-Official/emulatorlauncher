using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Model2Generator : Generator
    {
        private void ConfigureControllers(byte[] bytes, IniFile ini, string parentRom)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            if (Program.SystemConfig.isOptSet("m2_joystick_autoconfig") && Program.SystemConfig["m2_joystick_autoconfig"] == "template")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Model2");

            CleanupInputFile(bytes);

            if (SystemConfig.getOptBoolean("use_guns") && shooters.Contains(parentRom))
                ConfigureModel2Guns(ini, bytes, parentRom);

            else if (Program.Controllers.Count > 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                var c2 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                if (c1.IsKeyboard)
                    WriteKbMapping(bytes, parentRom, c1.Config);
                else
                    WriteJoystickMapping(ini, bytes, parentRom, c1, c2);
            }
            else if (Program.Controllers.Count == 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                if (c1.IsKeyboard)
                    WriteKbMapping(bytes, parentRom, c1.Config);
                else
                    WriteJoystickMapping(ini, bytes, parentRom, c1);
            }
            else if (Program.Controllers.Count == 0)
                return;
        }

        private void WriteJoystickMapping(IniFile ini, byte[] bytes, string parentRom, Controller c1, Controller c2 = null)
        {
            if (c1 == null || c1.Config == null)
                return;

            //initialize controller index, m2emulator uses directinput controller index (+1)
            //only index of player 1 is initialized as there might be only 1 controller at that point
            int j2index;
            int j1index = c1.DirectInput != null ? c1.DirectInput.DeviceIndex + 1 : c1.DeviceIndex + 1;

            //If a secod controller is connected, get controller index of player 2, if there is no 2nd controller, just increment the index
            if (c2 != null && c2.Config != null && !c2.IsKeyboard)
                j2index = c2.DirectInput != null ? c2.DirectInput.DeviceIndex + 1 : c2.DeviceIndex + 1;
            else
                j2index = 0;

            // Define driver to use for input
            string tech1 = "xinput";
            string vendor1 = "";
            string vendor2 = "";
            string tech2 = "xinput";
            bool dinput1 = false;
            bool dinput2 = false;
            bool deportedShifter = false;
            int shifterID = -1;

            if (_dinput || !c1.IsXInputDevice)
            {
                tech1 = "dinput";
                dinput1 = true;
                if (c1.VendorID == USB_VENDOR.SONY)
                    vendor1 = "dualshock";
                else if (c1.VendorID == USB_VENDOR.MICROSOFT)
                    vendor1 = "microsoft";
                else if (c1.VendorID == USB_VENDOR.NINTENDO)
                    vendor1 = "nintendo";
            }

            if ((c2 != null && c2.Config != null && _dinput) || (c2 != null && c2.Config != null && !c2.IsXInputDevice))
            {
                tech2 = "dinput";
                dinput2 = true;
                if (c2.VendorID == USB_VENDOR.SONY)
                    vendor2 = "dualshock";
                else if (c2.VendorID == USB_VENDOR.MICROSOFT)
                    vendor2 = "microsoft";
                else if (c2.VendorID == USB_VENDOR.NINTENDO)
                    vendor2 = "nintendo";
            }

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            SdlToDirectInput ctrl1 = null;
            SdlToDirectInput ctrl2 = null;

            // Wheels
            int wheelNb = 0;
            bool useWheel = SystemConfig.isOptSet("use_wheel") && SystemConfig.getOptBoolean("use_wheel");
            bool useShoulders = SystemConfig.getOptBoolean("m2_racingshoulder");
            string wheelGuid = "nul";
            List<Wheel> usableWheels = new List<Wheel>();
            Wheel wheel = null;

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

            YmlFile ymlFile = null;
            YmlContainer wheelMapping = null;
            Dictionary<string, string> wheelbuttonMap = new Dictionary<string, string>();
            if (useWheel)
            {
                string wheeltype = "default";

                if (wheelNb > 0)
                {
                    usableWheels.Sort((x, y) => x.GetWheelPriority().CompareTo(y.GetWheelPriority()));

                    wheel = usableWheels[0];
                    int wheelPadIndex = wheel.DinputIndex;
                    wheeltype = wheel.Type.ToString();
                    SimpleLogger.Instance.Info("[WHEELS] Wheeltype identified : " + wheeltype);

                    // Get mapping in yml file
                    
                    string model2WheelMapping = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "model2_wheels.yml");
                    if (File.Exists(model2WheelMapping))
                    {
                        ymlFile = YmlFile.Load(model2WheelMapping);

                        wheelMapping = ymlFile.Elements.Where(c => c.Name == wheeltype).FirstOrDefault() as YmlContainer;

                        if (wheelMapping == null)
                        {
                            wheelMapping = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;
                            if (wheelMapping == null)
                            {
                                SimpleLogger.Instance.Info("[WHEELS] No mapping exists for the wheel and model2 emulator in yml file.");
                                return;
                            }
                            else
                                SimpleLogger.Instance.Info("[WHEELS] Using default wheel mapping in yml file.");
                        }

                        SimpleLogger.Instance.Info("[WHEELS] Retrieving wheel mapping from yml file.");

                        foreach (var mapEntry in wheelMapping.Elements)
                        {

                            if (mapEntry is YmlElement button)
                            {
                                if (button.Value == null || button.Value == "nul")
                                    continue;
                                wheelbuttonMap.Add(button.Name, button.Value);
                            }
                        }
                    }
                    else
                    {
                        SimpleLogger.Instance.Info("[WHEELS] Mapping file for model2 does not exist.");
                        return;
                    }

                    if (wheelbuttonMap != null && wheelbuttonMap.Count > 0)
                    {
                        j1index = wheelPadIndex + 1;
                        shifterID = j1index - 1;

                        ini.WriteValue("Input", "XInput", "0");
                        tech1 = "dinput";
                        tech2 = "dinput";
                        dinput1 = true;
                        dinput2 = true;

                        c1 = this.Controllers.FirstOrDefault(c => c.DeviceIndex == wheel.ControllerIndex);
                        wheelGuid = c1.Guid;

                        SimpleLogger.Instance.Info("[WHEELS] Wheel index : " + wheelPadIndex);
                    }
                    else
                        SimpleLogger.Instance.Info("[WHEELS] Wheel " + wheel.DevicePath.ToString() + " not found as Gamepad.");

                    // Set force feedback by default if wheel supports it
                    if (SystemConfig.getOptBoolean("m2_force_feedback") || !SystemConfig.isOptSet("m2_force_feedback"))
                        ini.WriteValue("Input", "EnableFF", "1");
                }
            }

            // Deported Shifter
            if (wheelbuttonMap.ContainsKey("DeportedShifter") && wheelbuttonMap["DeportedShifter"] == "true")
                deportedShifter = true;

            if (tech1 == "dinput" || tech2 == "dinput")
            {
                string guid1 = wheel != null ? (wheelGuid.ToString()).Substring(0, 24) + "00000000" : (c1.Guid.ToString()).Substring(0, 24) + "00000000";
                string guid2 = (c2 != null && c2.Config != null) ? (c2.Guid.ToString()).Substring(0, 24) + "00000000" : null;

                if (!File.Exists(gamecontrollerDB))
                {
                    SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                    gamecontrollerDB = null;
                }
                else
                {
                    SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file found in tools folder. Controller mapping will be available.");
                }
                ctrl1 = gamecontrollerDB == null ? null : GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1);

                if (tech2 == "dinput" && guid2 != null)
                    ctrl2 = gamecontrollerDB == null ? null : GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid2);
            }

            if (wheelMapping == null)
                useWheel = false;

            // Invert indexes option
            if (c2 != null && c2.Config != null && !_dinput)
            {
                if (SystemConfig.getOptBoolean("m2_indexswitch"))
                {
                    int tempIndex = j1index;
                    j1index = j2index;
                    j2index = tempIndex;
                }
            }

            // Override index with force index option
            if (SystemConfig.isOptSet("m2_forcep1index") && !string.IsNullOrEmpty(SystemConfig["m2_forcep1index"]))
                j1index = SystemConfig["m2_forcep1index"].ToInteger();
            if (SystemConfig.isOptSet("m2_forcep2index") && !string.IsNullOrEmpty(SystemConfig["m2_forcep2index"]))
                j2index = SystemConfig["m2_forcep2index"].ToInteger();

            // Write end of binary file for service buttons, test buttons and keyboard buttons for stats display
            WriteServiceBytes(bytes, j1index, c1, tech1, vendor1, serviceByte[parentRom], ctrl1, useWheel, wheelbuttonMap);
            WriteStatsBytes(bytes, serviceByte[parentRom] + 8);

            // Per game category mapping
            #region  shooters
            if (shooters.Contains(parentRom))
            {
                // Player index bytes
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = Convert.ToByte(j1index);
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j2index);

                bytes[0] = dinput1 ? GetInputCode(InputKey.up, c1, tech1, vendor1, ctrl1) : (byte)0x02;
                bytes[4] = dinput1 ? GetInputCode(InputKey.down, c1, tech1, vendor1, ctrl1) : (byte)0x03;
                bytes[8] = dinput1 ? GetInputCode(InputKey.left, c1, tech1, vendor1, ctrl1) : (byte)0x00;
                bytes[12] = dinput1 ? GetInputCode(InputKey.right, c1, tech1, vendor1, ctrl1) : (byte)0x01;
                bytes[16] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                bytes[20] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                bytes[24] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                bytes[28] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                bytes[32] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                bytes[33] = Convert.ToByte(j1index);
                bytes[36] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;
                bytes[37] = Convert.ToByte(j1index);

                for (int i = 0; i < 37; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }

                if (c2 != null && !c2.IsKeyboard)
                {
                    bytes[40] = dinput2 ? GetInputCode(InputKey.up, c2, tech2, vendor2, ctrl2) : (byte)0x02;
                    bytes[44] = dinput2 ? GetInputCode(InputKey.down, c2, tech2, vendor2, ctrl2) : (byte)0x03;
                    bytes[48] = dinput2 ? GetInputCode(InputKey.left, c2, tech2, vendor2, ctrl2) : (byte)0x00;
                    bytes[52] = dinput2 ? GetInputCode(InputKey.right, c2, tech2, vendor2, ctrl2) : (byte)0x01;
                    bytes[56] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                    bytes[60] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                    bytes[64] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                    bytes[68] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                    bytes[72] = dinput2 ? GetInputCode(InputKey.start, c2, tech2, vendor2, ctrl2) : (byte)0xB0;
                    bytes[73] = Convert.ToByte(j2index);
                    bytes[76] = dinput2 ? GetInputCode(InputKey.select, c2, tech2, vendor2, ctrl2) : (byte)0xC0;
                    bytes[77] = Convert.ToByte(j2index);

                    for (int i = 40; i < 77; i += 4)
                    {
                        if (highButtonMapping.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMapping[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16);
                        }
                        else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMappingPlus[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                        }
                    }
                }
                else
                {
                    bytes[40] = (byte)0x00;
                    bytes[44] = (byte)0x00;
                    bytes[48] = (byte)0x00;
                    bytes[52] = (byte)0x00;
                    bytes[56] = (byte)0x00;
                    bytes[60] = (byte)0x00;
                    bytes[64] = (byte)0x00;
                    bytes[68] = (byte)0x00;
                    bytes[72] = (byte)0x00;
                    bytes[76] = (byte)0x00;
                }  
            }
            #endregion

            #region driving shift up/down
            else if (drivingshiftupdown.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = bytes[45] = bytes[49] = Convert.ToByte(j1index);

                if (useWheel && wheelbuttonMap.Count > 0)
                {
                    SimpleLogger.Instance.Info("[WHEELS] Configuring wheel specific inputs.");

                    WriteWheelBytes(bytes, wheelbuttonMap, "Up", 0, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Down", 4, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Left", 8, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Right", 12, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Steer", 16, j1index, shifterID, deportedShifter);

                    if (parentRom == "overrev")
                        WriteWheelBytes(bytes, wheelbuttonMap, "Brake", 20, j1index, shifterID, deportedShifter, true);
                    else if (parentRom != "sgt24h")
                        WriteWheelBytes(bytes, wheelbuttonMap, "Throttle", 20, j1index, shifterID, deportedShifter, true);
                    else
                        WriteWheelBytes(bytes, wheelbuttonMap, "Throttle", 20, j1index, shifterID, deportedShifter, false);

                    if (parentRom == "overrev")
                        WriteWheelBytes(bytes, wheelbuttonMap, "Throttle", 24, j1index, shifterID, deportedShifter, true);
                    else if (parentRom != "sgt24h")
                        WriteWheelBytes(bytes, wheelbuttonMap, "Brake", 24, j1index, shifterID, deportedShifter, true);
                    else
                        WriteWheelBytes(bytes, wheelbuttonMap, "Brake", 24, j1index, shifterID, deportedShifter, false);

                    if (parentRom != "manxtt" && parentRom != "manxttc" && parentRom != "motoraid")
                    {
                        WriteWheelBytes(bytes, wheelbuttonMap, "East", 28, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "ShiftDown", 32, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "ShiftUp", 36, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "West", 40, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Start", 44, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Select", 48, j1index, shifterID, deportedShifter);

                        bytes[72] = (byte)0x01;
                        bytes[73] = (byte)0x01;
                        bytes[74] = (byte)0x01;
                    }
                    else
                    {
                        WriteWheelBytes(bytes, wheelbuttonMap, "ShiftUp", 28, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "ShiftDown", 32, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Start", 36, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Select", 40, j1index, shifterID, deportedShifter);

                        bytes[44] = (byte)0x00;
                        bytes[45] = (byte)0x00;

                        bytes[68] = (byte)0x01;
                        bytes[69] = (byte)0x01;
                        bytes[70] = (byte)0x01;
                    }
                }

                else
                {
                    if (useShoulders)
                    {
                        bytes[0] = dinput1 ? GetInputCode(InputKey.pagedown, c1, tech1, vendor1, ctrl1) : (byte)0x60;
                        bytes[4] = dinput1 ? GetInputCode(InputKey.pageup, c1, tech1, vendor1, ctrl1) : (byte)0x50;
                    }
                    else
                    {
                        bytes[0] = dinput1 ? GetInputCode(InputKey.up, c1, tech1, vendor1, ctrl1) : (byte)0x02;
                        bytes[4] = dinput1 ? GetInputCode(InputKey.down, c1, tech1, vendor1, ctrl1) : (byte)0x03;
                    }
                    bytes[8] = dinput1 ? GetInputCode(InputKey.left, c1, tech1, vendor1, ctrl1) : (byte)0x00;
                    bytes[12] = dinput1 ? GetInputCode(InputKey.right, c1, tech1, vendor1, ctrl1) : (byte)0x01;

                    bytes[16] = dinput1 ? GetInputCode(InputKey.leftanalogleft, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x02;
                    if (axisBytes.Contains(bytes[16]))
                        bytes[19] = 0xFF;
                    else
                        bytes[19] = 0x00;

                    bytes[20] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, true) : (byte)0x07;
                    if (parentRom == "indy500" || parentRom == "stcc" || parentRom == "motoraid" || parentRom.StartsWith("manxtt"))
                    {
                        if (dinput1 && vendor1 != "dualshock")
                            bytes[21] = Convert.ToByte(j1index + 16);
                    }
                    else if (!dinput1 || vendor1 == "dualshock")
                        bytes[21] = Convert.ToByte(j1index + 16);

                    if (axisBytes.Contains(bytes[20]))
                        bytes[23] = 0xFF;
                    else
                        bytes[23] = 0x00;

                    bytes[24] = dinput1 ? GetInputCode(InputKey.l2, c1, tech1, vendor1, ctrl1, false, true) : (byte)0x06;
                    if (parentRom != "indy500" && parentRom != "stcc" && parentRom != "motoraid" && !parentRom.StartsWith("manxtt"))
                    {
                        bytes[25] = Convert.ToByte(j1index + 16);
                    }

                    if (axisBytes.Contains(bytes[24]))
                        bytes[27] = 0xFF;
                    else
                        bytes[27] = 0x00;

                    if (parentRom == "motoraid")
                    {

                        bytes[28] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                        bytes[32] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;

                        bytes[36] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[40] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;
                        bytes[44] = (byte)0x00;
                        bytes[45] = (byte)0x00;

                        bytes[68] = (byte)0x01;
                        bytes[69] = useShoulders ? (byte)0x00 : (byte)0x01;
                        bytes[70] = useShoulders ? (byte)0x00 : (byte)0x01;
                    }
                    else if (parentRom != "manxtt" && parentRom != "manxttc")
                    {
                        bytes[28] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;         // view
                        bytes[32] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;         // shift down
                        bytes[36] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;         // shift up
                        bytes[40] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;         // view
                        bytes[44] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[48] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                        bytes[72] = (byte)0x01;
                        bytes[73] = useShoulders ? (byte)0x00 : (byte)0x01;
                        bytes[74] = useShoulders ? (byte)0x00 : (byte)0x01;
                    }
                    else
                    {
                        bytes[28] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;         // Shift up
                        bytes[32] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;         // Shift down
                        bytes[36] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[40] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;
                        bytes[44] = (byte)0x00;
                        bytes[45] = (byte)0x00;

                        bytes[68] = (byte)0x01;
                        bytes[69] = useShoulders ? (byte)0x00 : (byte)0x01;
                        bytes[70] = useShoulders ? (byte)0x00 : (byte)0x01;
                    }
                }

                for (int i = 0; i < 49; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }

            }
            #endregion

            #region driving gear stick
            else if (drivingshiftlever.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = Convert.ToByte(j1index);

                if (useWheel && wheelbuttonMap.Count > 0)
                {
                    SimpleLogger.Instance.Info("[WHEELS] Configuring wheel specific inputs.");

                    WriteWheelBytes(bytes, wheelbuttonMap, "Up", 0, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Down", 4, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Left", 8, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Right", 12, j1index, shifterID, deportedShifter);

                    if (SystemConfig.isOptSet("gearstick_deviceid") && !string.IsNullOrEmpty(SystemConfig["gearstick_deviceid"]))
                    {
                        deportedShifter = true;
                        shifterID = SystemConfig["gearstick_deviceid"].ToInteger();
                    }

                    if (deportedShifter)
                        SimpleLogger.Instance.Info("[WHEELS] Deported shifter enabled for wheel " + usableWheels[0].Name + " with ID " + shifterID);

                    
                    WriteWheelBytes(bytes, wheelbuttonMap, "Steer", 16, j1index, shifterID, deportedShifter);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Throttle", 20, j1index, shifterID, deportedShifter, true);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Brake", 24, j1index, shifterID, deportedShifter, true);

                    if (SystemConfig.isOptSet("wheel_nogearstick") && SystemConfig.getOptBoolean("wheel_nogearstick"))
                    {
                        WriteWheelBytes(bytes, wheelbuttonMap, "Up", 28, j1index, shifterID, false);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Down", 32, j1index, shifterID, false);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Left", 36, j1index, shifterID, false);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Right", 40, j1index, shifterID, false);
                    }

                    else
                    {
                        WriteWheelBytes(bytes, wheelbuttonMap, "Gear1", 28, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Gear2", 32, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Gear3", 36, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Gear4", 40, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "GearN", 44, j1index, shifterID, deportedShifter);
                    }

                    bytes[48] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;

                    if (parentRom == "daytona")
                    {
                        bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j1index);
                        WriteWheelBytes(bytes, wheelbuttonMap, "South", 52, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "North", 56, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "East", 60, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Start", 64, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Select", 68, j1index, shifterID, deportedShifter);
                        
                        bytes[96] = (byte)0x01;
                        bytes[97] = (byte)0x01;
                        bytes[98] = (byte)0x01;
                    }

                    else if (parentRom.StartsWith("srally"))
                    {
                        WriteWheelBytes(bytes, wheelbuttonMap, "Start", 52, j1index, shifterID, deportedShifter);
                        WriteWheelBytes(bytes, wheelbuttonMap, "Select", 56, j1index, shifterID, deportedShifter);

                        bytes[84] = (byte)0x01;
                        bytes[85] = (byte)0x01;
                        bytes[86] = (byte)0x01;
                    }
                }

                else
                {
                    if (useShoulders)
                    {
                        bytes[0] = dinput1 ? GetInputCode(InputKey.pagedown, c1, tech1, vendor1, ctrl1) : (byte)0x60;
                        bytes[4] = dinput1 ? GetInputCode(InputKey.pageup, c1, tech1, vendor1, ctrl1) : (byte)0x50;
                    }
                    else
                    {
                        bytes[0] = dinput1 ? GetInputCode(InputKey.up, c1, tech1, vendor1, ctrl1) : (byte)0x02;
                        bytes[4] = dinput1 ? GetInputCode(InputKey.down, c1, tech1, vendor1, ctrl1) : (byte)0x03;
                    }
                    bytes[8] = dinput1 ? GetInputCode(InputKey.left, c1, tech1, vendor1, ctrl1) : (byte)0x00;
                    bytes[12] = dinput1 ? GetInputCode(InputKey.right, c1, tech1, vendor1, ctrl1) : (byte)0x01;

                    bytes[16] = dinput1 ? GetInputCode(InputKey.leftanalogleft, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x02;  // Steering
                    if (axisBytes.Contains(bytes[16]))
                        bytes[19] = 0xFF;
                    else
                        bytes[19] = 0x00;

                    bytes[20] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, true) : (byte)0x07;  // Accelerate (R2)
                    if (vendor1 == "nintendo")
                        bytes[21] = 0x11;
                    if (axisBytes.Contains(bytes[20]))
                        bytes[23] = 0xFF;
                    else
                        bytes[23] = 0x00;

                    bytes[24] = dinput1 ? GetInputCode(InputKey.l2, c1, tech1, vendor1, ctrl1, false, true) : (byte)0x06;  // Brake (L2)
                    if (axisBytes.Contains(bytes[24]))
                        bytes[27] = 0xFF;
                    else
                        bytes[27] = 0x00;

                    bytes[28] = dinput1 ? GetInputCode(InputKey.joystick2up, c1, tech1, vendor1, ctrl1) : (byte)0x0A;
                    bytes[32] = dinput1 ? GetInputCode(InputKey.joystick2down, c1, tech1, vendor1, ctrl1) : (byte)0x0B;
                    bytes[36] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1) : (byte)0x08;
                    bytes[40] = dinput1 ? GetInputCode(InputKey.joystick2right, c1, tech1, vendor1, ctrl1) : (byte)0x09;

                    bytes[44] = dinput1 ? GetInputCode(InputKey.pagedown, c1, tech1, vendor1, ctrl1) : (byte)0x60;

                    bytes[48] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;

                    if (parentRom == "daytona")
                    {
                        bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j1index);
                        bytes[52] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                        bytes[56] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                        bytes[60] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                        bytes[64] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[68] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                        bytes[96] = (byte)0x01;
                        bytes[97] = useShoulders ? (byte)0x00 : (byte)0x01;
                        bytes[98] = useShoulders ? (byte)0x00 : (byte)0x01;
                    }

                    else if (parentRom.StartsWith("srally"))
                    {
                        bytes[52] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[56] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                        bytes[84] = (byte)0x01;
                        bytes[85] = useShoulders ? (byte)0x00 : (byte)0x01;
                        bytes[86] = useShoulders ? (byte)0x00 : (byte)0x01;
                    }
                }

                for (int i = 0; i < 69; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }
            }
            #endregion

            #region fighters
            else if (fighters.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = Convert.ToByte(j1index);
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = Convert.ToByte(j2index);

                // Player 1
                bytes[0] = dinput1 ? GetInputCode(InputKey.up, c1, tech1, vendor1, ctrl1) : (byte)0x02;
                bytes[4] = dinput1 ? GetInputCode(InputKey.down, c1, tech1, vendor1, ctrl1) : (byte)0x03;
                bytes[8] = dinput1 ? GetInputCode(InputKey.left, c1, tech1, vendor1, ctrl1) : (byte)0x00;
                bytes[12] = dinput1 ? GetInputCode(InputKey.right, c1, tech1, vendor1, ctrl1) : (byte)0x01;

                if (parentRom == "doa")
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                }
                else
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                }

                if (parentRom == "fvipers")
                    bytes[24] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                else
                    bytes[24] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;

                if (parentRom == "doa")
                    bytes[28] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                else if (parentRom == "fvipers")
                    bytes[28] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                else
                    bytes[28] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;

                bytes[32] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                bytes[36] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                for (int i = 0; i < 37; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }

                // Player 2
                if (c2 != null)
                {
                    bytes[40] = dinput2 ? GetInputCode(InputKey.up, c2, tech2, vendor2, ctrl2) : (byte)0x02;
                    bytes[44] = dinput2 ? GetInputCode(InputKey.down, c2, tech2, vendor2, ctrl2) : (byte)0x03;
                    bytes[48] = dinput2 ? GetInputCode(InputKey.left, c2, tech2, vendor2, ctrl2) : (byte)0x00;
                    bytes[52] = dinput2 ? GetInputCode(InputKey.right, c2, tech2, vendor2, ctrl2) : (byte)0x01;

                    if (parentRom == "doa")
                    {
                        bytes[56] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                        bytes[60] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                    }
                    else
                    {
                        bytes[56] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                        bytes[60] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                    }

                    if (parentRom == "fvipers")
                        bytes[64] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                    else
                        bytes[64] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;

                    if (parentRom == "doa")
                        bytes[68] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                    else if (parentRom == "fvipers")
                        bytes[68] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                    else
                        bytes[68] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;

                    bytes[72] = dinput2 ? GetInputCode(InputKey.start, c2, tech2, vendor2, ctrl2) : (byte)0xB0;
                    bytes[76] = dinput2 ? GetInputCode(InputKey.select, c2, tech2, vendor2, ctrl2) : (byte)0xC0;

                    for (int i = 40; i < 77; i += 4)
                    {
                        if (highButtonMapping.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMapping[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j2index + 16);
                        }
                        else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMappingPlus[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                        }
                    }
                }
                else
                    bytes[40] = bytes[44] = bytes[48] = bytes[52] = bytes[56] = bytes[60] = bytes[64] = bytes[68] = bytes[72] = bytes[76] = 0x00;
            }
            #endregion

            #region standard
            else if (standard.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = Convert.ToByte(j1index);
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = Convert.ToByte(j2index);

                // Player 1
                bytes[0] = dinput1 ? GetInputCode(InputKey.joystick1up, c1, tech1, vendor1, ctrl1) : (byte)0x06;
                bytes[4] = dinput1 ? GetInputCode(InputKey.joystick1down, c1, tech1, vendor1, ctrl1) : (byte)0x07;
                bytes[8] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1) : (byte)0x04;
                bytes[12] = dinput1 ? GetInputCode(InputKey.joystick1right, c1, tech1, vendor1, ctrl1) : (byte)0x05;

                if (parentRom == "vstriker")
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                }
                else if (parentRom == "dynamcop")
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                }
                else if (parentRom == "pltkids")
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                }
                else if (parentRom == "zerogun")
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                }

                bytes[32] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                bytes[36] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                for (int i = 0; i < 37; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }

                // Player 2
                if (c2 != null)
                {
                    bytes[40] = dinput2 ? GetInputCode(InputKey.leftanalogup, c2, tech2, vendor2, ctrl2) : (byte)0x06;
                    bytes[44] = dinput2 ? GetInputCode(InputKey.leftanalogdown, c2, tech2, vendor2, ctrl2) : (byte)0x07;
                    bytes[48] = dinput2 ? GetInputCode(InputKey.leftanalogleft, c2, tech2, vendor2, ctrl2) : (byte)0x04;
                    bytes[52] = dinput2 ? GetInputCode(InputKey.leftanalogright, c2, tech2, vendor2, ctrl2) : (byte)0x05;

                    if (parentRom == "vstriker")
                    {
                        bytes[56] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                        bytes[60] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                        bytes[64] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                        bytes[68] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                    }
                    else if (parentRom == "dynamcop")
                    {
                        bytes[56] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                        bytes[60] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                        bytes[64] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                        bytes[68] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                    }
                    else if (parentRom == "pltkids")
                    {
                        bytes[56] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                        bytes[60] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                        bytes[64] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                        bytes[68] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                    }
                    else if (parentRom == "zerogun")
                    {
                        bytes[56] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                        bytes[60] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                        bytes[64] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                        bytes[68] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                    }

                    bytes[72] = dinput2 ? GetInputCode(InputKey.start, c2, tech2, vendor2, ctrl2) : (byte)0xB0;
                    bytes[76] = dinput2 ? GetInputCode(InputKey.select, c2, tech2, vendor2, ctrl2) : (byte)0xC0;

                    for (int i = 40; i < 77; i += 4)
                    {
                        if (highButtonMapping.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMapping[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j2index + 16);
                        }
                        else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMappingPlus[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                        }
                    }
                }
                else
                    bytes[40] = bytes[44] = bytes[48] = bytes[52] = bytes[56] = bytes[60] = bytes[64] = bytes[68] = bytes[72] = bytes[76] = 0x00;
            }
            #endregion

            #region sports
            else if (sports.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = Convert.ToByte(j1index);
                if (parentRom != "segawski")
                    bytes[41] = Convert.ToByte(j1index);
                if (parentRom != "segawski" && parentRom != "waverunr")
                    bytes[45] = Convert.ToByte(j1index);

                if (parentRom == "waverunr")
                {
                    bytes[0] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1) : (byte)0x08;
                    bytes[4] = dinput1 ? GetInputCode(InputKey.joystick2right, c1, tech1, vendor1, ctrl1) : (byte)0x09;
                    bytes[8] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1) : (byte)0x04;
                    bytes[12] = dinput1 ? GetInputCode(InputKey.joystick1right, c1, tech1, vendor1, ctrl1) : (byte)0x05;
                    bytes[16] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x80;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x02;
                    if (axisBytes.Contains(bytes[20]))
                        bytes[23] = 0xFF;
                    else
                        bytes[23] = 0x00;

                    bytes[24] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x04;
                    if (axisBytes.Contains(bytes[24]))
                        bytes[27] = 0xFF;
                    else
                        bytes[27] = 0x00;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, true) : (byte)0x07;
                    if (axisBytes.Contains(bytes[28]))
                        bytes[31] = 0xFF;
                    else
                        bytes[31] = 0x00;

                    // Disable analog triggers for Nintendo
                    if (vendor1 == "nintendo")
                    {
                        bytes[28] = bytes[29] = bytes[30] = bytes[31] = 0xFF;
                    }

                    bytes[32] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                    bytes[36] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                    bytes[40] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                    bytes[64] = (byte)0x01;
                    bytes[65] = (byte)0x01;
                    bytes[66] = vendor1 == "nintendo" ? (byte)0x00 : (byte)0x01;
                }

                else if (parentRom == "skisuprg")
                {
                    bytes[0] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1) : (byte)0x04;
                    bytes[4] = dinput1 ? GetInputCode(InputKey.joystick1right, c1, tech1, vendor1, ctrl1) : (byte)0x05;
                    bytes[8] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1) : (byte)0x08;
                    bytes[12] = dinput1 ? GetInputCode(InputKey.joystick2right, c1, tech1, vendor1, ctrl1) : (byte)0x09;

                    bytes[16] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x04;

                    if (axisBytes.Contains(bytes[16]))
                        bytes[19] = 0xFF;
                    else
                        bytes[19] = 0x00;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x02;
                    bytes[21] = Convert.ToByte(j1index + 16);
                    if (axisBytes.Contains(bytes[20]))
                        bytes[23] = 0xFF;
                    else
                        bytes[23] = 0x00;

                    bytes[24] = dinput1 ? GetInputCode(InputKey.down, c1, tech1, vendor1, ctrl1) : (byte)0x03;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.up, c1, tech1, vendor1, ctrl1) : (byte)0x02;

                    bytes[32] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[36] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                    bytes[40] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                    bytes[44] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                    bytes[68] = (byte)0x01;
                    bytes[69] = (byte)0x01;
                }

                else if (parentRom == "topskatr" || parentRom == "segawski")
                {
                    bytes[0] = dinput1 ? GetInputCode(InputKey.joystick1up, c1, tech1, vendor1, ctrl1) : (byte)0x06;
                    bytes[4] = dinput1 ? GetInputCode(InputKey.joystick1down, c1, tech1, vendor1, ctrl1) : (byte)0x07;
                    bytes[8] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1) : (byte)0x04;
                    bytes[12] = dinput1 ? GetInputCode(InputKey.joystick1right, c1, tech1, vendor1, ctrl1) : (byte)0x05;
                    bytes[16] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x02;
                    bytes[17] = Convert.ToByte(j1index + 16);

                    if (axisBytes.Contains(bytes[16]))
                        bytes[19] = 0xFF;
                    else
                        bytes[19] = 0x00;

                    if (parentRom == "topskatr")
                    {
                        bytes[20] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x04;
                        if (axisBytes.Contains(bytes[20]))
                            bytes[23] = 0xFF;
                        else
                            bytes[23] = 0x00;
                        bytes[24] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                        bytes[28] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                        bytes[32] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                        bytes[36] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                        bytes[40] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[44] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                        bytes[68] = (byte)0x01;
                        bytes[69] = (byte)0x01;
                    }

                    if (parentRom == "segawski")
                    {
                        bytes[20] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                        bytes[24] = dinput1 ? GetInputCode(InputKey.l2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x70;
                        bytes[28] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x80;
                        bytes[32] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                        bytes[36] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                        bytes[60] = (byte)0x01;
                    }
                }

                for (int i = 0; i < 45; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }
            }
            #endregion

            #region games with specific schemes
            // Games with completely specific schemes
            // Desert Tank
            else if (parentRom == "desert")
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j1index);
                bytes[0] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1) : (byte)0x08;
                bytes[4] = dinput1 ? GetInputCode(InputKey.joystick2right, c1, tech1, vendor1, ctrl1) : (byte)0x09;
                bytes[8] = dinput1 ? GetInputCode(InputKey.joystick2up, c1, tech1, vendor1, ctrl1) : (byte)0x0A;
                bytes[12] = dinput1 ? GetInputCode(InputKey.joystick2down, c1, tech1, vendor1, ctrl1) : (byte)0x0B;
                bytes[16] = dinput1 ? GetInputCode(InputKey.joystick1up, c1, tech1, vendor1, ctrl1) : (byte)0x06;
                bytes[20] = dinput1 ? GetInputCode(InputKey.joystick1down, c1, tech1, vendor1, ctrl1) : (byte)0x07;
                bytes[24] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x04;
                if (axisBytes.Contains(bytes[24]))
                    bytes[27] = 0xFF;
                else
                    bytes[27] = 0x00;
                bytes[28] = dinput1 ? GetInputCode(InputKey.joystick2up, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x05;
                if (axisBytes.Contains(bytes[28]))
                    bytes[31] = 0xFF;
                else
                    bytes[31] = 0x00;
                bytes[32] = dinput1 ? GetInputCode(InputKey.joystick1up, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x03;
                if (axisBytes.Contains(bytes[32]))
                    bytes[35] = 0xFF;
                else
                    bytes[35] = 0x00;
                bytes[36] = dinput1 ? GetInputCode(InputKey.l2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x70;
                bytes[40] = dinput1 ? GetInputCode(InputKey.pagedown, c1, tech1, vendor1, ctrl1) : (byte)0x60;
                bytes[44] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x80;
                bytes[48] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                bytes[52] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                bytes[56] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                bytes[60] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                bytes[64] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;
                bytes[68] = 0x07;
                bytes[69] = 0x00;

                for (int i = 0; i < 65; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }
            }

            else
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = Convert.ToByte(j1index);

                bytes[0] = dinput1 ? GetInputCode(InputKey.joystick1up, c1, tech1, vendor1, ctrl1) : (byte)0x06;
                bytes[4] = dinput1 ? GetInputCode(InputKey.joystick1down, c1, tech1, vendor1, ctrl1) : (byte)0x07;
                bytes[8] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1) : (byte)0x04;
                bytes[12] = dinput1 ? GetInputCode(InputKey.joystick1right, c1, tech1, vendor1, ctrl1) : (byte)0x05;

                for (int i = 0; i < 13; i += 4)
                {
                    if (highButtonMapping.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMapping[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16);
                    }
                    else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                    {
                        bytes[i] = highButtonMappingPlus[bytes[i]];
                        bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                    }
                }

                // Dynamite Baseball '97
                if (parentRom == "dynabb97")
                {
                    if (c2 != null)
                        bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = bytes[81] = bytes[85] = Convert.ToByte(j2index);
                    else
                        bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = bytes[81] = bytes[85] = 0x00;

                    bytes[16] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, true) : (byte)0x07;
                    if (!dinput1)
                        bytes[17] = Convert.ToByte(j1index + 16);

                    if (axisBytes.Contains(bytes[16]))
                        bytes[19] = 0xFF;
                    else
                        bytes[19] = 0x00;

                    bytes[20] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.a, c1, tech1, vendor1, ctrl1) : (byte)0x30;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;
                    bytes[32] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                    bytes[36] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                    bytes[40] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;

                    for (int i = 16; i < 41; i += 4)
                    {
                        if (highButtonMapping.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMapping[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16);
                        }
                        else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMappingPlus[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                        }
                    }

                    if (c2 != null)
                    {
                        bytes[44] = dinput2 ? GetInputCode(InputKey.joystick1up, c2, tech2, vendor2, ctrl2) : (byte)0x06;
                        bytes[48] = dinput2 ? GetInputCode(InputKey.joystick1down, c2, tech2, vendor2, ctrl2) : (byte)0x07;
                        bytes[52] = dinput2 ? GetInputCode(InputKey.joystick1left, c2, tech2, vendor2, ctrl2) : (byte)0x04;
                        bytes[56] = dinput2 ? GetInputCode(InputKey.joystick1right, c2, tech2, vendor2, ctrl2) : (byte)0x05;

                        bytes[60] = dinput2 ? GetInputCode(InputKey.r2, c2, tech2, vendor2, ctrl2, false, true) : (byte)0x07;
                        if (axisBytes.Contains(bytes[60]))
                            bytes[63] = 0xFF;
                        else
                            bytes[63] = 0x00;
                        bytes[64] = dinput2 ? GetInputCode(InputKey.y, c2, tech2, vendor2, ctrl2) : (byte)0x10;
                        bytes[68] = dinput2 ? GetInputCode(InputKey.a, c2, tech2, vendor2, ctrl2) : (byte)0x30;
                        bytes[72] = dinput2 ? GetInputCode(InputKey.b, c2, tech2, vendor2, ctrl2) : (byte)0x40;
                        bytes[76] = dinput2 ? GetInputCode(InputKey.x, c2, tech2, vendor2, ctrl2) : (byte)0x20;
                        bytes[80] = dinput2 ? GetInputCode(InputKey.start, c2, tech2, vendor2, ctrl2) : (byte)0xB0;
                        bytes[84] = dinput2 ? GetInputCode(InputKey.select, c2, tech2, vendor2, ctrl2) : (byte)0xC0;

                        for (int i = 44; i < 85; i += 4)
                        {
                            if (highButtonMapping.ContainsKey(bytes[i]))
                            {
                                bytes[i] = highButtonMapping[bytes[i]];
                                bytes[i + 1] = Convert.ToByte(j2index + 16);
                            }
                            else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                            {
                                bytes[i] = highButtonMappingPlus[bytes[i]];
                                bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                            }
                        }
                    }

                    bytes[108] = (byte)0x01;
                    if (c2 != null && !c2.IsKeyboard)
                        bytes[109] = (byte)0x01;
                    else
                        bytes[109] = (byte)0x00;
                }
                // Sky Target
                else if (parentRom == "skytargt")
                {
                    bytes[16] = dinput1 ? GetInputCode(InputKey.joystick1left, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x02;
                    if (axisBytes.Contains(bytes[16]))
                        bytes[19] = 0xFF;
                    else
                        bytes[19] = 0x00;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.joystick1up, c1, tech1, vendor1, ctrl1, true, false) : (byte)0x03;
                    if (axisBytes.Contains(bytes[20]))
                        bytes[23] = 0xFF;
                    else
                        bytes[23] = 0x00;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x80;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.l2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x70;
                    bytes[32] = dinput1 ? GetInputCode(InputKey.x, c1, tech1, vendor1, ctrl1) : (byte)0x20;
                    bytes[36] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                    bytes[40] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;
                    bytes[44] = 0x07;
                    bytes[45] = 0x00;

                    bytes[68] = (byte)0x01;

                    for (int i = 16; i < 41; i += 4)
                    {
                        if (highButtonMapping.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMapping[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16);
                        }
                        else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMappingPlus[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                        }
                    }
                }
                else if (parentRom == "von")
                {
                    bytes[45] = bytes[49] = bytes[53] = Convert.ToByte(j1index);
                    bytes[16] = dinput1 ? GetInputCode(InputKey.l2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x70;
                    bytes[20] = dinput1 ? GetInputCode(InputKey.y, c1, tech1, vendor1, ctrl1) : (byte)0x10;
                    bytes[24] = dinput1 ? GetInputCode(InputKey.start, c1, tech1, vendor1, ctrl1) : (byte)0xB0;
                    bytes[28] = dinput1 ? GetInputCode(InputKey.select, c1, tech1, vendor1, ctrl1) : (byte)0xC0;
                    bytes[32] = dinput1 ? GetInputCode(InputKey.joystick2up, c1, tech1, vendor1, ctrl1) : (byte)0x0A;
                    bytes[36] = dinput1 ? GetInputCode(InputKey.joystick2down, c1, tech1, vendor1, ctrl1) : (byte)0x0B;
                    bytes[40] = dinput1 ? GetInputCode(InputKey.joystick2left, c1, tech1, vendor1, ctrl1) : (byte)0x08;
                    bytes[44] = dinput1 ? GetInputCode(InputKey.joystick2right, c1, tech1, vendor1, ctrl1) : (byte)0x09;
                    bytes[48] = dinput1 ? GetInputCode(InputKey.r2, c1, tech1, vendor1, ctrl1, false, false, true) : (byte)0x80;
                    bytes[52] = dinput1 ? GetInputCode(InputKey.b, c1, tech1, vendor1, ctrl1) : (byte)0x40;

                    for (int i = 16; i < 53; i += 4)
                    {
                        if (highButtonMapping.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMapping[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16);
                        }
                        else if (highButtonMappingPlus.ContainsKey(bytes[i]))
                        {
                            bytes[i] = highButtonMappingPlus[bytes[i]];
                            bytes[i + 1] = Convert.ToByte(j1index + 16 + 16);
                        }
                    }
                }
            }
            #endregion

            SimpleLogger.Instance.Info("[WHEELS] Input values all set.");

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + c1.DevicePath + " to player : " + c1.PlayerIndex.ToString());
            if (c2 != null && c2.Config != null && !c2.IsKeyboard)
                SimpleLogger.Instance.Info("[INFO] Assigned controller " + c2.DevicePath + " to player : " + c2.PlayerIndex.ToString());
        }

        private void WriteKbMapping(byte[] bytes, string parentRom, InputConfig keyboard)
        {
            if (keyboard == null)
                return;

            if (shooters.Contains(parentRom))
            {
                // Player index bytes
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = 0x00;
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = bytes[81] = bytes[85] = 0x00;

                bytes[0] = (byte)0xC8;      // up
                bytes[4] = (byte)0xD0;      // down
                bytes[8] = (byte)0xCB;      // left
                bytes[12] = (byte)0xCD;     // right
                bytes[16] = (byte)0x11;     // Z
                bytes[20] = (byte)0x2D;     // X
                bytes[24] = (byte)0x1C;     // Return
                bytes[28] = (byte)0x2F;     // V
                bytes[32] = (byte)0x02;     // 1
                bytes[36] = (byte)0x06;     // 5
                bytes[40] = (byte)0xC8;     // up
                bytes[44] = (byte)0xD0;     // down
                bytes[48] = (byte)0xCB;     // left
                bytes[52] = (byte)0xCD;     // right
                bytes[56] = (byte)0x2A;     // maj
                bytes[60] = (byte)0x1D;     // CTRL
                bytes[64] = (byte)0x38;     // ALT
                bytes[68] = (byte)0x39;     // SPACE
                bytes[72] = (byte)0x03;     // 2
                bytes[76] = (byte)0x07;     // 6
                bytes[80] = (byte)0x3B;     // F1
                bytes[84] = (byte)0x3C;     // F2
                bytes[88] = (byte)0x42;     // F8
                bytes[92] = (byte)0x41;     // F7
                bytes[96] = (byte)0x40;     // F6
            }
        }

        private static byte GetInputCode(InputKey key, Controller c, string tech, string brand, SdlToDirectInput ctrl, bool globalAxis = false, bool trigger = false, bool digital = false)
        {
            key = key.GetRevertedAxis(out bool revertAxis);

            var keyConfig = c.Config[key];
            if (keyConfig == null)
            {
                return 0x00;
            }
            string esName = keyConfig.Name.ToString();

            // Nintendo has no analog triggers : use right stick
            if (brand == "nintendo")
            {
                if (trigger && !digital)
                {
                    if (key == InputKey.r2 || key == InputKey.l2)
                        return 0x05;
                }
            }

            if (esName == null || !esToDinput.ContainsKey(esName))
                return 0x00;

            string dinputName = esToDinput[esName];

            if (dinputName == null)
                return 0x00;

            if (!ctrl.ButtonMappings.ContainsKey(dinputName))
                return 0x00;

            string button = ctrl.ButtonMappings[dinputName];

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger()) + 1;
                if (buttonID > 15)
                {
                    switch (buttonID)
                    {
                        case 16: return 0xF4;
                        case 17: return 0x15;
                        case 18: return 0x25;
                        case 19: return 0x35;
                        case 20: return 0x45;
                        case 21: return 0x55;
                        case 22: return 0x65;
                        case 23: return 0x75;
                        case 24: return 0x85;
                        case 25: return 0x95;
                        case 26: return 0xA5;
                        case 27: return 0xB5;
                        case 28: return 0xC5;
                        case 29: return 0xD5;
                        case 30: return 0xE5;
                        case 31: return 0xF5;
                        case 32: return 0xF6;
                        case 33: return 0x17;
                        case 34: return 0x27;
                        case 35: return 0x37;
                        case 36: return 0x47;
                        case 37: return 0x57;
                        case 38: return 0x67;
                        case 39: return 0x77;
                        case 40: return 0x87;
                        case 41: return 0x97;
                        case 42: return 0xA7;
                        case 43: return 0xB7;
                        case 44: return 0xC7;
                        case 45: return 0xD7;
                        case 46: return 0xE7;
                        case 47: return 0xF7;
                    };
                }
                else return (byte)(0x10 * buttonID);
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1: return 0x0E;
                    case 2: return 0x0D;
                    case 4: return 0x0F;
                    case 8: return 0x0C;
                };
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                if (globalAxis)
                {
                    switch (axisID)
                    {
                        case 0: return 0x00;
                        case 1: return 0x01;
                        case 2: return 0x03;
                        case 3: return 0x04;
                        case 4: return 0x05;
                        case 5: return 0x02;
                    };
                }

                else if (trigger)
                {
                    switch (axisID)
                    {
                        case 0: return 0x00;
                        case 1: return 0x01;
                        case 2: return 0x03;
                        case 3: return 0x04;
                        case 4: return 0x05;
                        case 5:
                            if (brand == "microsoft") return 0x04;
                            else return 0x02;
                    };
                }

                else if (digital)
                {
                    switch (axisID)
                    {
                        case 0:
                            return 0x00;
                        case 1:
                            return 0x01;
                        case 2:
                            if (brand == "microsoft") return 0x07;
                            else return 0x03;
                        case 3:
                            if (brand == "dualshock") return 0x70;
                            else return 0x04;
                        case 4:
                            if (brand == "dualshock") return 0x80;
                            else return 0x05;
                        case 5:
                            if (brand == "microsoft") return 0x06;
                            else return 0x02;
                    };
                }

                else
                {
                    switch (axisID)
                    {
                        case 0:
                            if (revertAxis) return 0x01;
                            else return 0x00;
                        case 1:
                            if (revertAxis) return 0x03;
                            else return 0x02;
                        case 2:
                            if (revertAxis) return 0x07;
                            else return 0x06;
                        case 3:
                            if (revertAxis) return 0x09;
                            else return 0x08;
                        case 4:
                            if (revertAxis) return 0x0B;
                            else return 0x0A;
                        case 5:
                            if (revertAxis) return 0x05;
                            else return 0x04;
                    };
                }
            }

            return 0x00;
        }

        private void WriteServiceBytes(byte[] bytes, int index, Controller c, string tech, string brand, int startByte, SdlToDirectInput ctrl, bool useWheel, Dictionary<string,string> wheelbuttonMap)
        {
            bool dinput = tech == "dinput";

            if (!SystemConfig.isOptSet("m2_enable_service") || !SystemConfig.getOptBoolean("m2_enable_service"))
            {
                bytes[startByte] = (byte)0x3B;
                bytes[startByte + 1] = 0x00;
                bytes[startByte + 4] = (byte)0x3C;
                bytes[startByte + 5] = 0x00;
            }
            else
            {
                if (!useWheel)
                {
                    bytes[startByte] = dinput ? GetInputCode(InputKey.l3, c, tech, brand, ctrl) : (byte)0x90;
                    bytes[startByte + 1] = (byte)index;
                    bytes[startByte + 4] = dinput ? GetInputCode(InputKey.r3, c, tech, brand, ctrl) : (byte)0xA0;
                    bytes[startByte + 5] = (byte)index;
                }
                else
                {
                    WriteWheelBytes(bytes, wheelbuttonMap, "Service", startByte, index, 0, false);
                    WriteWheelBytes(bytes, wheelbuttonMap, "Test", startByte + 4, index, 0, false);
                }
            }
        }

        static readonly Dictionary<string, int> serviceByte = new Dictionary<string, int>()
        {
            { "bel", 80 },
            { "daytona", 76 },
            { "desert", 72 },
            { "doa", 80 },
            { "dynabb97", 88 },
            { "dynamcop", 80 },
            { "fvipers", 80 },
            { "gunblade", 80 },
            { "hotd", 80 },
            { "indy500", 52 },
            { "lastbrnx", 80 },
            { "manxtt", 48 },
            { "manxttc", 48 },
            { "motoraid", 48 },
            { "overrev", 52 },
            { "pltkids", 80 },
            { "rchase2", 80 },
            { "schamp", 80 },
            { "segawski", 40 },
            { "sgt24h", 52 },
            { "skisuprg", 48 },
            { "skytargt", 48 },
            { "srallyc", 64 },
            { "srallyp", 64 },
            { "stcc", 52 },
            { "topskatr", 48 },
            { "vcop", 80 },
            { "vcop2", 80 },
            { "vf2", 80 },
            { "von", 56 },
            { "vstriker", 80 },
            { "waverunr", 44 },
            { "zerogun", 80 }
        };

        private void WriteStatsBytes(byte[] bytes, int startByte)
        {
            bytes[startByte] = (byte)0x42;
            bytes[startByte + 1] = (byte)0x00;
            bytes[startByte + 4] = (byte)0x41;
            bytes[startByte + 5] = (byte)0x00;
            bytes[startByte + 8] = (byte)0x40;
            bytes[startByte + 9] = (byte)0x00;
        }

        static readonly List<string> shooters = new List<string>() { "bel", "gunblade", "hotd", "rchase2", "vcop", "vcop2" };
        static readonly List<string> fighters = new List<string>() { "doa", "fvipers", "lastbrnx", "schamp", "vf2" };
        static readonly List<string> standard = new List<string>() { "dynamcop", "pltkids", "vstriker", "zerogun" };
        static readonly List<string> drivingshiftupdown = new List<string>() { "indy500", "motoraid", "overrev", "sgt24h", "stcc", "manxtt", "manxttc" };
        static readonly List<string> drivingshiftlever = new List<string>() { "daytona", "srallyc" };
        static readonly List<string> sports = new List<string>() { "segawski", "skisuprg", "topskatr", "waverunr" };
        static readonly List<byte> axisBytes = new List<byte>() { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

        private static readonly Dictionary<string, string> esToDinput = new Dictionary<string, string>()
        {
            { "a", "a" },
            { "b", "b" },
            { "x", "y" },
            { "y", "x" },
            { "select", "back" },
            { "start", "start" },
            { "joystick1left", "leftx" },
            { "leftanalogleft", "leftx" },
            { "joystick1up", "lefty" },
            { "leftanalogup", "lefty" },
            { "joystick2left", "rightx" },
            { "rightanalogleft", "rightx" },
            { "joystick2up", "righty" },
            { "rightanalogup", "righty" },
            { "up", "dpup" },
            { "down", "dpdown" },
            { "left", "dpleft" },
            { "right", "dpright" },
            { "l2", "lefttrigger" },
            { "l3", "leftstick" },
            { "pagedown", "rightshoulder" },
            { "pageup", "leftshoulder" },
            { "r2", "righttrigger" },
            { "r3", "rightstick" },
            { "leftthumb", "lefttrigger" },
            { "rightthumb", "righttrigger" },
            { "l1", "leftshoulder" },
            { "r1", "rightshoulder" },
            { "lefttrigger", "leftstick" },
            { "righttrigger", "rightstick" },
        };

        private static readonly Dictionary<byte, byte> highButtonMapping = new Dictionary<byte, byte>()
        {
            { 0xF4, 0x00 },
            { 0x15, 0x10 },
            { 0x25, 0x20 },
            { 0x35, 0x30 },
            { 0x45, 0x40 },
            { 0x55, 0x50 },
            { 0x65, 0x60 },
            { 0x75, 0x70 },
            { 0x85, 0x80 },
            { 0x95, 0x90 },
            { 0xA5, 0xA0 },
            { 0xB5, 0xB0 },
            { 0xC5, 0xC0 },
            { 0xD5, 0xD0 },
            { 0xE5, 0xE0 },
            { 0xF5, 0xF0 },
        };

        private static readonly Dictionary<byte, byte> highButtonMappingPlus = new Dictionary<byte, byte>()
        {
            { 0xF6, 0x00 },
            { 0x17, 0x10 },
            { 0x27, 0x20 },
            { 0x37, 0x30 },
            { 0x47, 0x40 },
            { 0x57, 0x50 },
            { 0x67, 0x60 },
            { 0x77, 0x70 },
            { 0x87, 0x80 },
            { 0x97, 0x90 },
            { 0xA7, 0xA0 },
            { 0xB7, 0xB0 },
            { 0xC7, 0xC0 },
            { 0xD7, 0xD0 },
            { 0xE7, 0xE0 },
            { 0xF7, 0xF0 },
        };

        private void CleanupInputFile(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = 0x00;
        }

        private void WriteWheelBytes(byte[] bytes, Dictionary<string, string> mapping, string buttonkey, int startingbyte, int joyIndex, int shifterID, bool deportedShifter = false, bool forceinv = false)
        {
            if (mapping.ContainsKey(buttonkey) && !string.IsNullOrEmpty(mapping[buttonkey]))
            {
                if (!buttonkey.ToLowerInvariant().StartsWith("gear"))
                {
                    string value = mapping[buttonkey];
                    if (value.ToLowerInvariant().StartsWith("button"))
                    {
                        value = value.Substring(6);
                        try
                        {
                            int buttonID = value.ToInteger() + 1;
                            int toConvert = buttonID * 16;
                            byte lsb = (byte)(toConvert & 0xFF);
                            byte msb = (byte)((toConvert >> 8) & 0xFF);

                            bytes[startingbyte] = lsb;
                            int multiplicator = (int)msb;

                            bytes[startingbyte + 1] = Convert.ToByte(joyIndex + (16 * multiplicator));
                            bytes[startingbyte + 2] = 0x00;
                            bytes[startingbyte + 3] = 0x00;
                        }
                        catch { }
                    }
                    else if (value.ToLowerInvariant().StartsWith("axisinv"))
                    {
                        try
                        {
                            value = value.Substring(7);
                            int buttonID = value.ToInteger();
                            bytes[startingbyte] = (byte)buttonID;
                            bytes[startingbyte + 1] = Convert.ToByte(joyIndex + 16);
                            bytes[startingbyte + 2] = 0x00;
                            bytes[startingbyte + 3] = 0xFF;
                        }
                        catch { }
                    }
                    else if (value.ToLowerInvariant().StartsWith("axis"))
                    {
                        try
                        {
                            value = value.Substring(4);
                            int buttonID = value.ToInteger();
                            bytes[startingbyte] = (byte)buttonID;
                            bytes[startingbyte + 1] = forceinv ? Convert.ToByte(joyIndex + 16) : Convert.ToByte(joyIndex);
                            bytes[startingbyte + 2] = 0x00;
                            bytes[startingbyte + 3] = 0xFF;
                        }
                        catch { }
                    }
                    else if (value.ToLowerInvariant().StartsWith("hat"))
                    {
                        try
                        {
                            if (value.ToLowerInvariant().EndsWith("up"))
                                bytes[startingbyte] = 0x0E;
                            else if(value.ToLowerInvariant().EndsWith("down"))
                                bytes[startingbyte] = 0x0F;
                            else if (value.ToLowerInvariant().EndsWith("left"))
                                bytes[startingbyte] = 0x0C;
                            else if (value.ToLowerInvariant().EndsWith("right"))
                                bytes[startingbyte] = 0x0D;

                            bytes[startingbyte + 1] = Convert.ToByte(joyIndex);
                            bytes[startingbyte + 2] = 0x00;
                            bytes[startingbyte + 3] = 0x00;
                        }
                        catch { }
                    }
                }

                else
                {
                    string value = mapping[buttonkey];
                    if (value.ToLowerInvariant().StartsWith("button"))
                    {
                        value = value.Substring(6);
                        try
                        {
                            int buttonID = value.ToInteger() + 1;
                            int toConvert = buttonID * 16;
                            byte lsb = (byte)(toConvert & 0xFF);
                            byte msb = (byte)((toConvert >> 8) & 0xFF);

                            bytes[startingbyte] = lsb;
                            int multiplicator = (int)msb;

                            if (deportedShifter)
                            {
                                bytes[startingbyte + 1] = Convert.ToByte(shifterID + (16 * multiplicator));
                                bytes[startingbyte + 2] = 0x00;
                                bytes[startingbyte + 3] = 0x00;
                            }
                            else
                            {
                                bytes[startingbyte + 1] = Convert.ToByte(joyIndex + (16 * multiplicator));
                                bytes[startingbyte + 2] = 0x00;
                                bytes[startingbyte + 3] = 0x00;
                            }
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
