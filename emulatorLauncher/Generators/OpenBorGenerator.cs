using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.Tools;
using System.Globalization;
using System.Runtime.InteropServices;

namespace emulatorLauncher
{
    class OpenBorGenerator : Generator
    {
        private string destFile;

        public static int JoystickValue(InputKey key, Controller c)
        {
            var a = c.Input[key];
            if (a == null)
            {
                if (key == InputKey.hotkey)
                    a = c.Input[InputKey.hotkeyenable];

                if (a == null)
                    return 0;
            }

            int JOY_MAX_INPUTS = 64;

            int value = 0;

            if (a.Type == "button")
                value = 1 + (c.Index - 1) * JOY_MAX_INPUTS + (int)a.Id;
            else if (a.Type == "hat")
            {
                int hatfirst = 1 + (c.Index - 1) * JOY_MAX_INPUTS + c.NbButtons + 2 * c.NbAxes + 4 * (int)a.Id;
                if (a.Value == 2) // SDL_HAT_RIGHT
                    hatfirst += 1;
                else if (a.Value == 4) // SDL_HAT_DOWN
                    hatfirst += 2;
                else if (a.Value == 8) // SDL_HAT_LEFT
                    hatfirst += 3;

                value = hatfirst;
            }
            else if (a.Type == "axis")
            {
                int axisfirst = 1 + (c.Index - 1) * JOY_MAX_INPUTS + c.NbButtons + 2 * (int)a.Id;
                if (a.Value > 0) axisfirst++;
                value = axisfirst;
            }

            if (c.Input.Type != "keyboard")
                value += 600;

            return value;
        }

        public int KeyboardValue(InputKey key, Controller c)
        {
            var a = c.Input[key];
            if (a == null)
                return 0;

            List<int> azertyLayouts = new List<int>() { 1036, 2060, 3084, 5132, 4108 };

            int id = (int)a.Id;
            if (azertyLayouts.Contains(CultureInfo.CurrentCulture.KeyboardLayoutId))
            {
                if (id == 'a')
                    id = 'q';
                else if (id == 'q')
                    id = 'a';
                else if (id == 'w')
                    id = 'z';
                else if (id == 'z')
                    id = 'w';
            }

            var mapped = SDL.SDL_default_keymap.Select(k => Convert.ToInt32(k)).ToList().IndexOf(id);
            if (mapped >= 0)
                return mapped;

            return 0;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {            
            string path = AppConfig.GetFullPath("openbor");

            string exe = Path.Combine(path, "OpenBOR.exe");
            if (!File.Exists(exe))
                return null;

            string build = GetBuildToUse(rom);
            if (!string.IsNullOrEmpty(build))
            {
                path = Path.Combine(path, build);
                exe = Path.Combine(path, "OpenBOR.exe");
            }

            if (setupConfigIni(path))
            {
                UsePadToKey = false;

                return new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = "\"" + rom + "\"",
                    WorkingDirectory = path
                };
            }       

            if (build == "4432")
                setupConfigBor4432Cfg(path);
            else 
                setupConfigBorCfg(path);

            string pakDir = Path.Combine(path, "Paks");
            Directory.CreateDirectory(pakDir);
            foreach (var file in Directory.GetFiles(pakDir))
            {
                if (Path.GetFileName(file) == Path.GetFileName(rom))
                    continue;

                File.Delete(file);
            }

            destFile = Path.Combine(pakDir, Path.GetFileName(rom));
            if (!File.Exists(destFile))
                File.Copy(rom, destFile);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path
            };
        }

        public override void Cleanup()
        {
            if (destFile != null && File.Exists(destFile))
                File.Delete(destFile);
        }

        string GetBuildToUse(string rom)
        {
            return null;

            string path = AppConfig.GetFullPath("openbor");

            int buildIndex = rom.LastIndexOf(']');
            if (buildIndex >= 5)
            {
                int buildNumber = rom.Substring(buildIndex - 4, 4).ToInteger();
                if (buildNumber == 0)
                    return null;

                if (buildNumber < 4000 && File.Exists(Path.Combine(path, "3318", "OpenBOR.exe")))
                    return "3318";
                else if (buildNumber < 6000 && File.Exists(Path.Combine(path, "4432", "OpenBOR.exe")))
                    return "4432";
                else if (buildNumber < 6340 && File.Exists(Path.Combine(path, "6330", "OpenBOR.exe")))
                    return "6330";
            }

            return null;
        }
        
