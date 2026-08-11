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
        // Function alias → canonical TAG matching keys in teknoparrot_wheels.yml
        // Rule: one physical DirectInput button = one canonical key in the YML
        // Aliases here are all the alternative names a game can use for that physical button
        private readonly static Dictionary<string, string> wheelAliasToRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Axes (canonical keys in YML: SteerLeft/SteerRight/Gas/Brake/AnalogY)
            { "steer", "Steer" },
            { "gas", "Gas" },
            { "brake", "Brake" },
            { "analogy", "AnalogY" },
            { "clutch", "AnalogY" },

            // System (canonical keys: Start, Coin, Test, Service)
            { "start", "Start" },
            { "coin", "Coin" },
            { "select", "Coin" },
            { "test", "Test" },
            { "service", "Service" },

            // Shifter & Paddles (canonical: GearUp/GearDown/Gear1-6/GearReverse)
            { "gearup", "GearUp" },
            { "geardown", "GearDown" },
            { "paddleright", "GearUp" },   // PaddleRight → GearUp (BUTTON5)
            { "paddleleft", "GearDown" },  // PaddleLeft → GearDown (BUTTON6)
            { "gearleft", "GearDown" },
            { "gearright", "GearUp" },
            { "gear1", "Gear1" },
            { "gear2", "Gear2" },
            { "gear3", "Gear3" },
            { "gear4", "Gear4" },
            { "gear5", "Gear5" },
            { "gear6", "Gear6" },
            { "gearreverse", "GearReverse" },

            // Boost/Nitro (canonical: Boost → BUTTON7)
            { "boost", "Boost" },
            { "nitro", "Boost" },          // Nitro → Boost (BUTTON7)
            { "attack", "Boost" },

            // Handbrake
            { "handbrake", "Handbrake" },
            { "sidebrake", "Handbrake" },

            // Face Buttons (canonical: South/East/West/North)
            { "south", "South" },           // BUTTON1
            { "east", "East" },             // BUTTON2
            { "west", "West" },             // BUTTON3
            { "north", "North" },           // BUTTON4
            { "insertcard", "South" },      // InsertCard → South (BUTTON1)
            { "lookbehind2", "South" },     // LookBehind2 → South (BUTTON1)
            { "lookbehind", "West" },       // LookBehind → West (BUTTON3)
            { "resetbutton", "East" },      // ResetButton → East (BUTTON2)
            { "replaybutton", "North" },    // ReplayButton → North (BUTTON4)

            // Views → Menu (canonical: MenuUp/MenuDown/MenuLeft/MenuRight)
            { "viewchange", "MenuUp" },     // ViewChange → MenuUp (POV1_UP)
            { "viewchange2", "MenuRight" }, // ViewChange2 → MenuRight (POV1_RIGHT)
            { "viewchange3", "MenuLeft" },  // ViewChange3 → MenuLeft (POV1_LEFT)
            { "viewchange4", "MenuDown" },  // ViewChange4 → MenuDown (POV1_DOWN)
            { "intrudechange", "MenuDown" },// IntrudeChange → MenuDown (POV1_DOWN)

            // Menu / D-Pad (canonical: MenuUp/MenuDown/MenuLeft/MenuRight)
            { "menuup", "MenuUp" },
            { "menudown", "MenuDown" },
            { "menuleft", "MenuLeft" },
            { "menuright", "MenuRight" }
        };

        private static void LoadGameButtonMappingsFromYml(YmlFile ymlFile, Dictionary<string, Dictionary<string, string>> gameButtonMappings)
        {
            if (ymlFile == null || ymlFile.Elements == null)
                return;

            foreach (var rootEntry in ymlFile.Elements)
            {
                var gameContainer = rootEntry as YmlContainer;
                if (gameContainer == null)
                    continue;

                string gameName = gameContainer.Name.ToLowerInvariant();
                if (!gameButtonMappings.ContainsKey(gameName))
                    gameButtonMappings[gameName] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var mappings = gameButtonMappings[gameName];
                foreach (var mappingEntry in gameContainer.Elements)
                {
                    var mappingEl = mappingEntry as YmlElement;
                    if (mappingEl != null && mappingEl.Value != null)
                        mappings[mappingEl.Name] = mappingEl.Value.Trim('"', '\'', ' ');
                }
            }
        }

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

            else if (code.StartsWith("SLIDER"))
            {
                string sign = code.EndsWith("_NEG") ? " -" : " +";
                string sliderNum = code.Contains("1") ? "1" : "0";
                return "Driving Sliders" + sliderNum + sign;
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

            if (userProfile == null || string.IsNullOrEmpty(userProfile.FileName))
            {
                SimpleLogger.Instance.Warning("[WHEELS] No user profile, cannot resolve game mapping.");
                return false;
            }

            string tpGameName = Path.GetFileNameWithoutExtension(userProfile.FileName).ToLowerInvariant();

            // Handle BattleGear4Tuned Shift Mode
            if (tpGameName == "battlegear4tuned")
            {
                string bg4tShiftMode = Program.SystemConfig.isOptSet("bg4t_shiftmode") ? Program.SystemConfig["bg4t_shiftmode"] : "paddles";
                
                if (!string.IsNullOrEmpty(bg4tShiftMode) && bg4tShiftMode != "paddles")
                {
                    // Sequential mode
                    if (bg4tShiftMode == "sequential")
                    {
                        tpGameName = "battlegear4tuned_sequential";
                        SimpleLogger.Instance.Info("[WHEELS] Using BattleGear4Tuned Sequential mapping");
                    }
                    // Pro Mode Sequential
                    else if (bg4tShiftMode == "prosequential")
                    {
                        tpGameName = "battlegear4tuned_prosequential";
                        // Enable Professional Edition
                        var proModeEnable = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Professional Edition Enable");
                        if (proModeEnable != null)
                        {
                            proModeEnable.FieldValue = "1";
                            SimpleLogger.Instance.Info("[WHEELS] BattleGear4Tuned Pro Mode enabled");
                        }
                        // Disable Hold Gear for Pro Sequential mode (auto-neutral)
                        var proModeHoldGear = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Professional Edition Hold Gear");
                        if (proModeHoldGear != null)
                        {
                            proModeHoldGear.FieldValue = "0";
                            SimpleLogger.Instance.Info("[WHEELS] BattleGear4Tuned Pro Mode Hold Gear disabled (sequential mode)");
                        }
                        SimpleLogger.Instance.Info("[WHEELS] Using BattleGear4Tuned Pro Sequential mapping");
                    }
                    // H-pattern mode (Pro Mode)
                    else if (bg4tShiftMode == "hpattern")
                    {
                        tpGameName = "battlegear4tuned_hpattern";
                        // Enable Professional Edition
                        var proModeEnable = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Professional Edition Enable");
                        if (proModeEnable != null)
                        {
                            proModeEnable.FieldValue = "1";
                            SimpleLogger.Instance.Info("[WHEELS] BattleGear4Tuned Pro Mode enabled");
                        }
                        // Enable Hold Gear for H-pattern mode
                        var proModeHoldGear = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Professional Edition Hold Gear");
                        if (proModeHoldGear != null)
                        {
                            proModeHoldGear.FieldValue = "1";
                            SimpleLogger.Instance.Info("[WHEELS] BattleGear4Tuned Pro Mode Hold Gear enabled (H-pattern mode)");
                        }
                        SimpleLogger.Instance.Info("[WHEELS] Using BattleGear4Tuned H-pattern mapping");
                    }
                }
                else
                {
                    // Ensure Pro Mode is disabled for default paddle mode
                    var proModeEnable = userProfile.ConfigValues.FirstOrDefault(c => c.FieldName == "Professional Edition Enable");
                    if (proModeEnable != null)
                    {
                        proModeEnable.FieldValue = "0";
                        SimpleLogger.Instance.Info("[WHEELS] BattleGear4Tuned Pro Mode disabled (paddle mode)");
                    }
                }
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

            // Read game-specific button mappings from teknoparrot_wheel_mapping.yml
            var gameButtonMappings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            string wheelMappingyml = Controller.GetSystemYmlMappingFile("teknoparrot", "", "teknoparrot", wheelMappingPaths);

            if (wheelMappingyml != null && File.Exists(wheelMappingyml))
            {
                SimpleLogger.Instance.Info("[WHEELS] Using game mapping file: " + wheelMappingyml);
                LoadGameButtonMappingsFromYml(YmlFile.Load(wheelMappingyml), gameButtonMappings);
            }
            else
                SimpleLogger.Instance.Warning("[WHEELS] File teknoparrot_wheel_mapping.yml does not exist.");

            SimpleLogger.Instance.Info("[WHEELS] Loaded " + gameButtonMappings.Count + " game-specific button mappings.");

            // Handle Custom Profile for any game
            string customProfile = Program.SystemConfig.isOptSet("custom_profile") ? Program.SystemConfig["custom_profile"] : "default";
            if (!string.IsNullOrEmpty(customProfile) && customProfile != "default")
            {
                string customGameName = tpGameName + "_" + customProfile;
                
                // Check if custom profile exists in the game mappings
                if (gameButtonMappings.ContainsKey(customGameName))
                {
                    tpGameName = customGameName;
                    SimpleLogger.Instance.Info("[WHEELS] Using custom profile: " + customGameName);
                }
                else
                {
                    SimpleLogger.Instance.Info("[WHEELS] Custom profile " + customGameName + " not found, using default: " + tpGameName);
                }
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
                { "GearDown", new[] { InputMapping.Wmmt5GearChangeDown, InputMapping.SrcGearChangeDown, InputMapping.FnfGearChangeDown, InputMapping.IDZGearChangeDown } },
                { "Gear1", new[] { InputMapping.Wmmt5GearChange1, InputMapping.SrcGearChange1, InputMapping.FnfGearChange1, InputMapping.IDZGearChange1 } },
                { "Gear2", new[] { InputMapping.Wmmt5GearChange2, InputMapping.SrcGearChange2, InputMapping.FnfGearChange2, InputMapping.IDZGearChange2 } },
                { "Gear3", new[] { InputMapping.Wmmt5GearChange3, InputMapping.SrcGearChange3, InputMapping.FnfGearChange3, InputMapping.IDZGearChange3 } },
                { "Gear4", new[] { InputMapping.Wmmt5GearChange4, InputMapping.SrcGearChange4, InputMapping.FnfGearChange4, InputMapping.IDZGearChange4 } },
                { "Gear5", new[] { InputMapping.Wmmt5GearChange5, InputMapping.IDZGearChange5 } },
                { "Gear6", new[] { InputMapping.Wmmt5GearChange6, InputMapping.IDZGearChange6 } },
                { "GearReverse", new[] { InputMapping.Wmmt5GearChange6, InputMapping.IDZGearChange6, InputMapping.P1Button5 } },
                { "ViewChange", new[] { InputMapping.ExtensionOne1 } },
                { "ViewChange2", new InputMapping[] { } },
                { "ViewChange3", new InputMapping[] { } },
                { "ViewChange4", new InputMapping[] { } },
                { "PaddleLeft", new InputMapping[] { } },
                { "PaddleRight", new InputMapping[] { } },
                { "IntrudeChange", new[] { InputMapping.ExtensionOne2 } },
                { "InsertCard", new[] { InputMapping.Wmmt3InsertCard } },
                { "Test", new[] { InputMapping.Test, InputMapping.JvsTwoTest } },
                { "Service", new[] { InputMapping.Service1, InputMapping.JvsTwoService1 } },
                { "Coin", new[] { InputMapping.Coin1, InputMapping.JvsTwoCoin1 } },
                { "Start", new[] { InputMapping.P1ButtonStart, InputMapping.JvsTwoP1ButtonStart } },
                { "Handbrake", new[] { InputMapping.P1Button4, InputMapping.P1ButtonDown, InputMapping.ExtensionOne3 } },
                { "Boost", new[] { InputMapping.P1Button4, InputMapping.P1Button5 } },
                { "South", new[] { InputMapping.P1Button1 } },
                { "East", new[] { InputMapping.P1Button2 } },
                { "West", new[] { InputMapping.P1Button3 } },
                { "North", new[] { InputMapping.P1Button4 } },
                { "ResetButton", new[] { InputMapping.P1Button2 } },
                { "ReplayButton", new[] { InputMapping.P1ButtonUp } },
                { "LookBehind", new[] { InputMapping.P1Button3 } },
                { "LookBehind2", new[] { InputMapping.P1ButtonDown } },
                { "MenuUp", new[] { InputMapping.P1ButtonUp } },
                { "MenuDown", new[] { InputMapping.P1ButtonDown } },
                { "MenuLeft", new[] { InputMapping.P1ButtonLeft } },
                { "MenuRight", new[] { InputMapping.P1ButtonRight } },
            };

            // Steer/Gas/Brake/AnalogY axis slot overrides: default Analog0/Analog2/Analog4, configurable per game via SteerMapping/GasMapping/BrakeMapping/AnalogYMapping in YML
            InputMapping steerMappingSlot = InputMapping.Analog0;
            InputMapping gasMappingSlot = InputMapping.Analog2;
            InputMapping brakeMappingSlot = InputMapping.Analog4;
            InputMapping? analogYMappingSlot = null; // Optional: only injected if game declares AnalogYMapping
            InputMapping? clutchMappingSlot = null; // Optional: only injected if game declares ClutchMapping
            
            // Track which axis slots have direct mappings to skip default processing
            var directMappedAxisSlots = new HashSet<InputMapping>();

            // Track role keys overridden by Syntax 2 entries (tag: InputMappingEnum), used by Pass 1/2
            var gameSpecificKeys = new HashSet<string>();

            // Direct slot mappings from Syntax 1: InputMapping → DInput codeKey (role)
            // Syntax 1 (e.g. "P1Button1: boost") means: "apply the DInput code of 'Boost' directly to the P1Button1 XML slot"
            var gameDirectSlotMappings = new Dictionary<InputMapping, string>();

            // Apply game-specific button mappings if available for this game
            bool gearConverterEnabled = false;
            bool noGearStick = Program.SystemConfig.getOptBoolean("wheel_nogearstick");
            
            // Only enable converter if there IS a gear stick (not nogearstick) and not for BattleGear4Tuned
            if (!noGearStick && !tpGameName.StartsWith("battlegear4tuned"))
            {
                string gearConverter = Program.SystemConfig.isOptSet("gearupdown_to_gear34") ? Program.SystemConfig["gearupdown_to_gear34"] : "disabled";
                gearConverterEnabled = (!string.IsNullOrEmpty(gearConverter) && gearConverter == "enabled");
                if (gearConverterEnabled)
                    SimpleLogger.Instance.Info("[WHEELS] GearUp/GearDown to Gear3/Gear4 converter enabled");
            }

            if (gameButtonMappings.ContainsKey(tpGameName))
            {
                var gameMappings = gameButtonMappings[tpGameName];
                foreach (var gm in gameMappings)
                {
                    string k = gm.Key;
                    string v = gm.Value != null ? gm.Value.Trim('"', '\'', ' ') : null;
                    if (string.IsNullOrEmpty(v)) continue;

                    // Apply GearUp/GearDown to Gear3/Gear4 conversion if enabled
                    if (gearConverterEnabled)
                    {
                        if (v.Equals("gearup", StringComparison.OrdinalIgnoreCase))
                            v = "gear4";
                        else if (v.Equals("geardown", StringComparison.OrdinalIgnoreCase))
                            v = "gear3";
                        // Skip original gear3/gear4 mappings when converter is enabled
                        else if (v.Equals("gear3", StringComparison.OrdinalIgnoreCase) || v.Equals("gear4", StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    // Syntax 1: InputMappingEnum: tag (e.g. P1ButtonStart: boost, Analog0: steer)
                    // Meaning: "apply the physical DInput code of the role 'tag' to this specific InputMapping slot"
                    if (Enum.TryParse(k, out InputMapping targetInputFromKey))
                    {
                        if (v.Equals("steer", StringComparison.OrdinalIgnoreCase) || v.Equals("steermapping", StringComparison.OrdinalIgnoreCase))
                            steerMappingSlot = targetInputFromKey;
                        else if (v.Equals("gas", StringComparison.OrdinalIgnoreCase) || v.Equals("gasmapping", StringComparison.OrdinalIgnoreCase))
                            gasMappingSlot = targetInputFromKey;
                        else if (v.Equals("brake", StringComparison.OrdinalIgnoreCase) || v.Equals("brakemapping", StringComparison.OrdinalIgnoreCase))
                            brakeMappingSlot = targetInputFromKey;
                        else if (v.Equals("analogy", StringComparison.OrdinalIgnoreCase) || v.Equals("analogymapping", StringComparison.OrdinalIgnoreCase))
                            analogYMappingSlot = targetInputFromKey;
                        else if (v.Equals("clutch", StringComparison.OrdinalIgnoreCase) || v.Equals("clutchmapping", StringComparison.OrdinalIgnoreCase))
                            clutchMappingSlot = targetInputFromKey;
                        else if (wheelAliasToRole.TryGetValue(v, out string targetRoleFromVal))
                        {
                            // Store: this XML slot will receive the DInput code of 'targetRoleFromVal' directly
                            gameDirectSlotMappings[targetInputFromKey] = targetRoleFromVal;
                            // Track if this is an axis slot to skip default processing
                            if (targetInputFromKey.ToString().StartsWith("Analog"))
                                directMappedAxisSlots.Add(targetInputFromKey);
                            SimpleLogger.Instance.Info("[WHEELS] Game-specific mapping: " + targetInputFromKey + " -> " + targetRoleFromVal);
                        }
                    }
                    // Syntax 2: tag: InputMappingEnum (e.g. Boost: P1ButtonStart, SteerMapping: Analog0)
                    // Meaning: "the role 'tag' now targets this InputMapping instead of the default"
                    else
                    {
                        if (k.Equals("SteerMapping", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(v, out InputMapping customSteer))
                            steerMappingSlot = customSteer;
                        else if (k.Equals("GasMapping", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(v, out InputMapping customGas))
                            gasMappingSlot = customGas;
                        else if (k.Equals("BrakeMapping", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(v, out InputMapping customBrake))
                            brakeMappingSlot = customBrake;
                        else if (k.Equals("AnalogYMapping", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(v, out InputMapping customAnalogY))
                            analogYMappingSlot = customAnalogY;
                        else if (k.Equals("ClutchMapping", StringComparison.OrdinalIgnoreCase) && Enum.TryParse(v, out InputMapping customClutch))
                            clutchMappingSlot = customClutch;
                        else if (Enum.TryParse(v, out InputMapping customMappingFromVal))
                        {
                            string resolvedRoleKey = wheelAliasToRole.ContainsKey(k) ? wheelAliasToRole[k] : k;
                            // REPLACE (not concat) so the game-specific override is exclusive
                            buttonToInputMappings[resolvedRoleKey] = new[] { customMappingFromVal };
                            gameSpecificKeys.Add(resolvedRoleKey);
                            SimpleLogger.Instance.Info("[WHEELS] Game-specific mapping (override): " + resolvedRoleKey + " -> " + customMappingFromVal);
                        }
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

            // Check if game has explicit mappings for gear slots to skip automatic defaults
            // Also check for explicit null/disabled values
            HashSet<string> disabledSlots = new HashSet<string>();
            bool hasExplicitGear3 = gameSpecificKeys.Contains("Wmmt5GearChange3") || 
                                   gameSpecificKeys.Contains("SrcGearChange3") ||
                                   gameSpecificKeys.Contains("FnfGearChange3") ||
                                   gameSpecificKeys.Contains("IDZGearChange3");
            bool hasExplicitGear4 = gameSpecificKeys.Contains("Wmmt5GearChange4") || 
                                   gameSpecificKeys.Contains("SrcGearChange4") ||
                                   gameSpecificKeys.Contains("FnfGearChange4") ||
                                   gameSpecificKeys.Contains("IDZGearChange4");
            
            // Check for explicit null/disabled values in game-specific mappings
            if (gameButtonMappings.ContainsKey(tpGameName))
            {
                var gameMappings = gameButtonMappings[tpGameName];
                foreach (var gm in gameMappings)
                {
                    string v = gm.Value != null ? gm.Value.Trim('"', '\'', ' ') : null;
                    if (v != null && (v.Equals("null", StringComparison.OrdinalIgnoreCase) || 
                                     v.Equals("disabled", StringComparison.OrdinalIgnoreCase) ||
                                     v.Equals("none", StringComparison.OrdinalIgnoreCase)))
                    {
                        disabledSlots.Add(gm.Key);
                    }
                }
            }

            // Pass 1: Apply generic defaults first (skip game-specific overridden keys)
            foreach (var btnEntry in buttonToInputMappings)
            {
                if (!gameSpecificKeys.Contains(btnEntry.Key))
                {
                    // Skip any slots that are explicitly disabled in YAML
                    bool skipMapping = false;
                    foreach (var slot in btnEntry.Value)
                    {
                        string slotName = slot.ToString();
                        if (disabledSlots.Contains(slotName))
                        {
                            skipMapping = true;
                            break;
                        }
                    }
                    if (skipMapping) continue;
                    
                    // Skip Gear3/Gear4 default mappings when converter is enabled OR when explicit YAML mappings exist
                    if (gearConverterEnabled || hasExplicitGear3 || hasExplicitGear4)
                    {
                        bool skipGearMapping = false;
                        foreach (var slot in btnEntry.Value)
                        {
                            if (slot == InputMapping.Wmmt5GearChange3 || slot == InputMapping.Wmmt5GearChange4 ||
                                slot == InputMapping.SrcGearChange3 || slot == InputMapping.SrcGearChange4 ||
                                slot == InputMapping.FnfGearChange3 || slot == InputMapping.FnfGearChange4 ||
                                slot == InputMapping.IDZGearChange3 || slot == InputMapping.IDZGearChange4)
                            {
                                skipGearMapping = true;
                                break;
                            }
                        }
                        if (skipGearMapping) continue;
                    }
                    mapButtonMulti(btnEntry.Key, btnEntry.Value);
                }
            }

            // Pass 2: Apply game-specific overrides LAST so they always win over defaults
            foreach (var key in gameSpecificKeys)
            {
                if (buttonToInputMappings.ContainsKey(key))
                {
                    // Skip any game-specific slots that are explicitly disabled in YAML
                    if (disabledSlots.Contains(key))
                        continue;
                    
                    // Skip Gear3/Gear4 game-specific mappings when converter is enabled
                    if (gearConverterEnabled)
                    {
                        bool skipMapping = false;
                        foreach (var slot in buttonToInputMappings[key])
                        {
                            if (slot == InputMapping.Wmmt5GearChange3 || slot == InputMapping.Wmmt5GearChange4 ||
                                slot == InputMapping.SrcGearChange3 || slot == InputMapping.SrcGearChange4 ||
                                slot == InputMapping.FnfGearChange3 || slot == InputMapping.FnfGearChange4 ||
                                slot == InputMapping.IDZGearChange3 || slot == InputMapping.IDZGearChange4)
                            {
                                skipMapping = true;
                                break;
                            }
                        }
                        if (skipMapping) continue;
                    }
                    mapButtonMulti(key, buttonToInputMappings[key]);
                }
            }

            // Pass 3: Apply direct slot mappings from Syntax 1 (e.g. "P1Button1: boost", "Analog6: gas")
            // Applies the DInput code of the named role directly to the exact XML slot, LAST to guarantee priority.
            foreach (var directEntry in gameDirectSlotMappings)
            {
                InputMapping targetSlot = directEntry.Key;
                string roleKey = directEntry.Value;

                if (!dinputCodes.ContainsKey(roleKey) || string.IsNullOrEmpty(dinputCodes[roleKey]))
                    continue;

                string code = dinputCodes[roleKey].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == targetSlot && !j.HideWithDirectInput);
                if (xmlPlace == null)
                    continue;

                // Handle axis codes for direct slot mappings
                if (code.StartsWith("XAXIS") || code.StartsWith("YAXIS") || code.StartsWith("ZAXIS") || 
                    code.StartsWith("RZAXIS") || code.StartsWith("RXAXIS") || code.StartsWith("RYAXIS") ||
                    code.StartsWith("SLIDER"))
                {
                    CreateAxisButton(xmlPlace, diGuid, code, targetSlot);
                    SimpleLogger.Instance.Info("[WHEELS] Direct axis slot mapping: " + targetSlot + " -> " + code + " (role: " + roleKey + ")");
                    continue;
                }

                var diButton = new JoystickButton
                {
                    JoystickGuid = diGuid,
                    IsAxis = false, IsAxisMinus = false, IsFullAxis = false, IsReverseAxis = false, PovDirection = 0
                };

                if (code.StartsWith("BUTTON"))
                {
                    int btnNum = int.Parse(code.Substring(6));
                    diButton.Button = btnNum + 47;
                }
                else if (code.StartsWith("POV"))
                {
                    diButton.Button = 32;
                    if (code.Contains("UP")) diButton.PovDirection = 0;
                    else if (code.Contains("DOWN")) diButton.PovDirection = 18000;
                    else if (code.Contains("LEFT")) diButton.PovDirection = 27000;
                    else if (code.Contains("RIGHT")) diButton.PovDirection = 9000;
                }

                xmlPlace.DirectInputButton = diButton;
                xmlPlace.BindNameDi = GetDrivingBindName(code);
                xmlPlace.BindName = xmlPlace.BindNameDi;
                SimpleLogger.Instance.Info("[WHEELS] Direct slot mapping: " + targetSlot + " -> " + xmlPlace.BindNameDi + " (role: " + roleKey + ")");
            }

            // Map axes (steering, gas, brake) - skip if directly mapped via game-specific mappings
            if (!directMappedAxisSlots.Contains(steerMappingSlot) && dinputCodes.ContainsKey("SteerLeft") && dinputCodes.ContainsKey("SteerRight"))
            {
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == steerMappingSlot && !j.HideWithDirectInput);
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
                    SimpleLogger.Instance.Info("[WHEELS] Bound Steering to " + xmlPlace.BindNameDi + " (slot " + steerMappingSlot + ")");
                }
            }

            // Gas - slot configurable via GasMapping in YML (default: Analog2) - skip if directly mapped
            if (!directMappedAxisSlots.Contains(gasMappingSlot) && dinputCodes.ContainsKey("Gas"))
            {
                string code = dinputCodes["Gas"].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == gasMappingSlot && !j.HideWithDirectInput);
                if (xmlPlace != null)
                    CreateAxisButton(xmlPlace, diGuid, code, gasMappingSlot);
            }

            // Brake - slot configurable via BrakeMapping in YML (default: Analog4) - skip if directly mapped
            if (!directMappedAxisSlots.Contains(brakeMappingSlot) && dinputCodes.ContainsKey("Brake"))
            {
                string code = dinputCodes["Brake"].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == brakeMappingSlot && !j.HideWithDirectInput);
                if (xmlPlace != null)
                    CreateAxisButton(xmlPlace, diGuid, code, brakeMappingSlot);
            }

            // AnalogY - optional secondary axis (e.g. clutch pedal), only injected if game declares AnalogYMapping in YML
            if (analogYMappingSlot.HasValue && dinputCodes.ContainsKey("AnalogY"))
            {
                string code = dinputCodes["AnalogY"].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == analogYMappingSlot.Value && !j.HideWithDirectInput);
                if (xmlPlace != null)
                {
                    CreateAxisButton(xmlPlace, diGuid, code, analogYMappingSlot.Value);
                    SimpleLogger.Instance.Info("[WHEELS] Bound AnalogY (" + code + ") to slot " + analogYMappingSlot.Value);
                }
            }

            // Clutch - optional clutch axis, only injected if game declares ClutchMapping in YML
            if (clutchMappingSlot.HasValue && dinputCodes.ContainsKey("AnalogY"))
            {
                string code = dinputCodes["AnalogY"].ToUpperInvariant();
                var xmlPlace = userProfile.JoystickButtons.FirstOrDefault(j => j.InputMapping == clutchMappingSlot.Value && !j.HideWithDirectInput);
                if (xmlPlace != null)
                {
                    CreateAxisButton(xmlPlace, diGuid, code, clutchMappingSlot.Value);
                    SimpleLogger.Instance.Info("[WHEELS] Bound Clutch (" + code + ") to slot " + clutchMappingSlot.Value);
                }
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
            else if (code.StartsWith("SLIDER0") || code.StartsWith("SLIDERS0")) axisId = 6;
            else if (code.StartsWith("SLIDER1") || code.StartsWith("SLIDERS1")) axisId = 7;

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
            xmlPlace.BindNameDi = GetDrivingBindName(code);
            xmlPlace.BindName = xmlPlace.BindNameDi;
        }

        static readonly string[] wheelMappingPaths =
        {
            // User specific
            "{userpath}\\inputmapping\\teknoparrot_wheel_mapping.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\teknoparrot_wheel_mapping.yml",
        };
    }
}