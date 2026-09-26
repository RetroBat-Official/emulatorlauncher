using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmulatorLauncher
{
    partial class BlastemGenerator : Generator
    {
        /// <summary>L/R as X/Z. Native Genesis Plus GX layout, used when the feature is unset.</summary>
        static readonly Dictionary<InputKey, string> padLayoutXZ = new Dictionary<InputKey, string>()
        {
            { InputKey.x,        "y"     },
            { InputKey.a,        "b"     },
            { InputKey.b,        "c"     },
            { InputKey.pageup,   "x"     },
            { InputKey.y,        "a"     },
            { InputKey.pagedown, "z"     },
            { InputKey.start,    "start" },
            { InputKey.select,   "mode"  },
        };

        static readonly Dictionary<InputKey, string> padLayoutRotate = new Dictionary<InputKey, string>()
        {
            { InputKey.x,        "y"     },
            { InputKey.a,        "c"     },
            { InputKey.b,        "a"     },
            { InputKey.pageup,   "x"     },
            { InputKey.y,        "b"     },
            { InputKey.pagedown, "z"     },
            { InputKey.start,    "start" },
            { InputKey.select,   "mode"  },
        };

        /// <summary>L/R as Z/C. Also the layout BlastEm itself ships as its "default" pad.</summary>
        static readonly Dictionary<InputKey, string> padLayoutZC = new Dictionary<InputKey, string>()
        {
            { InputKey.a,        "a"     },
            { InputKey.b,        "b"     },
            { InputKey.pagedown, "c"     },
            { InputKey.x,        "y"     },
            { InputKey.y,        "x"     },
            { InputKey.pageup,   "z"     },
            { InputKey.start,    "start" },
            { InputKey.select,   "mode"  },
        };

        /// <summary>L/R as Y/Z.</summary>
        static readonly Dictionary<InputKey, string> padLayoutYZ = new Dictionary<InputKey, string>()
        {
            { InputKey.x,        "x"     },
            { InputKey.a,        "b"     },
            { InputKey.b,        "c"     },
            { InputKey.y,        "a"     },
            { InputKey.pageup,   "y"     },
            { InputKey.pagedown, "z"     },
            { InputKey.start,    "start" },
            { InputKey.select,   "mode"  },
        };

        /// <summary>L/R as Y/B.</summary>
        static readonly Dictionary<InputKey, string> padLayoutYB = new Dictionary<InputKey, string>()
        {
            { InputKey.a,        "a"     },
            { InputKey.pagedown, "b"     },
            { InputKey.b,        "c"     },
            { InputKey.x,        "z"     },
            { InputKey.pageup,   "y"     },
            { InputKey.y,        "x"     },
            { InputKey.start,    "start" },
            { InputKey.select,   "mode"  },
        };

        /// <summary>L/R as B/Y.</summary>
        static readonly Dictionary<InputKey, string> padLayoutBY = new Dictionary<InputKey, string>()
        {
            { InputKey.a,        "a"     },
            { InputKey.pageup,   "b"     },
            { InputKey.b,        "c"     },
            { InputKey.x,        "z"     },
            { InputKey.pagedown, "y"     },
            { InputKey.y,        "x"     },
            { InputKey.start,    "start" },
            { InputKey.select,   "mode"  },
        };

        static readonly Dictionary<string, Dictionary<InputKey, string>> padLayouts = new Dictionary<string, Dictionary<InputKey, string>>()
        {
            { "lr_xz", padLayoutXZ },
            { "lr_zc", padLayoutZC },
            { "lr_yz", padLayoutYZ },
            { "lr_yb", padLayoutYB },
            { "lr_by", padLayoutBY },
        };

        static readonly Dictionary<InputKey, string> dpadMapping = new Dictionary<InputKey, string>()
        {
            { InputKey.up,    "up"    },
            { InputKey.down,  "down"  },
            { InputKey.left,  "left"  },
            { InputKey.right, "right" },
        };

        private void CreateControllerConfiguration(BlastemConfigFile cfg, string path)
        {
            var bindings = cfg.GetOrCreateSection("bindings");

            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled, keeping existing pad bindings.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for BlastEm");

            var pads = bindings.GetOrCreateSection("pads");

            foreach (var key in pads.Keys.Where(k => k.Length <= 2 && k.All(char.IsDigit)).ToArray())
            {
                SimpleLogger.Instance.Info("[INFO] Removing slot indexed pad section : " + key);
                pads.Remove(key);
            }

            int maxPlayers = GetMaxPlayers();

            var controllers = Program.Controllers
                .Where(c => !c.IsKeyboard && c.Config != null && c.PlayerIndex >= 1 && c.PlayerIndex <= maxPlayers)
                .OrderBy(c => c.PlayerIndex)
                .ToList();

            if (Program.Controllers.Count(c => !c.IsKeyboard) > maxPlayers)
                SimpleLogger.Instance.Info("[INFO] More pads connected than emulated ports, only players 1 to " + maxPlayers + " are mapped.");

            // A pad section is keyed on an SDL GUID, which identifies a model and not an instance, so
            // two identical pads necessarily share one section and cannot be given a port each. As
            // soon as that happens every pad has to use the relative "gamepads.n" form : an absolute
            // target on one pad would otherwise collide with a relative one on another.
            int duplicateModels = controllers
                .GroupBy(c => c.GetSdlGuid(_sdlVersion, true).ToLowerInvariant())
                .Count(g => g.Count() > 1);

            bool relativeTargets = duplicateModels > 0;

            if (relativeTargets)
                SimpleLogger.Instance.Warning("[WARNING] " + duplicateModels + " pad model(s) used by several players : player order will follow whichever pad sends an input first.");

            // Only the first player of a shared model writes the section, the next ones would just
            // rewrite the very same bindings.
            var written = new HashSet<string>();

            foreach (var controller in controllers)
            {
                if (!written.Add(controller.GetSdlGuid(_sdlVersion, true).ToLowerInvariant()))
                {
                    SimpleLogger.Instance.Info("[INFO] Player " + controller.PlayerIndex + " shares its pad section with an earlier player, skipping.");
                    continue;
                }

                ConfigureJoystick(pads, controller, relativeTargets);
            }

            RemoveUiPadBindings(pads);
            ConfigurePorts(cfg);
        }

        private void ConfigureJoystick(BlastemConfigNode pads, Controller controller, bool relativeTargets)
        {
            var cfg = controller.Config;
            if (cfg == null)
                return;

            string guid = controller.GetSdlGuid(_sdlVersion, true).ToLowerInvariant();

            string newGuid = SdlJoystickGuid.GetGuidFromFile(
                Path.Combine(AppConfig.GetFullPath("tools"), "controllerinfo.yml"),
                controller.SdlController, controller.Guid, "blastem", 0,
                AppConfig.GetFullPath("retrobat"));

            if (newGuid != null)
                guid = newGuid.ToLowerInvariant();

            var seeds = new List<string>() { guid };

            foreach (var variant in controller.CompatibleSdlGuids)
                seeds.Add(variant.ToLowerInvariant());

            foreach (var version in new[] { SdlVersion.SDL2_30, SdlVersion.SDL2_26, SdlVersion.SDL2_24, SdlVersion.SDL2_0_X })
                seeds.Add(controller.GetSdlGuid(version, true).ToLowerInvariant());

            var lookupKeys = new List<string>();

            foreach (var seed in seeds)
            {
                var sdlGuid = new SdlJoystickGuid(seed);
                string rawInput = sdlGuid.ToRawInputGuid().ToLowerInvariant();
                string xInput = sdlGuid.ToXInputGuid().ToLowerInvariant();

                foreach (var candidate in new[]
                    {
                        seed, StripNameCrc(seed),
                        rawInput, StripNameCrc(rawInput),
                        xInput, StripNameCrc(xInput)
                    })
                {
                    if (!lookupKeys.Contains(candidate))
                        lookupKeys.Add(candidate);
                }
            }

            string target = relativeTargets ? "gamepads.n" : "gamepads." + controller.PlayerIndex;

            MegadriveController mdGamepad = null;

            if (megadrivePadSystems.Contains(_system))
            {
                foreach (var key in lookupKeys)
                {
                    mdGamepad = GetMegadriveController(key);

                    if (mdGamepad != null)
                    {
                        SimpleLogger.Instance.Info("[Controller] Performing specific megadrive mapping for " + mdGamepad.Name);
                        break;
                    }
                }
            }

            var pad = pads.ResetSection(guid);

            if (mdGamepad == null || !ApplyMegadriveLikePad(pad, controller, mdGamepad, target))
                FillStandardBindings(pad, cfg, target);

            if (_system == "mastersystem")
                RedirectStartToSmsPause(pad, target);

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + controller.DevicePath + " to player : " + controller.PlayerIndex + " (section " + guid + ", target " + target + ")");
        }

        static readonly HashSet<string> megadrivePadSystems = new HashSet<string>()
        {
            "megadrive", "megadrive-japan", "genesis", "sega32x", "segacd", "megacd", "md"
        };

        private static void RedirectStartToSmsPause(BlastemConfigNode pad, string target)
        {
            foreach (var groupName in new[] { "buttons", "axes" })
            {
                var group = pad.GetSection(groupName);
                if (group == null)
                    continue;

                foreach (var key in group.Keys)
                {
                    if (group[key] == target + ".start")
                    {
                        SimpleLogger.Instance.Info("[INFO] Master System : redirecting " + groupName + "." + key + " to ui.sms_pause.");
                        group[key] = "ui.sms_pause";
                    }
                }
            }
        }
        private static string StripNameCrc(string guid)
        {
            if (string.IsNullOrEmpty(guid) || guid.Length != 32)
                return guid;

            return guid.Substring(0, 4) + "0000" + guid.Substring(8);
        }

        /// <summary>Mega Drive pad bindings built from the EmulationStation configuration.</summary>
        private void FillStandardBindings(BlastemConfigNode pad, InputConfig cfg, string target)
        {
            var buttons = pad.GetOrCreateSection("buttons");
            var axes = pad.GetOrCreateSection("axes");

            Dictionary<InputKey, string> mapping;

            // rotate_buttons belongs to the 8 bit systems and megadrive_control_layout to the 16 bit
            // ones, so the two are never offered together, but rotation must win if both are set.
            if (SystemConfig.getOptBoolean("rotate_buttons"))
                mapping = padLayoutRotate;
            else
            {
                string layout = SystemConfig.isOptSet("megadrive_control_layout") ? SystemConfig["megadrive_control_layout"] : "lr_xz";

                if (!padLayouts.TryGetValue(layout, out mapping))
                {
                    SimpleLogger.Instance.Warning("[WARNING] Unknown megadrive_control_layout '" + layout + "', falling back on lr_xz.");
                    mapping = padLayoutXZ;
                }
            }

            foreach (var map in mapping)
                WriteInput(pad, buttons, axes, cfg[map.Key], target + "." + map.Value);

            foreach (var map in dpadMapping)
                WriteInput(pad, buttons, axes, cfg[map.Key], target + "." + map.Value);

            // Left stick doubles for the d-pad, as BlastEm does in its own default pad.
            WriteAnalogAxis(axes, cfg[InputKey.joystick1left], target + ".left", target + ".right");
            WriteAnalogAxis(axes, cfg[InputKey.joystick1up], target + ".up", target + ".down");
        }

        static readonly string[] mdMappingPaths =
        {
            "user\\inputmapping\\mdControllers.json",
            "system\\resources\\inputmapping\\mdControllers.json"
        };

        private MegadriveController GetMegadriveController(string guid)
        {
            var mdControllers = GetMegadriveControllers();
            if (mdControllers == null)
                return null;

            // The 4 argument overload splits a comma separated GUID list and falls back on a
            // driver agnostic lookup, which the 3 argument one does not. BlastEm is always SDL2.
            var ret = MegadriveController.GetMDController("blastem", guid, "sdl2", mdControllers);

            return ret != null && ret.Mapping != null && ret.Mapping.Count > 0 ? ret : null;
        }


        private bool ApplyMegadriveLikePad(BlastemConfigNode pad, Controller controller, MegadriveController mdGamepad, string target)
        {
            if (NeedsActivationSwitch(mdGamepad) && !SystemConfig.getOptBoolean("md_pad"))
            {
                SimpleLogger.Instance.Info("[Controller] " + mdGamepad.Name + " (player " + controller.PlayerIndex + ") requires the Megadrive-like controller option, using the standard mapping.");
                return false;
            }

            foreach (var entry in mdGamepad.Mapping)
            {
                if (string.IsNullOrEmpty(entry.Key) || string.IsNullOrEmpty(entry.Value))
                    continue;

                string[] parts = entry.Value.Split('.');

                if (parts.Length == 2 && parts[0] == "buttons")
                    pad.GetOrCreateSection("buttons")[parts[1]] = target + "." + entry.Key;

                else if (parts.Length == 3 && parts[0] == "axes")
                    pad.GetOrCreateSection("axes")[parts[1] + "." + parts[2]] = target + "." + entry.Key;

                // A semantic axis name carries no sign : BlastEm defaults it to the positive half.
                else if (parts.Length == 2 && parts[0] == "axes")
                    pad.GetOrCreateSection("axes")[parts[1]] = target + "." + entry.Key;

                else if (parts.Length == 3 && parts[0] == "dpads")
                    pad.GetOrCreateSection("dpads").GetOrCreateSection(parts[1])[parts[2]] = target + "." + entry.Key;

                else
                    SimpleLogger.Instance.Warning("[Controller] Unsupported mdControllers entry '" + entry.Key + "' : " + entry.Value);
            }

            return true;
        }

        private static List<MegadriveController> _mdControllers;
        private static bool _mdControllersLoaded;

        private List<MegadriveController> GetMegadriveControllers()
        {
            if (_mdControllersLoaded)
                return _mdControllers;

            _mdControllersLoaded = true;

            string json = null;
            foreach (var relative in mdMappingPaths)
            {
                string candidate = Path.Combine(AppConfig.GetFullPath("retrobat"), relative);
                if (File.Exists(candidate))
                {
                    json = candidate;
                    break;
                }
            }

            if (json == null)
            {
                SimpleLogger.Instance.Info("[Controller] No Megadrive JSON file found.");
                return null;
            }

            try { _mdControllers = MegadriveController.LoadControllersFromJson(json); }
            catch { SimpleLogger.Instance.Warning("[Controller] Failed to read " + json); }

            if (_mdControllers == null)
                SimpleLogger.Instance.Info("[Controller] Error loading JSON file.");

            return _mdControllers;
        }

        private static bool NeedsActivationSwitch(MegadriveController mdGamepad)
        {
            if (mdGamepad.ControllerInfo == null)
                return false;

            string value;
            if (!mdGamepad.ControllerInfo.TryGetValue("needActivationSwitch", out value))
                return false;

            if (string.IsNullOrEmpty(value))
                return true;

            return value != "false" && value != "0";
        }

        private static void WriteInput(BlastemConfigNode pad, BlastemConfigNode buttons, BlastemConfigNode axes, Input input, string target)
        {
            if (input == null)
                return;

            switch (input.Type)
            {
                case "button":
                    buttons[input.Id.ToString()] = target;
                    break;

                case "axis":
                    axes[input.Id + (input.Value > 0 ? ".positive" : ".negative")] = target;
                    break;

                case "hat":
                    string direction = HatDirection(input.Value);
                    if (direction != null)
                        pad.GetOrCreateSection("dpads").GetOrCreateSection(input.Id.ToString())[direction] = target;
                    break;
            }
        }

        /// <summary>Bind both halves of an analog axis, each to one direction.</summary>
        private static void WriteAnalogAxis(BlastemConfigNode axes, Input input, string negativeTarget, string positiveTarget)
        {
            if (input == null || input.Type != "axis")
                return;

            bool reversed = input.Value > 0;

            axes[input.Id + ".negative"] = reversed ? positiveTarget : negativeTarget;
            axes[input.Id + ".positive"] = reversed ? negativeTarget : positiveTarget;
        }

        /// <summary>SDL hat bitmask to the direction name BlastEm expects under "dpads".</summary>
        private static string HatDirection(long value)
        {
            switch (value)
            {
                case 1: return "up";
                case 2: return "right";
                case 4: return "down";
                case 8: return "left";
            }

            return null;
        }

        private void ConfigurePorts(BlastemConfigFile cfg)
        {
            // RetroBat exposes the 3 button pad through the shared "md_3buttons" feature, which is
            // also what the libretro and jgenesis generators read.
            string pad = SystemConfig.getOptBoolean("md_3buttons") ? "gamepad3" : "gamepad6";

            var io = cfg.GetOrCreateSection("io");
            var devices = io.ResetSection("devices");

            switch (GetMultitapMode())
            {
                case 1:     // tap on port A, a plain pad on port B
                    devices["1"] = "sega_multitap.1";
                    devices["2"] = pad + ".5";
                    FillTap(io, "sega_multitap.1", pad, 1);
                    break;

                case 2:     // a plain pad on port A, tap on port B
                    devices["1"] = pad + ".1";
                    devices["2"] = "sega_multitap.2";
                    FillTap(io, "sega_multitap.2", pad, 2);
                    break;

                case 3:     // a tap on each port, 8 players
                    devices["1"] = "sega_multitap.1";
                    devices["2"] = "sega_multitap.2";
                    FillTap(io, "sega_multitap.1", pad, 1);
                    FillTap(io, "sega_multitap.2", pad, 5);
                    break;

                default:
                    devices["1"] = pad + ".1";
                    devices["2"] = pad + ".2";
                    io.Remove("sega_multitap.1");
                    io.Remove("sega_multitap.2");
                    break;
            }

            // 8 bit systems have their own io section and no multitap, always a 2 button pad.
            var smsDevices = cfg.GetOrCreateSection("sms").GetOrCreateSection("io").ResetSection("devices");
            smsDevices["1"] = "gamepad2.1";
            smsDevices["2"] = "gamepad2.2";
        }

        /// <summary>Wire the four ports of one tap to consecutive emulated pads.</summary>
        private static void FillTap(BlastemConfigNode io, string tapName, string pad, int firstPadNumber)
        {
            var tap = io.ResetSection(tapName);
            for (int i = 0; i < 4; i++)
                tap[(i + 1).ToString()] = pad + "." + (firstPadNumber + i);
        }

        /// <summary>0 none, 1 port A, 2 port B, 3 both. Same wording as the other Mega Drive emulators.</summary>
        private int GetMultitapMode()
        {
            if (!SystemConfig.isOptSet("blastem_multitap"))
                return 0;

            int mode;
            if (!int.TryParse(SystemConfig["blastem_multitap"], out mode) || mode < 0 || mode > 3)
                return 0;

            return mode;
        }

        /// <summary>Emulated pads reachable with the current port layout. BlastEm caps them at 8.</summary>
        private int GetMaxPlayers()
        {
            switch (GetMultitapMode())
            {
                case 1:
                case 2: return 5;
                case 3: return 8;
                default: return 2;
            }
        }

        private static void RemoveUiPadBindings(BlastemConfigNode pads)
        {
            var unwanted = new HashSet<string>() { "ui.save_state", "ui.next_speed", "ui.prev_speed" };

            foreach (var padName in pads.Keys)
            {
                var pad = pads.GetSection(padName);
                if (pad == null)
                    continue;

                foreach (var groupName in new[] { "buttons", "axes" })
                {
                    var group = pad.GetSection(groupName);
                    if (group == null)
                        continue;

                    foreach (var key in group.Keys)
                    {
                        if (unwanted.Contains(group[key]))
                        {
                            SimpleLogger.Instance.Info("[INFO] Removing pad binding " + padName + "." + groupName + "." + key + " (" + group[key] + ")");
                            group.Remove(key);
                        }
                    }
                }
            }
        }

        private static void SetupHotkeys(BlastemConfigFile cfg, string core)
        {
            _pad2KeyOverride = false;

            var keys = cfg.GetOrCreateSection("bindings").GetOrCreateSection("keys");

            foreach (var key in keys.Keys)
            {
                string action = keys[key];
                if (action != null && action.StartsWith("ui."))
                    keys.Remove(key);
            }

            keys["f9"] = "cassette.play";
            keys["f10"] = "cassette.rewind";
            keys.Remove("f2");
            keys.Remove("f4");

            keys["g"] = "gamepads.1.mode";
            keys.Remove("f");
            keys["tab"] = "ui.soft_reset";

            if (Hotkeys.GetHotKeysFromFile("blastem", core, out Dictionary<string, HotkeyResult> hotkeys))
            {
                foreach (var h in hotkeys)
                    keys[h.Value.EmulatorValue] = h.Value.EmulatorKey;

                if (!hotkeys.ContainsKey("input_exit_emulator"))
                    keys["esc"] = "ui.exit";

                if (!hotkeys.ContainsKey("input_menu_toggle"))
                    keys["f1"] = "ui.menu";

                _pad2KeyOverride = true;

                return;
            }

            keys["esc"] = "ui.exit";                    // Escape
            keys["f1"] = "ui.menu";                     // F1
            keys["f2"] = "ui.save_state";               // F2
            keys["f4"] = "ui.load_state";               // F4
            keys["f8"] = "ui.screenshot";               // F8
            keys["p"] = "ui.pause";                     // P
            keys["f"] = "ui.toggle_fullscreen";         // F
            keys["k"] = "ui.advance";                   // K
            keys["l"] = "ui.next_speed";                // L
            keys["backspace"] = "ui.prev_speed";        // Backspace
        }
    }
}