        #region Ini file
        private void setupControllers(ConfigFile ini)
        {
            if (!Controllers.Any())
                return;

            bool hasKeyb = false;

            for (int idx = 0; idx < 4; idx++)
            {
                var c = Controllers.FirstOrDefault(j => j.Index == idx + 1);
                if (c == null || c.Input == null)
                {
                    if (hasKeyb)
                    {
                        ini["keys." + idx + ".0"] = "0";
                        ini["keys." + idx + ".1"] = "0";
                        ini["keys." + idx + ".2"] = "0";
                        ini["keys." + idx + ".3"] = "0";
                        ini["keys." + idx + ".4"] = "0";
                        ini["keys." + idx + ".5"] = "0";
                        ini["keys." + idx + ".6"] = "0";
                        ini["keys." + idx + ".7"] = "0";
                        ini["keys." + idx + ".8"] = "0";
                        ini["keys." + idx + ".9"] = "0";
                        ini["keys." + idx + ".10"] = "0";
                        ini["keys." + idx + ".11"] = "0";
                        ini["keys." + idx + ".12"] = "0";
                    }
                    else
                    {
                        ini["keys." + idx + ".0"] = "82";
                        ini["keys." + idx + ".1"] = "81";
                        ini["keys." + idx + ".2"] = "80";
                        ini["keys." + idx + ".3"] = "79";
                        ini["keys." + idx + ".4"] = "4";
                        ini["keys." + idx + ".5"] = "22";
                        ini["keys." + idx + ".6"] = "29";
                        ini["keys." + idx + ".7"] = "27";
                        ini["keys." + idx + ".8"] = "7";
                        ini["keys." + idx + ".9"] = "9";
                        ini["keys." + idx + ".10"] = "40";
                        ini["keys." + idx + ".11"] = "69";
                        ini["keys." + idx + ".12"] = "41"; // Esc
                        hasKeyb = true;
                    }

                    continue;
                }

                if (c.Input.Type == "keyboard")
                {
                    hasKeyb = true;
                    ini["keys." + idx + ".0"] = KeyboardValue(InputKey.up, c).ToString();
                    ini["keys." + idx + ".1"] = KeyboardValue(InputKey.down, c).ToString();
                    ini["keys." + idx + ".2"] = KeyboardValue(InputKey.left, c).ToString();
                    ini["keys." + idx + ".3"] = KeyboardValue(InputKey.right, c).ToString();
                    ini["keys." + idx + ".4"] = KeyboardValue(InputKey.a, c).ToString(); // ATTACK
                    ini["keys." + idx + ".5"] = KeyboardValue(InputKey.x, c).ToString();
                    ini["keys." + idx + ".6"] = KeyboardValue(InputKey.y, c).ToString();
                    ini["keys." + idx + ".7"] = KeyboardValue(InputKey.rightshoulder, c).ToString(); // ATTACK4
                    ini["keys." + idx + ".8"] = KeyboardValue(InputKey.b, c).ToString(); // JUMP
                    ini["keys." + idx + ".9"] = KeyboardValue(InputKey.select, c).ToString();
                    ini["keys." + idx + ".10"] = KeyboardValue(InputKey.start, c).ToString();
                    ini["keys." + idx + ".11"] = "69"; // F12
                    ini["keys." + idx + ".12"] = "41"; // Esc
                    continue;
                }

                ini["keys." + idx + ".0"] = JoystickValue(InputKey.up, c).ToString();
                ini["keys." + idx + ".1"] = JoystickValue(InputKey.down, c).ToString();
                ini["keys." + idx + ".2"] = JoystickValue(InputKey.left, c).ToString();
                ini["keys." + idx + ".3"] = JoystickValue(InputKey.right, c).ToString();
                ini["keys." + idx + ".4"] = JoystickValue(InputKey.a, c).ToString(); // ATTACK
                ini["keys." + idx + ".5"] = JoystickValue(InputKey.x, c).ToString();
                ini["keys." + idx + ".6"] = JoystickValue(InputKey.y, c).ToString();
                ini["keys." + idx + ".7"] = JoystickValue(InputKey.rightshoulder, c).ToString(); // ATTACK4
                ini["keys." + idx + ".8"] = JoystickValue(InputKey.b, c).ToString(); // JUMP
                ini["keys." + idx + ".9"] = JoystickValue(InputKey.select, c).ToString();
                ini["keys." + idx + ".10"] = JoystickValue(InputKey.start, c).ToString();
                ini["keys." + idx + ".11"] = "0";

                if (Program.EnableHotKeyStart)
                    ini["keys." + idx + ".12"] = JoystickValue(InputKey.hotkey, c).ToString(); // esc
                else
                    ini["keys." + idx + ".12"] = "0";
            }
        }

