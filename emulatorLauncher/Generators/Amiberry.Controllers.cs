using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    /// <summary>
    /// Amiberry controller configuration.
    ///
    /// IMPORTANT - why we do NOT use device indexes:
    ///
    ///   Amiberry's get_joystick_uniquename() simply returns "JOY%d", where %d is
    ///   Amiberry's own enumeration slot (src/osdep/amiberry_input.cpp:1882). That slot
    ///   comes from SDL_GetJoysticks() order, minus devices filtered out as non-joysticks
    ///   (input_platform_internal_host.h), and it can be shifted further by the on-screen
    ///   joystick pseudo-device (ensure_onscreen_joystick_registered()).
    ///   It therefore has no stable relationship with RetroBat's DeviceIndex.
    ///
    ///   We bind ports by name instead, using joyportfriendlyname{N}. Amiberry resolves
    ///   it through inputdevice_joyport_config() with INPUT_MATCH_FRIENDLY_NAME_ONLY,
    ///   which is enabled by default (input_device_match_mask = -1).
    ///
    ///   Known limitation: when two pads report the exact same name, the name match is
    ///   ambiguous (matched = -2) and Amiberry falls back to "joydefault". For that case
    ///   the user can enable the amiberry_padindex option, which writes joyport{N}=joy{i}
    ///   explicitly. This is opt-in precisely because the index is a guess.
    ///
    /// Mapping strategy:
    ///
    ///   Amiberry's default mapping is the identity over the SDL3 gamepad enum
    ///   (fill_default_controller(): button[b] = b). For any pad recognised as an SDL
    ///   gamepad - which covers XInput and virtually every pad on Windows - that default
    ///   is already correct. We therefore write NO mapping file by default; this is the
    ///   most compatible option and avoids fighting SDL's own database.
    ///
    ///   We only write a when hotkeys are requested. That file
    ///   is seeded with fill_default_controller() before being read, so specifying just
    ///   the hotkey keys leaves the identity mapping intact.
    /// </summary>
    partial class AmiberryGenerator : Generator
    {
        #region SDL3 gamepad button indexes

        // Verified against SDL3 SDL_gamepad.h (SDL_GamepadButton).
        private const int SDL_GAMEPAD_BUTTON_INVALID = -1;
        private const int SDL_GAMEPAD_BUTTON_SOUTH = 0;
        private const int SDL_GAMEPAD_BUTTON_EAST = 1;
        private const int SDL_GAMEPAD_BUTTON_WEST = 2;
        private const int SDL_GAMEPAD_BUTTON_NORTH = 3;
        private const int SDL_GAMEPAD_BUTTON_BACK = 4;
        private const int SDL_GAMEPAD_BUTTON_GUIDE = 5;
        private const int SDL_GAMEPAD_BUTTON_START = 6;
        private const int SDL_GAMEPAD_BUTTON_LEFT_STICK = 7;
        private const int SDL_GAMEPAD_BUTTON_RIGHT_STICK = 8;
        private const int SDL_GAMEPAD_BUTTON_LEFT_SHOULDER = 9;
        private const int SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER = 10;

        #endregion

        /// <summary>
        /// joyport{N}mode values, in the order of joyportmodes[] (cfgfile.cpp:221):
        /// "", mouse, mousenowheel, djoy, gamepad, ajoy, cdtvjoy, cd32joy, lightpen
        /// </summary>
        private const string MODE_MOUSE = "mouse";
        private const string MODE_DJOY = "djoy";
        private const string MODE_CD32JOY = "cd32joy";

        private void ConfigureControls(Action<string, string> Set, string system)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            Set("input.config", "1");
            Set("input.1.keyboard.0.button.59", "SPC_STATESAVE.0");    // F2
            Set("input.1.keyboard.0.button.61", "SPC_STATERESTORE.0"); // F4

            var pads = this.Controllers
                .Where(c => !c.IsKeyboard)
                .OrderBy(c => c.PlayerIndex)
                .ToList();

            // Joyport0 is the mouse port (port 1 on the machine),
            // joyport1 is the joystick port (port 2). Games expect the joystick in port 2,
            // so player 1 goes to joyport1 and the mouse stays on joyport0.
            //
            // Exceptions:
            //   - CD32 uses a pad in joyport1 with the cd32joy mode.
            //   - Some games (point & click, Lemmings...) want the joystick in port 1;
            //     amiberry_swapports covers that.
            bool swapPorts = SystemConfig.getOptBoolean("amiberry_swapports");

            int joystickPort = swapPorts ? 0 : 1;
            int secondPort = swapPorts ? 1 : 0;

            string mode = _model.IsCd32 ? MODE_CD32JOY : MODE_DJOY;
            if (SystemConfig.isOptSet("amiberry_padmode") && !string.IsNullOrEmpty(SystemConfig["amiberry_padmode"]))
                mode = SystemConfig["amiberry_padmode"];

            var pad1 = pads.FirstOrDefault();
            var pad2 = pads.Skip(1).FirstOrDefault();

            // Primary joystick port
            if (pad1 != null)
            {
                BindPort(Set, joystickPort, pad1, mode);
                WriteHotkeyFile(pad1);
            }
            else
            {
                // No pad
                string layout = SystemConfig.isOptSet("amiberry_kbdlayout") && !string.IsNullOrEmpty(SystemConfig["amiberry_kbdlayout"]) ? SystemConfig["amiberry_kbdlayout"] : "kbd2";
                Set("joyport" + joystickPort, layout);
                Set("joyport" + joystickPort + "mode", mode);
            }

            // Second port: mouse, or player 2
            string secondPortDevice = SystemConfig.isOptSet("amiberry_port1") ? SystemConfig["amiberry_port1"] : (_model.IsCd == false ? "mouse" : "none");

            if (secondPortDevice == "joystick" && pad2 != null)
            {
                BindPort(Set, secondPort, pad2, mode);
                WriteHotkeyFile(pad2);
            }
            else if (secondPortDevice == "none")
            {
                Set("joyport" + secondPort, "none");
            }
            else
            {
                // "mousedefault" binds the system mouse without depending on any index.
                Set("joyport" + secondPort, "mousedefault");
                Set("joyport" + secondPort + "mode", MODE_MOUSE);
            }

            // Mouse speed (Amiberry option, percentage).
            if (SystemConfig.isOptSet("amiberry_mouse_speed") && !string.IsNullOrEmpty(SystemConfig["amiberry_mouse_speed"]))
                Set("input.mouse_speed", SystemConfig["amiberry_mouse_speed"].ToIntegerString());

            if (SystemConfig.isOptSet("amiberry_deadzone") && !string.IsNullOrEmpty(SystemConfig["amiberry_deadzone"]))
                Set("input.joymouse_deadzone", SystemConfig["amiberry_deadzone"].ToIntegerString());
        }

        /// <summary>
        /// Binds a pad to an Amiga port by name. Index binding is only used when the user
        /// explicitly asks for it (two identical pads scenario).
        /// </summary>
        private void BindPort(Action<string, string> Set, int port, Controller controller, string mode)
        {
            string name = GetAmiberryDeviceName(controller);

            if (SystemConfig.getOptBoolean("amiberry_padindex"))
            {
                int index = controller.PlayerIndex - 1;
                Set("joyport" + port, "joy" + index.ToString(CultureInfo.InvariantCulture));
                SimpleLogger.Instance.Warning("[Amiberry] Port " + port + " bound by index (joy" + index +
                    "). Amiberry's enumeration may differ from RetroBat's.");
            }
            else if (!string.IsNullOrEmpty(name))
            {
                Set("joyportfriendlyname" + port, name);
                SimpleLogger.Instance.Info("[Amiberry] Port " + port + " bound to \"" + name + "\" (name match).");
            }
            else
            {
                Set("joyport" + port, "joydefault");
            }

            Set("joyport" + port + "mode", mode);
        }

        /// <summary>
        /// Returns the name Amiberry will see for this pad.
        /// Preference order matches Amiberry's own: the SDL gamepad name when the pad is
        /// mapped in gamecontrollerdb, else the raw SDL joystick name.
        /// </summary>
        private static string GetAmiberryDeviceName(Controller controller)
        {
            string gamepadName = controller.Sdl3Controller?.Name;
            if (controller.IsXInputDevice)
                gamepadName = "XInput Controller";
            if (string.IsNullOrEmpty(gamepadName))
                gamepadName = controller.Sdl3Controller?.Name;
            if (!string.IsNullOrEmpty(gamepadName))
                return gamepadName;

            return controller.Name;
        }

        /// <summary>
        /// Writes file with hotkeys only, leaving the identity
        /// mapping untouched (the file is read on top of fill_default_controller()).
        /// </summary>
        private void WriteHotkeyFile(Controller controller)
        {
            string controllersPath = Path.Combine(_emulatorPath, "Controllers");
            TryCreateDirectory(controllersPath);

            int hotkey = GetButtonOption("amiberry_hotkey_button", SDL_GAMEPAD_BUTTON_INVALID);
            int menu = GetButtonOption("amiberry_menu_button", SDL_GAMEPAD_BUTTON_RIGHT_STICK);
            int reset = GetButtonOption("amiberry_reset_button", SDL_GAMEPAD_BUTTON_INVALID);
            int vkbd = GetButtonOption("amiberry_vkbd_button", SDL_GAMEPAD_BUTTON_BACK);

            var lines = new List<string>
            {
                "is_retroarch=0",
                "hotkey_button=" + hotkey.ToString(CultureInfo.InvariantCulture),
                "menu_button=" + menu.ToString(CultureInfo.InvariantCulture),
                "reset_button=" + reset.ToString(CultureInfo.InvariantCulture),
                "vkbd_button=" + vkbd.ToString(CultureInfo.InvariantCulture),
            };

            // Amiberry looks the file up under did->name. When the SDL gamepad name and
            // the raw joystick name differ (Amiberry ships its own gamecontrollerdb, which
            // may not match RetroBat's), we write both so the lookup cannot miss.
            var names = new List<string>();
            if (controller.IsXInputDevice)
                names.Add("XInput Controller");
            if (!string.IsNullOrEmpty(controller.Sdl3Controller?.Name))
                names.Add(controller.Sdl3Controller.Name);
            if (!string.IsNullOrEmpty(controller.SdlController?.Name))
                names.Add(controller.SdlController.Name);
            if (!string.IsNullOrEmpty(controller.Name) && !names.Contains(controller.Name))
                names.Add(controller.Name);

            foreach (var name in names)
            {
                string file = Path.Combine(controllersPath, SanitizeDeviceName(name) + ".controller");
                try
                {
                    File.WriteAllLines(file, lines);
                    SimpleLogger.Instance.Info("[Amiberry] Wrote hotkey mapping: " + file);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[Amiberry] Could not write " + file + ": " + ex.Message);
                }
            }
        }

        private int GetButtonOption(string feature, int defaultValue)
        {
            if (SystemConfig.isOptSet(feature) && !string.IsNullOrEmpty(SystemConfig[feature]))
                return SystemConfig[feature].ToInteger();
            return defaultValue;
        }

        /// <summary>
        /// Mirrors sanitize_retroarch_name() (src/osdep/retroarch.cpp:225):
        /// only \ / : ? " &lt; &gt; | are removed, nothing else.
        /// </summary>
        private static string SanitizeDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if ("\\/:?\"<>|".IndexOf(c) < 0)
                    sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
