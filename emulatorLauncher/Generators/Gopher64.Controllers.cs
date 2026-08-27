using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmulatorLauncher
{
    partial class Gopher64Generator : Generator
    {
        // "inputs" table index in config.json >= 1.1.34
        // (cf. src/ui/input_profile.rs, PROFILE_SIZE=19)
        private const int PROFILE_SIZE = 19;

        private static readonly Dictionary<string, int> _profileIndex = new Dictionary<string, int>
        {
            { "Right", 0 },        // R_DPAD
            { "Left", 1 },         // L_DPAD
            { "Down", 2 },         // D_DPAD
            { "Up", 3 },           // U_DPAD
            { "Start", 4 },        // START_BUTTON
            { "Z", 5 },            // Z_TRIG
            { "B", 6 },            // B_BUTTON
            { "A", 7 },            // A_BUTTON
            { "CRight", 8 },       // R_CBUTTON
            { "CLeft", 9 },        // L_CBUTTON
            { "CDown", 10 },       // D_CBUTTON
            { "CUp", 11 },         // U_CBUTTON
            { "R", 12 },           // R_TRIG
            { "L", 13 },           // L_TRIG
            { "Stickright", 14 },  // AXIS_RIGHT
            { "Stickleft", 15 },   // AXIS_LEFT
            { "Stickdown", 16 },   // AXIS_DOWN
            { "Stickup", 17 },     // AXIS_UP
            { "Hotkey", 18 },      // HOTKEY (hold + button = pak change, savestate, loadstate, fast-forward, rewind...)
        };

        // Default keyboard keys for player 1 (SDL_SCANCODE_*, same values as the native gopher64 default profile)
        private static readonly Dictionary<string, int> _player1KeyboardScancodes = new Dictionary<string, int>
        {
            { "Right", 7 },     // D            (SDL_SCANCODE_D)
            { "Left", 4 },      // A            (SDL_SCANCODE_A)
            { "Down", 22 },     // S            (SDL_SCANCODE_S)
            { "Up", 26 },       // W            (SDL_SCANCODE_W)
            { "Start", 40 },    // Return       (SDL_SCANCODE_RETURN)
            { "Z", 29 },        // Z            (SDL_SCANCODE_Z)
            { "B", 224 },       // Left Ctrl    (SDL_SCANCODE_LCTRL)
            { "A", 225 },       // Left Shift   (SDL_SCANCODE_LSHIFT)
            { "CRight", 15 },   // L            (SDL_SCANCODE_L)
            { "CLeft", 13 },    // J            (SDL_SCANCODE_J)
            { "CDown", 14 },    // K            (SDL_SCANCODE_K)
            { "CUp", 12 },      // I            (SDL_SCANCODE_I)
            { "R", 6 },         // C            (SDL_SCANCODE_C)
            { "L", 27 },        // X            (SDL_SCANCODE_X)
            { "Stickright", 79 }, // Right arrow (SDL_SCANCODE_RIGHT)
            { "Stickleft", 80 },  // Left arrow  (SDL_SCANCODE_LEFT)
            { "Stickdown", 81 },  // Down arrow  (SDL_SCANCODE_DOWN)
            { "Stickup", 82 },    // Up arrow    (SDL_SCANCODE_UP)
        };

        private static readonly HashSet<string> _triggerAxisActions = new HashSet<string> { "Z", "R", "L" };

        private void ConfigureControls(JObject input, JObject profiles)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            Sdl3GameController.SetEnumerationHints(Sdl3GameController.Sdl3HintProfile.Sdl3Default);

            // Cleanup all
            profiles.Property("RetroBat1")?.Remove();
            profiles.Property("RetroBat2")?.Remove();
            profiles.Property("RetroBat3")?.Remove();
            profiles.Property("RetroBat4")?.Remove();

            JArray bindingArray = new JArray("default", "default", "default", "default");
            input["input_profile_binding"] = bindingArray;

            JArray controllerAssignment = new JArray(null, null, null, null);
            input["controller_assignment"] = controllerAssignment;

            JArray enableControllers = new JArray(false, false, false, false);
            input["controller_enabled"] = enableControllers;

            // Transfer Pak : not managed by the generator for the moment (no ES UI to choose
            // a Game Boy rom/save), we leave it disabled on all 4 ports.
            JArray transferPack = new JArray(false, false, false, false);
            input["transfer_pak"] = transferPack;

            bindingArray = new JArray();
            enableControllers = new JArray();
            controllerAssignment = new JArray();

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(4))
            {
                int index = controller.PlayerIndex;
                string profile = "RetroBat" + index.ToString();

                if (profiles[profile] == null || profiles[profile].Type != JTokenType.Object)
                    profiles[profile] = new JObject();
                var cProfile = (JObject)profiles[profile];

                bool assigned = ConfigureInput(cProfile, controller, controllerAssignment);

                bindingArray.Add(profile);
                enableControllers.Add(assigned);
            }

            // Fill next sections
            while (bindingArray.Count > 4)
                bindingArray.RemoveAt(bindingArray.Count - 1);
            while (bindingArray.Count < 4)
                bindingArray.Add("default");
            input["input_profile_binding"] = bindingArray;

            while (controllerAssignment.Count > 4)
                controllerAssignment.RemoveAt(controllerAssignment.Count - 1);
            while (controllerAssignment.Count < 4)
                controllerAssignment.Add(null);
            input["controller_assignment"] = controllerAssignment;

            while (enableControllers.Count > 4)
                enableControllers.RemoveAt(enableControllers.Count - 1);
            while (enableControllers.Count < 4)
                enableControllers.Add(false);
            input["controller_enabled"] = enableControllers;
        }

        /// <summary>
        /// gopher64 format profile for >= 1.1.34 ("inputs": 19 cases x 2 bindings).
        /// Returns true if the controller could be identified via SDL3 (or, failing that, via the fallbacks
        /// SDL2 then XInput below), therefore actually assignable.
        /// </summary>
        private bool ConfigureInput(JObject profile, Controller controller, JArray controllerAssignment)
        {
            // gopher64 compares controller_assignment[i] literally to SDL_GetJoystickPathForID().
            var sdl3 = controller.Sdl3Controller;
            string cPath = sdl3?.Path;
            bool isRecognizedGamepad = sdl3 != null && sdl3.IsGamepad;

            if (string.IsNullOrEmpty(cPath))
            {
                // No SDL3 match for this device (see Controller.cs, log "No SDL3 match" -
                // can happen if internal matching fails, or if SDL3Controllers.dll did not resolve
                // this device).
                //
                // 1st fallback: if it's an XInput controller ==> reconstruct the literal format
                // "XInput#<index>"
                if (controller.IsXInputDevice && controller.XInput != null)
                {
                    cPath = "XInput#" + controller.XInput.DeviceIndex;
                    isRecognizedGamepad = true; // any XInput controller is a SDL gamepad
                    SimpleLogger.Instance.Warning("[WARNING] Gopher64 : no SDL3 match for XInput controller " + controller.ToShortString() + ", falling back to literal \"XInput#\" path.");
                }
                else
                {
                    // 2nd fallback (non-XInput controllers only): the SDL2 mapping
                    var sdl2 = controller.SdlController;
                    if (sdl2 != null && !string.IsNullOrEmpty(sdl2.Path))
                    {
                        cPath = sdl2.Path;
                        isRecognizedGamepad = true;
                        SimpleLogger.Instance.Warning("[WARNING] Gopher64 : no SDL3 match for controller " + controller.ToShortString() + ", falling back to SDL2 path/mapping.");
                    }
                    else
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Gopher64 : no SDL3, XInput or SDL2 path found for controller " + controller.ToShortString() + ", it will not be assigned.");
                        controllerAssignment.Add(null);
                        profile["inputs"] = CreateEmptyInputsArray();
                        profile["dinput"] = false;
                        profile["deadzone"] = GetDeadzone();
                        return false;
                    }
                }
            }

            JArray inputs = CreateEmptyInputsArray();
            int deadzone = GetDeadzone();
            bool dinput;

            // 1) Specific mapping per controller, from n64Controllers.json
            var n64Gamepad = GetN64SpecificController(controller);

            if (n64Gamepad != null)
            {
                dinput = n64Gamepad.ControllerInfo != null
                    && n64Gamepad.ControllerInfo.TryGetValue("dinput", out string dinputFlag)
                    && dinputFlag == "true";

                var mapping = new Dictionary<string, string>(n64Gamepad.Mapping);

                // Trigger invert ==> from L2 to R2 for some controllers
                if (n64Gamepad.ControllerInfo != null
                    && n64Gamepad.ControllerInfo.TryGetValue("switch_trigger", out string switchTrigger)
                    && !string.IsNullOrEmpty(switchTrigger)
                    && SystemConfig.getOptBoolean("n64_special_trigger"))
                {
                    mapping["Z"] = switchTrigger;
                }

                SimpleLogger.Instance.Info("[Controller] Performing specific N64 mapping for " + n64Gamepad.Name);

                foreach (var kv in mapping)
                {
                    if (!_profileIndex.TryGetValue(kv.Key, out int slot))
                        continue;

                    JToken padItem = ParseN64MappingValue(kv.Key, kv.Value, dinput);

                    JToken keyboardItem = null;
                    if (controller.PlayerIndex == 1 && _player1KeyboardScancodes.TryGetValue(kv.Key, out int scancode1))
                        keyboardItem = KeyItem(scancode1);

                    inputs[slot] = new JArray(
                        keyboardItem ?? (JToken)JValue.CreateNull(),
                        padItem ?? (JToken)JValue.CreateNull()
                    );
                }
            }
            else
            {
                // 2) Standard mapping based on the chosen layout (CONTROLLER LAYOUT Px)
                // dinput=true => gopher64 opens the device via SDL_OpenJoystick (API "raw joystick"),
                // dinput=false => via SDL_OpenGamepad (API "gamepad" with canonical SDL mapping).
                // IMPORTANT: this is NOT equivalent to "XInput or not" - a non-XInput controller can be recognized as an SDL gamepad
                // (isRecognizedGamepad also includes the SDL2 fallback above when sdl3 is null.)
                dinput = !isRecognizedGamepad;

                var mapping = standardMapping;
                string mappingProfile = "mupen64_inputprofile" + controller.PlayerIndex.ToString();

                if (SystemConfig.isOptSet(mappingProfile) && !string.IsNullOrEmpty(SystemConfig[mappingProfile]))
                {
                    switch (SystemConfig[mappingProfile])
                    {
                        case "c_stick_zl": mapping = standardMapping; break;
                        case "c_face_zl": mapping = cFaceZLMapping; break;
                        case "c_stick": mapping = cStickMapping; break;
                        case "c_face": mapping = cFaceMapping; break;
                        case "xbox": mapping = xboxMapping; break;
                    }
                }

                foreach (var kv in mapping)
                {
                    string action = kv.Key;      // "Right", "A", "Z", "Hotkey", ...
                    InputKey physical = kv.Value; // physical key chosen by the layout

                    if (!_profileIndex.TryGetValue(action, out int slot))
                        continue; // unknown action in the new scheme, ignore

                    JToken bound = null;

                    if (controller.Config != null)
                    {
                        var existsKey = revertedAxis.ContainsKey(physical) ? revertedAxis[physical] : physical;
                        if (controller.Config[existsKey] != null)
                        {
                            if (!dinput)
                                bound = BuildCanonicalItem(physical); // controller recognized as SDL gamepad (SDL_IsGamepad=true) -> canonical SDL3 IDs
                            else
                                bound = BuildRawJoystickItem(controller.Config[physical] ?? controller.Config[existsKey], physical); // no known SDL gamepad mapping -> raw joystick IDs
                        }
                    }

                    JToken keyboardItem = null;
                    if (controller.PlayerIndex == 1 && _player1KeyboardScancodes.TryGetValue(action, out int scancode2))
                        keyboardItem = KeyItem(scancode2);

                    inputs[slot] = new JArray(
                        keyboardItem ?? (JToken)JValue.CreateNull(),
                        bound ?? (JToken)JValue.CreateNull()
                    );
                }
            }

            profile["inputs"] = inputs;
            profile["dinput"] = dinput;
            profile["deadzone"] = deadzone;

            string gopherPath = NormalizeControllerPath(cPath);

            controllerAssignment.Add(gopherPath);
            return true;
        }

        private string NormalizeControllerPath(string path)
        {
            // gopher64 expects a tolower path with some parts uppercased

            if (string.IsNullOrEmpty(path))
                return path;

            if (path.IndexOf('\\') < 0)
                return path;

            string normalized = path.ToLowerInvariant();
            normalized = Regex.Replace(normalized, "hid", "HID", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "vid", "VID", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "pid", "PID", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "usb", "USB", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "usb", "USB", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, "ig", "IG", RegexOptions.IgnoreCase);
            return normalized;
        }

        /// <summary>
        /// Searches for this controller in n64Controllers.json
        /// (system/resources/inputmapping/n64Controllers.json), for the "gopher64" emulator.
        /// Returns null if the file is missing, if no entry matches the GUID, or if
        /// the entry requires activation via "USE N64-LIKE CONTROLLER" (n64_pad) not checked.
        /// </summary>
        private N64Controller GetN64SpecificController(Controller controller)
        {
            string n64json = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "inputmapping", "n64Controllers.json");
            if (!File.Exists(n64json))
                return null;

            string guid = controller.Guid.ToString().ToLowerInvariant();

            try
            {
                var n64Controllers = N64Controller.LoadControllersFromJson(n64json);
                if (n64Controllers == null)
                {
                    SimpleLogger.Instance.Info("[Controller] Error loading n64Controllers.json.");
                    return null;
                }

                var n64Gamepad = N64Controller.GetN64Controller("gopher64", guid, n64Controllers);
                if (n64Gamepad == null || n64Gamepad.Mapping == null || n64Gamepad.Mapping.Count == 0)
                {
                    SimpleLogger.Instance.Info("[Controller] No specific N64 mapping found for gopher64 / " + guid);
                    return null;
                }

                bool needActivationSwitch = n64Gamepad.ControllerInfo != null
                    && n64Gamepad.ControllerInfo.TryGetValue("needActivationSwitch", out string needSwitch)
                    && needSwitch == "true";

                if (needActivationSwitch && !SystemConfig.getOptBoolean("n64_pad"))
                {
                    SimpleLogger.Instance.Info("[Controller] Specific N64 mapping for " + n64Gamepad.Name + " needs 'USE N64-LIKE CONTROLLER' option enabled.");
                    return null;
                }

                return n64Gamepad;
            }
            catch
            {
                SimpleLogger.Instance.Info("[Controller] Error loading n64Controllers.json.");
                return null;
            }
        }

        /// <summary>
        /// Converts a Mapping value from the n64Controllers.json file into a gopher64 InputItem.
        /// Expected format: "button:id", "axis:id:sign +1/-1" or
        /// "hat:id:direction bitmask SDL_HAT_*".
        /// With dinput=false, "button"/"axis" use canonical SDL3 IDs (ControllerButton/ControllerAxis);
        /// with dinput=true, these are the raw device IDs (JoystickButton/JoystickAxis). Hats are
        /// always raw IDs (JoystickHat).
        /// </summary>
        private static JToken ParseN64MappingValue(string action, string value, bool dinput)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            var parts = value.Split(':');
            if (parts.Length < 2 || !int.TryParse(parts[1], out int id))
                return null;

            switch (parts[0].ToLowerInvariant())
            {
                case "button":
                    return dinput ? JoystickButtonItem(id) : ControllerButtonItem(id);
                case "axis":
                    int axis = (parts.Length > 2 && int.TryParse(parts[2], out int a)) ? a : 1;
                    if (!dinput)
                        return ControllerAxisItem(id, axis); 
                    
                    // Canonical SDL3 API: normalized triggers, rest=0 guaranteed by the spec.
                    // Pure DirectInput controller: "initial_state" = rest position, necessary for gopher64 to correctly detect presses.
                    int initialState = _triggerAxisActions.Contains(action) ? (axis > 0 ? -32768 : 32767) : 0;
                    return JoystickAxisItem(id, axis, initialState);
                case "hat":
                    int direction = (parts.Length > 2 && int.TryParse(parts[2], out int d)) ? d : 1;
                    return JoystickHatItem(id, direction);
            }
            return null;
        }

        private int GetDeadzone()
        {
            int deadzone = 5;
            if (SystemConfig.isOptSet("gopher64_deadzone") && !string.IsNullOrEmpty(SystemConfig["gopher64_deadzone"]))
                deadzone = SystemConfig["gopher64_deadzone"].ToIntegerString().ToInteger();
            return deadzone;
        }

        private static JArray CreateEmptyInputsArray()
        {
            var arr = new JArray();
            for (int i = 0; i < PROFILE_SIZE; i++)
                arr.Add(new JArray(JValue.CreateNull(), JValue.CreateNull()));
            return arr;
        }

        // ----- Helpers -----

        private static JObject KeyItem(int scancode) =>
            new JObject { ["Key"] = new JObject { ["id"] = scancode } };

        private static JObject ControllerButtonItem(int id) =>
            new JObject { ["ControllerButton"] = new JObject { ["id"] = id } };

        private static JObject ControllerAxisItem(int id, int axis) =>
            new JObject { ["ControllerAxis"] = new JObject { ["id"] = id, ["axis"] = axis, ["initial_state"] = 0 } };

        private static JObject JoystickButtonItem(int id) =>
            new JObject { ["JoystickButton"] = new JObject { ["id"] = id } };

        private static JObject JoystickAxisItem(int id, int axis, int initialState = 0) =>
            new JObject { ["JoystickAxis"] = new JObject { ["id"] = id, ["axis"] = axis, ["initial_state"] = initialState } };

        private static JObject JoystickHatItem(int id, int direction) =>
            new JObject { ["JoystickHat"] = new JObject { ["id"] = id, ["direction"] = direction } };

        /// <summary>
        /// SDL3 IDs (SDL_GamepadButton / SDL_GamepadAxis) for a controller recognized as
        /// a gamepad (XInput or SDL gamecontrollerdb).
        /// </summary>
        private static JToken BuildCanonicalItem(InputKey key)
        {
            switch (key)
            {
                case InputKey.a: return ControllerButtonItem(0);   // SOUTH
                case InputKey.b: return ControllerButtonItem(1);   // EAST
                case InputKey.x: return ControllerButtonItem(2);   // WEST
                case InputKey.y: return ControllerButtonItem(3);   // NORTH
                case InputKey.select: return ControllerButtonItem(4);   // BACK
                case InputKey.start: return ControllerButtonItem(6);    // START
                case InputKey.l3: return ControllerButtonItem(7);       // LEFTSTICK
                case InputKey.r3: return ControllerButtonItem(8);       // RIGHTSTICK
                case InputKey.pageup: return ControllerButtonItem(9);   // LEFTSHOULDER
                case InputKey.pagedown: return ControllerButtonItem(10); // RIGHTSHOULDER
                case InputKey.up: return ControllerButtonItem(11);      // DPAD_UP
                case InputKey.down: return ControllerButtonItem(12);    // DPAD_DOWN
                case InputKey.left: return ControllerButtonItem(13);    // DPAD_LEFT
                case InputKey.right: return ControllerButtonItem(14);   // DPAD_RIGHT

                case InputKey.l2: return ControllerAxisItem(4, 1);   // TRIGGERLEFT
                case InputKey.r2: return ControllerAxisItem(5, 1);   // TRIGGERRIGHT

                case InputKey.leftanalogleft: return ControllerAxisItem(0, -1);  // LEFTX-
                case InputKey.leftanalogright: return ControllerAxisItem(0, 1);  // LEFTX+
                case InputKey.leftanalogup: return ControllerAxisItem(1, -1);    // LEFTY-
                case InputKey.leftanalogdown: return ControllerAxisItem(1, 1);   // LEFTY+
                case InputKey.rightanalogleft: return ControllerAxisItem(2, -1); // RIGHTX-
                case InputKey.rightanalogright: return ControllerAxisItem(2, 1); // RIGHTX+
                case InputKey.rightanalogup: return ControllerAxisItem(3, -1);   // RIGHTY-
                case InputKey.rightanalogdown: return ControllerAxisItem(3, 1);  // RIGHTY+
            }
            return null;
        }

        /// <summary>
        /// DirectInput path (dinput=true in gopher64) : raw IDs
        /// </summary>
        private static JToken BuildRawJoystickItem(Input input, InputKey physical)
        {
            if (input == null)
                return null;

            switch (input.Type)
            {
                case "button":
                    return JoystickButtonItem((int)input.Id);
                case "axis":
                    int sign = input.Value >= 0 ? 1 : -1;
                    // "initial_state" = resting position of the axis, necessary for gopher64 to detect
                    // the press.
                    // For a trigger the rest is at the OPPOSITE end in the "pressed" direction.
                    // For a stick axis ==> 0
                    int initialState = (physical == InputKey.l2 || physical == InputKey.r2)
                        ? (sign > 0 ? -32768 : 32767)
                        : 0;
                    return JoystickAxisItem((int)input.Id, sign, initialState);
                case "hat":
                    // direction = bitmask SDL_HAT_* (1=up, 2=right, 4=down, 8=left)
                    return JoystickHatItem(0, (int)input.Value);
            }
            return null;
        }

        private string FormatDeviceId(string deviceId)
        {
            // Conserved but not used for Gopher64: gopher64 expects the
            // raw SDL3 path (Controller.Sdl3Controller.Path), not a reformatted identifier.
            int secondHash = deviceId.IndexOf('#', deviceId.IndexOf('#') + 1);
            if (secondHash == -1)
                return deviceId;

            return deviceId.Substring(0, secondHash).ToUpper()
                 + deviceId.Substring(secondHash).ToLower();
        }

        private static Dictionary<InputKey, InputKey> revertedAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.leftanalogright, InputKey.leftanalogleft },
            { InputKey.leftanalogdown, InputKey.leftanalogup },
            { InputKey.rightanalogright, InputKey.rightanalogleft },
            { InputKey.rightanalogdown, InputKey.rightanalogup },
        };

        private Dictionary<string, InputKey> standardMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.l2 },
            { "B", InputKey.x },
            { "A", InputKey.a },
            { "CRight", InputKey.rightanalogright },
            { "CLeft", InputKey.rightanalogleft },
            { "CDown", InputKey.rightanalogdown },
            { "CUp", InputKey.rightanalogup },
            { "R", InputKey.pagedown },
            { "L", InputKey.pageup },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Hotkey", InputKey.select }
        };

        private Dictionary<string, InputKey> cFaceZLMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.l2 },
            { "B", InputKey.r2 },
            { "A", InputKey.pagedown },
            { "CRight", InputKey.b },
            { "CLeft", InputKey.x },
            { "CDown", InputKey.a },
            { "CUp", InputKey.y },
            { "R", InputKey.pageup },
            { "L", InputKey.select },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Hotkey", InputKey.select }
        };

        private Dictionary<string, InputKey> cStickMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.r2 },
            { "B", InputKey.x },
            { "A", InputKey.a },
            { "CRight", InputKey.rightanalogright },
            { "CLeft", InputKey.rightanalogleft },
            { "CDown", InputKey.rightanalogdown },
            { "CUp", InputKey.rightanalogup },
            { "R", InputKey.pagedown },
            { "L", InputKey.pageup },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Hotkey", InputKey.select }
        };

        private Dictionary<string, InputKey> cFaceMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.r2 },
            { "B", InputKey.l2 },
            { "A", InputKey.pageup },
            { "CRight", InputKey.b },
            { "CLeft", InputKey.x },
            { "CDown", InputKey.a },
            { "CUp", InputKey.y },
            { "R", InputKey.pagedown },
            { "L", InputKey.select },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Hotkey", InputKey.select }
        };

        private Dictionary<string, InputKey> xboxMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.l2 },
            { "B", InputKey.b },
            { "A", InputKey.a },
            { "CRight", InputKey.rightanalogright },
            { "CLeft", InputKey.rightanalogleft },
            { "CDown", InputKey.rightanalogdown },
            { "CUp", InputKey.rightanalogup },
            { "R", InputKey.pagedown },
            { "L", InputKey.pageup },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Hotkey", InputKey.select }
        };
    }
}
