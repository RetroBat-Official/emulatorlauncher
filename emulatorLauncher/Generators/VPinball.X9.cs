using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    // Visual Pinball 10.9+ : built-in plugins (PinMAME, B2S, FlexDMD, PinUP) replace the external COM components
    partial class VPinballGenerator : Generator
    {
        private static readonly Version X9Version = new Version(10, 9, 0, 0);

        /// <summary>BGFX build when it is a 10.9+ one, else the same executables as before.</summary>
        private static string FindExecutable(string path)
        {
            bool x64 = Environment.Is64BitOperatingSystem;

            // A BGFX test build of an older version must not replace the regular executable
            string bgfx = Path.Combine(path, x64 ? "VPinballX_BGFX64.exe" : "VPinballX_BGFX.exe");
            if (File.Exists(bgfx) && GetExeVersion(bgfx) >= X9Version)
                return bgfx;

            string exe = Path.Combine(path, x64 ? "VPinballX64.exe" : "VPinballX.exe");
            if (!File.Exists(exe) && x64)
                exe = Path.Combine(path, "VPinballX.exe");

            return File.Exists(exe) ? exe : null;
        }

        private static Version GetExeVersion(string exe)
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            }
            catch { return new Version(0, 0, 0, 0); }
        }

        /// <summary>
        /// 10.9+ command line : tables are played without the Win32 editor, -Minimized and -ExtMinimized are refused with -Play.
        /// -PrefPath keeps the preferences (cache...) in the emulator folder instead of %AppData%.
        /// </summary>
        private List<string> GetCommandLineX9(string path, string rom)
        {
            string prefPath = Path.Combine(path, "prefs");
            try { Directory.CreateDirectory(prefPath); }
            catch { }

            return new List<string>
            {
                "-PrefPath", prefPath,
                "-Ini", Path.Combine(path, "VPinballX.ini"),
                "-Play", rom
            };
        }

        /// <summary>10.9+ : VPinballX.ini.</summary>
        private void SetupOptionsIniFileX9(string path)
        {
            string iniFile = Path.Combine(path, "VPinballX.ini");

            // Input mappings contain ';' : it must not be read as a comment
            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines | IniOptions.NoInlineComments))
            {
                SimpleLogger.Instance.Info("[Generator] Writing config to VPinballX.ini file (10.9).");

                // Plugins : an enabled plugin is used instead of the COM object asked by the table script
                bool b2s = !SystemConfig.isOptSet("enableb2s") || string.IsNullOrEmpty(SystemConfig["enableb2s"]) || SystemConfig.getOptBoolean("enableb2s");

                ini.WriteValue("Plugin.PinMAME", "Enable", "1");
                ini.WriteValue("Plugin.FlexDMD", "Enable", "1");
                ini.WriteValue("Plugin.PUP", "Enable", "1");
                ini.WriteValue("Plugin.Nixie", "Enable", "1");   // DMD and score displays
                ini.WriteValue("Plugin.B2S", "Enable", b2s ? "1" : "0");

                // HIDE GRILL : the plugin hides the grill unless told to show it
                ini.WriteValue("Plugin.B2S", "ShowGrill", SystemConfig.getOptBoolean("vpbg_hidegrill") ? "0" : "1");

                SetupFoldersX9(ini, path);

                ini.WriteValue("Controller", "ForceDisableB2S", b2s ? "0" : "1");
                BindIniFeature(ini, "Controller", "DOFContactors", "vpdof_contractors", "2");
                BindIniFeature(ini, "Controller", "DOFKnocker", "vpdof_knocker", "2");
                BindIniFeature(ini, "Controller", "DOFChimes", "vpdof_chimes", "2");
                BindIniFeature(ini, "Controller", "DOFBell", "vpdof_bell", "2");
                BindIniFeature(ini, "Controller", "DOFGear", "vpdof_gear", "2");
                BindIniFeature(ini, "Controller", "DOFShaker", "vpdof_shaker", "2");
                BindIniFeature(ini, "Controller", "DOFFlippers", "vpdof_flippers", "2");
                BindIniFeature(ini, "Controller", "DOFTargets", "vpdof_targets", "2");
                BindIniFeature(ini, "Controller", "DOFDropTargets", "vpdof_droptargets", "2");

                SetupScreensX9(ini);

                // Vertical sync
                if (SystemConfig.isOptSet("vp_vsync") && !string.IsNullOrEmpty(SystemConfig["vp_vsync"]))
                    ini.WriteValue("Player", "SyncMode", SystemConfig["vp_vsync"]);
                else
                    ini.WriteValue("Player", "SyncMode", "3");

                // Video options
                if (SystemConfig.isOptSet("vp_ambient_occlusion") && SystemConfig["vp_ambient_occlusion"] == "dynamic")
                {
                    ini.WriteValue("Player", "DisableAO", "0");
                    ini.WriteValue("Player", "DynamicAO", "1");
                }
                else
                {
                    ini.WriteValue("Player", "DisableAO", SystemConfig["vp_ambient_occlusion"] == "0" ? "1" : "0");
                    ini.WriteValue("Player", "DynamicAO", "0");
                }

                ini.WriteValue("Player", "AAFactor", SystemConfig.GetValueOrDefault("vp_supersampling", "1.000000"));
                ini.WriteValue("Player", "FXAA", SystemConfig.GetValueOrDefault("vp_antialiasing", "0"));
                ini.WriteValue("Player", "Sharpen", SystemConfig.GetValueOrDefault("vp_sharpen", "0"));

                // View setup : 0 = desktop, 1 = cabinet, 2 = full single screen (FSS). AUTO follows the screen layout
                if (SystemConfig.isOptSet("vp_viewmode") && !string.IsNullOrEmpty(SystemConfig["vp_viewmode"]))
                    ini.WriteValue("Player", "BGSet", SystemConfig["vp_viewmode"]);
                else
                    ini.WriteValue("Player", "BGSet", _layout != null && !_layout.IsManual ? _layout.DefaultViewMode.ToString() : "0");

                bool aniFilter = !SystemConfig.isOptSet("vp_anisotropic_filtering") || SystemConfig.getOptBoolean("vp_anisotropic_filtering");
                ini.WriteValue("Player", "ForceAnisotropicFiltering", aniFilter ? "1" : "0");

                // Level of details
                if (SystemConfig.isOptSet("vp_details") && !string.IsNullOrEmpty(SystemConfig["vp_details"]))
                    ini.WriteValue("Player", "AlphaRampAccuracy", SystemConfig["vp_details"].ToIntegerString());
                else
                    ini.WriteValue("Player", "AlphaRampAccuracy", "10");

                // Lighting : table lighting unless ENVIRONMENT LIGHTING is set
                if (SystemConfig.isOptSet("vp_lighting") && !string.IsNullOrEmpty(SystemConfig["vp_lighting"]))
                {
                    ini.WriteValue("Player", "OverrideTableEmissionScale", "1");
                    ini.WriteValue("Player", "EmissionScale", SystemConfig["vp_lighting"]);
                }
                else
                    ini.WriteValue("Player", "OverrideTableEmissionScale", "0");

                // Audio
                BindIniFeature(ini, "Player", "Sound3D", "vp_audiochannels", "0");

                SetupControlsX9(ini);

                ini.Save();
            }
        }

        /// <summary>
        /// 10.9+ : playfield, backglass and DMD (score view) windows, from the screen layout.
        /// Windows are tied to a display, their position is relative to that display.
        /// </summary>
        private void SetupScreensX9(IniFile ini)
        {
            bool hasLayout = _layout != null && !_layout.IsManual;

            // Playfield : borderless fullscreen, VPX takes the size of the display (video mode and rotation already applied)
            Screen playfield = hasLayout ? _layout.PlayfieldScreen : VPinballScreenLayout.GetPlayfieldScreen();
            ini.WriteValue("Player", "PlayfieldDisplay", GetDisplayIdX9(playfield) ?? "");
            ini.WriteValue("Player", "PlayfieldFullScreen", "1");

            // No layout : backglass and DMD windows are left as set in VPX
            if (!hasLayout)
                return;

            // Backglass : whole screen, or its top part when the DMD shares the screen
            string backglassId = _layout.HasBackglass ? GetDisplayIdX9(_layout.BackglassScreen) : null;
            if (backglassId != null)
                WriteWindowX9(ini, "Backglass", backglassId, _layout.Backglass, _layout.BackglassScreenBounds);
            else
                ini.WriteValue("Backglass", "BackglassOutput", "0");

            // DMD
            switch (_layout.DmdLocation)
            {
                case VPinballDmdLocation.Playfield:
                    {
                        // Embedded in the playfield window, position relative to that window
                        Point pos = VPinballScreenLayout.RelativeTo(_layout.DmdArea, _layout.Playfield);

                        ini.WriteValue("ScoreView", "ScoreViewOutput", "2");
                        ini.WriteValue("ScoreView", "ScoreViewWndX", pos.X.ToString());
                        ini.WriteValue("ScoreView", "ScoreViewWndY", pos.Y.ToString());
                        ini.WriteValue("ScoreView", "ScoreViewWidth", _layout.DmdArea.Width.ToString());
                        ini.WriteValue("ScoreView", "ScoreViewHeight", _layout.DmdArea.Height.ToString());
                    }
                    break;

                case VPinballDmdLocation.Backglass:
                    if (backglassId != null)
                        WriteWindowX9(ini, "ScoreView", backglassId, _layout.DmdArea, _layout.BackglassScreenBounds);
                    else
                        ini.WriteValue("ScoreView", "ScoreViewOutput", "0");
                    break;

                case VPinballDmdLocation.Screen:
                    {
                        string dmdId = GetDisplayIdX9(_layout.DmdScreen);
                        if (dmdId != null)
                            WriteWindowX9(ini, "ScoreView", dmdId, _layout.DmdArea, _layout.DmdArea);
                        else
                            ini.WriteValue("ScoreView", "ScoreViewOutput", "0");
                    }
                    break;

                default:
                    ini.WriteValue("ScoreView", "ScoreViewOutput", "0");
                    break;
            }
        }

        /// <summary>Window on a display : borderless fullscreen when it covers the display, else borderless window at a position relative to the display.</summary>
        private static void WriteWindowX9(IniFile ini, string section, string displayId, Rectangle area, Rectangle display)
        {
            Point pos = VPinballScreenLayout.RelativeTo(area, display);

            ini.WriteValue(section, section + "Output", "1");
            ini.WriteValue(section, section + "Display", displayId);
            ini.WriteValue(section, section + "FullScreen", area.Size == display.Size ? "1" : "0");
            ini.WriteValue(section, section + "WndX", pos.X.ToString());
            ini.WriteValue(section, section + "WndY", pos.Y.ToString());
            ini.WriteValue(section, section + "Width", area.Width.ToString());
            ini.WriteValue(section, section + "Height", area.Height.ToString());
        }

        /// <summary>Display identifier of VPX 10.9+ : "SDL display name [x, y]" (Window.cpp / GetDisplays). Null if the name is not found.</summary>
        private static string GetDisplayIdX9(Screen screen)
        {
            if (screen == null)
                return null;

            string name = Displays.GetSdlDisplayName(screen);
            if (string.IsNullOrEmpty(name))
            {
                SimpleLogger.Instance.Warning("[Generator] VPX 10.9+ : display name not found for " + screen.DeviceName + ".");
                return null;
            }

            Rectangle bounds = VPinballScreenLayout.GetBounds(screen);
            return string.Format(CultureInfo.InvariantCulture, "{0} [{1}, {2}]", name, bounds.X, bounds.Y);
        }

        // Action of VPX, SDL scancode of the key, position on the controller (same layout as 10.8)
        private static readonly string[][] _actionsX9 = new string[][]
        {
            new string[] { "LeftFlipper", "225", "l1" },            // left shift
            new string[] { "RightFlipper", "229", "r1" },           // right shift
            new string[] { "LeftStagedFlipper", "225", "l1" },      // VPX default : same input as the flipper
            new string[] { "RightStagedFlipper", "229", "r1" },
            new string[] { "LeftMagna", "224", "west" },            // left ctrl
            new string[] { "RightMagna", "228", "north" },          // right ctrl
            new string[] { "LaunchBall", "40", "south" },           // enter
            new string[] { "Lockbar", "226", "east" },              // left alt (action button)
            new string[] { "Start", "30", "start" },                // 1
            new string[] { "Credit1", "34", "select" },             // 5
            new string[] { "Credit2", "33", null },                 // 4
            new string[] { "LeftNudge", "29", "left" },             // Z
            new string[] { "RightNudge", "56", "right" },           // slash
            new string[] { "CenterNudge", "44", "up" },             // space
            new string[] { "Tilt", "23", "down" },                  // T
            new string[] { "Pause", "19", "l3" },                   // P
            new string[] { "VolumeDown", "45", null },              // minus
            new string[] { "VolumeUp", "46", null },                // equals
            new string[] { "PerfOverlay", "68", null },             // F11
            new string[] { "DebugBalls", "18", null },              // O
            new string[] { "Debugger", "7", null }                  // D
        };

        /// <summary>
        /// 10.9+ : [Input] section, "Mapping.action = device;element | device;element".
        /// Keyboard : device "Key", SDL scancodes. Controller : raw joystick inputs of the SDL3 device.
        /// </summary>
        private void SetupControlsX9(IniFile ini)
        {
            // HOTKEY + START closes the window. USE EXIT TABLE TO EXIT : it sends the X key, bound to the exit action with ESC
            _remapexit = SystemConfig.getOptBoolean("vp_useexit");
            ini.WriteValue("Input", "Mapping.ExitGame", _remapexit ? "Key;41 | Key;27" : "Key;41");

            if (SystemConfig.isOptSet("disableautocontrollers") && SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // PAD2KEY : keyboard only, the controller is handled by the padtokey profile
            bool pad2key = SystemConfig["vp_inputdriver"] == "pad2key";
            if (pad2key)
            {
                string sourcep2kFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "padtokey", "vpinball.keys.old");
                string targetp2kFile = Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "padtokey", "vpinball.keys");

                if (!File.Exists(targetp2kFile) && File.Exists(sourcep2kFile))
                    try { File.Copy(sourcep2kFile, targetp2kFile); } catch { }
            }

            // VPX uses SDL3 with its default hints : same view of the devices here
            Sdl3GameController.SetEnumerationHints(Sdl3GameController.Sdl3HintProfile.Sdl3Default);
            SetSdl3EmulatorVersion(_exe);

            Controller controller = pad2key ? null : Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && !c.IsKeyboard);
            Sdl3GameController sdl3 = controller != null ? controller.Sdl3Controller : null;
            string device = sdl3 != null ? GetJoystickIdX9(sdl3) : null;

            if (controller != null && sdl3 == null)
                SimpleLogger.Instance.Warning("[Generator] VPX 10.9+ : SDL3 device not found for " + controller.ToShortString() + ", controller not configured.");

            foreach (string[] action in _actionsX9)
            {
                string mapping = "Key;" + action[1];

                string pad = device != null && action[2] != null ? GetPadBindingX9(sdl3, device, action[2]) : null;
                if (pad != null)
                    mapping += " | " + pad;

                ini.WriteValue("Input", "Mapping." + action[0], mapping);
            }

            BindIniFeature(ini, "Player", "RumbleMode", "vp_rumble", "3");

            if (device != null)
                SetupAnalogControlsX9(ini, sdl3, device);
            else if (pad2key)
            {
                // Sticks set by a previous launch must not stay active with the padtokey profile
                ini.WriteValue("Input", "PlungerSensorCount", "0");
                ini.WriteValue("Input", "NudgeSensorCount", "0");
            }

            // RetroBat layout is used : VPX must not offer its own default layout for the gamepads
            if (Controllers.Any(c => !c.IsKeyboard) && Controller.CheckSDL3dll())
            {
                foreach (var gamepad in Sdl3GameController.GetControllers().Where(c => c.IsGamepad))
                    ini.WriteValue("Input", "Device." + GetJoystickIdX9(gamepad) + ".NoAutoLayout", "1");
            }
        }

        /// <summary>Device identifier of VPX : "SDLJoy_" + SDL3 GUID + "_" + rank among the devices with the same GUID, in SDL order (SDLInputHandler.h / OnJoystickAdded).</summary>
        private static string GetJoystickIdX9(Sdl3GameController sdl3)
        {
            string guid = sdl3.GuidString;
            int rank = 1 + Sdl3GameController.GetControllers().Count(c => c.EnumerationIndex < sdl3.EnumerationIndex && c.GuidString == guid);

            return "SDLJoy_" + guid + "_" + rank;
        }

        /// <summary>
        /// Binding of a controller position (south, l1, up...) : "device;element".
        /// Element : button number, 0x0100 + 4 * hat + direction, or 0x0200 + axis followed by the side that presses it.
        /// </summary>
        private static string GetPadBindingX9(Sdl3GameController sdl3, string device, string position)
        {
            Input input = sdl3.GetRawInput(position);
            if (input == null)
                return null;

            switch (input.Type)
            {
                case "button":
                    return device + ";" + input.Id;

                case "hat":
                    {
                        // Direction : 0 left, 1 right, 2 up, 3 down (SDL hat mask : 1 up, 2 right, 4 down, 8 left)
                        int direction;
                        switch (input.Value)
                        {
                            case 8: direction = 0; break;
                            case 2: direction = 1; break;
                            case 1: direction = 2; break;
                            case 4: direction = 3; break;
                            default: return null;
                        }

                        return device + ";" + (0x0100 + 4 * input.Id + direction);
                    }

                case "axis":
                    // Axis used as a button : "o" pressed above the threshold, "x" pressed below
                    return device + ";" + (0x0200 + input.Id) + (input.Value < 0 ? ";x;-0.5" : ";o;0.5");
            }

            return null;
        }

        /// <summary>
        /// 10.9+ : analog plunger (right stick pulled down) and analog nudge (left stick, ENABLE ANALOG NUDGE).
        /// Both are sensors at rest on 0 : a trigger, at rest on -1, can't be used for the plunger.
        /// </summary>
        private void SetupAnalogControlsX9(IniFile ini, Sdl3GameController sdl3, string device)
        {
            bool useAxes = !SystemConfig.getOptBoolean("nouse_joyaxis");

            // JOYSTICK DEADZONE : percentage, a fraction of the axis for VPX
            float percent;
            if (!float.TryParse(SystemConfig["joy_deadzone"], NumberStyles.Float, CultureInfo.InvariantCulture, out percent))
                percent = 15;

            string deadZone = (Math.Max(0, Math.Min(50, percent)) / 100f).ToString("0.00", CultureInfo.InvariantCulture);

            // Plunger
            string plunger = useAxes ? GetPadSensorX9(sdl3, device, "righty", deadZone) : null;

            ini.WriteValue("Input", "PlungerSensorCount", plunger != null ? "1" : "0");
            if (plunger != null)
            {
                ini.WriteValue("Input", "Mapping.Plunger0.Position", plunger);
                ini.WriteValue("Input", "Mapping.Plunger0.Velocity", "");
            }

            // Nudge
            bool analogNudge = useAxes && SystemConfig.getOptBoolean("vp_analognudge");
            string nudgeX = analogNudge ? GetPadSensorX9(sdl3, device, "leftx", deadZone) : null;
            string nudgeY = analogNudge ? GetPadSensorX9(sdl3, device, "lefty", deadZone) : null;

            if (nudgeX != null && nudgeY != null)
            {
                ini.WriteValue("Input", "NudgeSensorCount", "1");
                ini.WriteValue("Input", "Mapping.Nudge0.Type", "0");    // game controller
                ini.WriteValue("Input", "Mapping.Nudge0.X", nudgeX);
                ini.WriteValue("Input", "Mapping.Nudge0.Y", nudgeY);
            }
            else
                ini.WriteValue("Input", "NudgeSensorCount", "0");
        }

        /// <summary>Sensor on a stick axis : "device;axis;type;dead zone;scale;limit", type P = position, scale -1 for an inverted axis. Null if the stick is not an axis.</summary>
        private static string GetPadSensorX9(Sdl3GameController sdl3, string device, string position, string deadZone)
        {
            Input input = sdl3.GetRawInput(position);
            if (input == null || input.Type != "axis")
                return null;

            // Whole stick axis : -1 when left / up is its negative side, +1 when it is inverted
            return device + ";" + (0x0200 + input.Id) + ";P;" + deadZone + ";" + (input.Value > 0 ? "-1" : "1") + ";1";
        }

        /// <summary>
        /// 10.9+ : folders of the PuP packs, colorizations and alternate sounds, in roms\vpinball.
        /// A 10.8 folder of the emulator is kept as long as it is the only one that contains files.
        /// </summary>
        private void SetupFoldersX9(IniFile ini, string path)
        {
            string roms = Path.Combine(AppConfig.GetFullPath("roms"), "vpinball");
            string vpinMame = Path.Combine(path, "VPinMAME");

            // PuP packs : the setting is the folder that contains "pupvideos"
            string pupVideos = GetAddonFolderX9(Path.Combine(roms, "pupvideos"), Path.Combine(path, "PinUPSystem", "PUPVideos"));
            ini.WriteValue("Plugin.PUP", "PUPFolder", Path.GetDirectoryName(pupVideos));

            // Colorizations : one subfolder per rom, for both Serum and VNI
            string altColor = GetAddonFolderX9(Path.Combine(roms, "altcolor"), Path.Combine(vpinMame, "altcolor"));
            ini.WriteValue("Plugin.Serum", "SerumPath", altColor);
            ini.WriteValue("Plugin.VNI", "VniPath", altColor);

            // Alternate sounds : the folder must be named "altsound"
            ini.WriteValue("Plugin.AltSound", "Folder", GetAddonFolderX9(Path.Combine(roms, "altsound"), Path.Combine(vpinMame, "altsound")));
        }

        private static string GetAddonFolderX9(string folder, string legacyFolder)
        {
            if (!HasFilesX9(folder) && HasFilesX9(legacyFolder))
                return legacyFolder;

            try { Directory.CreateDirectory(folder); }
            catch { }

            return folder;
        }

        private static bool HasFilesX9(string folder)
        {
            try { return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Any(); }
            catch { return false; }
        }
    }
}