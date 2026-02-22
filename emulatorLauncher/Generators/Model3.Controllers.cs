using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using DI = SharpDX.DirectInput;

namespace EmulatorLauncher
{
    partial class Model3Generator : Generator
    {
        private bool _sindenSoft = false;

        // Gun configuration variables
        private string _mouse1 = "MOUSE1";
        private string _mouse2 = "MOUSE2";
        private bool _multigun = false;
        private int _gunCount = 0;

        /// <summary>
        /// Cf. https://github.com/trzy/Supermodel
        /// </summary>
        /// <param name="ini"></param>
        private void UpdateSdlControllersWithHints()
        {
            var hints = new List<string>
            {
                "SDL_JOYSTICK_HIDAPI_WII = 0"
            };

            SdlGameController.ReloadWithHints(string.Join(",", hints));
            Program.Controllers.ForEach(c => c.ResetSdlController());
        }

        private void DefineGunIndexes(IniFile ini, out RawLightgun[] guns)
        {
            guns = RawLightgun.GetRawLightguns();
            _gunCount = RawLightgun.GetUsableLightGunCount();
            _multigun = _gunCount > 1;
            if (SystemConfig.isOptSet("multigun") && !SystemConfig.getOptBoolean("multigun"))
                _multigun = false;

            bool useGun = SystemConfig.getOptBoolean("use_guns");
            string mouseIndex1 = "1";
            string mouseIndex2 = "2";

            if (_gunCount > 0 && guns.Length > 0)
            {
                mouseIndex1 = (guns[0].Index + 1).ToString();
                if (_gunCount > 1 && guns.Length > 1)
                    mouseIndex2 = (guns[1].Index + 1).ToString();

                if (useGun && guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
                {
                    Guns.StartSindenSoftware();
                    _sindenSoft = true;
                }
            }

            if (SystemConfig.isOptSet("supermodel_gun1") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun1"]))
                mouseIndex1 = SystemConfig["supermodel_gun1"];
            if (SystemConfig.isOptSet("supermodel_gun2") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun2"]))
                mouseIndex2 = SystemConfig["supermodel_gun2"];

            if (_multigun)
                ini.WriteValue(" Global ", "InputSystem", "rawinput");
            else
                ini.WriteValue(" Global ", "InputSystem", "dinput");

            _mouse1 = "MOUSE" + mouseIndex1;
            _mouse2 = "MOUSE" + mouseIndex2;

            if (!_multigun)
            {
                _mouse1 = _mouse2 = "MOUSE";
                ini.WriteValue(" Global ", "Crosshairs", "1");
            }
            else
                ini.WriteValue(" Global ", "Crosshairs", "3");
        }

        /// <summary>
        /// Create controller configuration
        /// </summary>
        /// <param name="ini"></param>
        private void CreateControllerConfiguration(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for SuperModel");

            UpdateSdlControllersWithHints();

            DefineGunIndexes(ini, out RawLightgun[] guns);

            //.ini file for supermodel has entry for 2 players.
            //If 2 controllers or more are connected : get controllers from p1 and p2
            //If only 1 controller is connected : get player 1 controller
            if (Program.Controllers.Count > 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                var c2 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                if (c1.IsKeyboard)
                    WriteKeyboardMapping(ini);
                else
                    WriteJoystickMapping(ini, c1, c2);
            }
            else if (Program.Controllers.Count == 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
                if (c1.IsKeyboard)
                    WriteKeyboardMapping(ini);
                else
                    WriteJoystickMapping(ini, c1);
            }
            else if (Program.Controllers.Count == 0)
                return;

            // Handle guns configuration LAST (after all other mappings)
            if (_gunCount > 0 && SystemConfig.getOptBoolean("use_guns"))
            {
                SimpleLogger.Instance.Info("[GUNS] Found " + _gunCount + " usable guns.");
                ConfigureGuns(ini, guns);
            }
        }

        /// <summary>
        /// Gamepad
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        // All mappings generated here are detailed in the wiki, this is the most balanced mapping after intensive testing, if users need different mapping they might disable autoconfiguration
        private void WriteJoystickMapping(IniFile ini, Controller c1, Controller c2 = null)
        {
            if (c1 == null || c1.Config == null)
                return;

            // We clear the InputSystem key for all sections because we're controlling the input system and
            // don't want to have a game-specific override in the existing configuration take presedence over our value.
            foreach (var section in ini.EnumerateSections().Where(s => s != " Global "))
            {
                ini.Remove(section, "InputSystem");
            }

            // Initialize values
            foreach (var input in inputValues)
                ini.Remove(" Global ", input);

            // Enumerate the same way as supermodel
            var diDevices = new DirectInputInfo().GetDinputDevices();

            //initialize tech : as default we will use sdlgamepad
            string tech = "sdlgamepad";

            //Variables n1 and n2 will be used to check if controllers are "nintendo", if this is the case, face buttons will be reverted
            string n1 = null;
            string n2 = null;
            
            //Check if controllers are NINTENDO, will be used to revert buttons for sdl
            if (c1.VendorID == USB_VENDOR.NINTENDO)
                n1 = "nintendo";
            if (c2 != null && c2.Config != null && c2.VendorID == USB_VENDOR.NINTENDO)
                n2 = "nintendo";

            else if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "sdlgamepad")
                tech = "sdlgamepad";
            else if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "sdl")
                tech = "sdl";
            else if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "xinput")
                tech = "xinput";
            else if (SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"] == "dinput")
                tech = "dinput";

            SimpleLogger.Instance.Info("[INFO] setting " + tech + " inputdriver in SuperModel.");

            // Wheels
            int wheelNb = 0;
            bool useWheel = SystemConfig.isOptSet("use_wheel") && SystemConfig.getOptBoolean("use_wheel");
            bool deportedShifter = false;
            int shifterID = -1;

            if (useWheel)
                SimpleLogger.Instance.Info("[WHEELS] Wheels enabled.");

            List<Wheel> usableWheels = new List<Wheel>();

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard))
            {
                SimpleLogger.Instance.Info("[WHEELS] Fetching Wheel model.");
                var drivingWheel = Wheel.GetWheelType(controller.DevicePath.ToUpperInvariant());
                SimpleLogger.Instance.Info("[WHEELS] Wheel model found : " + drivingWheel.ToString());

                if (drivingWheel != WheelType.Default)
                {
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

                    var wheel = usableWheels[0];
                    wheeltype = wheel.Type.ToString();
                    SimpleLogger.Instance.Info("[WHEELS] Wheeltype identified : " + wheeltype);

                    // Get mapping in yml file
                    
                    string model3WheelMapping = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "wheels", "model3_wheels.yml");
                    if (File.Exists(model3WheelMapping))
                    {
                        ymlFile = YmlFile.Load(model3WheelMapping);

                        wheelMapping = ymlFile.Elements.Where(c => c.Name == wheeltype).FirstOrDefault() as YmlContainer;

                        if (wheelMapping == null)
                        {
                            wheelMapping = ymlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;
                            if (wheelMapping == null)
                            {
                                SimpleLogger.Instance.Info("[WHEELS] No mapping exists for the wheel and Model3 emulator in yml file.");
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
                        SimpleLogger.Instance.Info("[WHEELS] Mapping file for Model3 does not exist.");
                        return;
                    }

                    c1 = this.Controllers.FirstOrDefault(c => c.DeviceIndex == wheel.ControllerIndex);
                    c2 = null;

                    if (wheel != null)
                    {
                        tech = "dinput";
                        SimpleLogger.Instance.Info("[WHEELS] Wheel " + tech + " index : " + (wheel.DinputIndex + 1));
                    }
                    else
                        SimpleLogger.Instance.Info("[WHEELS] Wheel " + wheel.DevicePath.ToString() + " not found as Gamepad.");

                    // Set force feedback by default if wheel supports it
                    if (SystemConfig.isOptSet("forceFeedback") && !SystemConfig.getOptBoolean("forceFeedback"))
                        ini.WriteValue(" Global ", "ForceFeedback", "0");
                    else
                        ini.WriteValue(" Global ", "ForceFeedback", "1");
                }
            }

            // Input indexes in supermodel are by enum
            int j1index, j2index = -1;

            if (tech.StartsWith("sdl"))
            {
                j1index = c1.DeviceIndex + 1;

                if (c2 != null && c2.Config != null)
                    j2index = c2.DeviceIndex + 1;
            }

            else
            {
                if (c1.DirectInput != null)
                {
                    var dinputDevice1 = diDevices.Where(d => d.InstanceGuid == c1.DirectInput.InstanceGuid).FirstOrDefault();

                    if (dinputDevice1 != null)
                    {
                        j1index = diDevices.IndexOf(dinputDevice1) + 1;
                        SimpleLogger.Instance.Info("[INFO] Defined player 1 index based on dinput enumeration.");
                    }
                    else
                        j1index = c1.DirectInput != null ? c1.DirectInput.DeviceIndex + 1 : c1.DeviceIndex + 1;
                }
                else
                    j1index = c1.DirectInput != null ? c1.DirectInput.DeviceIndex + 1 : c1.DeviceIndex + 1;

                if (c2 != null && c2.Config != null)
                {
                    if (c2.DirectInput != null)
                    {
                        var dinputDevice2 = diDevices.Where(d => d.InstanceGuid == c2.DirectInput.InstanceGuid).FirstOrDefault();
                        if (dinputDevice2 != null)
                        {
                            j2index = diDevices.IndexOf(dinputDevice2) + 1;
                            SimpleLogger.Instance.Info("[INFO] Defined player 2 index based on dinput enumeration.");
                        }
                        else
                            j2index = c2.DirectInput != null ? c2.DirectInput.DeviceIndex + 1 : c2.DeviceIndex + 1;
                    }
                    else
                        j2index = c2.DirectInput != null ? c2.DirectInput.DeviceIndex + 1 : c2.DeviceIndex + 1;
                }
            }

            // Force index if option is set in es_features
            if (SystemConfig.isOptSet("model3_p1index") && !string.IsNullOrEmpty(SystemConfig["model3_p1index"]))
            {
                j1index = SystemConfig["model3_p1index"].ToInteger();
                SimpleLogger.Instance.Info("[INFO] Forcing index of joystick 1 to " + j1index.ToString());
            }
            if (SystemConfig.isOptSet("model3_p2index") && !string.IsNullOrEmpty(SystemConfig["model3_p2index"]))
            {
                j2index = SystemConfig["model3_p2index"].ToInteger();
                SimpleLogger.Instance.Info("[INFO] Forcing index of joystick 2 to " + j2index.ToString());
            }
            if (SystemConfig.isOptSet("wheel_index") && !string.IsNullOrEmpty(SystemConfig["wheel_index"]))
            {
                j1index = SystemConfig["wheel_index"].ToInteger();
                SimpleLogger.Instance.Info("[INFO] Forcing index of wheel/joystick to " + j1index.ToString());
            }

            bool multiplayer = j2index != -1;
            bool enableServiceMenu = SystemConfig.isOptSet("m3_service") && SystemConfig.getOptBoolean("m3_service");

            // Invert indexes option
            if (multiplayer && tech == "xinput")
            {
                if (SystemConfig.getOptBoolean("model3_indexswitch"))
                {
                    int tempIndex = j1index;
                    j1index = j2index;
                    j2index = tempIndex;
                }
            }

            if (c1 != null)
                SimpleLogger.Instance.Info("[INFO] setting index of joystick 1 to " + j1index.ToString() + " (" + c1.ToString()+")");
            if (c2 != null)
                SimpleLogger.Instance.Info("[INFO] setting index of joystick 2 to " + j2index.ToString() + " (" + c2.ToString()+")");

            SimpleLogger.Instance.Info("[INFO] Writing controls to emulator .ini file.");

            #region mapping yml file
            // Check if a mapping exists for the game in yml file
            YmlContainer game = null;
            YmlContainer gameLayout = null;
            
            string m3Mapping = null;
            SimpleLogger.Instance.Info("[INPUT] Looking for specific mapping in yml file.");

            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "user");

