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
                return;

            if (Controllers != null)
            {
                var controller = Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && c.Config != null && c.Config.Type != "keyboard");
                if (controller != null)
                {
                    var directInput = controller.DirectInput;
                    if (directInput != null)
                    {
                        string fpinballName = directInput.Name.Length > 47 ? directInput.Name.Substring(0, 47) : directInput.Name;

                        RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
                        if (regKeyc != null)
                            regKeyc = regKeyc.CreateSubKey("Future Pinball").CreateSubKey("GamePlayer").CreateSubKey("JoyPads");

                        if (regKeyc != null)
                        {
                            foreach (var name in regKeyc.GetValueNames())
                                regKeyc.DeleteValue(name);

                            regKeyc = regKeyc.CreateSubKey(fpinballName);
                            if (regKeyc != null)
                            {
                                regKeyc.SetValue("JoypadSupport", 1);

                                regKeyc.SetValue("JoypadDigitalPlunger", JoystickValue(InputKey.a, controller));
                                regKeyc.SetValue("JoypadToggleHud", JoystickValue(InputKey.y, controller));
                                regKeyc.SetValue("JoypadNextCamera", JoystickValue(InputKey.b, controller));
                                regKeyc.SetValue("JoypadExit", JoystickValue(InputKey.r3, controller));

                                regKeyc.SetValue("JoypadLeftFlipper", JoystickValue(InputKey.pageup, controller));
                                regKeyc.SetValue("JoypadRightFlipper", JoystickValue(InputKey.pagedown, controller));

                                regKeyc.SetValue("JoypadStartGame", JoystickValue(InputKey.start, controller));
                                regKeyc.SetValue("JoypadInsertCoin", JoystickValue(InputKey.select, controller));

                                regKeyc.SetValue("JoypadPause", JoystickValue(InputKey.x, controller));
                                regKeyc.SetValue("JoypadBackbox", JoystickValue(InputKey.l3, controller));

                                regKeyc.SetValue("JoypadSpecial1", -1);
                                regKeyc.SetValue("JoypadSpecial2", -1);
                                regKeyc.SetValue("JoypadInsertCoin2", -1);
                                regKeyc.SetValue("JoypadInsertCoin3", -1);
                                regKeyc.SetValue("JoypadLeft2ndFlipper", -1);
                                regKeyc.SetValue("JoypadRight2ndFlipper", -1);
                                regKeyc.SetValue("JoypadTest", -1);
                                regKeyc.SetValue("JoypadVolumeUp", -1);
                                regKeyc.SetValue("JoypadVolumeDown", -1);
                                regKeyc.SetValue("JoypadMusicUp", -1);
                                regKeyc.SetValue("JoypadMusicDown", -1);
                                regKeyc.SetValue("JoypadService", -1);
                                regKeyc.SetValue("JoypadPinballRoller", -1);
                                regKeyc.SetValue("JoypadPlungerAxis", -1);
                                regKeyc.SetValue("JoypadNudgeAxisX", -1);
                                regKeyc.SetValue("JoypadNudgeAxisY", -1);
                                regKeyc.SetValue("JoypadPinballRollerAxisX", -1);
                                regKeyc.SetValue("JoypadPinballRollerAxisY", -1);


                                regKeyc.Close();
                            }
                        }
                    }
                }
            }
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

            var ret = new ProcessStartInfo()
            {
                FileName = _bam != null && File.Exists(_bam) ? _bam : exe,
                Arguments = "/open \"" + rom + "\" /play /exit",            
            };

            // Check If COM components are well registered. If not : run elevated to register them.
            bool runAs = false;
            var key = Registry.ClassesRoot.OpenSubKey("TypeLib\\{FB22A459-4AD0-4CB3-B959-15158F7139F5}\\1.0\\0\\win32", false);
            if (key != null)
            {
                object registeredPath = key.GetValue(null);
                if (registeredPath == null)
                    runAs = true;

                var rp = registeredPath.ToString();
                if (rp != exe)
                    runAs = true;

                key.Close();
            }
            else
                runAs = true;

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
                string fileNameWithoutExtension = "Future Pinball";

                process = Process.GetProcessesByName(fileNameWithoutExtension).FirstOrDefault<Process>();
                while (process == null && (Environment.TickCount - tickCount < 1000))
                {
                    process = Process.GetProcessesByName(fileNameWithoutExtension).FirstOrDefault<Process>();
                    if (process == null)
                        Thread.Sleep(10);
                }
            }
            else
                process = Process.Start(path);

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
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
                regKeyc = regKeyc.CreateSubKey("Future Pinball").CreateSubKey("GamePlayer");

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

                if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                    regKeyc.SetValue("PlayfieldMonitorID", "\\\\.\\DISPLAY" + SystemConfig["MonitorIndex"]);
                else
                    regKeyc.SetValue("PlayfieldMonitorID", "\\\\.\\DISPLAY1");

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

            regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
                regKeyc = regKeyc.CreateSubKey("Future Pinball").CreateSubKey("Editor");

            if (regKeyc != null)
            {
                regKeyc.SetValue("LoadImagesIntoEditor", 0);
                regKeyc.Close();
            }
        }

        private LoadingForm ShowSplash(string rom)
        {
            if (rom == null)
                return null;

            if (Misc.IsWindowsEightOrTen && !Misc.IsDeveloperModeEnabled)
            {
                var controller = Controllers.FirstOrDefault(c => c.PlayerIndex == 1 && c.Config != null && c.Config.Type != "keyboard");
                if (controller != null)
                {
                    LoadingForm frm = new LoadingForm
                    {
                        WarningText = Properties.Resources.FPinballDeveloperMode
                    };
                    frm.Show();
                }
            }

            return null;
        }

    }
}
