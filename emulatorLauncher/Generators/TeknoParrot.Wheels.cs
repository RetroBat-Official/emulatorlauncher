using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeknoParrotUi.Common;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class TeknoParrotGenerator : Generator
    {
        private static string GetDrivingBindName(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "Driving " + code;

            if (code.StartsWith("BUTTON"))
            {
                if (int.TryParse(code.Substring(6), out int btnNum))
                    return "Driving Buttons" + (btnNum - 1);
            }
            else if (code.StartsWith("POV"))
            {
                if (code.Contains("UP")) return "Driving PointOfViewControllers0 Up";
                if (code.Contains("DOWN")) return "Driving PointOfViewControllers0 Down";
                if (code.Contains("LEFT")) return "Driving PointOfViewControllers0 Left";
                if (code.Contains("RIGHT")) return "Driving PointOfViewControllers0 Right";
            }

            return "Driving " + code;
        }

        private static bool ConfigureTPWheels(GameProfile userProfile, string rom)
        {
            if (!Program.SystemConfig.getOptBoolean("use_wheel"))
                return false;

            SimpleLogger.Instance.Info("[WHEELS] Wheels enabled, searching for wheels.");

            var usableWheels = Wheel.GetConnectedWheels(Program.Controllers);
            if (usableWheels.Count < 1)
            {
                SimpleLogger.Instance.Info("[WHEELS] No usable wheel detected.");
                return false;
            }

            SimpleLogger.Instance.Info("[WHEELS] Found " + usableWheels.Count + " usable wheel(s).");

            string tpGameName = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
            if (userProfile != null && !string.IsNullOrEmpty(userProfile.FileName))
            {
                string profName = Path.GetFileNameWithoutExtension(userProfile.FileName).ToLowerInvariant();
                if (!string.IsNullOrEmpty(profName))
                    tpGameName = profName;
            }

            var inputAPI = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Input API");
            if (inputAPI != null && inputAPI.FieldOptions != null && inputAPI.FieldOptions.Any(f => f == "DirectInput"))
                inputAPI.FieldValue = "DirectInput";

            Wheel w1 = usableWheels[0];
            var c1 = Program.Controllers.FirstOrDefault(c => c.DeviceIndex == w1.ControllerIndex);
            if (c1 == null || c1.Config == null || c1.DirectInput == null)
            {
                SimpleLogger.Instance.Warning("[WHEELS] Wheel controller not found or no DirectInput.");
                return false;
            }

            SimpleLogger.Instance.Info("[WHEELS] Wheel type: " + w1.Type.ToString());
            SimpleLogger.Instance.Info("[WHEELS] Wheel controller index: " + w1.ControllerIndex);
            SimpleLogger.Instance.Info("[WHEELS] Wheel DirectInput index: " + w1.DinputIndex);

            // Load wheel mapping from teknoparrot_wheels.yml
            string wheelMappingFile = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "teknoparrot_wheels.yml");
            if (!File.Exists(wheelMappingFile))
            {
                SimpleLogger.Instance.Warning("[WHEELS] teknoparrot_wheels.yml not found.");
                return false;
            }

            YmlFile wheelYmlFile = YmlFile.Load(wheelMappingFile);
            string wheelTypeName = w1.Type.ToString();
            YmlContainer wheelMapping = wheelYmlFile.Elements.FirstOrDefault(g => g.Name == wheelTypeName) as YmlContainer;
            
            if (wheelMapping == null)
            {
                wheelMapping = wheelYmlFile.Elements.FirstOrDefault(g => g.Name == "default") as YmlContainer;
                if (wheelMapping != null)
                    SimpleLogger.Instance.Info("[WHEELS] Using default wheel mapping.");
                else
                {
                    SimpleLogger.Instance.Warning("[WHEELS] No default wheel mapping found.");
                    return false;
                }
            }
            else
            {
                SimpleLogger.Instance.Info("[WHEELS] Using wheel-specific mapping for: " + wheelTypeName);
            }

            // Read game-specific button mappings from GameGearMappings or GameButtonMappings section
            var gameButtonMappings = new Dictionary<string, Dictionary<string, string>>();
            var gameMappingsContainer = wheelYmlFile.Elements.FirstOrDefault(g => g.Name == "GameGearMappings" || g.Name == "GameButtonMappings") as YmlContainer;
            if (gameMappingsContainer != null)
            {
                foreach (var gameEntry in gameMappingsContainer.Elements)
                {
                    var gameContainer = gameEntry as YmlContainer;
                    if (gameContainer == null) continue;
                    
                    var gameName = gameContainer.Name.ToLowerInvariant();
                    var mappings = new Dictionary<string, string>();
                    foreach (var mappingEntry in gameContainer.Elements)
                    {
                        var mappingEl = mappingEntry as YmlElement;
                        if (mappingEl != null && mappingEl.Value != null)
                            mappings.Add(mappingEl.Name, mappingEl.Value.Trim('"', '\'', ' '));
                    }
                    if (mappings.Count > 0)
                        gameButtonMappings[gameName] = mappings;
                }
                SimpleLogger.Instance.Info("[WHEELS] Loaded " + gameButtonMappings.Count + " game-specific button mappings.");
            }

            // Resolve actual game key for lookup (fallback to ROM name if needed)
            if (!gameButtonMappings.ContainsKey(tpGameName))
            {
                string romGameName = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
                if (gameButtonMappings.ContainsKey(romGameName))
                    tpGameName = romGameName;
            }
            SimpleLogger.Instance.Info("[WHEELS] Using game mapping key: " + tpGameName);

            var dinputCodes = new Dictionary<string, string>();
            foreach (var entry in wheelMapping.Elements)
            {
                YmlElement el = entry as YmlElement;
                if (el != null && el.Value != null)
                    dinputCodes.Add(el.Name, el.Value.Trim('"', '\'', ' '));
            }

            if (dinputCodes.Count == 0)
            {
                SimpleLogger.Instance.Warning("[WHEELS] No DInput codes found in wheel mapping.");
                return false;
            }

            bool nogearstick = Program.SystemConfig.getOptBoolean("wheel_nogearstick");
            int gearstickDeviceId = -1;
            if (Program.SystemConfig.isOptSet("gearstick_deviceid") && !string.IsNullOrEmpty(Program.SystemConfig["gearstick_deviceid"]))
                gearstickDeviceId = Program.SystemConfig["gearstick_deviceid"].ToInteger();

            // Cleanup
            foreach (var joyButton in userProfile.JoystickButtons)
            {
                joyButton.BindName = null;
                joyButton.BindNameDi = null;
                joyButton.BindNameRi = null;
                joyButton.BindNameXi = null;
                joyButton.DirectInputButton = null;
                joyButton.XInputButton = null;
                joyButton.RawInputButton = null;
            }

            Guid diGuid = c1.DirectInput.InstanceGuid;
            int diDeviceIndex = c1.DirectInput.DeviceIndex;

            Guid gearstickGuid = diGuid;
            if (gearstickDeviceId >= 0)
            {
                var gearStickCtrl = Program.Controllers.FirstOrDefault(c => c.DeviceIndex == gearstickDeviceId);
                if (gearStickCtrl != null && gearStickCtrl.DirectInput != null)
                {
                    gearstickGuid = gearStickCtrl.DirectInput.InstanceGuid;
                    SimpleLogger.Instance.Info("[WHEELS] Using separate gearstick controller with GUID: " + gearstickGuid);
                }
            }

            // Build dictionary of generic action keys -> target TeknoParrot InputMapping[]
            var buttonToInputMappings = new Dictionary<string, InputMapping[]>
            {
                { "GearUp", new[] { InputMapping.Wmmt5GearChangeUp, InputMapping.SrcGearChangeUp, InputMapping.FnfGearChangeUp, InputMapping.IDZGearChangeUp, InputMapping.P1ButtonRight, InputMapping.P1Button2, InputMapping.P1ButtonUp } },
                { "GearDown", new[] { InputMapping.Wmmt5GearChangeDown, InputMapping.SrcGearChangeDown, InputMapping.FnfGearChangeDown, InputMapping.IDZGearChangeDown, InputMapping.P1Button1, InputMapping.P1Button3, InputMapping.P1ButtonDown } },
                { "Gear1", new[] { InputMapping.Wmmt5GearChange1, InputMapping.SrcGearChange1, InputMapping.FnfGearChange1, InputMapping.IDZGearChange1 } },
                { "Gear2", new[] { InputMapping.Wmmt5GearChange2, InputMapping.SrcGearChange2, InputMapping.FnfGearChange2, InputMapping.IDZGearChange2 } },
                { "Gear3", new[] { InputMapping.Wmmt5GearChange3, InputMapping.SrcGearChange3, InputMapping.FnfGearChange3, InputMapping.IDZGearChange3 } },
                { "Gear4", new[] { InputMapping.Wmmt5GearChange4, InputMapping.SrcGearChange4, InputMapping.FnfGearChange4, InputMapping.IDZGearChange4 } },
                { "Gear5", new[] { InputMapping.Wmmt5GearChange5, InputMapping.IDZGearChange5 } },
                { "Gear6", new[] { InputMapping.Wmmt5GearChange6, InputMapping.IDZGearChange6 } },
                { "GearReverse", new[] { InputMapping.Wmmt5GearChange6, InputMapping.IDZGearChange6, InputMapping.P1Button5 } },
                { "ViewChange", new[] { InputMapping.ExtensionOne1, InputMapping.P1Button1 } },
                { "IntrudeChange", new[] { InputMapping.ExtensionOne2 } },
                { "InsertCard", new[] { InputMapping.Wmmt3InsertCard } },
                { "Test", new[] { InputMapping.Test, InputMapping.JvsTwoTest } },
                { "Service", new[] { InputMapping.Service1, InputMapping.JvsTwoService1 } },
                { "Coin", new[] { InputMapping.Coin1, InputMapping.JvsTwoCoin1 } },
                { "Start", new[] { InputMapping.P1ButtonStart, InputMapping.JvsTwoP1ButtonStart } },
                { "Handbrake", new[] { InputMapping.P1Button4, InputMapping.P1ButtonDown, InputMapping.ExtensionOne3 } },
                { "Nitro", new[] { InputMapping.P1Button4, InputMapping.P1Button5 } },
                { "Boost", new[] { InputMapping.P1Button4, InputMapping.P1Button5 } },
                { "ResetButton", new[] { InputMapping.P1Button2 } },
                { "ReplayButton", new[] { InputMapping.P1ButtonUp } },
                { "LookBehind", new[] { InputMapping.P1Button3 } },
                { "LookBehind2", new[] { InputMapping.P1ButtonDown } },
            };

            // Apply game-specific button mappings if available for this game
            if (gameButtonMappings.ContainsKey(tpGameName))
            {
                var gameMappings = gameButtonMappings[tpGameName];
                foreach (var gm in gameMappings)
                {
                    string cleanVal = gm.Value != null ? gm.Value.Trim('"', '\'', ' ') : null;
                    if (!string.IsNullOrEmpty(cleanVal) && Enum.TryParse(cleanVal, out InputMapping customMapping))
                    {
                        buttonToInputMappings[gm.Key] = new[] { customMapping };
                        SimpleLogger.Instance.Info("[WHEELS] Game-specific override: " + gm.Key + " -> " + cleanVal);
                    }
                }
            }

            // Helper to apply DirectInput code to multiple InputMappings
            Action<string, InputMapping[]> mapButtonMulti = (codeKey, mappings) =>
            {
                if (!dinputCodes.ContainsKey(codeKey) || string.IsNullOrEmpty(dinputCodes[codeKey]))
                    return;

                string code = dinputCodes[codeKey].ToUpperInvariant();
                foreach (var inputEnum in mappings)
                {
                    var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == inputEnum && !j.HideWithDirectInput);
                    if (xmlPlace == null)
                        continue;

                    if (nogearstick && codeKey.StartsWith("Gear") && codeKey != "GearUp" && codeKey != "GearDown" && codeKey != "GearReverse")
                        continue;

                    Guid targetGuid = codeKey.StartsWith("Gear") ? gearstickGuid : diGuid;

                    var diButton = new JoystickButton
                    {
                        JoystickGuid = targetGuid,
                        IsAxis = false,
                        IsAxisMinus = false,
                        IsFullAxis = false,
                        IsReverseAxis = false,
                        PovDirection = 0
                    };

                    if (code.StartsWith("BUTTON"))
                    {
                        int btnNum = int.Parse(code.Substring(6));
                        diButton.Button = btnNum + 47; // 1-based offset to DirectInput 48 (Button 1 = 48)
                        diButton.IsAxis = false;
                    }
                    else if (code.StartsWith("POV"))
                    {
                        diButton.Button = 32;
                        diButton.IsAxis = false;
                        if (code.Contains("UP")) diButton.PovDirection = 0;
                        else if (code.Contains("DOWN")) diButton.PovDirection = 18000;
                        else if (code.Contains("LEFT")) diButton.PovDirection = 27000;
                        else if (code.Contains("RIGHT")) diButton.PovDirection = 9000;
                    }

                    xmlPlace.DirectInputButton = diButton;
                    xmlPlace.BindNameDi = GetDrivingBindName(code);
                    xmlPlace.BindName = xmlPlace.BindNameDi;
                    SimpleLogger.Instance.Info("[WHEELS] Bound " + inputEnum + " to " + xmlPlace.BindNameDi);
                }
            };

            // Apply all configured button mappings
            foreach (var btnEntry in buttonToInputMappings)
            {
                mapButtonMulti(btnEntry.Key, btnEntry.Value);
            }

            // Map axes (steering, gas, brake)
            if (dinputCodes.ContainsKey("SteerLeft") && dinputCodes.ContainsKey("SteerRight"))
            {
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == InputMapping.Analog0 && !j.HideWithDirectInput);
                if (xmlPlace != null)
                {
                    string axisCode = dinputCodes["SteerLeft"].ToUpperInvariant();
                    int axisId = 0;
                    if (axisCode.StartsWith("XAXIS")) axisId = 0;
                    else if (axisCode.StartsWith("YAXIS")) axisId = 1;
                    else if (axisCode.StartsWith("ZAXIS")) axisId = 2;
                    else if (axisCode.StartsWith("RZAXIS")) axisId = 5;

                    xmlPlace.DirectInputButton = new JoystickButton
                    {
                        JoystickGuid = diGuid,
                        IsAxis = true,
                        IsAxisMinus = false,
                        IsFullAxis = true,
                        IsReverseAxis = false,
                        Button = axisId * 4,
                        PovDirection = 0
                    };
                    xmlPlace.BindNameDi = "Driving " + axisCode.Replace("_NEG", "").Replace("_POS", "");
                    xmlPlace.BindName = xmlPlace.BindNameDi;
                }
            }

            // Gas (Analog2)
            if (dinputCodes.ContainsKey("Gas"))
            {
                string code = dinputCodes["Gas"].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == InputMapping.Analog2 && !j.HideWithDirectInput);
                if (xmlPlace != null)
                    CreateAxisButton(xmlPlace, diGuid, code, InputMapping.Analog2);
            }

            // Brake (Analog4)
            if (dinputCodes.ContainsKey("Brake"))
            {
                string code = dinputCodes["Brake"].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == InputMapping.Analog4 && !j.HideWithDirectInput);
                if (xmlPlace != null)
                    CreateAxisButton(xmlPlace, diGuid, code, InputMapping.Analog4);
            }

            // Handle ES features for Coin & Start (wheel, gamepad, keyboard)
            string coinStartOpt = Program.SystemConfig["wheel_coin_start"];
            if (coinStartOpt == "keyboard")
            {
                BindKeyboardKey(userProfile, InputMapping.Coin1, Keys.D5, "D5");
                BindKeyboardKey(userProfile, InputMapping.JvsTwoCoin1, Keys.D5, "D5");
                BindKeyboardKey(userProfile, InputMapping.P1ButtonStart, Keys.D1, "D1");
                BindKeyboardKey(userProfile, InputMapping.JvsTwoP1ButtonStart, Keys.D1, "D1");
                SimpleLogger.Instance.Info("[WHEELS] Mapped Coin/Start to Keyboard D5/D1");
            }
            else if (coinStartOpt == "gamepad")
            {
                Controller p1Controller = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && !c.IsKeyboard && c.DeviceIndex != w1.ControllerIndex);
                if (p1Controller != null)
                {
                    ImportDirectInputButton(userProfile, p1Controller, InputKey.select, InputMapping.Coin1, InputMapping.JvsTwoCoin1);
                    ImportDirectInputButton(userProfile, p1Controller, InputKey.start, InputMapping.P1ButtonStart, InputMapping.JvsTwoP1ButtonStart);
                    SimpleLogger.Instance.Info("[WHEELS] Mapped Coin/Start to P1 Controller Select/Start");
                }
            }

            // Handle ES features for Test & Service (wheel, gamepad, keyboard)
            string testServiceOpt = Program.SystemConfig["wheel_test_service"];
            if (testServiceOpt == "keyboard")
            {
                BindKeyboardKey(userProfile, InputMapping.Test, Keys.D0, "D0");
                BindKeyboardKey(userProfile, InputMapping.JvsTwoTest, Keys.D0, "D0");
                BindKeyboardKey(userProfile, InputMapping.Service1, Keys.D9, "D9");
                BindKeyboardKey(userProfile, InputMapping.JvsTwoService1, Keys.D9, "D9");
                SimpleLogger.Instance.Info("[WHEELS] Mapped Test/Service to Keyboard D0/D9");
            }
            else if (testServiceOpt == "gamepad")
            {
                Controller p1Controller = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && !c.IsKeyboard && c.DeviceIndex != w1.ControllerIndex);
                if (p1Controller != null)
                {
                    ImportDirectInputButton(userProfile, p1Controller, InputKey.l3, InputMapping.Test, InputMapping.JvsTwoTest);
                    ImportDirectInputButton(userProfile, p1Controller, InputKey.r3, InputMapping.Service1, InputMapping.JvsTwoService1);
                    SimpleLogger.Instance.Info("[WHEELS] Mapped Test/Service to P1 Controller L3/R3");
                }
            }

            SimpleLogger.Instance.Info("[WHEELS] Wheel configuration applied.");
            return true;
        }

        private static void BindKeyboardKey(GameProfile userProfile, InputMapping mapping, Keys key, string bindName)
        {
            var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == mapping && !j.HideWithRawInput);
            if (xmlPlace == null)
                return;

            xmlPlace.DirectInputButton = new JoystickButton
            {
                JoystickGuid = new Guid("6f1d2b61-d5a0-11cf-bfc7-444553540000"),
                Button = (int)key,
                IsAxis = false,
                IsAxisMinus = false,
                IsFullAxis = false,
                IsReverseAxis = false,
                PovDirection = 0
            };

            xmlPlace.RawInputButton = new RawInputButton
            {
                DeviceType = RawDeviceType.Keyboard,
                KeyboardKey = key,
                MouseButton = RawMouseButton.None,
                DevicePath = null
            };

            xmlPlace.BindNameDi = "Keyboard Button " + bindName;
            xmlPlace.BindNameRi = bindName;
            xmlPlace.BindName = "Keyboard Button " + bindName;
        }

        private static void CreateAxisButton(JoystickButtons xmlPlace, Guid diGuid, string code, InputMapping inputEnum)
        {
            int axisId = 0;
            if (code.StartsWith("XAXIS")) axisId = 0;
            else if (code.StartsWith("YAXIS")) axisId = 1;
            else if (code.StartsWith("ZAXIS")) axisId = 2;
            else if (code.StartsWith("RZAXIS")) axisId = 5;
            else if (code.StartsWith("RXAXIS")) axisId = 3;
            else if (code.StartsWith("RYAXIS")) axisId = 4;

            bool isMinus = code.EndsWith("_NEG");

            xmlPlace.DirectInputButton = new JoystickButton
            {
                JoystickGuid = diGuid,
                IsAxis = true,
                IsAxisMinus = isMinus,
                IsFullAxis = false,
                IsReverseAxis = false,
                Button = axisId * 4,
                PovDirection = 0
            };
            xmlPlace.BindNameDi = "Driving " + code;
            xmlPlace.BindName = xmlPlace.BindNameDi;
        }
    }
}