        private bool setupConfigIni(string path)
        {
            string ini = Path.Combine(path, "config.ini");
            if (!File.Exists(ini))
                return false;

            var conf = ConfigFile.FromFile(ini);
            if (conf == null)
                return false;

            setupControllers(conf);

            conf["fullscreen"] = "1";
            conf["vsync"] = "1";
            conf["usegl"] = "1";

            if( SystemConfig.isOptSet("ratio"))
                conf["stretch"] = SystemConfig["ratio"] == "1" ? "1" : "0";

            if (SystemConfig.isOptSet("filter"))
                conf["swfilter"] = SystemConfig["filter"];
            else
                conf["swfilter"] = "0";

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig["screenshots"]))
            {
                string dir = AppConfig.GetFullPath("screenshots");

                Uri relRoot = new Uri(path, UriKind.Absolute);
                string relPath = relRoot.MakeRelativeUri(new Uri(dir, UriKind.Absolute)).ToString().Replace("/", "\\");

                conf["screenShotsDir"] = dir; // ".\\" + relPath;
            }

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
            {
                string dir = Path.Combine(AppConfig.GetFullPath("saves"), "openbor");

                Uri relRoot = new Uri(path, UriKind.Absolute);
                string relPath = relRoot.MakeRelativeUri(new Uri(dir, UriKind.Absolute)).ToString().Replace("/", "\\");

                Directory.CreateDirectory(dir);
                conf["savesDir"] = dir; // ".\\" + relPath;
            }

            conf.Save(ini, false);
            return true;
        }
        #endregion

        #region bor.cfg
        private void setupControllersCfg(savedata conf)
        {
            if (!Controllers.Any())
                return;

            bool hasKeyb = false;

            for (int idx = 0; idx < 4; idx++)
            {
                var c = Controllers.FirstOrDefault(j => j.Index == idx + 1);
                if (c == null || c.Input == null)
                {
                    if (hasKeyb)
                    {
                        conf.keys[idx].up = 0;
                        conf.keys[idx].down = 0;
                        conf.keys[idx].left = 0;
                        conf.keys[idx].right = 0;
                        conf.keys[idx].attack1 = 0;
                        conf.keys[idx].attack2 = 0;
                        conf.keys[idx].attack3 = 0;
                        conf.keys[idx].attack4 = 0;
                        conf.keys[idx].jump = 0;
                        conf.keys[idx].special = 0;
                        conf.keys[idx].start = 0;
                        conf.keys[idx].screenshot = 0;
                        conf.keys[idx].esc = 0;
                    }
                    else
                    {
                        conf.keys[idx].up = 82;
                        conf.keys[idx].down = 81;
                        conf.keys[idx].left = 80;
                        conf.keys[idx].right = 79;
                        conf.keys[idx].attack1 = 4;
                        conf.keys[idx].attack2 = 22;
                        conf.keys[idx].attack3 = 29;
                        conf.keys[idx].attack4 = 27;
                        conf.keys[idx].jump = 7;
                        conf.keys[idx].special = 9;
                        conf.keys[idx].start = 40;
                        conf.keys[idx].screenshot = 69;
                        conf.keys[idx].esc = 41;
                        hasKeyb = true;
                    }

                    continue;
                }

                if (c.Input.Type == "keyboard")
                {
                    hasKeyb = true;
                    conf.keys[idx].up = KeyboardValue(InputKey.up, c);
                    conf.keys[idx].down = KeyboardValue(InputKey.down, c);
                    conf.keys[idx].left = KeyboardValue(InputKey.left, c);
                    conf.keys[idx].right = KeyboardValue(InputKey.right, c);
                    conf.keys[idx].attack1 = KeyboardValue(InputKey.a, c); // ATTACK
                    conf.keys[idx].attack2 = KeyboardValue(InputKey.x, c);
                    conf.keys[idx].attack3 = KeyboardValue(InputKey.y, c);
                    conf.keys[idx].attack4 = KeyboardValue(InputKey.rightshoulder, c); // ATTACK4
                    conf.keys[idx].jump = KeyboardValue(InputKey.b, c); // JUMP
                    conf.keys[idx].special = KeyboardValue(InputKey.select, c);
                    conf.keys[idx].start  = KeyboardValue(InputKey.start, c);
                    conf.keys[idx].screenshot  = 69; // F12
                    conf.keys[idx].esc  = 41; // Esc
                    continue;
                }

                conf.keys[idx].up = JoystickValue(InputKey.up, c);
                conf.keys[idx].down = JoystickValue(InputKey.down, c);
                conf.keys[idx].left = JoystickValue(InputKey.left, c);
                conf.keys[idx].right = JoystickValue(InputKey.right, c);
                conf.keys[idx].attack1 = JoystickValue(InputKey.a, c); // ATTACK
                conf.keys[idx].attack2 = JoystickValue(InputKey.x, c);
                conf.keys[idx].attack3 = JoystickValue(InputKey.y, c);
                conf.keys[idx].attack4 = JoystickValue(InputKey.rightshoulder, c); // ATTACK4
                conf.keys[idx].jump = JoystickValue(InputKey.b, c); // JUMP
                conf.keys[idx].special = JoystickValue(InputKey.select, c);
                conf.keys[idx].start  = JoystickValue(InputKey.start, c);
                conf.keys[idx].screenshot  = 0;

                if (Program.EnableHotKeyStart)
                    conf.keys[idx].esc = JoystickValue(InputKey.hotkey, c); // esc
                else
                    conf.keys[idx].esc = 0;
            }
        }
        
        private bool setupConfigBorCfg(string path)
        {
            savedata conf = new savedata();
            conf.init();

            Directory.CreateDirectory(Path.Combine(path, "Saves"));
            foreach (var file in Directory.GetFiles(Path.Combine(path, "Saves"), "*.cfg"))
                if (Path.GetFileName(file).ToLower() != "default.cfg")
                    File.Delete(file);

            string cfg = Path.Combine(path, "Saves", "default.cfg");
            if (File.Exists(cfg))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(cfg);
                    if (bytes.Length >= 320 && bytes[0] == 0x48 && bytes[1] == 0x37 && bytes[2] == 0x03 && bytes[3] == 0x00)
                    {
                        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        conf = (savedata)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(savedata));
                        handle.Free();
                    }
                }
                catch
                {
                    return false;
                }
            }

            setupControllersCfg(conf);

            conf.fullscreen = 1;
            conf.vsync = 1;
            conf.usegl = 1;

            try
            {
                int size = Marshal.SizeOf(conf);
                byte[] arr = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(conf, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
                Marshal.FreeHGlobal(ptr);

                File.WriteAllBytes(cfg, arr);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private bool setupConfigBor4432Cfg(string path)
        {
            savedata4432 conf = new savedata4432();
            // 324 ??
            conf.init();

            Directory.CreateDirectory(Path.Combine(path, "Saves"));
            foreach (var file in Directory.GetFiles(Path.Combine(path, "Saves"), "*.cfg"))
                if (Path.GetFileName(file).ToLower() != "default.cfg")
                    File.Delete(file);

            string cfg = Path.Combine(path, "Saves", "default.cfg");
            if (File.Exists(cfg))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(cfg);
                    if (bytes.Length >= 320 && bytes[0] == 0x47 && bytes[1] == 0x37 && bytes[2] == 0x03 && bytes[3] == 0x00)
                    {
                        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                        conf = (savedata4432)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(savedata));
                        handle.Free();
                    }
                }
                catch
                {
                    return false;
                }
            }

           // setupControllersCfg(conf);

            conf.fullscreen = 1;
       //     conf.vsync = 1;
       //     conf.usegl = 1;

            try
            {
                int size = Marshal.SizeOf(conf);
                byte[] arr = new byte[size];

                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(conf, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
                Marshal.FreeHGlobal(ptr);

              //  File.WriteAllBytes(cfg, arr);
            }
            catch
            {
                return false;
            }
            return true;
        }
        #endregion
    }

    [StructLayoutAttribute(LayoutKind.Sequential, Pack = 4)]
    struct savekey
    {
        [MarshalAs(UnmanagedType.I4)]
        public int up;
        [MarshalAs(UnmanagedType.I4)]
        public int down;
        [MarshalAs(UnmanagedType.I4)]
        public int left;
        [MarshalAs(UnmanagedType.I4)]
        public int right;
        [MarshalAs(UnmanagedType.I4)]
        public int attack1;
        [MarshalAs(UnmanagedType.I4)]
        public int attack2;
        [MarshalAs(UnmanagedType.I4)]
        public int attack3;
        [MarshalAs(UnmanagedType.I4)]
        public int attack4;
        [MarshalAs(UnmanagedType.I4)]
        public int jump;
        [MarshalAs(UnmanagedType.I4)]
        public int special;
        [MarshalAs(UnmanagedType.I4)]
        public int start;
        [MarshalAs(UnmanagedType.I4)]
        public int screenshot;
        [MarshalAs(UnmanagedType.I4)]
        public int esc;
    }

    [StructLayoutAttribute(LayoutKind.Sequential, Pack = 4)]
    struct savedata
    {        
        public void init()
        {
            keys = new savekey[4];
            joyrumble = new int[4];
            screen = new int[2];

            compatibleversion = 0x00033748;
            gamma = 0;
            brightness = 0;
            usejoy = 1;
            showtitles = 0;
            logo = 0;
            mode = 1;
            windowpos = 0;
            soundvol = 15;
            musicvol = 100;
            effectvol = 120;
            debuginfo = 0;
            usemusic = 1;
            uselog = 1;
            stretch = 0;
            hwscale = 2.0f;
            hwfilter = 1;
            videoNTSC = 0;
            swfilter = 0;
            fullscreen = 1;
            vsync = 1;
            usegl = 1;
        }

        [MarshalAs(UnmanagedType.I4)]
        public int compatibleversion;
        [MarshalAs(UnmanagedType.I4)]
        public int gamma;
        [MarshalAs(UnmanagedType.I4)]
        public int brightness;
        [MarshalAs(UnmanagedType.I4)]
        public int soundvol; // SB volume
        [MarshalAs(UnmanagedType.I4)]
        public int usemusic; // Play music
        [MarshalAs(UnmanagedType.I4)]
        public int musicvol; // Music volume
        [MarshalAs(UnmanagedType.I4)]
        public int effectvol; // Sound fx volume
        [MarshalAs(UnmanagedType.I4)]
        public int usejoy;
        [MarshalAs(UnmanagedType.I4)]
        public int mode; // Mode now saves
        [MarshalAs(UnmanagedType.I4)]
        public int windowpos;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public savekey[] keys;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4, ArraySubType = UnmanagedType.I4)]
        public int[] joyrumble;

        [MarshalAs(UnmanagedType.I4)]
        public int showtitles;
        [MarshalAs(UnmanagedType.I4)]
        public int videoNTSC;
        [MarshalAs(UnmanagedType.I4)]
        public int swfilter; // Software scaling filter
        [MarshalAs(UnmanagedType.I4)]
        public int logo;
        [MarshalAs(UnmanagedType.I4)]
        public int uselog;
        [MarshalAs(UnmanagedType.I4)]
        public int debuginfo; // FPS, Memory, etc...
        [MarshalAs(UnmanagedType.I4)]
        public int fullscreen; // Window or Full Screen Mode
        [MarshalAs(UnmanagedType.I4)]
        public int stretch; // Stretch (1) or preserve aspect ratio (0) in fullscreen mode

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I4)]
        public int[] screen;

        [MarshalAs(UnmanagedType.I4)]
        public int vsync; // Sync to monitor refresh (1) or don't (0)        
        [MarshalAs(UnmanagedType.I4)]
        public int usegl; // 1 if OpenGL is preferred over SDL software blitting
        [MarshalAs(UnmanagedType.R4)]
        public float hwscale; // Scale factor for OpenGL
        [MarshalAs(UnmanagedType.I4)]
        public int hwfilter; // Simple or bilinear scaling        
    }

    #region v3318
    //  3318-3400 - 3698
    // #define		COMPATIBLEVERSION	0x00030000
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = 4)]
    struct savedata30000
    {
        [MarshalAs(UnmanagedType.I4)]
        public int compatibleversion;
        [MarshalAs(UnmanagedType.I2)]
        public short gamma;
        [MarshalAs(UnmanagedType.I2)]
        public short brightness;
        [MarshalAs(UnmanagedType.I1)]
        public char usesound;	// Use SB
        [MarshalAs(UnmanagedType.I2)]
        public short soundrate;	// SB freq
        [MarshalAs(UnmanagedType.I2)]
        public short soundvol;	// SB volume
        [MarshalAs(UnmanagedType.I1)]
        public char usemusic;	// Play music
        [MarshalAs(UnmanagedType.I2)]
        public short musicvol;	// Music volume
        [MarshalAs(UnmanagedType.I2)]
        public short effectvol;	// Sound fx volume
        [MarshalAs(UnmanagedType.I1)]
        public char soundbits;	// SB bits
        [MarshalAs(UnmanagedType.I1)]
        public char usejoy;
        [MarshalAs(UnmanagedType.I1)]
        public char mode;		// Mode now saves
        [MarshalAs(UnmanagedType.I1)]
        public char windowpos;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public savekey33747[] keys; //[MAX_PLAYERS][12];

        [MarshalAs(UnmanagedType.I1)]
        public char showtitles;
        [MarshalAs(UnmanagedType.I1)]
        public char videoNTSC;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14, ArraySubType = UnmanagedType.I1)]
        public char[] screen; // [7][2]; // Screen Filtering/Scaling Effects

        [MarshalAs(UnmanagedType.I1)]
        public char logo;
        [MarshalAs(UnmanagedType.I1)]
        public char uselog;
        [MarshalAs(UnmanagedType.I1)]
        public char debuginfo;	// FPS, Memory, etc...        
        [MarshalAs(UnmanagedType.I1)]
        public char fullscreen;	// Window or Full Screen Mode
        [MarshalAs(UnmanagedType.I1)]
        public char stretch;		// Stretch (1) or preserve aspect ratio (0) in fullscreen mode
        [MarshalAs(UnmanagedType.I1)]
        public char usegl;		// 1 if OpenGL is preferred over SDL software blitting         
        [MarshalAs(UnmanagedType.R4)]
        public float glscale;		// Scale factor for OpenGL
        [MarshalAs(UnmanagedType.I1)]
        public char glfilter;	// Simple or bilinear scaling
    }

    // #define		COMPATIBLEVERSION	0x00033747
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = 4)]
    struct savekey33747
    {
        [MarshalAs(UnmanagedType.I4)]
        public int up;
        [MarshalAs(UnmanagedType.I4)]
        public int down;
        [MarshalAs(UnmanagedType.I4)]
        public int left;
        [MarshalAs(UnmanagedType.I4)]
        public int right;
        [MarshalAs(UnmanagedType.I4)]
        public int attack1;
        [MarshalAs(UnmanagedType.I4)]
        public int attack2;
        [MarshalAs(UnmanagedType.I4)]
        public int attack3;
        [MarshalAs(UnmanagedType.I4)]
        public int attack4;
        [MarshalAs(UnmanagedType.I4)]
        public int jump;
        [MarshalAs(UnmanagedType.I4)]
        public int special;
        [MarshalAs(UnmanagedType.I4)]
        public int start;
        [MarshalAs(UnmanagedType.I4)]
        public int screenshot;
    }
    #endregion

    #region v4432


    // 4432
    [StructLayoutAttribute(LayoutKind.Sequential, Pack = 4)]
    struct savedata4432
    {
        public void init()
        {
            keys = new savekey[4];
            screen = new int[2];

            compatibleversion = 0x00033747;
            gamma = 0;
            brightness = 0;
            usejoy = 1;
            showtitles = 0;
            logo = 0;
            mode = 1;
            windowpos = 0;
            soundvol = 15;
            musicvol = 100;
            effectvol = 120;
            debuginfo = 0;
            usemusic = 1;
            uselog = 1;
            stretch = 0;
            videoNTSC = 0;
            fullscreen = 1;
            usegl = new int[2];
            usegl[0] = 1;
            usegl[1] = 1;
            usesound = 1;
            soundrate = 44100;
            soundbits = 16;
            glscale = 2.000000f;
            glfilter = new int[2];
        }

        [MarshalAs(UnmanagedType.I4)]
	    public int compatibleversion;
        [MarshalAs(UnmanagedType.I4)]
	    public int gamma;
        [MarshalAs(UnmanagedType.I4)]
	    public int brightness;
        [MarshalAs(UnmanagedType.I4)]
	    public int usesound; // Use SB
        [MarshalAs(UnmanagedType.I4)]
	    public int soundrate; // SB freq
        [MarshalAs(UnmanagedType.I4)]
	    public int soundvol; // SB volume
        [MarshalAs(UnmanagedType.I4)]
	    public int usemusic; // Play music
        [MarshalAs(UnmanagedType.I4)]
	    public int musicvol; // Music volume
        [MarshalAs(UnmanagedType.I4)]
	    public int effectvol; // Sound fx volume
        [MarshalAs(UnmanagedType.I4)]
	    public int soundbits; // SB bits
        [MarshalAs(UnmanagedType.I4)]
	    public int usejoy;
        [MarshalAs(UnmanagedType.I4)]
	    public int mode; // Mode now saves
        [MarshalAs(UnmanagedType.I4)]
	    public int windowpos;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
	    public savekey[] keys;

        [MarshalAs(UnmanagedType.I4)]
	    public int showtitles;        
        [MarshalAs(UnmanagedType.I4)]
	    public int videoNTSC;
        [MarshalAs(UnmanagedType.I4)]
        public int swfilter;        
        [MarshalAs(UnmanagedType.I4)]
	    public int logo;
        [MarshalAs(UnmanagedType.I4)]
	    public int uselog;
        [MarshalAs(UnmanagedType.I4)]
	    public int debuginfo; // FPS, Memory, etc...
        
        [MarshalAs(UnmanagedType.I4)]
        public int debug_collision_attack; // FPS, Memory, etc...
        [MarshalAs(UnmanagedType.I4)]
        public int debug_collision_body; // FPS, Memory, etc...
        [MarshalAs(UnmanagedType.I4)]
        public int debug_collision_range; // FPS, Memory, etc...
        [MarshalAs(UnmanagedType.I4)]
        public int debug_position; // FPS, Memory, etc...
        [MarshalAs(UnmanagedType.I4)]
        public int debug_features; // FPS, Memory, etc...
        
        [MarshalAs(UnmanagedType.I4)]
	    public int fullscreen; // Window or Full Screen Mode
        [MarshalAs(UnmanagedType.I4)]
	    public int stretch; // Stretch (1) or preserve aspect ratio (0) in fullscreen mode

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I4)]
        public int[] screen; // [7][2]; // Screen Filtering/Scaling Effects

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I4)]
	    public int[] usegl; // 1 if OpenGL is preferred over SDL software blitting

        [MarshalAs(UnmanagedType.R4)]
	    public float glscale; // Scale factor for OpenGL

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.I4)]
	    int[] glfilter; // Simple or bilinear scaling
    };    
    #endregion
}
