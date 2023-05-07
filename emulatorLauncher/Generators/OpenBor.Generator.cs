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
    partial class OpenBorGenerator : Generator
    {
        public OpenBorGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _destFile;
        private bool _isCustomRetrobatOpenBor; // This Version support harcoded NumButtons / NumAxes values for generic injection

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

            try
            {
                var versionInfo = FileVersionInfo.GetVersionInfo(exe);
                _isCustomRetrobatOpenBor = (versionInfo.FilePrivatePart == 5242); // 5242 stands for RB ( 'R' x52, 'B' x42 ) -> RetroBat !
            }
            catch { }

            if (setupConfigIni(path))
            {
                UseEsPadToKey = false;

                SetupBezelAndShaders(system, rom, resolution, path);

                return new ProcessStartInfo()
                {
                    FileName = exe,
                    Arguments = "\"" + rom + "\"",
                    WorkingDirectory = path
                };
            }

            // Old versions ?

            if (build == "4432")
                setupConfigBor4432Cfg(path);
            else 
                setupConfigBorCfg(path);

            string pakDir = Path.Combine(path, "Paks");
            if (!Directory.Exists(pakDir))
                Directory.CreateDirectory(pakDir); 

            foreach (var file in Directory.GetFiles(pakDir))
            {
                if (Path.GetFileName(file) == Path.GetFileName(rom))
                    continue;

                File.Delete(file);
            }

            _destFile = Path.Combine(pakDir, Path.GetFileName(rom));
            if (!File.Exists(_destFile))
                File.Copy(rom, _destFile);

            SetupBezelAndShaders(system, rom, resolution, path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path
            };
        }

        private void SetupBezelAndShaders(string system, string rom, ScreenResolution resolution, string path)
        {
            var bezels = BezelFiles.GetBezelFiles(system, rom, resolution);
            if (bezels != null && ((SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "1") || BorPak.GetVideoMode(rom).IsWideScreen))
                SystemConfig["forceNoBezel"] = "1";

            ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x86, system, rom, path, resolution, false);
        }

        public override void Cleanup()
        {
            if (_destFile != null && File.Exists(_destFile))
                File.Delete(_destFile);

            base.Cleanup();
        }

        #region Custom Ini file format
        private bool setupConfigIni(string path)
        {
            string ini = Path.Combine(path, "config.ini");
            if (!File.Exists(ini) && !_isCustomRetrobatOpenBor)
                return false;

            var conf = ConfigFile.FromFile(ini);
            conf["fullscreen"] = "1";
            conf["vsync"] = SystemConfig["VSync"] != "false" ? "1" : "0";
            conf["usegl"] = "1";
            conf["stretch"] = SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "1" ? "1" : "0";

            if (Features.IsSupported("filter") && SystemConfig.isOptSet("filter"))
                conf["swfilter"] = SystemConfig["filter"];
            else
                conf["swfilter"] = "0";

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig["screenshots"]))
            {
                string dir = AppConfig.GetFullPath("screenshots");

                Uri relRoot = new Uri(path, UriKind.Absolute);
                string relPath = relRoot.MakeRelativeUri(new Uri(dir, UriKind.Absolute)).ToString().Replace("/", "\\");

                conf["screenShotsDir"] = Path.GetFullPath(dir)+"\\";
            }

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig["saves"]))
            {
                string dir = Path.Combine(AppConfig.GetFullPath("saves"), "openbor");

                Uri relRoot = new Uri(path, UriKind.Absolute);
                string relPath = relRoot.MakeRelativeUri(new Uri(dir, UriKind.Absolute)).ToString().Replace("/", "\\");

                Directory.CreateDirectory(dir);
                conf["savesDir"] = Path.GetFullPath(dir) + "\\";
            }

            SetupControllers(conf);

            conf.Save(ini, false);
            return true;
        }
        #endregion

        #region Old Openbor versions with bor.cfg file format
        private string GetBuildToUse(string rom)
        {
            /*
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
            */

            return null;
        }


        private void setupControllersCfg(savedata conf)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!Controllers.Any())
                return;

            bool hasKeyb = false;

            for (int idx = 0; idx < 4; idx++)
            {
                var c = Controllers.FirstOrDefault(j => j.PlayerIndex == idx + 1);
                if (c == null || c.Config == null)
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

                if (c.Config.Type == "keyboard")
                {
                    hasKeyb = true;
                    conf.keys[idx].up = KeyboardValue(InputKey.up, c);
                    conf.keys[idx].down = KeyboardValue(InputKey.down, c);
                    conf.keys[idx].left = KeyboardValue(InputKey.left, c);
                    conf.keys[idx].right = KeyboardValue(InputKey.right, c);
                    conf.keys[idx].attack1 = KeyboardValue(InputKey.a, c); // ATTACK
                    conf.keys[idx].attack2 = KeyboardValue(InputKey.x, c);
                    conf.keys[idx].attack3 = KeyboardValue(InputKey.y, c);
                    conf.keys[idx].attack4 = KeyboardValue(InputKey.pagedown, c); // ATTACK4
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
                conf.keys[idx].attack4 = JoystickValue(InputKey.pagedown, c); // ATTACK4
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
            conf.stretch = SystemConfig.isOptSet("ratio") && SystemConfig["ratio"] == "1" ? 1 : 0;

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

    #region v6330
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
    #endregion

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

    #region Pak file Reader
    class BorPak
    {
        struct pn_t
        {
            public uint pns;
            public uint off;
            public uint size;
            public string name;
        };

        public class BorVideoMode
        {
            public int VideoMode { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public float Ratio
            {
                get
                {
                    if (Height == 0)
                        return 4 / 3;

                    return (float)Width / (float)Height;
                }
            }

            public bool IsWideScreen
            {
                get
                {
                    return (Ratio > 1.4);
                }
            }
        }

        public static BorVideoMode GetVideoMode(string fileName)
        {
            var bytes = BorPak.ReadAllLines(fileName, "DATA\\VIDEO.TXT");
            if (bytes != null)
            {
                int videoMode = 255;
                int hRes = 0;
                int vRes = 0;

                var video = bytes.FirstOrDefault(s => s.StartsWith("video"));
                var args = video.Split(new char[] { '\t' });
                if (args.Length > 1)
                {
                    int pos = args[1].IndexOf("x");
                    if (pos >= 0)
                    {
                        hRes = args[1].Substring(0, pos).ToInteger();
                        vRes = args[1].Substring(pos + 1).ToInteger();
                        videoMode = 255;
                    }
                    else
                    {
                        videoMode = args[1].ToInteger();

                        switch (videoMode)
                        {
                            // 320x240 - All Platforms
                            case 0:
                                hRes = 320;
                                vRes = 240;
                                break;

                            // 480x272 - All Platforms
                            case 1:
                                hRes = 480;
                                vRes = 272;
                                break;

                            // 640x480 - PC, Dreamcast, Wii
                            case 2:
                                hRes = 640;
                                vRes = 480;
                                break;

                            // 720x480 - PC, Wii
                            case 3:
                                hRes = 720;
                                vRes = 480;
                                break;

                            // 800x480 - PC, Wii, Pandora
                            case 4:
                                hRes = 800;
                                vRes = 480;
                                break;

                            // 800x600 - PC, Dreamcast, Wii
                            case 5:
                                hRes = 800;
                                vRes = 600;
                                break;

                            // 960x540 - PC, Wii
                            case 6:
                                hRes = 960;
                                vRes = 540;
                                break;
                        }
                    }

                    return new BorVideoMode()
                    {
                        VideoMode = videoMode,
                        Width = hRes,
                        Height = vRes
                    };
                }
            }

            return new BorVideoMode()
            {
                VideoMode = 1,
                Width = 320,
                Height = 240
            };
        }

        public static string[] ReadDirectory(string filename)
        {
            var files = new List<string>();
            Read(filename, (fd, pn) => files.Add(pn.name));
            return files.ToArray();
        }

        public static byte[] ReadFile(string filename, string fileNameAndPath)
        {
            byte[] ret = null;
            Read(filename, (fd, pn) =>
                {
                    if (fileNameAndPath.Equals(pn.name, StringComparison.InvariantCultureIgnoreCase))
                        ret = GetFile(fd, pn.name, pn.off, pn.size);
                });

            return ret;
        }

        public static string[] ReadAllLines(string filename, string fileNameAndPath)
        {
            byte[] ret = null;
            Read(filename, (fd, pn) =>
                {
                    if (fileNameAndPath.Equals(pn.name, StringComparison.InvariantCultureIgnoreCase))
                        ret = GetFile(fd, pn.name, pn.off, pn.size);
                });

            if (ret != null)
                return Encoding.UTF8.GetString(ret).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            return null;
        }

        private static void Read(string filename, Action<BinaryReader, pn_t> action)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open))
            using (BinaryReader fd = new BinaryReader(fs))
            {
                var pack = fd.ReadChars(4);
                if (new string(pack) != "PACK")
                    return;

                var packver = fdrinum(fd, 32);

                fs.Seek(-4, SeekOrigin.End);

                var off = fdrinum(fd, 32);

                fs.Seek(off, SeekOrigin.Begin);

                pn_t pn = new pn_t();

                for (; ; )
                {
                    pn.pns = fdrinum(fd, 32);
                    pn.off = fdrinum(fd, 32);
                    pn.size = fdrinum(fd, 32);

                    int len = (int) pn.pns - 12;
                    if (len <= 0)
                        break;

                    pn.name = new string(fd.ReadChars(len - 1)); // remove \0

                    if (action != null)
                        action(fd, pn);
                    // Debug.WriteLine(name);

                    if (pn.name.ToLower().Contains("video.txt"))
                        GetFile(fd, pn.name, pn.off, pn.size);

                    off += pn.pns;

                    if (off > fs.Length)
                        break;

                    fs.Seek(off, SeekOrigin.Begin);                    
                }
            }
        }

        private static byte[] GetFile(BinaryReader fd, string name, uint off, uint size)
        {
            var fs = fd.BaseStream;

           fs.Seek(off, SeekOrigin.Begin);                        
           return fd.ReadBytes((int) size);
        }

        static uint fdrinum(BinaryReader fd, int size)
        {
            uint num = 0;

            size >>= 3;
            byte[] tmp = fd.ReadBytes(size);
            for (int i = 0; i < tmp.Length; i++)
                num |= (uint) (((int)tmp[i]) << (i << 3));

            return num;
        }
    }
    #endregion
}
