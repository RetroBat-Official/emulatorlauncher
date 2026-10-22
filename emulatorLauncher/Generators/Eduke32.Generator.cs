using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace EmulatorLauncher
{
    class EDukeGenerator : Generator
    {
        public EDukeGenerator()
        {
            // EDuke32 sets the display mode itself from ScreenWidth / ScreenHeight (eduke32.cfg)
            DependsOnDesktopResolution = false;
        }

        // The screen is selected natively with ScreenDisplay (SDL display index) : moving the window would break exclusive fullscreen and the bezel layout
        protected override bool UseGenericScreenPlacement { get { return false; } }

        private bool _dirArgInFile = false;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private string _sf2Bank;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("eduke32");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "eduke32.exe");
            if (!File.Exists(exe))
                return null;

            _resolution = resolution;

            var commandArray = new List<string>();

            string gamegrp = null;
            if (Path.GetExtension(rom).ToLower() == ".eduke32")
            {
                var edukeArgs = Eduke32Arg.ParseConfig(rom, Path.GetDirectoryName(rom), out gamegrp, out _dirArgInFile);

                // Paths returned by ParseConfig are not quoted : quote the values containing spaces
                foreach (var arg in edukeArgs)
                {
                    if (!arg.StartsWith("-") && arg.Contains(" "))
                        commandArray.Add("\"" + arg + "\"");
                    else
                        commandArray.Add(arg);
                }

                if (gamegrp != null && !_dirArgInFile)
                {
                    string dirPath = Path.GetDirectoryName(gamegrp);
                    commandArray.Add("-j");
                    commandArray.Add("\"" + dirPath + "\"");
                }
            }

            else
            {
                gamegrp = rom;
                commandArray.Add("-gamegrp");
                commandArray.Add("\"" + Path.GetFileName(rom) + "\"");
                commandArray.Add("-j");
                commandArray.Add("\"" + Path.GetDirectoryName(rom) + "\"");
            }

            // SoundFont music : use the Roland SC-55 soundfont shipped with RetroBat, its folder is added to the game search path
            _sf2Bank = null;
            if (SystemConfig["eduke_music"] == "4")
            {
                string sf2File = Path.Combine(AppConfig.GetFullPath("bios"), "scummvm", "extra", "Roland_SC-55.sf2");
                if (File.Exists(sf2File))
                {
                    _sf2Bank = Path.GetFileName(sf2File);
                    commandArray.Add("-j");
                    commandArray.Add("\"" + Path.GetDirectoryName(sf2File) + "\"");
                }
            }

            // Fullscreen : borderless window at the desktop resolution (most compatible), exclusive fullscreen if preferred or if a resolution is forced
            bool fullscreen = ShouldRunFullscreen();
            bool forcedResolution = SystemConfig.isOptSet("eduke_resolution") && !string.IsNullOrEmpty(SystemConfig["eduke_resolution"]);
            bool exclusive = fullscreen && (SystemConfig.getOptBoolean("exclusivefs") || resolution != null || forcedResolution);

            // Bezel overlay only in borderless fullscreen : a topmost window cannot stay over an exclusive fullscreen window
            if (fullscreen && !exclusive)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            SetupConfiguration(path, gamegrp, resolution, fullscreen, exclusive);
            SetupCfg(path, fullscreen && !exclusive);

            var psi = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = string.Join(" ", commandArray),
                WorkingDirectory = path,
                UseShellExecute = false
            };

            ConfigureControllerFilter(psi);

            return psi;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            // EDuke32 loads the en-US keyboard layout when its window gets the focus (winbits.cpp windowsSetKeyboardLayout)
            // and only restores the user layout when it loses the focus or exits normally : killed by RetroBat, the layout stays in English
            var layoutsBefore = GetKeyboardLayouts();
            IntPtr userLayout = GetKeyboardLayout(0);

            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution, true);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            RestoreKeyboardLayout(layoutsBefore, userLayout);

            return ret;
        }

        private static List<IntPtr> GetKeyboardLayouts()
        {
            int count = GetKeyboardLayoutList(0, null);
            if (count <= 0)
                return new List<IntPtr>();

            var list = new IntPtr[count];
            count = GetKeyboardLayoutList(count, list);
            return list.Take(Math.Max(0, count)).ToList();
        }

        private static void RestoreKeyboardLayout(List<IntPtr> layoutsBefore, IntPtr userLayout)
        {
            try
            {
                // Remove the layouts loaded by EDuke32 that the user did not have before (en-US)
                foreach (var hkl in GetKeyboardLayouts())
                {
                    if (!layoutsBefore.Contains(hkl))
                    {
                        UnloadKeyboardLayout(hkl);
                        SimpleLogger.Instance.Info("[EDUKE32] Keyboard layout loaded by the game removed : 0x" + hkl.ToInt64().ToString("X"));
                    }
                }

                if (userLayout == IntPtr.Zero)
                    return;

                // Activate the user layout again (input language is per user since Windows 8), also for the window now in foreground
                ActivateKeyboardLayout(userLayout, 0);

                IntPtr foreground = GetForegroundWindow();
                if (foreground != IntPtr.Zero)
                    PostMessage(foreground, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, userLayout);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[EDUKE32] Unable to restore the keyboard layout : " + ex.Message);
            }
        }

        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        [DllImport("user32.dll")]
        private static extern int GetKeyboardLayoutList(int nBuff, [Out] IntPtr[] lpList);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint flags);

        [DllImport("user32.dll")]
        private static extern bool UnloadKeyboardLayout(IntPtr hkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private void SetupConfiguration(string path, string gamegrp, ScreenResolution resolution, bool fullscreen, bool exclusive)
        {
            Screen screen = Program.TargetScreen ?? Screen.PrimaryScreen;

            try
            {
                using (IniFile ini = new IniFile(Path.Combine(path, "eduke32.cfg"), IniOptions.UseSpaces))
                {
                    // ScreenMode 1 = exclusive fullscreen, borderless fullscreen is a borderless window (ScreenMode 0 + r_borderless in settings.cfg)
                    ini.WriteValue("Screen Setup", "ScreenMode", exclusive ? "1" : "0");

                    ini.WriteValue("Setup", "ForceSetup", "0");
                    BindBoolIniFeature(ini, "Setup", "NoAutoLoad", "autoload", "0", "1");
                    ini.WriteValue("Updates", "CheckForUpdates", "0");

                    // SelectedGRP is only used when no game file is given on the command line, it must be a game file name
                    if (!string.IsNullOrEmpty(gamegrp))
                        ini.WriteValue("Setup", "SelectedGRP", "\"" + Path.GetFileName(gamegrp) + "\"");
                    else
                        ini.Remove("Setup", "SelectedGRP");

                    if (SystemConfig.isOptSet("eduke_video") && SystemConfig["eduke_video"] == "polymer")
                    {
                        ini.WriteValue("Screen Setup", "Polymer", "1");
                        ini.WriteValue("Screen Setup", "ScreenBPP", "32");
                    }
                    else if (SystemConfig.isOptSet("eduke_video") && SystemConfig["eduke_video"] == "polymost")
                    {
                        ini.WriteValue("Screen Setup", "Polymer", "0");
                        ini.WriteValue("Screen Setup", "ScreenBPP", "32");
                    }
                    else
                    {
                        ini.WriteValue("Screen Setup", "Polymer", "0");
                        ini.WriteValue("Screen Setup", "ScreenBPP", "8");
                    }

                    // Screen : SDL display index (primary first), always written so that the window position below matches the display
                    ini.WriteValue("Screen Setup", "ScreenDisplay", Math.Max(0, Displays.IndexOf(screen, MonitorOrder.SdlLike)).ToString());

                    if (exclusive)
                    {
                        if (SystemConfig.isOptSet("eduke_resolution") && !string.IsNullOrEmpty(SystemConfig["eduke_resolution"]))
                        {
                            string[] parts = SystemConfig["eduke_resolution"].Split('x');
                            ini.WriteValue("Screen Setup", "ScreenWidth", parts[0]);
                            ini.WriteValue("Screen Setup", "ScreenHeight", parts[1]);
                        }
                        else if (resolution != null)
                        {
                            ini.WriteValue("Screen Setup", "ScreenWidth", resolution.Width.ToString());
                            ini.WriteValue("Screen Setup", "ScreenHeight", resolution.Height.ToString());
                        }
                        else
                        {
                            ini.WriteValue("Screen Setup", "ScreenWidth", screen.Bounds.Width.ToString());
                            ini.WriteValue("Screen Setup", "ScreenHeight", screen.Bounds.Height.ToString());
                        }
                    }
                    else if (fullscreen)
                    {
                        // Borderless window covering the screen, or a centered 4:3 window inside the bezel
                        int height = screen.Bounds.Height;
                        int width = _bezelFileInfo != null ? Math.Min(screen.Bounds.Width, height * 4 / 3) : screen.Bounds.Width;

                        ini.WriteValue("Screen Setup", "ScreenWidth", width.ToString());
                        ini.WriteValue("Screen Setup", "ScreenHeight", height.ToString());
                        ini.WriteValue("Screen Setup", "WindowPosX", (screen.Bounds.X + (screen.Bounds.Width - width) / 2).ToString());
                        ini.WriteValue("Screen Setup", "WindowPosY", screen.Bounds.Y.ToString());
                    }
                    else if (SystemConfig.isOptSet("eduke_resolution") && !string.IsNullOrEmpty(SystemConfig["eduke_resolution"]))
                    {
                        // Windowed : keep the window size chosen in EDuke32 unless a resolution is forced
                        string[] parts = SystemConfig["eduke_resolution"].Split('x');
                        ini.WriteValue("Screen Setup", "ScreenWidth", parts[0]);
                        ini.WriteValue("Screen Setup", "ScreenHeight", parts[1]);
                    }

                    ConfigureControls(ini);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[EDUKE32] Unable to write eduke32.cfg : " + ex.Message);
            }
        }

        private void SetupCfg(string path, bool borderless)
        {
            string cfgFile = Path.Combine(path, "settings.cfg");

            bool vsync = !SystemConfig.isOptSet("VSync") || SystemConfig.getOptBoolean("VSync");

            var cfg = new QuakeConfig(cfgFile);

            // r_borderless is only used in windowed mode (ScreenMode 0)
            cfg["r_borderless"] = borderless ? "1" : "0";
            cfg["r_vsync"] = vsync ? "1" : "0";

            // Rendering (texture filter, anisotropy, models and hightile only apply to the OpenGL renderers)
            cfg["r_texfilter"] = SystemConfig.GetValueOrDefault("eduke_texfilter", "0");
            cfg["r_anisotropy"] = SystemConfig.GetValueOrDefault("eduke_anisotropy", "16");
            cfg["r_hightile"] = SystemConfig.isOptSet("eduke_hightile") && !SystemConfig.getOptBoolean("eduke_hightile") ? "0" : "1";
            cfg["r_models"] = SystemConfig.isOptSet("eduke_models") && !SystemConfig.getOptBoolean("eduke_models") ? "0" : "1";
            cfg["r_voxels"] = SystemConfig.isOptSet("eduke_voxels") && !SystemConfig.getOptBoolean("eduke_voxels") ? "0" : "1";
            cfg["fov"] = SystemConfig.GetValueOrDefault("eduke_fov", "90");

            // User interface
            cfg["r_size"] = SystemConfig.GetValueOrDefault("eduke_hud", "4");
            cfg["hud_stats"] = SystemConfig.getOptBoolean("eduke_levelstats") ? "1" : "0";
            cfg["r_showfps"] = SystemConfig.getOptBoolean("eduke_showfps") ? "1" : "0";
            cfg["crosshair"] = SystemConfig.isOptSet("eduke_crosshair") && !SystemConfig.getOptBoolean("eduke_crosshair") ? "0" : "1";

            // Gameplay
            cfg["cl_autorun"] = SystemConfig.isOptSet("eduke_alwaysrun") && !SystemConfig.getOptBoolean("eduke_alwaysrun") ? "0" : "1";
            cfg["cl_autoaim"] = SystemConfig.GetValueOrDefault("eduke_autoaim", "1");
            cfg["in_rumble"] = SystemConfig.isOptSet("eduke_rumble") && !SystemConfig.getOptBoolean("eduke_rumble") ? "0" : "1";

            // Music : 2 = OPL3 emulation (EDuke32 default), 3 = Windows MIDI, 4 = SoundFont
            string music = SystemConfig.GetValueOrDefault("eduke_music", "2");
            if (music == "4" && _sf2Bank == null)
            {
                SimpleLogger.Instance.Warning("[EDUKE32] Roland_SC-55.sf2 not found in bios\\scummvm\\extra, using OPL3 music.");
                music = "2";
            }
            cfg["mus_device"] = music;
            if (_sf2Bank != null)
                cfg["mus_sf2_bank"] = _sf2Bank;

            // Keyboard keys used by RetroBat hotkeys (es_padtokey.cfg): F2 quicksave, F4 quickload, F8 screenshot
            // Once bound, F2 / F4 / F8 no longer trigger their hardcoded actions (save menu, sound menu, messages toggle). F6 / F9 / F12 still work.
            cfg["bind F2"] = "gamefunc_Quick_Save";
            cfg["bind F4"] = "gamefunc_Quick_Load";
            cfg["bind F8"] = "screenshot";

            cfg.Save();
        }

        // Controller mapping : EDuke32 defaults (config.cpp CONFIG_SetGameControllerDefaults), Back is left free for RetroBat hotkeys
        private void ConfigureControls(IniFile ini)
        {
            if (SystemConfig.getOptBoolean("disableautocontrollers"))
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            ini.WriteValue("Controls", "UseJoystick", "1");

            // SDL game controller buttons : A, B, X, Y, BACK, GUIDE, START, LSTICK, RSTICK, LSHOULDER, RSHOULDER, DPAD UP, DOWN, LEFT, RIGHT, MISC
            string[] buttons =
            {
                "Open", "Inventory", "Inventory", "Quick_Kick", "", "", "", "Run",
                "Toggle_Crouch", "Crouch", "Alt_Fire", "Previous_Weapon", "Next_Weapon", "Inventory_Left", "Inventory_Right", "Third_Person_View"
            };

            for (int i = 0; i < buttons.Length; i++)
            {
                // START opens the menu and cannot be assigned
                if (i == 6)
                    continue;

                ini.WriteValue("Controls", "ControllerButton" + i, "\"" + buttons[i] + "\"");
                ini.WriteValue("Controls", "ControllerButtonClicked" + i, "\"\"");
            }

            // SDL game controller axes : left X, left Y, right X, right Y, left trigger, right trigger
            string[] analogAxes = { "analog_strafing", "analog_moving", "analog_turning", "analog_lookingupanddown", "", "" };
            string[] digitalAxesPositive = { "", "", "", "", "Jump", "Fire" };

            bool invertLook = SystemConfig.getOptBoolean("eduke_invertlook");

            for (int i = 0; i < analogAxes.Length; i++)
            {
                ini.WriteValue("Controls", "ControllerAnalogAxes" + i, "\"" + analogAxes[i] + "\"");
                ini.WriteValue("Controls", "ControllerDigitalAxes" + i + "_0", "\"\"");
                ini.WriteValue("Controls", "ControllerDigitalAxes" + i + "_1", "\"" + digitalAxesPositive[i] + "\"");
            }

            ini.WriteValue("Controls", "ControllerAnalogInvert3", invertLook ? "1" : "0");

            // Stick dead zone, in 1/10000 of the axis range (EDuke32 default 1000 = 10%), applied to both sticks (axes 0 to 3)
            string deadZone = SystemConfig.GetValueOrDefault("eduke_deadzone", "1000");
            for (int i = 0; i < 4; i++)
                ini.WriteValue("Controls", "ControllerAnalogDead" + i, deadZone);
        }

        // EDuke32 only uses the first SDL game controller : with several controllers, hide the models that are not player 1's
        private void ConfigureControllerFilter(ProcessStartInfo psi)
        {
            if (SystemConfig.getOptBoolean("disableautocontrollers"))
                return;

            var pads = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.PlayerIndex).ToList();
            if (pads.Count < 2)
                return;

            var p1 = pads[0];
            ushort vid = (ushort)p1.VendorID;
            ushort pid = (ushort)p1.ProductID;
            if (vid == 0 || pid == 0)
                return;

            string vidPid = string.Format("0x{0:X4}/0x{1:X4}", vid, pid);
            psi.EnvironmentVariables["SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT"] = vidPid;
            SimpleLogger.Instance.Info("[EDUKE32] Only controllers " + vidPid + " are visible to the game.");
        }
    }

    class Eduke32Arg
    {
        public static List<Eduke32Arg> Eduke32Args = new List<Eduke32Arg>()
        {
            new Eduke32Arg("DIR", "-j", false),
            new Eduke32Arg("FILE", "-gamegrp", true),
            new Eduke32Arg("FILE+", "-g", false),
            new Eduke32Arg("CON", "-x", true),
            new Eduke32Arg("CON+", "-mx", false),
            new Eduke32Arg("DEF", "-h", true),
            new Eduke32Arg("DEF+", "-mh", false),
            new Eduke32Arg("MAP", "-map", true),
        };

        public Eduke32Arg(string key, string argument, bool single)
        {
            Key = key;
            Argument = argument;
            Single = single;
        }

        public string Key { get; set; }
        public string Argument { get; set; }
        public bool Single { get; set; }


        public static List<string> ParseConfig(string filePath, string basePath, out string gamegrp, out bool dirArgInFile)
        {
            gamegrp = null;
            dirArgInFile = false;
            var lines = File.ReadAllLines(filePath);
            var argsList = new List<string>();

            var availableArgs = new List<Eduke32Arg>(Eduke32Args);

            int i = 0;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    continue;

                var parts = line.Split(new[] { '=' }, 2);
                var key = parts[0].Trim();
                var value = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                var argDef = availableArgs.FirstOrDefault(a => a.Key == key);
                if (argDef != null)
                {
                    argsList.Add(argDef.Argument);

                    if (argDef.Key == "DIR")
                        dirArgInFile = true;

                    if (value.StartsWith("/") || value.StartsWith("\\"))
                    {
                        value = value.Substring(1);
                        string fullPath = Path.Combine(basePath, value);
                        argsList.Add(fullPath);
                        if (argDef.Argument == "-gamegrp" && i != 1)
                        {
                            i = 1;
                            gamegrp = fullPath;
                        }
                    }

                    else
                    {
                        argsList.Add(value);
                        if (argDef.Argument == "-gamegrp" && i != 1)
                        {
                            i = 1;
                            gamegrp = value;
                        }
                    }

                    // Ensure we do not add twice a single-use argument
                    if (argDef.Single)
                        availableArgs.Remove(argDef);
                }
            }

            return argsList;
        }
    }
}
