using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Windows.Forms;
using System.Threading;
using EmulatorLauncher.VPinballLauncher;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;
using EmulatorLauncher.PadToKeyboard;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class FpinballGenerator : Generator
    {
        private ScreenShotsWatcher _sswatch;
        private LoadingForm _splash;
        
        public static int JoystickValue(InputKey key, Controller c)
        {
            var a = c.GetDirectInputMapping(key);
            if (a == null)
                return -1;

            if (a.Type == "button")
                return (int) a.Id;

            return -1;
        }

        private void SetupControllers()
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            if (Controllers == null)
                return;

            var controller = Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && c.Config != null && c.Config.Type != "keyboard");
            if (controller == null)
            {
                SimpleLogger.Instance.Info("[INFO] No pad found for player 1, Future Pinball pad configuration skipped.");
                return;
            }

            var directInput = controller.DirectInput;
            if (directInput == null || string.IsNullOrEmpty(directInput.Name))
            {
                SimpleLogger.Instance.Warning("[WARNING] No DirectInput information for player 1 pad, Future Pinball pad configuration skipped.");
                return;
            }

            // Future Pinball stores the joypad name truncated to 47 characters
            string fpinballName = directInput.Name.Length > 47 ? directInput.Name.Substring(0, 47) : directInput.Name;

            using (var software = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                if (software == null)
                {
                    SimpleLogger.Instance.Warning("[WARNING] Unable to open HKCU\\Software, Future Pinball pad configuration skipped.");
                    return;
                }

                using (var joyPads = software.CreateSubKey("Future Pinball\\GamePlayer\\JoyPads"))
                {
                    if (joyPads == null)
                    {
                        SimpleLogger.Instance.Warning("[WARNING] Unable to create HKCU\\Software\\Future Pinball\\GamePlayer\\JoyPads.");
                        return;
                    }

                    // Previously configured pads are stored as SUBKEYS (one per pad name), not as values
                    foreach (var subKeyName in joyPads.GetSubKeyNames())
                    {
                        try { joyPads.DeleteSubKeyTree(subKeyName); }
                        catch (Exception ex) { SimpleLogger.Instance.Warning("[WARNING] Unable to delete stale pad key '" + subKeyName + "' : " + ex.Message); }
                    }

                    // Legacy cleanup : remove any leftover value written directly under JoyPads
                    foreach (var valueName in joyPads.GetValueNames())
                    {
                        try { joyPads.DeleteValue(valueName); }
                        catch { }
                    }

                    using (var pad = joyPads.CreateSubKey(fpinballName))
                    {
                        if (pad == null)
                        {
                            SimpleLogger.Instance.Warning("[WARNING] Unable to create registry key for pad '" + fpinballName + "'.");
                            return;
                        }

                        pad.SetValue("JoypadSupport", 1);

                        pad.SetValue("JoypadDigitalPlunger", JoystickValue(InputKey.a, controller));
                        pad.SetValue("JoypadToggleHud", JoystickValue(InputKey.y, controller));
                        pad.SetValue("JoypadNextCamera", JoystickValue(InputKey.b, controller));
                        pad.SetValue("JoypadExit", JoystickValue(InputKey.r3, controller));

                        pad.SetValue("JoypadLeftFlipper", JoystickValue(InputKey.pageup, controller));
                        pad.SetValue("JoypadRightFlipper", JoystickValue(InputKey.pagedown, controller));

                        pad.SetValue("JoypadStartGame", JoystickValue(InputKey.start, controller));
                        pad.SetValue("JoypadInsertCoin", JoystickValue(InputKey.select, controller));

                        pad.SetValue("JoypadPause", JoystickValue(InputKey.x, controller));
                        pad.SetValue("JoypadBackbox", JoystickValue(InputKey.l3, controller));

                        pad.SetValue("JoypadSpecial1", -1);
                        pad.SetValue("JoypadSpecial2", -1);
                        pad.SetValue("JoypadInsertCoin2", -1);
                        pad.SetValue("JoypadInsertCoin3", -1);
                        pad.SetValue("JoypadLeft2ndFlipper", -1);
                        pad.SetValue("JoypadRight2ndFlipper", -1);
                        pad.SetValue("JoypadTest", -1);
                        pad.SetValue("JoypadVolumeUp", -1);
                        pad.SetValue("JoypadVolumeDown", -1);
                        pad.SetValue("JoypadMusicUp", -1);
                        pad.SetValue("JoypadMusicDown", -1);
                        pad.SetValue("JoypadService", -1);
                        pad.SetValue("JoypadPinballRoller", -1);
                        pad.SetValue("JoypadPlungerAxis", -1);
                        pad.SetValue("JoypadNudgeAxisX", -1);
                        pad.SetValue("JoypadNudgeAxisY", -1);
                        pad.SetValue("JoypadPinballRollerAxisX", -1);
                        pad.SetValue("JoypadPinballRollerAxisY", -1);
                    }
                }
            }

            SimpleLogger.Instance.Info("[INFO] Future Pinball pad configured : " + fpinballName);
        }

        string _bam;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("fpinball");
            string exe = Path.Combine(path, "Future Pinball.exe");
            if (!File.Exists(exe))
            {
                exe = Path.Combine(path, "FuturePinball.exe");
                if (!File.Exists(exe))
                    return null;
            }

            rom = this.TryUnZipGameIfNeeded(system, rom, true, false);

            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.fpt", SearchOption.AllDirectories).FirstOrDefault();
                if (string.IsNullOrEmpty(rom))
                    throw new ApplicationException("Unable to find any table in the provided folder");
            }
            
            _splash = ShowSplash(rom);

            if ("bam".Equals(emulator, StringComparison.InvariantCultureIgnoreCase) || "bam".Equals(core, StringComparison.InvariantCultureIgnoreCase))
                _bam = Path.Combine(path, "BAM", "FPLoader.exe");

            if (_bam != null && File.Exists(_bam))
                ScreenResolution.SetHighDpiAware(_bam);

            ScreenResolution.SetHighDpiAware(exe);

            SetupBamConfig();
            SetupOptions(resolution);
            SetupControllers();

            // Run dmdext if needed
            if (SystemConfig.getOptBoolean("fpinball_dmdext"))
            {
                // check existence of dmdext & dmddevice.ini
                string dmdext = Path.Combine(path, "dmdext.exe");
                string dmddevice = Path.Combine(path, "DmdDevice.ini");
                if (File.Exists(dmdext) && File.Exists(dmddevice))
                    RunDMDExt(dmdext, dmddevice);
            }

            var ret = new ProcessStartInfo()
            {
                FileName = _bam != null && File.Exists(_bam) ? _bam : exe,
                Arguments = "/open \"" + rom + "\" /play /exit",            
            };

            // Check If COM components are well registered. If not : run elevated to register them.
            bool runAs = false;
            using (var key = Registry.ClassesRoot.OpenSubKey("TypeLib\\{FB22A459-4AD0-4CB3-B959-15158F7139F5}\\1.0\\0\\win32", false))
            {
                if (key == null)
                {
                    SimpleLogger.Instance.Info("[INFO] Future Pinball COM components are not registered, running elevated.");
                    runAs = true;
                }
                else
                {
                    string rp = key.GetValue(null) as string;

                    if (string.IsNullOrEmpty(rp))
                    {
                        SimpleLogger.Instance.Info("[INFO] Future Pinball TypeLib path is empty, running elevated.");
                        runAs = true;
                    }
                    else if (!rp.Equals(exe, StringComparison.InvariantCultureIgnoreCase))
                    {
                        SimpleLogger.Instance.Info("[INFO] Future Pinball TypeLib is registered to '" + rp + "' instead of '" + exe + "', running elevated.");
                        runAs = true;
                    }
                }
            }

            if (runAs)
                ret.Verb = "runas";

            return ret;
        }

        private void SetupBamConfig()
        {
            if (_bam == null || !File.Exists(_bam))
                return;

            string screenShotPath = AppConfig.GetFullPath("screenshots");
            if (string.IsNullOrEmpty(screenShotPath))
                return;

            string folder = Path.GetDirectoryName(_bam);
            string path = Path.Combine(Path.GetDirectoryName(_bam), "bam.cfg");
            if (!File.Exists(path))
                return;

            string relativeSSPath = "..\\" + FileTools.GetRelativePath(folder, screenShotPath);

            var lines = File.ReadAllLines(path).ToList();
            lines.RemoveWhere(l => l != null && l.StartsWith("SnapShotPath"));
            lines.RemoveWhere(l => l != null && l.StartsWith("SnapShotBackboxPath"));

            lines.Add("SnapShotPath = " + relativeSSPath);
            lines.Add("SnapShotBackboxPath = " + relativeSSPath);

            File.WriteAllLines(path, lines.ToArray());

            _sswatch = new ScreenShotsWatcher(screenShotPath, SystemConfig["system"], SystemConfig["rom"]);
        }

        private void RunDMDExt(string dmdext, string dmddevice)
        {
            if (!File.Exists(dmdext))
            {
                SimpleLogger.Instance.Error("[ERROR] dmdext.exe not found.");
                return;
            }

            if (!File.Exists(dmddevice))
            {
                SimpleLogger.Instance.Error("[ERROR] DmdDevice.ini not found.");
                return;
            }

            ConfigureDMDDevice(dmddevice);

            ProcessStartInfo p = new ProcessStartInfo
            {
                FileName = dmdext,
                WorkingDirectory = Path.GetDirectoryName(dmdext),
                Arguments = "mirror --source=futurepinball -q --virtual-stay-on-top --fps 60 --use-ini=\"DmdDevice.ini\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(p);
        }

        private void ConfigureDMDDevice(string dmddevice)
        {
            if (!File.Exists(dmddevice))
                return;

            using (var ini = new IniFile(dmddevice, IniOptions.KeepEmptyLines | IniOptions.UseSpaces | IniOptions.KeepEmptyValues))
            {
                BindBoolIniFeatureOn(ini, "virtualdmd", "enabled", "fpinball_virtualdmd", "true", "false");
                BindBoolIniFeature(ini, "zedmd", "enabled", "fpinball_zedmd", "true", "false");
                BindBoolIniFeature(ini, "pixelcade", "enabled", "fpinball_pixelcade", "true", "false");
                BindBoolIniFeature(ini, "pin2dmd", "enabled", "fpinball_pin2dmd", "true", "false");

                ini.Save();
            }

        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            
            if (_bam != null)
            {
                var fploaderMapping = mapping.Applications.Where(m => m.Name == "fploader").FirstOrDefault();
                var fpinballMapping = mapping.Applications.Where(m => m.Name == "Future Pinball").FirstOrDefault();
                if (fploaderMapping != null && fpinballMapping != null)
                    fpinballMapping.Input.AddRange(fploaderMapping.Input);
            }
            return PadToKey.AddOrUpdateKeyMapping(mapping, "future pinball", InputKey.hotkey | InputKey.r3, "(%{PRTSC})");
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            Process process;

            if (_bam != null && File.Exists(_bam))
            {
                Process.Start(path);

                int tickCount = Environment.TickCount;
                const string fpProcessName = "Future Pinball";

                process = Process.GetProcessesByName(fpProcessName).FirstOrDefault();
                while (process == null && (Environment.TickCount - tickCount < 10000))
                {
                    Thread.Sleep(50);
                    process = Process.GetProcessesByName(fpProcessName).FirstOrDefault();
                }
                
                if (process != null)
                    Job.Current.AddProcess(process);
                else
                    SimpleLogger.Instance.Warning("[WARNING] Future Pinball process was not found after launching BAM.");
            }
            else
            {
                process = Process.Start(path);
                Job.Current.AddProcess(process);
            }

            if (process != null)
            {
                process.WaitForExit();

                try { return process.ExitCode; }
                catch { }

                return 0;
            }

            return -1;
        }

        public override void Cleanup()
        {
            if (_splash != null)
            {
                _splash.Dispose();
                _splash = null;
            }

            if (_sswatch != null)
            {
                _sswatch.Dispose();
                _sswatch = null;
            }
            
            base.Cleanup();
        }

        private void SetupOptions(ScreenResolution resolution)
        {
            bool fullscreen = ShouldRunFullscreen();

            RegistryKey regKeyc = null;

            using (var software = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                if (software != null)
                    regKeyc = software.CreateSubKey("Future Pinball\\GamePlayer");
            }

            if (regKeyc == null)
                SimpleLogger.Instance.Warning("[WARNING] Unable to create HKCU\\Software\\Future Pinball\\GamePlayer, video options will not be applied.");

            if (regKeyc != null)
            {
                if (fullscreen)
                    regKeyc.SetValue("FullScreen", 1);
                else
                    regKeyc.SetValue("FullScreen", 0);

                if (SystemConfig.isOptSet("arcademode") && SystemConfig.getOptBoolean("arcademode"))
                {
                    if (Screen.PrimaryScreen.Bounds.Height > Screen.PrimaryScreen.Bounds.Width)
                        regKeyc.SetValue("RotateDegrees", 0); // Already rotated by system
                    else
                        regKeyc.SetValue("RotateDegrees", 3);

                    regKeyc.SetValue("ArcadeMode", 1);
                }
                else
                {
                    regKeyc.SetValue("RotateDegrees", 0);
                    regKeyc.SetValue("ArcadeMode", 0);
                }


                if (SystemConfig.isOptSet("ratio"))
                {
                    if (SystemConfig["ratio"] == "4/3")
                        regKeyc.SetValue("AspectRatio", 43);
                    else if (SystemConfig["ratio"] == "16/9")
                        regKeyc.SetValue("AspectRatio", 169);
                }
                else
                    regKeyc.SetValue("AspectRatio", 169);

                if (SystemConfig.isOptSet("fp_vsync") && !SystemConfig.getOptBoolean("fp_vsync"))
                    regKeyc.SetValue("VerticalSync", 0);
                else
                    regKeyc.SetValue("VerticalSync", 1);

                if (resolution != null)
                {
                    regKeyc.SetValue("Width", resolution.Width);
                    regKeyc.SetValue("Height", resolution.Height);
                    regKeyc.SetValue("BitsPerPixel", resolution.BitsPerPel);
                }
                else
                {
                    regKeyc.SetValue("Height", Screen.PrimaryScreen.Bounds.Height);
                    regKeyc.SetValue("Width", Screen.PrimaryScreen.Bounds.Width);
                    regKeyc.SetValue("BitsPerPixel", Screen.PrimaryScreen.BitsPerPixel);
                }

                // Monitor ID
                string monitorId = Screen.PrimaryScreen != null ? Screen.PrimaryScreen.DeviceName : "\\\\.\\DISPLAY1";
                if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                {
                    int monitorIndex = SystemConfig["MonitorIndex"].ToInteger();
                    var screens = Screen.AllScreens;

                    if (monitorIndex >= 0 && monitorIndex < screens.Length)
                        monitorId = screens[monitorIndex].DeviceName;
                    else
                        SimpleLogger.Instance.Warning("[WARNING] MonitorIndex " + monitorIndex + " is out of range (" + screens.Length + " screen(s) detected), falling back to primary screen.");
                }
                SimpleLogger.Instance.Info("[INFO] Future Pinball playfield monitor : " + monitorId);
                regKeyc.SetValue("PlayfieldMonitorID", monitorId);

                if (SystemConfig.isOptSet("DefaultCamera") && !string.IsNullOrEmpty(SystemConfig["DefaultCamera"]))
                    regKeyc.SetValue("DefaultCamera", SystemConfig["DefaultCamera"].ToInteger());
                else
                    regKeyc.SetValue("DefaultCamera", 0);

                if (SystemConfig.isOptSet("camerafollowball") && SystemConfig.getOptBoolean("camerafollowball"))
                    regKeyc.SetValue("CameraFollowsTheBall", 1);
                else
                    regKeyc.SetValue("CameraFollowsTheBall", 0);

                if (SystemConfig.isOptSet("preset") && SystemConfig["preset"] == "medium")
                {
                    regKeyc.SetValue("RenderGameRoom", 1);
                    regKeyc.SetValue("RenderOrnaments", 1);
                    regKeyc.SetValue("GlassOverlay", 1);
                    regKeyc.SetValue("LightFacets", 0x20);
                    regKeyc.SetValue("GlassReflections", 0);
                    regKeyc.SetValue("PlayfieldReflections", 1);
                    regKeyc.SetValue("RenderBallMirrors", 0);
                    regKeyc.SetValue("SuperNiceCrystal", 0);
                    regKeyc.SetValue("HighQualityPinballs", 1);
                    regKeyc.SetValue("BallDirt", 1);
                    regKeyc.SetValue("DisableShaders", 0);
                    regKeyc.SetValue("ModelRenderQuality", 1);
                    regKeyc.SetValue("RubberFacets", 0x10);
                    regKeyc.SetValue("RubberSides", 0x0c);
                    regKeyc.SetValue("WireGuideSides", 0x0c);
                    regKeyc.SetValue("HighQualityTextures", 1);                 
                }
                else if (SystemConfig.isOptSet("preset") && SystemConfig["preset"] == "min")
                {
                    regKeyc.SetValue("RenderGameRoom", 0);
                    regKeyc.SetValue("RenderOrnaments", 0);
                    regKeyc.SetValue("GlassOverlay", 0);
                    regKeyc.SetValue("LightFacets", 0x10);
                    regKeyc.SetValue("GlassReflections", 0);
                    regKeyc.SetValue("PlayfieldReflections", 0);
                    regKeyc.SetValue("RenderBallMirrors", 0);
                    regKeyc.SetValue("SuperNiceCrystal", 0);
                    regKeyc.SetValue("HighQualityPinballs", 0);
                    regKeyc.SetValue("BallDirt", 0);
                    regKeyc.SetValue("DisableShaders", 1);
                    regKeyc.SetValue("ModelRenderQuality", 0);
                    regKeyc.SetValue("RubberFacets", 0x08);
                    regKeyc.SetValue("RubberSides", 0x06);
                    regKeyc.SetValue("WireGuideSides", 0x06);
                    regKeyc.SetValue("HighQualityTextures", 0);
                }
                else
                {
                    regKeyc.SetValue("RenderGameRoom", 1);
                    regKeyc.SetValue("RenderOrnaments", 1);
                    regKeyc.SetValue("GlassOverlay", 1);
                    regKeyc.SetValue("LightFacets", 0x40);
                    regKeyc.SetValue("GlassReflections", 1);
                    regKeyc.SetValue("PlayfieldReflections", 1);
                    regKeyc.SetValue("RenderBallMirrors", 1);
                    regKeyc.SetValue("SuperNiceCrystal", 1);
                    regKeyc.SetValue("HighQualityPinballs", 1);
                    regKeyc.SetValue("BallDirt", 1);
                    regKeyc.SetValue("DisableShaders", 0);
                    regKeyc.SetValue("ModelRenderQuality", 2);
                    regKeyc.SetValue("RubberFacets", 0x40);
                    regKeyc.SetValue("RubberSides", 0x14);
                    regKeyc.SetValue("WireGuideSides", 0x14);
                    regKeyc.SetValue("HighQualityTextures", 1);
                }

                if (SystemConfig.isOptSet("fp_hide_gameroom") && SystemConfig.getOptBoolean("fp_hide_gameroom"))
                    regKeyc.SetValue("RenderGameRoom", 0);

                if (SystemConfig.isOptSet("fp_hide_ornaments") && SystemConfig.getOptBoolean("fp_hide_ornaments"))
                    regKeyc.SetValue("RenderOrnaments", 0);

                if (SystemConfig.isOptSet("fp_texture_filter"))
                    regKeyc.SetValue("TextureFilter", SystemConfig["fp_texture_filter"].ToInteger());
                else
                    regKeyc.SetValue("TextureFilter", 0);

                if (SystemConfig.isOptSet("fp_anisotropic") && !SystemConfig.getOptBoolean("fp_anisotropic"))
                    regKeyc.SetValue("AnisotropicFiltering", 0);
                else
                    regKeyc.SetValue("AnisotropicFiltering", 1);

                if (SystemConfig.isOptSet("fp_antialiasing"))
                    regKeyc.SetValue("AntiAliasing", SystemConfig["fp_antialiasing"].ToInteger());
                else
                    regKeyc.SetValue("AntiAliasing", 2);

                regKeyc.Close();
            }

            using (var software = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                if (software == null)
                    return;

                using (var editor = software.CreateSubKey("Future Pinball\\Editor"))
                {
                    if (editor != null)
                        editor.SetValue("LoadImagesIntoEditor", 0);
                }
            }
        }

        private LoadingForm ShowSplash(string rom)
        {
            if (rom == null)
                return null;

            if (!Misc.IsWindowsEightOrTen || Misc.IsDeveloperModeEnabled)
                return null;

            if (Controllers == null)
                return null;

            var controller = Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && c.Config != null && c.Config.Type != "keyboard");
            if (controller == null)
                return null;

            LoadingForm frm = new LoadingForm
            {
                WarningText = Properties.Resources.FPinballDeveloperMode
            };
            frm.Show();

            return frm;
        }

    }
}