                m3Mapping = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(m3Mapping))
                    break;
            }

            string controLayout = "";
            if (Program.SystemConfig.isOptSet("controller_layout") && !string.IsNullOrEmpty(Program.SystemConfig["controller_layout"]))
                controLayout = Program.SystemConfig["controller_layout"];

            if (File.Exists(m3Mapping))
            {
                YmlFile ctrlYmlFile = YmlFile.Load(m3Mapping);

                game = ctrlYmlFile.Elements.Where(c => c.Name == (_romName + "_" + controLayout)).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ctrlYmlFile.Elements.Where(c => c.Name == _romName).FirstOrDefault() as YmlContainer;

                if (game != null)
                {
                    string searchYmlLayout = _romName + "_" + controLayout;
                    gameLayout = ctrlYmlFile.Elements.Where(c => c.Name == searchYmlLayout).FirstOrDefault() as YmlContainer;
                    if (gameLayout != null)
                        game = gameLayout;
                }

                else if (game == null)
                {
                    game = ctrlYmlFile.Elements.Where(g => _romName.StartsWith(g.Name)).OrderByDescending(g => g.Name.Length).FirstOrDefault() as YmlContainer;
                    if (game != null)
                    {
                        string searchYmlLayout = game.Name + "_" + controLayout;
                        gameLayout = ctrlYmlFile.Elements.Where(c => c.Name == searchYmlLayout).FirstOrDefault() as YmlContainer;
                        if (gameLayout != null)
                            game = gameLayout;
                    }
                }

                string defsearch = "default";
                if (!string.IsNullOrEmpty(controLayout))
                    defsearch = defsearch + "_" + controLayout;

                if (game == null)
                    game = ctrlYmlFile.Elements.Where(g => g.Name == defsearch).FirstOrDefault() as YmlContainer;

                if (game == null)
                    game = ctrlYmlFile.Elements.Where(g => g.Name == "default").FirstOrDefault() as YmlContainer;

                if (game != null)
                {
                    var gameName = game.Name;
                    var buttonMap = new Dictionary<string, string>();

                    foreach (var buttonEntry in game.Elements)
                    {
                        if (buttonEntry is YmlElement button)
                            buttonMap.Add(button.Name, button.Value);
                    }

                    if (buttonMap.Count > 0)
                    {
                        if (tech == "sdl")
                            tech = "sdlgamepad";

                        SdlToDirectInput ctrl1 = null;
                        SdlToDirectInput ctrl2 = null;

                        if (tech == "dinput")
                        {
                            string guid1 = (c1.Guid.ToString()).Substring(0, 24) + "00000000";
                            
                            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");

                            if (!File.Exists(gamecontrollerDB))
                            {
                                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                                gamecontrollerDB = null;
                            }

                            if (gamecontrollerDB != null)
                            {
                                SimpleLogger.Instance.Info("[INFO] Player 1. Fetching gamecontrollerdb.txt file with guid : " + guid1);

                                ctrl1 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1);

                                if (ctrl1 == null)
                                    SimpleLogger.Instance.Info("[INFO] Player 1. No controller found in gamecontrollerdb.txt file for guid : " + guid1);
                                else
                                    SimpleLogger.Instance.Info("[INFO] Player 1: " + guid1 + " found in gamecontrollerDB file.");

                                if (multiplayer && c2 != null)
                                {
                                    string guid2 = (c2.Guid.ToString()).Substring(0, 24) + "00000000";

                                    SimpleLogger.Instance.Info("[INFO] Player 2. Fetching gamecontrollerdb.txt file with guid : " + guid2);

                                    ctrl2 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid2);

                                    if (ctrl2 == null)
                                        SimpleLogger.Instance.Info("[INFO] Player 2. No controller found in gamecontrollerdb.txt file for guid : " + guid2);
                                    else
                                        SimpleLogger.Instance.Info("[INFO] Player 2: " + guid2 + " found in gamecontrollerDB file.");
                                }
                            }
                        }

                        SimpleLogger.Instance.Info("[INPUT] Specific mapping found in yml file.");
                        foreach (var button in buttonMap)
                        {
                            string buttonValue = "";
                            var values = button.Value.Split(',');

                            foreach (var value in values)
                            {
                                if (value.StartsWith("KEY"))
                                {
                                    if (string.IsNullOrEmpty(buttonValue))
                                        buttonValue += value;
                                    else
                                        buttonValue += "," + value;
                                }
                                else if (value.StartsWith("MOUSE"))
                                {
                                    bool invert = value.ToLowerInvariant().Contains("inv");
                                    string target = "";
                                    
                                    if (string.IsNullOrEmpty(buttonValue))
                                    {
                                        if (_multigun && button.Key.Contains("2"))
                                            target = value.Replace("MOUSE_", "MOUSE2_");
                                        else if (_multigun)
                                            target = value.Replace("MOUSE_", "MOUSE1_");
                                        else
                                            target = value;

                                        if (invert)
                                            target += "_INV";

                                        buttonValue += target;
                                    }
                                    else
                                    {
                                        if (_multigun && button.Key.Contains("2"))
                                            target = "," + value.Replace("MOUSE_", "MOUSE2_");
                                        else if (_multigun)
                                            target = "," + value.Replace("MOUSE_", "MOUSE1_");
                                        else
                                            target = "," + value;

                                        if (invert)
                                            target += "_INV";

                                        buttonValue += target;
                                    }
                                }

                                else if (value.StartsWith("JOY"))
                                {
                                    string target = "";

                                    if (value.Contains("_"))
                                    {
                                        if (c2 == null && p2values.Contains(button.Key))
                                            continue;

                                        bool invert = value.ToLowerInvariant().Contains("inv");
                                        string toMap = value.Split('_')[1];
                                        string toFetch = "";
                                        int direction = 0;

                                        switch (tech)
                                        {
                                            case "sdlgamepad":
                                            case "xinput":

                                                if (toMap == "select")
                                                    toMap = "back";
                                                
                                                if (ymltoXinput.ContainsKey(toMap))
                                                    toFetch = ymltoXinput[toMap];
                                                else
                                                    toFetch = toMap;

                                                if (n1 == "nintendo" && !p2values.Contains(button.Key))
                                                {
                                                    if (nintendoMapping.ContainsKey(toFetch))
                                                        toFetch = nintendoMapping[toFetch];
                                                }

                                                if (n2 == "nintendo" && p2values.Contains(button.Key))
                                                {
                                                    if (nintendoMapping.ContainsKey(toFetch))
                                                        toFetch = nintendoMapping[toFetch];
                                                }

                                                if (p2values.Contains(button.Key))
                                                    target = "JOY" + j2index + "_" + toFetch;
                                                else
                                                    target = "JOY" + j1index + "_" + toFetch;
                                                break;
                                            
                                            case "dinput":
                                                if (dInputSpecials.Contains(toMap))
                                                {
                                                    toFetch = toMap.Split('-')[0];
                                                    string dir = toMap.Split('-')[1];

                                                    if (dir == "left" || dir == "up")
                                                        direction = -1;
                                                    else
                                                        direction = 1;

                                                    if (p2values.Contains(button.Key))
                                                        target = GetDinputMapping(j2index, ctrl1, toFetch, direction);
                                                    else
                                                        target = GetDinputMapping(j1index, ctrl1, toFetch, direction);
                                                }
                                                else if (faceButtons.ContainsKey(toMap))
                                                {
                                                    toFetch = faceButtons[toMap];

                                                    if (p2values.Contains(button.Key))
                                                        target = GetDinputMapping(j2index, ctrl1, toFetch);
                                                    else
                                                        target = GetDinputMapping(j1index, ctrl1, toFetch);
                                                }
                                                else
                                                {
                                                    if (p2values.Contains(button.Key))
                                                        target = GetDinputMapping(j2index, ctrl1, toMap, 1);
                                                    else
                                                        target = GetDinputMapping(j1index, ctrl1, toMap, 1);
                                                }

                                                break;
                                        }

                                        if (invert)
                                            target += "_INV";
                                    }
                                    else
                                    {
                                        SimpleLogger.Instance.Warning("[WARNING] Wrong value in yml file for " + game.Name + " and key " + button.Key);
                                        continue;
                                    }

                                    if (string.IsNullOrEmpty(buttonValue))
                                        buttonValue += target;
                                    else
                                        buttonValue += "," + target;
                                }

                                else
                                {
                                    if (string.IsNullOrEmpty(buttonValue))
                                        buttonValue += value;
                                    else
                                        buttonValue += "," + value;
                                }
                            }

                            ini.WriteValue(" Global ", button.Key, buttonValue);
                        }

                        ini.WriteValue(" Global ", "InputSystem", tech);

                        //deadzones - set 5 as default deadzone, good compromise to avoid joystick drift
                        string deadzone = "5";
                        if (SystemConfig.isOptSet("supermodel_joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["supermodel_joy_deadzone"]))
                            deadzone = SystemConfig["supermodel_joy_deadzone"].ToIntegerString();

                        for (int i = 1; i <= 6; i++)
                        {
                            ini.WriteValue(" Global ", "InputJoy" + i + "XDeadZone", deadzone);
                            ini.WriteValue(" Global ", "InputJoy" + i + "YDeadZone", deadzone);
                        }

                        return;
                    }
                }
            }
            #endregion

            #region sdlgamepad
            //Now write buttons mapping for generic sdlgamepad case (when player 1 controller is NOT XINPUT)
            if (tech == "sdlgamepad")
            {
                ini.WriteValue(" Global ", "InputSystem", "sdlgamepad");

                //common - start to start and select to input coins
                //service menu and test menu can be accessed via L3 and R3 buttons if option is enabled

                ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,KEY_RETURN,KEY_PGDN,JOY" + j1index + "_BUTTON8\"");
                ini.WriteValue(" Global ", "InputCoin1", "\"KEY_5,KEY_LEFTCTRL,KEY_PGUP,KEY_BACKSPACE,JOY" + j1index + "_BUTTON7\"");
                ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_3,JOY" + j1index + "_BUTTON9\"" : "\"KEY_3\"");
                ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_4,JOY" + j1index + "_BUTTON10\"" : "\"KEY_4\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputStart2", "\"KEY_2,JOY" + j2index + "_BUTTON8\"");
                    ini.WriteValue(" Global ", "InputCoin2", "\"KEY_6,JOY" + j2index + "_BUTTON7\"");
                    ini.WriteValue(" Global ", "InputServiceB", enableServiceMenu ? "\"KEY_7,JOY" + j2index + "_BUTTON9\"" : "\"KEY_7\"");
                    ini.WriteValue(" Global ", "InputTestB", enableServiceMenu ? "\"KEY_8,JOY" + j2index + "_BUTTON10\"" : "\"KEY_8\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputStart2");
                    ini.Remove(" Global ", "InputCoin2");
                    ini.Remove(" Global ", "InputServiceB");
                    ini.Remove(" Global ", "InputTestB");
                }

                //4-way digital joysticks - directional stick
                ini.WriteValue(" Global ", "InputJoyUp", "\"JOY" + j1index + "_YAXIS_NEG,JOY" + j1index + "_POV1_UP\"");
                ini.WriteValue(" Global ", "InputJoyDown", "\"JOY" + j1index + "_YAXIS_POS,JOY" + j1index + "_POV1_DOWN\"");
                ini.WriteValue(" Global ", "InputJoyLeft", "\"JOY" + j1index + "_XAXIS_NEG,JOY" + j1index + "_POV1_LEFT\"");
                ini.WriteValue(" Global ", "InputJoyRight", "\"JOY" + j1index + "_XAXIS_POS,JOY" + j1index + "_POV1_RIGHT\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoyUp2", "\"JOY" + j2index + "_YAXIS_NEG,JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputJoyDown2", "\"JOY" + j2index + "_YAXIS_POS,JOY" + j2index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputJoyLeft2", "\"JOY" + j2index + "_XAXIS_NEG,JOY" + j2index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputJoyRight2", "\"JOY" + j2index + "_XAXIS_POS,JOY" + j2index + "_POV1_RIGHT\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputJoyUp2");
                    ini.Remove(" Global ", "InputJoyDown2");
                    ini.Remove(" Global ", "InputJoyLeft2");
                    ini.Remove(" Global ", "InputJoyRight2");
                }

                //Fighting game buttons - used for virtua fighters will be mapped with the 4 buttons
                ini.WriteValue(" Global ", "InputPunch", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputKick", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputGuard", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputEscape", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputPunch2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON4\"" : "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputKick2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON3\"" : "\"JOY" + j2index + "_BUTTON4\"");
                    ini.WriteValue(" Global ", "InputGuard2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputEscape2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON1\"" : "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputPunch2");
                    ini.Remove(" Global ", "InputKick2");
                    ini.Remove(" Global ", "InputGuard2");
                    ini.Remove(" Global ", "InputEscape2");
                }

                //Spikeout buttons
                ini.WriteValue(" Global ", "InputShift", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1,JOY" + j1index + "_BUTTON6\"" : "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON6\"");
                ini.WriteValue(" Global ", "InputBeat", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputCharge", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputJump", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");

                //Virtua Striker buttons
                ini.WriteValue(" Global ", "InputShortPass", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputLongPass", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputShoot", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputShortPass2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON4\"" : "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputLongPass2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputShoot2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON1\"" : "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputShortPass2");
                    ini.Remove(" Global ", "InputLongPass2");
                    ini.Remove(" Global ", "InputShoot2");
                }


                //Steering wheel - left analog stick horizontal axis
                ini.WriteValue(" Global ", "InputSteeringLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteeringRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteering", "\"JOY" + j1index + "_XAXIS\"");

                if (SystemConfig.getOptBoolean("model3_racingshoulder"))
                {
                    //Pedals - accelerate with R1, brake with L1
                    ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_BUTTON6\"");
                    ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_BUTTON5\"");

                    //Up/down shifter manual transmission (all racers) - DPAD up and DOWN
                    ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_POV1_DOWN\"");

                    //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with D-PAD (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                    ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_POV1_RIGHT\"");
                    ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                }

                else
                {
                    //Pedals - accelerate with R2, brake with L2
                    ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_RZAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_ZAXIS_POS\"");

                    //Up/down shifter manual transmission (all racers) - L1 gear down and R1 gear up
                    ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_BUTTON6\"");
                    ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_BUTTON5\"");

                    //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with right stick (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                    ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_RYAXIS_NEG\"");
                    ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_RYAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                    ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_RXAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                }

                //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race) - the 4 buttons will be used to change view in the games listed
                ini.WriteValue(" Global ", "InputVR1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputVR2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputVR3", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputVR4", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2) - use north button to change view in these games
                ini.WriteValue(" Global ", "InputViewChange", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");

                //Handbrake (Dirt Devils, Sega Rally 2) - south button to handbrake in these games
                ini.WriteValue(" Global ", "InputHandBrake", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                //Harley-Davidson controls
                ini.WriteValue(" Global ", "InputRearBrake", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputMusicSelect", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Virtual On macros
                ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"JOY" + j1index + "_RXAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyForward", "\"JOY" + j1index + "_YAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"JOY" + j1index + "_YAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"JOY" + j1index + "_XAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"JOY" + j1index + "_XAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyJump", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputTwinJoyCrouch", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                //Virtual On individual joystick mapping
                ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

                //Virtual On buttons
                ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4,JOY" + j1index + "_BUTTON5\"" : "\"JOY" + j1index + "_BUTTON3,JOY" + j1index + "_BUTTON5\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1,JOY" + j1index + "_BUTTON6\"" : "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON6\"");

                //Analog joystick (Star Wars Trilogy)
                ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyX", "\"JOY" + j1index + "_XAXIS_INV," + _mouse1 + "_XAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyY", "\"JOY" + j1index + "_YAXIS_INV," + _mouse1 + "_YAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger", n1 == "nintendo" ? "\"JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON4," + _mouse1 + "_LEFT_BUTTON\"" : "\"JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON3," + _mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2," + _mouse1 + "_RIGHT_BUTTON\"" : "\"JOY" + j1index + "_BUTTON1," + _mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

                //Light guns (Lost World) - MOUSE
                if (!_multigun)
                {
                    ini.WriteValue(" Global ", "InputGunLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunX", "\"" + _mouse1 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputGunY", "\"" + _mouse1 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputTrigger", "\"" + _mouse1 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputOffscreen", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAutoTrigger", "1");
                    ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");

                    // no multigun support in sdl, disable second mouse input
                    ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAutoTrigger2", "1");
                }

                //Analog guns (Ocean Hunter, LA Machineguns) - MOUSE
                if (!_multigun)
                {
                    ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + _mouse1 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + _mouse1 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"" + _mouse1 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");

                    // no multigun support in sdl, disable second mouse input
                    ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");
                }

                //Ski Champ controls
                ini.WriteValue(" Global ", "InputSkiLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputSkiY", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputSkiPollLeft", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiPollRight", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiSelect1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputSkiSelect2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputSkiSelect3", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Magical Truck Adventure controls
                ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLever1", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputMagicalPedal1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputMagicalLever2", "\"JOY" + j2index + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputMagicalPedal2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputMagicalLever2");
                    ini.Remove(" Global ", "InputMagicalPedal2");
                }

                //Sega Bass Fishing / Get Bass controls
                ini.WriteValue(" Global ", "InputFishingRodLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputFishingRodY", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickX", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickY", "\"JOY" + j1index + "_RYAXIS\"");
                ini.WriteValue(" Global ", "InputFishingReel", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputFishingCast", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputFishingSelect", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");
                ini.WriteValue(" Global ", "InputFishingTension", "\"NONE\"");

                //deadzones - set 5 as default deadzone, good compromise to avoid joystick drift
                string deadzone = "5";
                if (SystemConfig.isOptSet("supermodel_joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["supermodel_joy_deadzone"]))
                    deadzone = SystemConfig["supermodel_joy_deadzone"].ToIntegerString();

                for (int i = 1; i <= 6; i++)
                {
                    ini.WriteValue(" Global ", "InputJoy" + i + "XDeadZone", deadzone);
                    ini.WriteValue(" Global ", "InputJoy" + i + "YDeadZone", deadzone);
                }

                //other stuff
                ini.WriteValue(" Global ", "DirectInputConstForceLeftMax", "100");
                ini.WriteValue(" Global ", "DirectInputConstForceRightMax", "100");
                ini.WriteValue(" Global ", "DirectInputSelfCenterMax", "100");
                ini.WriteValue(" Global ", "DirectInputFrictionMax", "100");
                ini.WriteValue(" Global ", "DirectInputVibrateMax", "100");
            }
            #endregion

            #region sdl
            //Now write buttons mapping for generic sdl case (when player 1 controller is NOT XINPUT)
            else if (tech == "sdl")
            {
                ini.WriteValue(" Global ", "InputSystem", "sdl");

                //common - start to start and select to input coins
                //service menu and test menu can be accessed via L3 and R3 buttons if option is enabled

                ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,KEY_RETURN,KEY_PGDN,JOY" + j1index + "_BUTTON7\"");
                ini.WriteValue(" Global ", "InputCoin1", "\"KEY_5,KEY_LEFTCTRL,KEY_PGUP,KEY_BACKSPACE,JOY" + j1index + "_BUTTON5\"");
                ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_3,JOY" + j1index + "_BUTTON8\"" : "\"KEY_3\"");
                ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_4,JOY" + j1index + "_BUTTON9\"" : "\"KEY_4\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputStart2", "\"KEY_2,JOY" + j2index + "_BUTTON7\"");
                    ini.WriteValue(" Global ", "InputCoin2", "\"KEY_6,JOY" + j2index + "_BUTTON5\"");
                    ini.WriteValue(" Global ", "InputServiceB", enableServiceMenu ? "\"KEY_7,JOY" + j2index + "_BUTTON8\"" : "\"KEY_7\"");
                    ini.WriteValue(" Global ", "InputTestB", enableServiceMenu ? "\"KEY_8,JOY" + j2index + "_BUTTON9\"" : "\"KEY_8\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputStart2");
                    ini.Remove(" Global ", "InputCoin2");
                    ini.Remove(" Global ", "InputServiceB");
                    ini.Remove(" Global ", "InputTestB");
                }
                    
                //4-way digital joysticks - directional stick
                ini.WriteValue(" Global ", "InputJoyUp", "\"JOY" + j1index + "_YAXIS_NEG,JOY" + j1index + "_POV1_UP\"");
                ini.WriteValue(" Global ", "InputJoyDown", "\"JOY" + j1index + "_YAXIS_POS,JOY" + j1index + "_POV1_DOWN\"");
                ini.WriteValue(" Global ", "InputJoyLeft", "\"JOY" + j1index + "_XAXIS_NEG,JOY" + j1index + "_POV1_LEFT\"");
                ini.WriteValue(" Global ", "InputJoyRight", "\"JOY" + j1index + "_XAXIS_POS,JOY" + j1index + "_POV1_RIGHT\"");
                
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoyUp2", "\"JOY" + j2index + "_YAXIS_NEG,JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputJoyDown2", "\"JOY" + j2index + "_YAXIS_POS,JOY" + j2index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputJoyLeft2", "\"JOY" + j2index + "_XAXIS_NEG,JOY" + j2index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputJoyRight2", "\"JOY" + j2index + "_XAXIS_POS,JOY" + j2index + "_POV1_RIGHT\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputJoyUp2");
                    ini.Remove(" Global ", "InputJoyDown2");
                    ini.Remove(" Global ", "InputJoyLeft2");
                    ini.Remove(" Global ", "InputJoyRight2");
                }
                
                //Fighting game buttons - used for virtua fighters will be mapped with the 4 buttons
                ini.WriteValue(" Global ", "InputPunch", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputKick", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputGuard", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputEscape", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");
                
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputPunch2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON4\"" : "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputKick2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON3\"" : "\"JOY" + j2index + "_BUTTON4\"");
                    ini.WriteValue(" Global ", "InputGuard2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputEscape2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON1\"" : "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputPunch2");
                    ini.Remove(" Global ", "InputKick2");
                    ini.Remove(" Global ", "InputGuard2");
                    ini.Remove(" Global ", "InputEscape2");
                }
                
                //Spikeout buttons
                ini.WriteValue(" Global ", "InputShift", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1,JOY" + j1index + "_BUTTON11\"" : "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON11\"");
                ini.WriteValue(" Global ", "InputBeat", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputCharge", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputJump", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");

                //Virtua Striker buttons
                ini.WriteValue(" Global ", "InputShortPass", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputLongPass", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputShoot", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputShortPass2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON4\"" : "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputLongPass2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputShoot2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON1\"" : "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputShortPass2");
                    ini.Remove(" Global ", "InputLongPass2");
                    ini.Remove(" Global ", "InputShoot2");
                }
                

                //Steering wheel - left analog stick horizontal axis
                ini.WriteValue(" Global ", "InputSteeringLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteeringRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteering", "\"JOY" + j1index + "_XAXIS\"");

                if (SystemConfig.getOptBoolean("model3_racingshoulder"))
                {
                    //Pedals - accelerate with R1, brake with L1
                    ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_BUTTON11\"");
                    ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_BUTTON10\"");

                    //Up/down shifter manual transmission (all racers) - DPAD up and DOWN
                    ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_POV1_DOWN\"");

                    //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with D-PAD (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                    ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_POV1_RIGHT\"");
                    ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                }

                else
                {
                    //Pedals - accelerate with R2, brake with L2
                    ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_RZAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_RYAXIS_POS\"");

                    //Up/down shifter manual transmission (all racers) - L1 gear down and R1 gear up
                    ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_BUTTON11\"");
                    ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_BUTTON10\"");

                    //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with right stick (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                    ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                    ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_RXAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_ZAXIS_NEG\"");
                    ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_ZAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                }

                //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race) - the 4 buttons will be used to change view in the games listed
                ini.WriteValue(" Global ", "InputVR1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputVR2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputVR3", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputVR4", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2) - use north button to change view in these games
                ini.WriteValue(" Global ", "InputViewChange", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");

                //Handbrake (Dirt Devils, Sega Rally 2) - south button to handbrake in these games
                ini.WriteValue(" Global ", "InputHandBrake", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                //Harley-Davidson controls
                ini.WriteValue(" Global ", "InputRearBrake", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputMusicSelect", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Virtual On macros
                ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"JOY" + j1index + "_ZAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyForward", "\"JOY" + j1index + "_YAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"JOY" + j1index + "_YAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"JOY" + j1index + "_XAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"JOY" + j1index + "_XAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyJump", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON3\"" : "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputTwinJoyCrouch", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                //Virtual On individual joystick mapping
                ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

                //Virtual On buttons
                ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"JOY" + j1index + "_RYAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4,JOY" + j1index + "_BUTTON10\"" : "\"JOY" + j1index + "_BUTTON3,JOY" + j1index + "_BUTTON10\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1,JOY" + j1index + "_BUTTON11\"" : "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON11\"");

                //Analog joystick (Star Wars Trilogy)
                ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyX", "\"JOY" + j1index + "_XAXIS_INV," + _mouse1 + "_XAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyY", "\"JOY" + j1index + "_YAXIS_INV," + _mouse1 + "_YAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger", n1 == "nintendo" ? "\"JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON4," + _mouse1 + "_LEFT_BUTTON\"" : "\"JOY" + j1index + "_RZAXIS_POS,JOY" + j1index + "_BUTTON3," + _mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2," + _mouse1 + "_RIGHT_BUTTON\"" : "\"JOY" + j1index + "_BUTTON1," + _mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

                //Light guns (Lost World) - MOUSE
                ini.WriteValue(" Global ", "InputGunLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunX", "\"" + _mouse1 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputGunY", "\"" + _mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputTrigger", "\"" + _mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputOffscreen", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAutoTrigger", "1");
                ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");
                
                // no multigun support in sdl, disable second mouse input
                ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAutoTrigger2", "1");

                //Analog guns (Ocean Hunter, LA Machineguns) - MOUSE
                ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + _mouse1 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + _mouse1 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"" + _mouse1 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");

                // no multigun support in sdl, disable second mouse input
                ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");

                //Ski Champ controls
                ini.WriteValue(" Global ", "InputSkiLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputSkiY", "\"JOY" + j1index + "_ZAXIS\"");
                ini.WriteValue(" Global ", "InputSkiPollLeft", "\"JOY" + j1index + "_RYAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiPollRight", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiSelect1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON4\"" : "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputSkiSelect2", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputSkiSelect3", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");

                //Magical Truck Adventure controls
                ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLever1", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputMagicalPedal1", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputMagicalLever2", "\"JOY" + j2index + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputMagicalPedal2", n2 == "nintendo" ? "\"JOY" + j2index + "_BUTTON2\"" : "\"JOY" + j2index + "_BUTTON1\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputMagicalLever2");
                    ini.Remove(" Global ", "InputMagicalPedal2");
                }

                //Sega Bass Fishing / Get Bass controls
                ini.WriteValue(" Global ", "InputFishingRodLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputFishingRodY", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickX", "\"JOY" + j1index + "_ZAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickY", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputFishingReel", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputFishingCast", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON2\"" : "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputFishingSelect", n1 == "nintendo" ? "\"JOY" + j1index + "_BUTTON1\"" : "\"JOY" + j1index + "_BUTTON2\"");
                ini.WriteValue(" Global ", "InputFishingTension", "\"NONE\"");

                //deadzones - set 5 as default deadzone, good compromise to avoid joystick drift
                string deadzone = "5";
                if (SystemConfig.isOptSet("supermodel_joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["supermodel_joy_deadzone"]))
                    deadzone = SystemConfig["supermodel_joy_deadzone"].ToIntegerString();

                for (int i = 1; i <= 6; i++)
                {
                    ini.WriteValue(" Global ", "InputJoy" + i + "XDeadZone", deadzone);
                    ini.WriteValue(" Global ", "InputJoy" + i + "YDeadZone", deadzone);
                }

                //other stuff
                ini.WriteValue(" Global ", "DirectInputConstForceLeftMax", "100");
                ini.WriteValue(" Global ", "DirectInputConstForceRightMax", "100");
                ini.WriteValue(" Global ", "DirectInputSelfCenterMax", "100");
                ini.WriteValue(" Global ", "DirectInputFrictionMax", "100");
                ini.WriteValue(" Global ", "DirectInputVibrateMax", "100");
            }
            #endregion

            #region dinput
            else if (tech == "dinput")
            {
                string guid1 = (c1.Guid.ToString()).Substring(0, 24) + "00000000";

                // set inputsystem
                if (_multigun)
                {
                    ini.WriteValue(" Global ", "InputSystem", "rawinput");
                    SimpleLogger.Instance.Info("[GUNS] Overriding emulator input driver : rawinput");
                }
                else
                    ini.WriteValue(" Global ", "InputSystem", "dinput");

                // Fetch information in retrobat/system/tools/gamecontrollerdb.txt file
                SdlToDirectInput ctrl1 = null;
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
                
                if (!File.Exists(gamecontrollerDB))
                {
                    SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                    gamecontrollerDB = null;
                }

                if (gamecontrollerDB != null)
                {
                    SimpleLogger.Instance.Info("[INFO] Player 1. Fetching gamecontrollerdb.txt file with guid : " + guid1);

                    ctrl1 = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid1);

                    if (ctrl1 == null)
                        SimpleLogger.Instance.Info("[INFO] Player 1. No controller found in gamecontrollerdb.txt file for guid : " + guid1);
                    else
                        SimpleLogger.Instance.Info("[INFO] Player 1: " + guid1 + " found in gamecontrollerDB file.");
                }

                if (ctrl1 != null)
                {
                    //common - start to start and select to input coins
                    //service menu and test menu can be accessed via L3 and R3 buttons if option is enabled

                    if (!useWheel)
                    {
                        ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,KEY_RETURN,KEY_PGDN," + GetDinputMapping(j1index, ctrl1, "start") + "\"");
                        ini.WriteValue(" Global ", "InputCoin1", "\"KEY_5,KEY_LEFTCTRL,KEY_PGUP,KEY_BACKSPACE," + GetDinputMapping(j1index, ctrl1, "back") + "\"");
                        ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_3," + GetDinputMapping(j1index, ctrl1, "leftstick") + "\"" : "\"KEY_3\"");
                        ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_4," + GetDinputMapping(j1index, ctrl1, "rightstick") + "\"" : "\"KEY_4\"");
                    }
                    else
                    {
                        ini.WriteValue(" Global ", "InputStart1", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Start") + "\"");
                        ini.WriteValue(" Global ", "InputCoin1", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Coin") + "\"");
                        ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_8,JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Service") + "\"" : "\"KEY_8\"");
                        ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_9,JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Test") + "\"" : "\"KEY_9\"");
                    }

                    //4-way digital joysticks - directional stick
                    ini.WriteValue(" Global ", "InputJoyUp", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", -1) + "," + GetDinputMapping(j1index, ctrl1, "dpup") + "\"");
                    ini.WriteValue(" Global ", "InputJoyDown", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", 1) + "," + GetDinputMapping(j1index, ctrl1, "dpdown") + "\"");
                    ini.WriteValue(" Global ", "InputJoyLeft", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", -1) + "," + GetDinputMapping(j1index, ctrl1, "dpleft") + "\"");
                    ini.WriteValue(" Global ", "InputJoyRight", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", 1) + "," + GetDinputMapping(j1index, ctrl1, "dpright") + "\"");

                    //Fighting game buttons - used for virtua fighters will be mapped with the 4 buttons
                    ini.WriteValue(" Global ", "InputPunch", "\"" + GetDinputMapping(j1index, ctrl1, "x") + "\"");
                    ini.WriteValue(" Global ", "InputKick", "\"" + GetDinputMapping(j1index, ctrl1, "y") + "\"");
                    ini.WriteValue(" Global ", "InputGuard", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                    ini.WriteValue(" Global ", "InputEscape", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "\"");

                    //Spikeout buttons
                    ini.WriteValue(" Global ", "InputShift", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "," + GetDinputMapping(j1index, ctrl1, "rightshoulder") + "\"");
                    ini.WriteValue(" Global ", "InputBeat", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                    ini.WriteValue(" Global ", "InputCharge", "\"" + GetDinputMapping(j1index, ctrl1, "x") + "\"");
                    ini.WriteValue(" Global ", "InputJump", "\"" + GetDinputMapping(j1index, ctrl1, "y") + "\"");

                    //Virtua Striker buttons
                    ini.WriteValue(" Global ", "InputShortPass", "\"" + GetDinputMapping(j1index, ctrl1, "x") + "\"");
                    ini.WriteValue(" Global ", "InputLongPass", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                    ini.WriteValue(" Global ", "InputShoot", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "\"");

                    if (useWheel)
                    {
                        if (wheelbuttonMap.ContainsKey("DeportedShifter") && wheelbuttonMap["DeportedShifter"] == "true")
                        {
                            deportedShifter = true;
                            shifterID = j1index - 1;
                        }

                        if (SystemConfig.isOptSet("gearstick_deviceid") && !string.IsNullOrEmpty(SystemConfig["gearstick_deviceid"]))
                        {
                            deportedShifter = true;
                            shifterID = SystemConfig["gearstick_deviceid"].ToInteger();
                        }

                        if (deportedShifter)
                            SimpleLogger.Instance.Info("[WHEELS] Deported shifter enabled for wheel " + usableWheels[0].Name + " with ID " + shifterID);

                        //Steering wheel - left analog stick horizontal axis
                        ini.WriteValue(" Global ", "InputSteeringLeft", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputSteeringLeft") + "\"");
                        ini.WriteValue(" Global ", "InputSteeringRight", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputSteeringRight") + "\"");
                        ini.WriteValue(" Global ", "InputSteering", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputSteering") + "\"");

                        //Pedals - accelerate with R2, brake with L2
                        ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputAccelerator") + "\"");
                        ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputBrake") + "\"");

                        //Up/down shifter manual transmission (all racers) - L1 gear down and R1 gear up
                        if (wheelbuttonMap["InputGearShiftUp"].StartsWith("Shifter_"))
                        {
                            string buttonValue = "";
                            string[] buttonValueSplit = wheelbuttonMap["InputGearShiftUp"].Split('_');
                            if (buttonValueSplit.Length > 1)
                                buttonValue = buttonValueSplit[1];

                            ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + shifterID + "_" + buttonValue + "\"");
                        }
                        else
                            ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputGearShiftUp") + "\"");
                        
                        if (wheelbuttonMap["InputGearShiftDown"].StartsWith("Shifter_"))
                        {
                            string buttonValue = "";
                            string[] buttonValueSplit = wheelbuttonMap["InputGearShiftDown"].Split('_');
                            if (buttonValueSplit.Length > 1)
                                buttonValue = buttonValueSplit[1];

                            ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + shifterID + "_" + buttonValue + "\"");
                        }
                        else
                            ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputGearShiftDown") + "\"");

                        //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with right stick (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                        if (SystemConfig.getOptBoolean("wheel_nogearstick"))
                        {
                            ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Gear1") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Gear2") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Gear3") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "Gear4") + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftN", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "GearN") + "\"");
                        }

                        else if (deportedShifter)
                        {
                            ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + shifterID + "_" + GetWheelButton(wheelbuttonMap, "StickGear1") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + shifterID + "_" + GetWheelButton(wheelbuttonMap, "StickGear2") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + shifterID + "_" + GetWheelButton(wheelbuttonMap, "StickGear3") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + shifterID + "_" + GetWheelButton(wheelbuttonMap, "StickGear4") + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftN", "\"JOY" + shifterID + "_" + GetWheelButton(wheelbuttonMap, "StickGearN") + "\"");
                        }
                        else
                        {
                            ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "StickGear1") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "StickGear2") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "StickGear3") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "StickGear4") + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftN", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "StickGearN") + "\"");
                        }

                        //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race) - the 4 buttons will be used to change view in the games listed
                        ini.WriteValue(" Global ", "InputVR1", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputVR1") + "\"");
                        ini.WriteValue(" Global ", "InputVR2", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputVR2") + "\"");
                        ini.WriteValue(" Global ", "InputVR3", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputVR3") + "\"");
                        ini.WriteValue(" Global ", "InputVR4", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputVR4") + "\"");

                        //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2) - use north button to change view in these games
                        ini.WriteValue(" Global ", "InputViewChange", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputViewChange") + "\"");

                        //Handbrake (Dirt Devils, Sega Rally 2) - south button to handbrake in these games
                        ini.WriteValue(" Global ", "InputHandBrake", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputHandBrake") + "\"");

                        //Harley-Davidson controls
                        ini.WriteValue(" Global ", "InputRearBrake", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputRearBrake") + "\"");
                        ini.WriteValue(" Global ", "InputMusicSelect", "\"JOY" + j1index + "_" + GetWheelButton(wheelbuttonMap, "InputMusicSelect") + "\"");
                    }
                    else
                    {
                        //Steering wheel - left analog stick horizontal axis
                        ini.WriteValue(" Global ", "InputSteeringLeft", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputSteeringRight", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputSteering", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", 0) + "\"");

                        if (SystemConfig.getOptBoolean("model3_racingshoulder"))
                        {
                            //Pedals - accelerate with R1, brake with L1
                            ini.WriteValue(" Global ", "InputAccelerator", "\"" + GetDinputMapping(j1index, ctrl1, "rightshoulder") + "\"");
                            ini.WriteValue(" Global ", "InputBrake", "\"" + GetDinputMapping(j1index, ctrl1, "leftshoulder") + "\"");

                            //Up/down shifter manual transmission (all racers) - DPAD up and DOWN
                            ini.WriteValue(" Global ", "InputGearShiftUp", "\"" + GetDinputMapping(j1index, ctrl1, "dpup") + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftDown", "\"" + GetDinputMapping(j1index, ctrl1, "dpdown") + "\"");

                            //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with D-PAD (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                            ini.WriteValue(" Global ", "InputGearShift1", "\"" + GetDinputMapping(j1index, ctrl1, "dpup") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift2", "\"" + GetDinputMapping(j1index, ctrl1, "dpdown") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift3", "\"" + GetDinputMapping(j1index, ctrl1, "dpleft") + "\"");
                            ini.WriteValue(" Global ", "InputGearShift4", "\"" + GetDinputMapping(j1index, ctrl1, "dpright") + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                        }
                        else
                        {
                            //Pedals - accelerate with R2, brake with L2
                            ini.WriteValue(" Global ", "InputAccelerator", "\"" + GetDinputMapping(j1index, ctrl1, "righttrigger", 1) + "\"");
                            ini.WriteValue(" Global ", "InputBrake", "\"" + GetDinputMapping(j1index, ctrl1, "lefttrigger", 1) + "\"");

                            //Up/down shifter manual transmission (all racers) - L1 gear down and R1 gear up
                            ini.WriteValue(" Global ", "InputGearShiftUp", "\"" + GetDinputMapping(j1index, ctrl1, "rightshoulder") + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftDown", "\"" + GetDinputMapping(j1index, ctrl1, "leftshoulder") + "\"");

                            //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with right stick (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                            ini.WriteValue(" Global ", "InputGearShift1", "\"" + GetDinputMapping(j1index, ctrl1, "righty", -1) + "\"");
                            ini.WriteValue(" Global ", "InputGearShift2", "\"" + GetDinputMapping(j1index, ctrl1, "righty", 1) + "\"");
                            ini.WriteValue(" Global ", "InputGearShift3", "\"" + GetDinputMapping(j1index, ctrl1, "righty", -1) + "\"");
                            ini.WriteValue(" Global ", "InputGearShift4", "\"" + GetDinputMapping(j1index, ctrl1, "righty", 1) + "\"");
                            ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                        }

                        //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race) - the 4 buttons will be used to change view in the games listed
                        ini.WriteValue(" Global ", "InputVR1", "\"" + GetDinputMapping(j1index, ctrl1, "y") + "\"");
                        ini.WriteValue(" Global ", "InputVR2", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                        ini.WriteValue(" Global ", "InputVR3", "\"" + GetDinputMapping(j1index, ctrl1, "x") + "\"");
                        ini.WriteValue(" Global ", "InputVR4", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "\"");

                        //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2) - use north button to change view in these games
                        ini.WriteValue(" Global ", "InputViewChange", "\"" + GetDinputMapping(j1index, ctrl1, "y") + "\"");

                        //Handbrake (Dirt Devils, Sega Rally 2) - south button to handbrake in these games
                        ini.WriteValue(" Global ", "InputHandBrake", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");

                        //Harley-Davidson controls
                        ini.WriteValue(" Global ", "InputRearBrake", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                        ini.WriteValue(" Global ", "InputMusicSelect", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "\"");
                    }

                    //Virtual On macros
                    ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"" + GetDinputMapping(j1index, ctrl1, "rightx", -1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"" + GetDinputMapping(j1index, ctrl1, "rightx", 1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyForward", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", -1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", 1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", -1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", 1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyJump", "\"" + GetDinputMapping(j1index, ctrl1, "y") + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyCrouch", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");

                    //Virtual On individual joystick mapping
                    ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

                    //Virtual On buttons
                    ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"" + GetDinputMapping(j1index, ctrl1, "lefttrigger", 1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"" + GetDinputMapping(j1index, ctrl1, "righttrigger", 1) + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyTurbo1", "\"" + GetDinputMapping(j1index, ctrl1, "x") + "," + GetDinputMapping(j1index, ctrl1, "leftshoulder") + "\"");
                    ini.WriteValue(" Global ", "InputTwinJoyTurbo2", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "," + GetDinputMapping(j1index, ctrl1, "rightshoulder") + "\"");

                    //Analog joystick (Star Wars Trilogy)
                    ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyX", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", 0) + "" + "_INV," + _mouse1 + "_XAXIS_INV\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyY", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", 0) + "_INV," + _mouse1 + "_YAXIS_INV\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyTrigger", "\"" + GetDinputMapping(j1index, ctrl1, "righttrigger", 1) + "," + GetDinputMapping(j1index, ctrl1, "x") + "," + _mouse1 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyEvent", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "," + _mouse1 + "_RIGHT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

                    //Light guns (Lost World) - MOUSE
                    ini.WriteValue(" Global ", "InputGunLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunX", "\"" + _mouse1 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputGunY", "\"" + _mouse1 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputTrigger", "\"" + _mouse1 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputOffscreen", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAutoTrigger", "1");
                    ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");

                    if (_multigun)
                    {
                        ini.WriteValue(" Global ", "InputGunX2", "\"" + _mouse2 + "_XAXIS\"");
                        ini.WriteValue(" Global ", "InputGunY2", "\"" + _mouse2 + "_YAXIS\"");
                        ini.WriteValue(" Global ", "InputTrigger2", "\"" + _mouse2 + "_LEFT_BUTTON\"");
                        ini.WriteValue(" Global ", "InputOffscreen2", "\"" + _mouse2 + "_RIGHT_BUTTON\"");
                        ini.WriteValue(" Global ", "InputAutoTrigger2", "1");
                    }
                    else
                    {
                        ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputAutoTrigger2", "1");
                    }

                    //Analog guns (Ocean Hunter, LA Machineguns) - MOUSE
                    ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + _mouse1 + "_XAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + _mouse1 + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"" + _mouse1 + "_LEFT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
                    ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");

                    if (_multigun)
                    {
                        ini.WriteValue(" Global ", "InputAnalogGunX2", "\"" + _mouse2 + "_XAXIS\"");
                        ini.WriteValue(" Global ", "InputAnalogGunY2", "\"" + _mouse2 + "_YAXIS\"");
                        ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"" + _mouse2 + "_LEFT_BUTTON\"");
                        ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"" + _mouse2 + "_RIGHT_BUTTON\"");
                    }
                    else
                    {
                        ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                        ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");
                    }

                    //Ski Champ controls
                    ini.WriteValue(" Global ", "InputSkiLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputSkiRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputSkiUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputSkiDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputSkiX", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", 0) + "\"");
                    ini.WriteValue(" Global ", "InputSkiY", "\"" + GetDinputMapping(j1index, ctrl1, "rightx", 0) + "\"");
                    ini.WriteValue(" Global ", "InputSkiPollLeft", "\"" + GetDinputMapping(j1index, ctrl1, "lefttrigger", 1) + "\"");
                    ini.WriteValue(" Global ", "InputSkiPollRight", "\"" + GetDinputMapping(j1index, ctrl1, "righttrigger", 1) + "\"");
                    ini.WriteValue(" Global ", "InputSkiSelect1", "\"" + GetDinputMapping(j1index, ctrl1, "x") + "\"");
                    ini.WriteValue(" Global ", "InputSkiSelect2", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                    ini.WriteValue(" Global ", "InputSkiSelect3", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "\"");

                    //Magical Truck Adventure controls
                    ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputMagicalLever1", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", 0) + "\"");
                    ini.WriteValue(" Global ", "InputMagicalPedal1", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");

                    //Sega Bass Fishing / Get Bass controls
                    ini.WriteValue(" Global ", "InputFishingRodLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingRodRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingRodUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingRodDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingStickLeft", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingStickRight", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingStickUp", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingStickDown", "\"NONE\"");
                    ini.WriteValue(" Global ", "InputFishingRodX", "\"" + GetDinputMapping(j1index, ctrl1, "leftx", 0) + "\"");
                    ini.WriteValue(" Global ", "InputFishingRodY", "\"" + GetDinputMapping(j1index, ctrl1, "lefty", 0) + "\"");
                    ini.WriteValue(" Global ", "InputFishingStickX", "\"" + GetDinputMapping(j1index, ctrl1, "rightx", 0) + "\"");
                    ini.WriteValue(" Global ", "InputFishingStickY", "\"" + GetDinputMapping(j1index, ctrl1, "righty", 0) + "\"");
                    ini.WriteValue(" Global ", "InputFishingReel", "\"" + GetDinputMapping(j1index, ctrl1, "righttrigger", 1) + "\"");
                    ini.WriteValue(" Global ", "InputFishingCast", "\"" + GetDinputMapping(j1index, ctrl1, "a") + "\"");
                    ini.WriteValue(" Global ", "InputFishingSelect", "\"" + GetDinputMapping(j1index, ctrl1, "b") + "\"");
                    ini.WriteValue(" Global ", "InputFishingTension", "\"NONE\"");

                    //deadzones - set 5 as default deadzone, good compromise to avoid joystick drift
                    string deadzone = "5";
                    if (SystemConfig.isOptSet("supermodel_joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["supermodel_joy_deadzone"]))
                        deadzone = SystemConfig["supermodel_joy_deadzone"].ToIntegerString();

                    for (int i = 1; i <= 6; i++)
                    {
                        ini.WriteValue(" Global ", "InputJoy" + i + "XDeadZone", deadzone);
                        ini.WriteValue(" Global ", "InputJoy" + i + "YDeadZone", deadzone);
                    }

                    //other stuff
                    ini.WriteValue(" Global ", "DirectInputConstForceLeftMax", "100");
                    ini.WriteValue(" Global ", "DirectInputConstForceRightMax", "100");
                    ini.WriteValue(" Global ", "DirectInputSelfCenterMax", "100");
                    ini.WriteValue(" Global ", "DirectInputFrictionMax", "100");
                    ini.WriteValue(" Global ", "DirectInputVibrateMax", "100");

                    if (c2 != null && multiplayer)
                    {
                        string guid2 = (c2.Guid.ToString()).Substring(0, 24) + "00000000";
                        SimpleLogger.Instance.Info("[INFO] Player 2. Fetching gamecontrollerdb.txt file with guid : " + guid2);
                        var ctrl2 = gamecontrollerDB == null ? null : GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid2);
                        
                        if (ctrl2 == null)
                            SimpleLogger.Instance.Info("[INFO] Player 2. No controller found in gamecontrollerdb.txt file for guid : " + guid2);
                        else
                            SimpleLogger.Instance.Info("[INFO] Player 2: " + guid2 + "found in gamecontrollerDB file.");

                        if (ctrl2 != null)
                        {
                            ini.WriteValue(" Global ", "InputStart2", "\"KEY_2," + GetDinputMapping(j2index, ctrl2, "start") + "\"");
                            ini.WriteValue(" Global ", "InputCoin2", "\"KEY_6," + GetDinputMapping(j2index, ctrl2, "back") + "\"");
                            ini.WriteValue(" Global ", "InputServiceB", enableServiceMenu ? "\"KEY_7," + GetDinputMapping(j2index, ctrl2, "leftstick") + "\"" : "\"KEY_7\"");
                            ini.WriteValue(" Global ", "InputTestB", enableServiceMenu ? "\"KEY_8," + GetDinputMapping(j2index, ctrl2, "rightstick") + "\"" : "\"KEY_8\"");
                            ini.WriteValue(" Global ", "InputJoyUp2", "\"" + GetDinputMapping(j2index, ctrl2, "lefty", -1) + "," + GetDinputMapping(j2index, ctrl2, "dpup") + "\"");
                            ini.WriteValue(" Global ", "InputJoyDown2", "\"" + GetDinputMapping(j2index, ctrl2, "lefty", 1) + "," + GetDinputMapping(j2index, ctrl2, "dpdown") + "\"");
                            ini.WriteValue(" Global ", "InputJoyLeft2", "\"" + GetDinputMapping(j2index, ctrl2, "leftx", -1) + "," + GetDinputMapping(j2index, ctrl2, "dpleft") + "\"");
                            ini.WriteValue(" Global ", "InputJoyRight2", "\"" + GetDinputMapping(j2index, ctrl2, "leftx", 1) + "," + GetDinputMapping(j2index, ctrl2, "dpright") + "\"");
                            ini.WriteValue(" Global ", "InputPunch2", "\"" + GetDinputMapping(j2index, ctrl2, "x") + "\"");
                            ini.WriteValue(" Global ", "InputKick2", "\"" + GetDinputMapping(j2index, ctrl2, "y") + "\"");
                            ini.WriteValue(" Global ", "InputGuard2", "\"" + GetDinputMapping(j2index, ctrl2, "a") + "\"");
                            ini.WriteValue(" Global ", "InputEscape2", "\"" + GetDinputMapping(j2index, ctrl2, "b") + "\"");
                            ini.WriteValue(" Global ", "InputShortPass2", "\"" + GetDinputMapping(j2index, ctrl2, "x") + "\"");
                            ini.WriteValue(" Global ", "InputLongPass2", "\"" + GetDinputMapping(j2index, ctrl2, "a") + "\"");
                            ini.WriteValue(" Global ", "InputShoot2", "\"" + GetDinputMapping(j2index, ctrl2, "b") + "\"");
                            ini.WriteValue(" Global ", "InputMagicalLever2", "\"" + GetDinputMapping(j2index, ctrl2, "lefty", 0) + "\"");
                            ini.WriteValue(" Global ", "InputMagicalPedal2", "\"" + GetDinputMapping(j2index, ctrl2, "a") + "\"");
                        }
                    }

                    else
                    {
                        ini.Remove(" Global ", "InputStart2");
                        ini.Remove(" Global ", "InputCoin2");
                        ini.Remove(" Global ", "InputServiceB");
                        ini.Remove(" Global ", "InputTestB");
                        ini.Remove(" Global ", "InputJoyUp2");
                        ini.Remove(" Global ", "InputJoyDown2");
                        ini.Remove(" Global ", "InputJoyLeft2");
                        ini.Remove(" Global ", "InputJoyRight2");
                        ini.Remove(" Global ", "InputPunch2");
                        ini.Remove(" Global ", "InputKick2");
                        ini.Remove(" Global ", "InputGuard2");
                        ini.Remove(" Global ", "InputEscape2");
                        ini.Remove(" Global ", "InputShortPass2");
                        ini.Remove(" Global ", "InputLongPass2");
                        ini.Remove(" Global ", "InputShoot2");
                        ini.Remove(" Global ", "InputMagicalLever2");
                        ini.Remove(" Global ", "InputMagicalPedal2");
                    }
                }
            }
            #endregion

            #region xinput
            //xinput case - this is used when player 1 controller is a xinput controller
            else if (tech == "xinput")
            {
                if (_multigun)
                {
                    ini.WriteValue(" Global ", "InputSystem", "rawinput");
                    SimpleLogger.Instance.Info("[GUNS] Overriding emulator input driver : rawinput");
                }
                else
                    ini.WriteValue(" Global ", "InputSystem", "xinput");

                //common - L3 and R3 will be used to navigate service menu
                ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,KEY_RETURN,KEY_PGDN,JOY" + j1index + "_BUTTON8\"");
                ini.WriteValue(" Global ", "InputCoin1", "\"KEY_5,KEY_LEFTCTRL,KEY_PGUP,KEY_BACKSPACE,JOY" + j1index + "_BUTTON7\"");
                ini.WriteValue(" Global ", "InputServiceA", enableServiceMenu ? "\"KEY_3,JOY" + j1index + "_BUTTON9\"" : "\"KEY_3\"");
                ini.WriteValue(" Global ", "InputTestA", enableServiceMenu ? "\"KEY_4,JOY" + j1index + "_BUTTON10\"" : "\"KEY_4\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputStart2", "\"KEY_2,JOY" + j2index + "_BUTTON8\"");
                    ini.WriteValue(" Global ", "InputCoin2", "\"KEY_6,JOY" + j2index + "_BUTTON7\"");
                    ini.WriteValue(" Global ", "InputServiceB", enableServiceMenu ? "\"KEY_7,JOY" + j2index + "_BUTTON9\"" : "\"KEY_7\"");
                    ini.WriteValue(" Global ", "InputTestB", enableServiceMenu ? "\"KEY_8,JOY" + j2index + "_BUTTON10\"" : "\"KEY_8\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputStart2");
                    ini.Remove(" Global ", "InputCoin2");
                    ini.Remove(" Global ", "InputServiceB");
                    ini.Remove(" Global ", "InputTestB");
                }

                //4-way digital joysticks
                ini.WriteValue(" Global ", "InputJoyUp", "\"JOY" + j1index + "_YAXIS_NEG,JOY" + j1index + "_POV1_UP\"");
                ini.WriteValue(" Global ", "InputJoyDown", "\"JOY" + j1index + "_YAXIS_POS,JOY" + j1index + "_POV1_DOWN\"");
                ini.WriteValue(" Global ", "InputJoyLeft", "\"JOY" + j1index + "_XAXIS_NEG,JOY" + j1index + "_POV1_LEFT\"");
                ini.WriteValue(" Global ", "InputJoyRight", "\"JOY" + j1index + "_XAXIS_POS,JOY" + j1index + "_POV1_RIGHT\"");
                
                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputJoyUp2", "\"JOY" + j2index + "_YAXIS_NEG,JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputJoyDown2", "\"JOY" + j2index + "_YAXIS_POS,JOY" + j2index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputJoyLeft2", "\"JOY" + j2index + "_XAXIS_NEG,JOY" + j2index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputJoyRight2", "\"JOY" + j2index + "_XAXIS_POS,JOY" + j2index + "_POV1_RIGHT\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputJoyUp2");
                    ini.Remove(" Global ", "InputJoyDown2");
                    ini.Remove(" Global ", "InputJoyLeft2");
                    ini.Remove(" Global ", "InputJoyRight2");
                }

                //Fighting game buttons
                ini.WriteValue(" Global ", "InputPunch", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputKick", "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputGuard", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputEscape", "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputPunch2", "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputKick2", "\"JOY" + j2index + "_BUTTON4\"");
                    ini.WriteValue(" Global ", "InputGuard2", "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputEscape2", "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputPunch2");
                    ini.Remove(" Global ", "InputKick2");
                    ini.Remove(" Global ", "InputGuard2");
                    ini.Remove(" Global ", "InputEscape2");
                }

                //Spikeout buttons
                ini.WriteValue(" Global ", "InputShift", "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON6\"");
                ini.WriteValue(" Global ", "InputBeat", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputCharge", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputJump", "\"JOY" + j1index + "_BUTTON4\"");

                //Virtua Striker buttons
                ini.WriteValue(" Global ", "InputShortPass", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputLongPass", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputShoot", "\"JOY" + j1index + "_BUTTON2\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputShortPass2", "\"JOY" + j2index + "_BUTTON3\"");
                    ini.WriteValue(" Global ", "InputLongPass2", "\"JOY" + j2index + "_BUTTON1\"");
                    ini.WriteValue(" Global ", "InputShoot2", "\"JOY" + j2index + "_BUTTON2\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputShortPass2");
                    ini.Remove(" Global ", "InputLongPass2");
                    ini.Remove(" Global ", "InputShoot2");
                }

                //Steering wheel
                ini.WriteValue(" Global ", "InputSteeringLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteeringRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSteering", "\"JOY" + j1index + "_XAXIS\"");

                if (SystemConfig.getOptBoolean("model3_racingshoulder"))
                {
                    //Pedals - accelerate with R1, brake with L1
                    ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_BUTTON6\"");
                    ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_BUTTON5\"");

                    //Up/down shifter manual transmission (all racers) - DPAD up and DOWN
                    ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j2index + "_POV1_DOWN\"");

                    //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race) - manual gears with D-PAD (up for gear 1, down for gear 2, left for gear 3 and right for gear 4)
                    ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j2index + "_POV1_UP\"");
                    ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j2index + "_POV1_DOWN\"");
                    ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j2index + "_POV1_LEFT\"");
                    ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j2index + "_POV1_RIGHT\"");
                    ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                }

                else
                {
                    //Pedals
                    ini.WriteValue(" Global ", "InputAccelerator", "\"JOY" + j1index + "_RZAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputBrake", "\"JOY" + j1index + "_ZAXIS_POS\"");

                    //Up/down shifter manual transmission (all racers)
                    ini.WriteValue(" Global ", "InputGearShiftUp", "\"JOY" + j1index + "_BUTTON6\"");
                    ini.WriteValue(" Global ", "InputGearShiftDown", "\"JOY" + j1index + "_BUTTON5\"");

                    //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race)
                    ini.WriteValue(" Global ", "InputGearShift1", "\"JOY" + j1index + "_RYAXIS_NEG\"");
                    ini.WriteValue(" Global ", "InputGearShift2", "\"JOY" + j1index + "_RYAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputGearShift3", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                    ini.WriteValue(" Global ", "InputGearShift4", "\"JOY" + j1index + "_RXAXIS_POS\"");
                    ini.WriteValue(" Global ", "InputGearShiftN", "NONE");
                }

                //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race)
                ini.WriteValue(" Global ", "InputVR1", "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputVR2", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputVR3", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputVR4", "\"JOY" + j1index + "_BUTTON2\"");

                //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2)
                ini.WriteValue(" Global ", "InputViewChange", "\"JOY" + j1index + "_BUTTON4\"");

                //Handbrake (Dirt Devils, Sega Rally 2)
                ini.WriteValue(" Global ", "InputHandBrake", "\"JOY" + j1index + "_BUTTON1\"");

                //Harley-Davidson controls
                ini.WriteValue(" Global ", "InputRearBrake", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputMusicSelect", "\"JOY" + j1index + "_BUTTON2\"");

                //Virtual On macros
                ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"JOY" + j1index + "_RXAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"JOY" + j1index + "_RXAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyForward", "\"JOY" + j1index + "_YAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"JOY" + j1index + "_YAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"JOY" + j1index + "_XAXIS_NEG\"");
                ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"JOY" + j1index + "_XAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyJump", "\"JOY" + j1index + "_BUTTON4\"");
                ini.WriteValue(" Global ", "InputTwinJoyCrouch", "\"JOY" + j1index + "_BUTTON1\"");

                //Virtual On individual joystick mapping
                ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

                //Virtual On buttons
                ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo1", "\"JOY" + j1index + "_BUTTON3,JOY" + j1index + "_BUTTON\"");
                ini.WriteValue(" Global ", "InputTwinJoyTurbo2", "\"JOY" + j1index + "_BUTTON2,JOY" + j1index + "_BUTTON6\"");

                //Analog joystick (Star Wars Trilogy)
                ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogJoyX", "\"" + _mouse1 + "_XAXIS_INV,JOY" + j1index + "_XAXIS_INV\"");
                ini.WriteValue(" Global ", "InputAnalogJoyY", "\"" + _mouse1 + "_YAXIS_INV,JOY" + j1index + "_YAXIS_INV\"");
                ini.WriteValue(" Global ", "InputSkiRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputSkiX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputSkiY", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputSkiPollLeft", "\"JOY" + j1index + "_ZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiPollRight", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputSkiSelect1", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputSkiSelect2", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputSkiSelect3", "\"JOY" + j1index + "_BUTTON2\"");

                //Magical Truck Adventure controls
                ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputMagicalLever1", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputMagicalPedal1", "\"JOY" + j1index + "_BUTTON1\"");

                if (multiplayer)
                {
                    ini.WriteValue(" Global ", "InputMagicalLever2", "\"JOY" + j2index + "_YAXIS\"");
                    ini.WriteValue(" Global ", "InputMagicalPedal2", "\"JOY" + j2index + "_BUTTON1\"");
                }
                else
                {
                    ini.Remove(" Global ", "InputMagicalLever2");
                    ini.Remove(" Global ", "InputMagicalPedal2");
                }

                //Sega Bass Fishing / Get Bass controls
                ini.WriteValue(" Global ", "InputFishingRodLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickLeft", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickRight", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickUp", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingStickDown", "\"NONE\"");
                ini.WriteValue(" Global ", "InputFishingRodX", "\"JOY" + j1index + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputFishingRodY", "\"JOY" + j1index + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickX", "\"JOY" + j1index + "_RXAXIS\"");
                ini.WriteValue(" Global ", "InputFishingStickY", "\"JOY" + j1index + "_RYAXIS\"");
                ini.WriteValue(" Global ", "InputFishingReel", "\"JOY" + j1index + "_RZAXIS_POS\"");
                ini.WriteValue(" Global ", "InputFishingCast", "\"JOY" + j1index + "_BUTTON3\"");
                ini.WriteValue(" Global ", "InputFishingSelect", "\"JOY" + j1index + "_BUTTON1\"");
                ini.WriteValue(" Global ", "InputFishingTension", "\"NONE\"");

                //deadzones - set 5 as default deadzone, good compromise to avoid joystick drift
                string deadzone = "5";
                if (SystemConfig.isOptSet("supermodel_joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["supermodel_joy_deadzone"]))
                    deadzone = SystemConfig["supermodel_joy_deadzone"].ToIntegerString();

                for (int i = 1; i <= 6; i++)
                {
                    ini.WriteValue(" Global ", "InputJoy" + i + "XDeadZone", deadzone);
                    ini.WriteValue(" Global ", "InputJoy" + i + "YDeadZone", deadzone);
                }

                //other stuff
                ini.WriteValue(" Global ", "XInputConstForceThreshold", "20");
                ini.WriteValue(" Global ", "XInputConstForceMax", "40");
                ini.WriteValue(" Global ", "XInputVibrateMax", "100");
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + c1.DevicePath + " to player : " + c1.PlayerIndex.ToString());
            if (c2 != null && c2.Config != null && !c2.IsKeyboard)
                SimpleLogger.Instance.Info("[INFO] Assigned controller " + c2.DevicePath + " to player : " + c2.PlayerIndex.ToString()); 
        }
        #endregion

        #region keyboard
        /// <summary>
        /// Keyboard
        /// </summary>
        /// <param name="ini"></param>
        /// <param name="c1"></param>
        // no need to diferentiate qwerty and azerty (on azerty keyboard A is recognized as Q)
        private void WriteKeyboardMapping(IniFile ini)
        {
            //common
            ini.WriteValue(" Global ", "InputStart1", "\"KEY_1,KEY_RETURN,KEY_PGDN\"");
            ini.WriteValue(" Global ", "InputStart2", "\"KEY_2\"");
            ini.WriteValue(" Global ", "InputCoin1", "\"KEY_5,KEY_LEFTCTRL,KEY_PGUP,KEY_BACKSPACE\"");
            ini.WriteValue(" Global ", "InputCoin2", "\"KEY_6\"");
            ini.WriteValue(" Global ", "InputServiceA", "\"KEY_3\"");
            ini.WriteValue(" Global ", "InputServiceB", "\"KEY_7\"");
            ini.WriteValue(" Global ", "InputTestA", "\"KEY_4\"");
            ini.WriteValue(" Global ", "InputTestB", "\"KEY_8\"");

            //4-way digital joysticks
            ini.WriteValue(" Global ", "InputJoyUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputJoyDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputJoyLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputJoyRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputJoyUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputJoyDown2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputJoyLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputJoyRight2", "\"NONE\"");

            //Fighting game buttons
            ini.WriteValue(" Global ", "InputPunch", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputKick", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputGuard", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputEscape", "\"KEY_F\"");
            ini.WriteValue(" Global ", "InputPunch2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputKick2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGuard2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputEscape2", "\"NONE\"");

            //Spikeout buttons
            ini.WriteValue(" Global ", "InputShift", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputBeat", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputCharge", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputJump", "\"KEY_F\"");

            //Virtua Striker buttons
            ini.WriteValue(" Global ", "InputShortPass", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputLongPass", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputShoot", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputShortPass2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputLongPass2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputShoot2", "\"NONE\"");

            //Steering wheel
            ini.WriteValue(" Global ", "InputSteeringLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputSteeringRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputSteering", "\"NONE\"");

            //Pedals
            ini.WriteValue(" Global ", "InputAccelerator", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputBrake", "\"KEY_DOWN\"");

            //Up/down shifter manual transmission (all racers)
            ini.WriteValue(" Global ", "InputGearShiftUp", "\"KEY_Y\"");
            ini.WriteValue(" Global ", "InputGearShiftDown", "\"KEY_H\"");

            //4-Speed manual transmission (Daytona 2, Sega Rally 2, Scud Race)
            ini.WriteValue(" Global ", "InputGearShift1", "\"KEY_Q\"");
            ini.WriteValue(" Global ", "InputGearShift2", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputGearShift3", "\"KEY_E\"");
            ini.WriteValue(" Global ", "InputGearShift4", "\"KEY_R\"");
            ini.WriteValue(" Global ", "InputGearShiftN", "\"KEY_T\"");

            //VR4 view change buttons (Daytona 2, Le Mans 24, Scud Race)
            ini.WriteValue(" Global ", "InputVR1", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputVR2", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputVR3", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputVR4", "\"KEY_F\"");

            //Single view change button (Dirt Devils, ECA, Harley-Davidson, Sega Rally 2)
            ini.WriteValue(" Global ", "InputViewChange", "\"KEY_A\"");

            //Handbrake (Dirt Devils, Sega Rally 2)
            ini.WriteValue(" Global ", "InputHandBrake", "\"KEY_S\"");

            //Harley-Davidson controls
            ini.WriteValue(" Global ", "InputRearBrake", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputMusicSelect", "\"KEY_D\"");

            //Virtual On macros
            ini.WriteValue(" Global ", "InputTwinJoyTurnLeft", "\"KEY_Q\"");
            ini.WriteValue(" Global ", "InputTwinJoyTurnRight", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputTwinJoyForward", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputTwinJoyReverse", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputTwinJoyStrafeLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputTwinJoyStrafeRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputTwinJoyJump", "\"KEY_E\"");
            ini.WriteValue(" Global ", "InputTwinJoyCrouch", "\"KEY_R\"");

            //Virtual On individual joystick mapping
            ini.WriteValue(" Global ", "InputTwinJoyLeft1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyRight1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyRight2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyUp1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyDown1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputTwinJoyDown2", "\"NONE\"");

            //Virtual On buttons
            ini.WriteValue(" Global ", "InputTwinJoyShot1", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputTwinJoyShot2", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputTwinJoyTurbo1", "\"KEY_Z\"");
            ini.WriteValue(" Global ", "InputTwinJoyTurbo2", "\"KEY_X\"");

            //Analog joystick (Star Wars Trilogy)
            ini.WriteValue(" Global ", "InputAnalogJoyLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputAnalogJoyRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputAnalogJoyUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputAnalogJoyDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputAnalogJoyX", "\"" + _mouse1 + "_XAXIS_INV\"");
            ini.WriteValue(" Global ", "InputAnalogJoyY", "\"" + _mouse1 + "_YAXIS_INV\"");
            ini.WriteValue(" Global ", "InputAnalogJoyTrigger", "\"KEY_A,"+ _mouse1 + "_LEFT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAnalogJoyEvent", "\"KEY_S," + _mouse1 +"_RIGHT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAnalogJoyTrigger2", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputAnalogJoyEvent2", "\"NONE\"");

            //Light guns (Lost World)
            ini.WriteValue(" Global ", "InputGunLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputGunRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputGunUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputGunDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputGunX", "\"" + _mouse1 + "_XAXIS\"");
            ini.WriteValue(" Global ", "InputGunY", "\"" + _mouse1 + "_YAXIS\"");
            ini.WriteValue(" Global ", "InputTrigger", "\"KEY_A," + _mouse1 + "_LEFT_BUTTON\"");
            ini.WriteValue(" Global ", "InputOffscreen", "\"KEY_S," + _mouse1 + "_RIGHT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAutoTrigger", "1");
            ini.WriteValue(" Global ", "InputGunLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGunRight2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGunUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputGunDown2", "\"NONE\"");

            if (_multigun)
            {
                ini.WriteValue(" Global ", "InputGunX2", "\"" + _mouse2 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputGunY2", "\"" + _mouse2 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputTrigger2", "\"" + _mouse2 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputOffscreen2", "\"" + _mouse2 + "_RIGHT_BUTTON\"");
            }
            else
            {
                ini.WriteValue(" Global ", "InputGunX2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputGunY2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputTrigger2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputOffscreen2", "\"NONE\"");
            }
            ini.WriteValue(" Global ", "InputAutoTrigger2", "1");

            //Analog guns (Ocean Hunter, LA Machineguns)
            ini.WriteValue(" Global ", "InputAnalogGunLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputAnalogGunRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputAnalogGunUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputAnalogGunDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + _mouse1 + "_XAXIS\"");
            ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + _mouse1 + "_YAXIS\"");
            ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "\"KEY_A," + _mouse1 + "_LEFT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAnalogTriggerRight", "\"KEY_S," + _mouse1 + "_RIGHT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAnalogGunLeft2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogGunRight2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogGunUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputAnalogGunDown2", "\"NONE\"");

            if (_multigun)
            {
                ini.WriteValue(" Global ", "InputAnalogGunX2", "\"" + _mouse2 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogGunY2", "\"" + _mouse2 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"" + _mouse2 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"" + _mouse2 + "_RIGHT_BUTTON\"");
            }
            else
            {
                ini.WriteValue(" Global ", "InputAnalogGunX2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogGunY2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", "\"NONE\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight2", "\"NONE\"");
            }

            //Ski Champ controls
            ini.WriteValue(" Global ", "InputSkiLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputSkiRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputSkiUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputSkiDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputSkiX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputSkiY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputSkiPollLeft", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputSkiPollRight", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputSkiSelect1", "\"KEY_Q\"");
            ini.WriteValue(" Global ", "InputSkiSelect2", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputSkiSelect3", "\"KEY_E\"");

            //Magical Truck Adventure controls
            ini.WriteValue(" Global ", "InputMagicalLeverUp1", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputMagicalLeverDown1", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputMagicalLeverUp2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalLeverDown2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalLever1", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalLever2", "\"NONE\"");
            ini.WriteValue(" Global ", "InputMagicalPedal1", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputMagicalPedal2", "\"KEY_S\"");

            //Sega Bass Fishing / Get Bass controls
            ini.WriteValue(" Global ", "InputFishingRodLeft", "\"KEY_LEFT\"");
            ini.WriteValue(" Global ", "InputFishingRodRight", "\"KEY_RIGHT\"");
            ini.WriteValue(" Global ", "InputFishingRodUp", "\"KEY_UP\"");
            ini.WriteValue(" Global ", "InputFishingRodDown", "\"KEY_DOWN\"");
            ini.WriteValue(" Global ", "InputFishingStickLeft", "\"KEY_A\"");
            ini.WriteValue(" Global ", "InputFishingStickRight", "\"KEY_D\"");
            ini.WriteValue(" Global ", "InputFishingStickUp", "\"KEY_W\"");
            ini.WriteValue(" Global ", "InputFishingStickDown", "\"KEY_S\"");
            ini.WriteValue(" Global ", "InputFishingRodX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingRodY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingStickX", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingStickY", "\"NONE\"");
            ini.WriteValue(" Global ", "InputFishingReel", "\"KEY_SPACE\"");
            ini.WriteValue(" Global ", "InputFishingCast", "\"KEY_Z\"");
            ini.WriteValue(" Global ", "InputFishingSelect", "\"KEY_X\"");
            ini.WriteValue(" Global ", "InputFishingTension", "\"KEY_T\"");
        }
        #endregion

        private string GetDinputMapping(int index, SdlToDirectInput c, string buttonkey, int direction = 1, bool wheel = false)
        {
            if (c == null)
                return "";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "";
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "";
            }

            string button = c.ButtonMappings[buttonkey];

            // For wheels it seems axis 2 is recognized as RZAXIS, not ZAXIS
            if (wheel && button == "a2")
                button = "a5";

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger()) + 1;
                return "JOY" + index + "_BUTTON" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "JOY" + index + "_POV1_UP";
                    case 2:
                        return "JOY" + index + "_POV1_RIGHT";
                    case 4:
                        return "JOY" + index + "_POV1_DOWN";
                    case 8:
                        return "JOY" + index + "_POV1_LEFT";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    direction = -1;
                }

                else if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    direction = 1;
                }

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                switch (axisID)
                {
                    case 0:
                        if (direction == 1) return "JOY" + index + "_XAXIS_POS";             // right/down/push
                        else if (direction == -1) return "JOY" + index + "_XAXIS_NEG";       // left/up/release
                        else return "JOY" + index + "_XAXIS";
                    case 1:
                        if (direction == 1) return "JOY" + index + "_YAXIS_POS";
                        else if (direction == -1) return "JOY" + index + "_YAXIS_NEG";
                        else return "JOY" + index + "_YAXIS";
                    case 2:
                        if (direction == 1) return "JOY" + index + "_ZAXIS_POS";
                        else if (direction == -1) return "JOY" + index + "_ZAXIS_NEG";
                        else return "JOY" + index + "_ZAXIS";
                    case 3:
                        if (direction == 1) return "JOY" + index + "_RXAXIS_POS";
                        else if (direction == -1) return "JOY" + index + "_RXAXIS_NEG";
                        else return "JOY" + index + "_RXAXIS";
                    case 4:
                        if (direction == 1) return "JOY" + index + "_RYAXIS_POS";
                        else if (direction == -1) return "JOY" + index + "_RYAXIS_NEG";
                        else return "JOY" + index + "_RYAXIS";
                    case 5:
                        if (direction == 1) return "JOY" + index + "_RZAXIS_POS";
                        else if(direction == -1) return "JOY" + index + "_RZAXIS_NEG";
                        else return "JOY" + index + "_RZAXIS";
                }
            }

            return "";
        }

        private string GetWheelMapping(string button, SdlToDirectInput wheel, int index, string direction = "nul", bool invertAxis = false, int shifterid = -1)
        {
            if (wheel == null)
                return "\"NONE\"";

            string ret;

            if (button.StartsWith("button_"))
            {
                if (shifterid != -1)
                {
                    int buttonID = (button.Substring(7).ToInteger()) + 1;
                    return "\"JOY" + shifterid + "_BUTTON" + buttonID + "\"";
                }
                else
                {
                    int buttonID = (button.Substring(7).ToInteger()) + 1;
                    return "\"JOY" + index + "_BUTTON" + buttonID + "\"";
                }
            }
            
            else if (button.StartsWith("dp"))
                return "\"" + GetDinputMapping(index, wheel, button) + "\"";

            else
            {
                switch (button)
                {
                    case "throttle":
                    case "brake":
                    case "lefttrigger":
                    case "righttrigger":
                        return "\"" + GetDinputMapping(index, wheel, button, -1, true) + "\"";
                    case "leftx":
                        if (direction == "left")
                            ret = GetDinputMapping(index, wheel, button, -1, true);
                        else if (direction == "right")
                            ret = GetDinputMapping(index, wheel, button, 1, true);
                        else
                            ret = GetDinputMapping(index, wheel, button, 0, true);
                        return invertAxis ? ("\"" + ret + "_INV" + "\"") : ("\"" + ret + "\"");
                    case "rightshoulder":
                    case "leftshoulder":
                        return "\"" + GetDinputMapping(index, wheel, button, 0, true) + "\"";
                }
            }
            SimpleLogger.Instance.Info("[INFO] No mapping found for " + button + " in wheel database.");
            return "\"NONE\"";
        }

        private static string GetWheelButton(Dictionary<string, string> mapping, string buttonkey)
        {
            if (mapping.ContainsKey(buttonkey) && !string.IsNullOrEmpty(mapping[buttonkey]))
                return mapping[buttonkey];
            else
                return "";
        }

        static readonly string[] mappingPaths =
        {            
            // User specific
            "{userpath}\\inputmapping\\supermodel.yml",

            // RetroBat Default
            "{systempath}\\resources\\inputmapping\\supermodel.yml",
        };

        private static readonly List<string> inputValues = new List<string>
        {
            "InputStart1",
            "InputStart2",
            "InputCoin1",
            "InputCoin2",
            "InputServiceA",
            "InputServiceB",
            "InputTestA",
            "InputTestB",
            "InputJoyUp",
            "InputJoyDown",
            "InputJoyLeft",
            "InputJoyRight",
            "InputJoyUp2",
            "InputJoyDown2",
            "InputJoyLeft2",
            "InputJoyRight2",
            "InputPunch",
            "InputKick",
            "InputGuard",
            "InputEscape",
            "InputPunch2",
            "InputKick2",
            "InputGuard2",
            "InputEscape2",
            "InputShift",
            "InputBeat",
            "InputCharge",
            "InputJump",
            "InputShortPass",
            "InputLongPass",
            "InputShoot",
            "InputShortPass2",
            "InputLongPass2",
            "InputShoot2",
            "InputSteeringLeft",
            "InputSteeringRight",
            "InputSteering",
            "InputAccelerator",
            "InputBrake",
            "InputGearShiftUp",
            "InputGearShiftDown",
            "InputGearShift1",
            "InputGearShift2",
            "InputGearShift3",
            "InputGearShift4",
            "InputGearShiftN",
            "InputVR1",
            "InputVR2",
            "InputVR3",
            "InputVR4",
            "InputViewChange",
            "InputHandBrake",
            "InputRearBrake",
            "InputMusicSelect",
            "InputTwinJoyTurnLeft",
            "InputTwinJoyTurnRight",
            "InputTwinJoyForward",
            "InputTwinJoyReverse",
            "InputTwinJoyStrafeLeft",
            "InputTwinJoyStrafeRight",
            "InputTwinJoyJump",
            "InputTwinJoyCrouch",
            "InputTwinJoyLeft1",
            "InputTwinJoyLeft2",
            "InputTwinJoyRight1",
            "InputTwinJoyRight2",
            "InputTwinJoyUp1",
            "InputTwinJoyUp2",
            "InputTwinJoyDown1",
            "InputTwinJoyDown2",
            "InputTwinJoyShot1",
            "InputTwinJoyShot2",
            "InputTwinJoyTurbo1",
            "InputTwinJoyTurbo2",
            "InputAnalogJoyLeft",
            "InputAnalogJoyRight",
            "InputAnalogJoyUp",
            "InputAnalogJoyDown",
            "InputAnalogJoyX",
            "InputAnalogJoyY",
            "InputAnalogJoyTrigger",
            "InputAnalogJoyEvent",
            "InputAnalogJoyTrigger2",
            "InputAnalogJoyEvent2",
            "InputGunLeft",
            "InputGunRight",
            "InputGunUp",
            "InputGunDown",
            "InputGunX",
            "InputGunY",
            "InputTrigger",
            "InputOffscreen",
            "InputAutoTrigger",
            "InputGunLeft2",
            "InputGunRight2",
            "InputGunUp2",
            "InputGunDown2",
            "InputGunX2",
            "InputGunY2",
            "InputTrigger2",
            "InputOffscreen2",
            "InputAutoTrigger2",
            "InputAnalogGunLeft",
            "InputAnalogGunRight",
            "InputAnalogGunUp",
            "InputAnalogGunDown",
            "InputAnalogGunX",
            "InputAnalogGunY",
            "InputAnalogTriggerLeft",
            "InputAnalogTriggerRight",
            "InputAnalogGunLeft2",
            "InputAnalogGunRight2",
            "InputAnalogGunUp2",
            "InputAnalogGunDown2",
            "InputAnalogGunX2",
            "InputAnalogGunY2",
            "InputAnalogTriggerLeft2",
            "InputAnalogTriggerRight2",
            "InputSkiLeft",
            "InputSkiRight",
            "InputSkiUp",
            "InputSkiDown",
            "InputSkiX",
            "InputSkiY",
            "InputSkiPollLeft",
            "InputSkiPollRight",
            "InputSkiSelect1",
            "InputSkiSelect2",
            "InputSkiSelect3",
            "InputMagicalLeverUp1",
            "InputMagicalLeverDown1",
            "InputMagicalLeverUp2",
            "InputMagicalLeverDown2",
            "InputMagicalLever1",
            "InputMagicalLever2",
            "InputMagicalPedal1",
            "InputMagicalPedal2",
            "InputFishingRodLeft",
            "InputFishingRodRight",
            "InputFishingRodUp",
            "InputFishingRodDown",
            "InputFishingStickLeft",
            "InputFishingStickRight",
            "InputFishingStickUp",
            "InputFishingStickDown",
            "InputFishingRodX",
            "InputFishingRodY",
            "InputFishingStickX",
            "InputFishingStickY",
            "InputFishingReel",
            "InputFishingCast",
            "InputFishingSelect",
            "InputFishingTension",
        };

        private static readonly List<string> p2values = new List<string>
        {
            "InputStart2",
            "InputCoin2",
            "InputServiceB",
            "InputTestB",
            "InputJoyUp2",
            "InputJoyDown2",
            "InputJoyLeft2",
            "InputJoyRight2",
            "InputPunch2",
            "InputKick2",
            "InputGuard2",
            "InputEscape2",
            "InputShortPass2",
            "InputLongPass2",
            "InputShoot2",
            "InputAnalogJoyTrigger2",
            "InputAnalogJoyEvent2",
            "InputGunLeft2",
            "InputGunRight2",
            "InputGunUp2",
            "InputGunDown2",
            "InputGunX2",
            "InputGunY2",
            "InputTrigger2",
            "InputOffscreen2",
            "InputAutoTrigger2",
            "InputAnalogGunLeft2",
            "InputAnalogGunRight2",
            "InputAnalogGunUp2",
            "InputAnalogGunDown2",
            "InputAnalogGunX2",
            "InputAnalogGunY2",
            "InputAnalogTriggerLeft2",
            "InputAnalogTriggerRight2",
            "InputMagicalLeverUp2",
            "InputMagicalLeverDown2",
            "InputMagicalLever2",
            "InputMagicalPedal2",
        };

        private static readonly List<string> dInputSpecials = new List<string>
        {
            "righty-up",
            "righty-down",
            "lefty-up",
            "lefty-down",
            "rightx-left",
            "rightx-right",
            "leftx-left",
            "leftx-right"
        };

        private static Dictionary<string,string> faceButtons = new Dictionary<string, string>
        {
            { "north", "y" },
            { "south", "a" },
            { "west", "x" },
            { "east", "b" },
            { "select", "back" }
        };

        private static Dictionary<string, string> nintendoMapping = new Dictionary<string, string>
        {
            { "BUTTON3", "BUTTON4" },
            { "BUTTON4", "BUTTON3" },
            { "BUTTON1", "BUTTON2" },
            { "BUTTON2", "BUTTON1" }
        };

        private static Dictionary<string, string> ymltoXinput = new Dictionary<string, string>
        {
            { "start", "BUTTON8" },
            { "back", "BUTTON7" },
            { "leftstick", "BUTTON9" },
            { "rightstick", "BUTTON10" },
            { "leftx", "XAXIS" },
            { "lefty", "YAXIS" },
            { "rightx", "RXAXIS" },
            { "righty", "RYAXIS" },
            { "rightshoulder", "BUTTON6" },
            { "leftshoulder", "BUTTON5" },
            { "dpup", "POV1_UP" },
            { "dpdown", "POV1_DOWN" },
            { "dpleft", "POV1_LEFT" },
            { "dpright", "POV1_RIGHT" },
            { "righttrigger", "RZAXIS_POS" },
            { "lefttrigger", "ZAXIS_POS" },
            { "righty-up", "RYAXIS_NEG" },
            { "righty-down", "RYAXIS_POS" },
            { "lefty-up", "YAXIS_NEG" },
            { "lefty-down", "YAXIS_POS" },
            { "rightx-left", "RXAXIS_NEG" },
            { "rightx-right", "RXAXIS_POS" },
            { "leftx-left", "XAXIS_NEG" },
            { "leftx-right", "XAXIS_POS" },
            { "south", "BUTTON1" },
            { "north", "BUTTON4" },
            { "west", "BUTTON3" },
            { "east", "BUTTON2" }
        };

        private static RawInputDevice FindAssociatedKeyboard(string gunPath, List<RawInputDevice> keyboards, RawInputDevice keyboard)
        {
            // Handle Wiimote4Guns differently
            if (gunPath.ToLowerInvariant().Contains("vmulti"))
            {
                string gunIdentifier = "";
                if (gunPath.ToLowerInvariant().Contains("vmultia"))
                    gunIdentifier = "vmultia";
                else if (gunPath.ToLowerInvariant().Contains("vmultib"))
                    gunIdentifier = "vmultib";
                else if (gunPath.ToLowerInvariant().Contains("vmultic"))
                    gunIdentifier = "vmultic";
                else if (gunPath.ToLowerInvariant().Contains("vmultid"))
                    gunIdentifier = "vmultid";

                foreach (var kb in keyboards)
                {
                    if (kb.DevicePath.ToLowerInvariant().Contains(gunIdentifier))
                    {
                        return kb;
                    }
                }
            }
            return keyboard;
        }

        private void ConfigureGuns(IniFile ini, RawLightgun[] guns)
        {
            bool useGun = SystemConfig.getOptBoolean("use_guns");
            if (guns == null || guns.Length == 0 || !useGun)
                return;

            var hidDevices = RawInputDevice.GetRawInputDevices().ToList();
            var keyboards = hidDevices.Where(t => t.Type == RawInputDeviceType.Keyboard).OrderBy(u => u.DevicePath).ToList();

            // Keyboard association for Wiimote4Guns
            Dictionary<RawLightgun, RawInputDevice> gunsKbAssociation = new Dictionary<RawLightgun, RawInputDevice>();
            if (guns.Any(g => g.Type == RawLighGunType.Wiimote4Guns))
            {
                SimpleLogger.Instance.Info("[GUNS] Found " + keyboards.Count + " usable keyboards.");

                foreach (var gun in guns.Where(g => g.Type == RawLighGunType.Wiimote4Guns))
                {
                    var associatedKeyboard = FindAssociatedKeyboard(gun.DevicePath, keyboards, null);
                    if (associatedKeyboard != null)
                    {
                        gunsKbAssociation.Add(gun, associatedKeyboard);
                        SimpleLogger.Instance.Info("[GUNS] Associated keyboard for " + gun.Name + ": " + associatedKeyboard.FriendlyName);
                    }
                }
            }

            // default to multigun if we have more than one usable gun and the inputdriver is not set to sdl (which do not support multigun)
            bool hasSdlInputDriverConfigured = SystemConfig.isOptSet("inputdriver") && SystemConfig["inputdriver"].StartsWith("sdl");

            if (hasSdlInputDriverConfigured && !SystemConfig.getOptBoolean("multigun"))
                _multigun = true;

            string tech = ini.GetValue(" Global ", "InputSystem");
            if (_multigun)
            {
                // multigun and sdl are incompatible, so change our input mapping to dinput by default.
                if (tech != null && tech.StartsWith("sdl"))
                    tech = "dinput";
                SimpleLogger.Instance.Info("[GUNS] Using multigun with input mappings from " + tech + ".");
            }
            else SimpleLogger.Instance.Info("[GUNS] Not using multigun.");

            string mouseIndex1 = "1";
            string mouseIndex2 = "2";

            if (_gunCount > 0 && guns.Length > 0)
            {
                // Process guns based on their priority order from RawLightGun.cs
                var wiimote4Guns = guns.Where(g => g.Type == RawLighGunType.Wiimote4Guns).ToArray();

                // Map vmulti* identifiers to system indexes for Wiimote4Guns
                var vmultiToSystemIndex = new Dictionary<string, int>();
                for (int i = 0; i < guns.Length; i++)
                {
                    if (guns[i].Type == RawLighGunType.Wiimote4Guns && guns[i].DevicePath != null)
                    {
                        string vmultiId = "";
                        if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultia"))
                            vmultiId = "vmultia";
                        else if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultib"))
                            vmultiId = "vmultib";
                        else if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultic"))
                            vmultiId = "vmultic";
                        else if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultid"))
                            vmultiId = "vmultid";

                        if (!string.IsNullOrEmpty(vmultiId))
                            vmultiToSystemIndex[vmultiId] = guns[i].Index + 1;
                    }
                }

                // Apply Supermodel inversion: MOUSE(N) = TotalSouris - SystemIndex + 1
                int totalMice = guns.Length;

                // Find P1 and P2 by priority order
                int playerCount = 0;
                for (int i = 0; i < guns.Length && playerCount < 2; i++)
                {
                    playerCount++;
                    int systemIndex = guns[i].Index + 1;
                    string supermodelIndex = (totalMice - systemIndex + 1).ToString();

                    if (playerCount == 1)
                        mouseIndex1 = supermodelIndex;
                    else if (playerCount == 2)
                        mouseIndex2 = supermodelIndex;

                    // Log based on gun type
                    if (guns[i].Type == RawLighGunType.Wiimote4Guns)
                    {
                        string vmultiId = "";
                        if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultia"))
                            vmultiId = "vmultia";
                        else if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultib"))
                            vmultiId = "vmultib";
                        else if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultic"))
                            vmultiId = "vmultic";
                        else if (guns[i].DevicePath.ToLowerInvariant().Contains("vmultid"))
                            vmultiId = "vmultid";

                        if (gunsKbAssociation.ContainsKey(guns[i]))
                        {
                            var kb = gunsKbAssociation[guns[i]];
                            int kbIndex = keyboards.IndexOf(kb) + 1;
                            SimpleLogger.Instance.Info("[GUNS] P" + playerCount + " (" + vmultiId + ") keyboard index: " + kbIndex);
                        }
                        SimpleLogger.Instance.Info("[GUNS] P" + playerCount + " (" + vmultiId + ") system index: " + systemIndex + " → Supermodel MOUSE" + supermodelIndex);
                    }
                    else
                    {
                        SimpleLogger.Instance.Info("[GUNS] P" + playerCount + " (" + guns[i].Type + ") system index: " + systemIndex + " → Supermodel MOUSE" + supermodelIndex);
                    }
                }

                SimpleLogger.Instance.Info("[GUNS] Total mice: " + totalMice + " - Final mouse indexes - P1: " + mouseIndex1 + ", P2: " + mouseIndex2);

                if (SystemConfig.isOptSet("use_guns") && guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
                {
                    Guns.StartSindenSoftware();
                    _sindenSoft = true;
                }
            }

            if (SystemConfig.isOptSet("supermodel_gun1") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun1"]))
                mouseIndex1 = SystemConfig["supermodel_gun1"];
            if (SystemConfig.isOptSet("supermodel_gun2") && !string.IsNullOrEmpty(SystemConfig["supermodel_gun2"]))
                mouseIndex2 = SystemConfig["supermodel_gun2"];

            _mouse1 = "MOUSE" + mouseIndex1;
            _mouse2 = "MOUSE" + mouseIndex2;

            if (!_multigun)
            {
                _mouse1 = _mouse2 = "MOUSE";
                ini.WriteValue(" Global ", "Crosshairs", "1");
            }
            else
                ini.WriteValue(" Global ", "Crosshairs", "3");

            // Write gun mappings
            ini.WriteValue(" Global ", "InputGunX", "\"" + _mouse1 + "_XAXIS\"");
            ini.WriteValue(" Global ", "InputGunY", "\"" + _mouse1 + "_YAXIS\"");
            ini.WriteValue(" Global ", "InputTrigger", "\"" + _mouse1 + "_LEFT_BUTTON\"");
            ini.WriteValue(" Global ", "InputOffscreen", "\"" + _mouse1 + "_RIGHT_BUTTON\"");
            ini.WriteValue(" Global ", "InputAutoTrigger", "1");

            if (_multigun)
            {
                ini.WriteValue(" Global ", "InputGunX2", "\"" + _mouse2 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputGunY2", "\"" + _mouse2 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputTrigger2", "\"" + _mouse2 + "_LEFT_BUTTON\"");
                ini.WriteValue(" Global ", "InputOffscreen2", "\"" + _mouse2 + "_RIGHT_BUTTON\"");
                ini.WriteValue(" Global ", "InputAutoTrigger2", "1");
            }

            // Analog guns mappings
            ini.WriteValue(" Global ", "InputAnalogGunX", "\"" + _mouse1 + "_XAXIS\"");
            ini.WriteValue(" Global ", "InputAnalogGunY", "\"" + _mouse1 + "_YAXIS\"");
            ini.WriteValue(" Global ", "InputAnalogTriggerLeft", "KEY_A," + _mouse1 + "_LEFT_BUTTON");
            ini.WriteValue(" Global ", "InputAnalogTriggerRight", "KEY_S," + _mouse1 + "_RIGHT_BUTTON");

            if (_multigun)
            {
                ini.WriteValue(" Global ", "InputAnalogGunX2", "\"" + _mouse2 + "_XAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogGunY2", "\"" + _mouse2 + "_YAXIS\"");
                ini.WriteValue(" Global ", "InputAnalogTriggerLeft2", _mouse2 + "_LEFT_BUTTON");
                ini.WriteValue(" Global ", "InputAnalogTriggerRight2", _mouse2 + "_RIGHT_BUTTON");
            }

            // Star Wars trilogy
            Dictionary<string, string> starWarsMapping = new Dictionary<string, string>();

            string InputAnalogJoyX = ini.GetValue(" Global ", "InputAnalogJoyX");
            if (!string.IsNullOrEmpty(InputAnalogJoyX))
                starWarsMapping.Add("InputAnalogJoyX", InputAnalogJoyX);
            string InputAnalogJoyY = ini.GetValue(" Global ", "InputAnalogJoyY");
            if (!string.IsNullOrEmpty(InputAnalogJoyY))
                starWarsMapping.Add("InputAnalogJoyY", InputAnalogJoyY);
            string InputAnalogJoyTrigger = ini.GetValue(" Global ", "InputAnalogJoyTrigger");
            if (!string.IsNullOrEmpty(InputAnalogJoyTrigger))
                starWarsMapping.Add("InputAnalogJoyTrigger", InputAnalogJoyTrigger);
            string InputAnalogJoyEvent = ini.GetValue(" Global ", "InputAnalogJoyEvent");
            if (!string.IsNullOrEmpty(InputAnalogJoyEvent))
                starWarsMapping.Add("InputAnalogJoyEvent", InputAnalogJoyEvent);

            if (!_multigun)
            {
                foreach (var entry in starWarsMapping)
                {
                    string newValue = Regex.Replace(entry.Value, @"_MOUSE\d+", "_MOUSE");

                    if (newValue != entry.Value)
                        ini.WriteValue(" Global ", entry.Key, newValue);
                }
            }
            else
            {
                foreach (var entry in starWarsMapping)
                {
                    string targetValue = _mouse1 + "_";
                    string newValue = Regex.Replace(entry.Value, @"_MOUSE\d*_", targetValue);

                    if (newValue != entry.Value)
                        ini.WriteValue(" Global ", entry.Key, newValue);
                }
            }
        }
    }
}
