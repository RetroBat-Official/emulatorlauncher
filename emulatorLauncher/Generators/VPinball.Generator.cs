using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.VPinballLauncher;
using System.Xml;
using System.Drawing;
using Microsoft.Win32;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.PadToKeyboard;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using System.Drawing.Imaging;

namespace EmulatorLauncher
{
    class VPinballGenerator : Generator
    {
        public VPinballGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        private LoadingForm _splash;       
        private Version _version;
        private string _processName;
        private string _exe;        

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("vpinball");
            if (path == null)
                return null;

            string exe = Path.Combine(path, Environment.Is64BitOperatingSystem ? "VPinballX64.exe" : "VPinballX.exe");
            if (!File.Exists(exe) && Environment.Is64BitOperatingSystem)
                exe = Path.Combine(path, "VPinballX.exe");
            if (!File.Exists(exe))
                return null;

            _exe = exe;
            _processName = Path.GetFileNameWithoutExtension(exe);
            _version = new Version(10, 0, 0, 0);

            // Get version from executable
            var versionInfo = FileVersionInfo.GetVersionInfo(exe);
            string versionString = versionInfo.FileMajorPart + "." + versionInfo.FileMinorPart + "." + versionInfo.FileBuildPart + "." + versionInfo.FilePrivatePart;
            Version.TryParse(versionString, out _version);

            rom = this.TryUnZipGameIfNeeded(system, rom, true, false);
            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.vpx").FirstOrDefault();
                if (string.IsNullOrEmpty(rom))
                    return null;
            }

            _splash = ShowSplash(rom);

            ScreenResolution.SetHighDpiAware(exe);
            EnsureUltraDMDRegistered(path);
            EnsureBackglassServerRegistered(path);
            EnsureVPinMameRegistered(path);

            string romPath = Path.Combine(Path.GetDirectoryName(rom), "roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(Path.GetDirectoryName(rom), ".roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(AppConfig.GetFullPath("roms"), "vpinball", "roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(AppConfig.GetFullPath("roms"), "vpinball", ".roms");
            if (!Directory.Exists(romPath))
                romPath = null;

            ScreenRes sr = ScreenRes.Load(Path.GetDirectoryName(rom));
            if (sr != null)
            {
                if (Screen.AllScreens.Length == 1 || SystemInformation.TerminalServerSession)
                    sr.Delete();
                else
                {
                    sr.ScreenResX = resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width;
                    sr.ScreenResY = resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height;

                    Screen secondary = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
                    sr.Screen2ResX = secondary.Bounds.Width;
                    sr.Screen2ResY = secondary.Bounds.Height;
                    sr.Monitor = 2;

                    sr.Save();
                }
            }

            SetupOptions(path, romPath, resolution);

            var commands = new List<string>();

            if (_version >= new Version(10, 7, 0, 0))
                commands.Add("-ExtMinimized");

            if (_version >= new Version(10, 8, 0, 0))
            {
                commands.Add("-Ini");
                commands.Add(Path.Combine(path, "VPinballX.ini"));

            }
            commands.Add("-play");
            commands.Add(rom);

            string args = string.Join(" ", commands.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WindowStyle = _splash != null ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
                UseShellExecute = true
            };
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _processName, InputKey.hotkey | InputKey.r3, () => SaveScreenshot());
        }
         
        public override int RunAndWait(ProcessStartInfo path)
        {
            try
            {
                var px = Process.Start(path);

                using (var kb = new KeyboardManager(() => KillProcess(px)))
                {
                    kb.RegisterKeyboardAction(() => SaveScreenshot(), (vkCode, scanCode) => vkCode == 44 && scanCode == 55);

                    while (!px.HasExited)
                    {
                        if (px.WaitForExit(10))
                            break;

                        Application.DoEvents();
                    }
                }
                
                try
                {
                    Process[] backGlasses = Process.GetProcessesByName("B2SBackglassServerEXE");
                    foreach (Process backGlass in backGlasses)
                        backGlass.Kill();

                    Process[] ultraDMDs = Process.GetProcessesByName("UltraDMD");
                    foreach (Process ultraDMD in ultraDMDs)
                        ultraDMD.Kill();
                }
                catch { }

                int exitCode = px.ExitCode;

                // vpinball always returns -1 when exiting
                if (exitCode == -1)
                    return 0;

                return exitCode;
            }
            catch 
            { 

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

            base.Cleanup();
        }

        private static void SaveScreenshot()
        {
            if (!ScreenCapture.AddScreenCaptureToGameList(Program.SystemConfig["system"], Program.SystemConfig["rom"]))
            {
                string path = Program.AppConfig.GetFullPath("screenshots");
                if (!Directory.Exists(path))
                    return;

                int index = 0;
                string fn;

                do
                {
                    fn = Path.Combine(path, Path.GetFileNameWithoutExtension(Program.SystemConfig["rom"]) + (index == 0 ? "" : "_" + index.ToString()) + ".jpg");
                    index++;
                } 
                while (File.Exists(fn));

                ScreenCapture.CaptureScreen(fn);
            }
        }

        private static void KillProcess(Process px)
        {
            try { px.Kill(); }
            catch { }
        }

        private static LoadingForm ShowSplash(string rom)
        {
            if (rom == null)
                return null;

            string fn = Path.ChangeExtension(rom, ".directb2s");

            var data = DirectB2sData.FromFile(fn);
            if (data != null)
            {
                int last = Environment.TickCount;
                int index = 0;

                var frm = new LoadingForm
                {
                    Image = data.RenderBackglass(index)
                };
                frm.Timer += (a, b) =>
                    {
                        int now = Environment.TickCount;
                        if (now - last > 1000)
                        {
                            index++;
                            if (index >= 4)
                                index = 0;

                            frm.Image = data.RenderBackglass(index);
                            frm.Invalidate();
                            last = now;
                        }

                    };
                frm.Show();

                return frm;
            }

            return null;
        }

        private static bool FileUrlValueExists(object value)
        {
            if (value == null)
                return false;

            try
            {
                string localPath = new Uri(value.ToString()).LocalPath;
                if (File.Exists(localPath))
                    return true;
            }
            catch { }

            return false;
        }

        private static bool IsComServerAvailable(string name)
        {
            RegistryKey key = Registry.ClassesRoot.OpenSubKey(name, false);
            if (key == null)
                return false;

            object defaultValue = key.GetValue(null);

            if (!"mscoree.dll".Equals(defaultValue) && FileUrlValueExists(key.GetValue(null)))
            {
                key.Close();
                return true;
            }

            if ("mscoree.dll".Equals(defaultValue) && FileUrlValueExists(key.GetValue("CodeBase")))
            {
                key.Close();
                return true;
            }

            key.Close();
            return false;
        }

        private static void EnsureUltraDMDRegistered(string path)
        {
            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring UltraDMD is registered.");

                // Check for valid out-of-process COM server ( UltraDMD ) 
                if (IsComServerAvailable(@"CLSID\{E1612654-304A-4E07-A236-EB64D6D4F511}\LocalServer32"))
                    return;

                // Check for valid in-process COM server ( FlexDMD )
                if (IsComServerAvailable(@"CLSID\{E1612654-304A-4E07-A236-EB64D6D4F511}\InprocServer32"))
                    return;
                
                string ultraDMD = Path.Combine(path, "UltraDMD", "UltraDMD.exe");
                if (!File.Exists(ultraDMD))
                    ultraDMD = Path.Combine(path, "XDMD", "UltraDMD.exe");

                if (File.Exists(ultraDMD))
                {
                    Process px = new Process
                    {
                        EnableRaisingEvents = true
                    };
                    px.StartInfo.Verb = "RunAs";
                    px.StartInfo.FileName = ultraDMD;
                    px.StartInfo.Arguments = " /i";
                    px.StartInfo.UseShellExecute = true;
                    px.StartInfo.CreateNoWindow = true;
                    px.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                    px.Start();
                    px.WaitForExit();
                }
            }
            catch { }
        }

        private static string ReadRegistryValue(RegistryKeyEx key, string path, string value, RegistryViewEx view = RegistryViewEx.Registry32)
        {
            var regKeyc = key.OpenSubKey(path, view);
            if (regKeyc != null)
            {
                object pth = regKeyc.GetValue(value);
                    if (pth != null)
                        return pth.ToString();

                regKeyc.Close();
            }

            return null;
        }

        private bool ShouldRegisterBackglassServer(string path, RegistryViewEx view)
        {
            try            
            {
                var clsid = ReadRegistryValue(RegistryKeyEx.ClassesRoot, @"B2S.B2SPlayer\CLSID", null);
                if (string.IsNullOrEmpty(clsid))
                    return true;

                var codeBase = ReadRegistryValue(RegistryKeyEx.ClassesRoot, @"CLSID\" + clsid + @"\InprocServer32", "CodeBase", view);
                if (string.IsNullOrEmpty(codeBase))
                    return true;
                
                string localPath = new Uri(codeBase).LocalPath;
                if (!File.Exists(localPath))
                    return true;

                // Path has changed ?
                if (!localPath.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                // Version changed ?
                var assembly = ReadRegistryValue(RegistryKeyEx.ClassesRoot, @"CLSID\" + clsid + @"\InprocServer32", "Assembly", view);
                var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(localPath).FullName;

                return assembly != assemblyName;
            }
            catch
            {
                return true;
            }
        }

        private void EnsureBackglassServerRegistered(string path)
        {
            var view = Kernel32.IsX64(_exe) ? RegistryViewEx.Registry64 : RegistryViewEx.Registry32;

            string dllPath = Path.Combine(path, "BackglassServer", "B2SBackglassServer.dll");
            if (!ShouldRegisterBackglassServer(dllPath, view))
                return;

            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring BackGlass Server is registered.");

                Process px = new Process
                {
                    EnableRaisingEvents = true
                };
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = Path.Combine(GetRegAsmPath(view), "regasm.exe");
                px.StartInfo.Arguments = "\"" + dllPath + "\" /codebase";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }

        private static string GetRegAsmPath(RegistryViewEx view = RegistryViewEx.Registry32)
        {
            string installRoot = string.Empty;
            string str2 = null;

            var key = RegistryKeyEx.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NetFramework", view);
            if (key != null)
            {
                object oInstallRoot = key.GetValue("InstallRoot");
                if (oInstallRoot != null)
                    installRoot = oInstallRoot.ToString();

                key.Close();
            }

            if (string.IsNullOrEmpty(installRoot))
                return null;

            key = RegistryKeyEx.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NetFramework\Policy\v4.0", view);
            if (key != null)
            {
                string str3 = "v4.0";
                foreach (string str4 in key.GetValueNames())
                {
                    string path = Path.Combine(installRoot, str3 + "." + str4);
                    if (Directory.Exists(path))
                    {
                        str2 = path;
                        break;
                    }
                }

                key.Close();
            }

            return str2;
        }

        private static bool ShouldRegisterVPinMame(string path, RegistryViewEx view)
        {
            try
            {
                var dll = RegistryKeyEx.GetRegistryValue( 
                    view == RegistryViewEx.Registry64 ?
                    @"HKEY_CLASSES_ROOT\TypeLib\{57270B76-C846-4B1E-88D4-53C8337A0623}\1.0\0\win64" :
                    @"HKEY_CLASSES_ROOT\TypeLib\{57270B76-C846-4B1E-88D4-53C8337A0623}\1.0\0\win32", null, view);

                if (dll == null)
                    return true;

                var dllPath = dll.ToString();
                if (string.IsNullOrEmpty(dllPath))
                    return true;

                string localPath = new Uri(dllPath).LocalPath;
                if (!File.Exists(localPath))
                    return true;

                // Path has changed ?
                if (!localPath.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                    return true;

                return false;
            }
            catch
            {
                return true;
            }
        }

        private void EnsureVPinMameRegistered(string path)
        {
            RegistryViewEx view = Kernel32.IsX64(_exe) ? RegistryViewEx.Registry64 : RegistryViewEx.Registry32;

            string dllPath = Path.Combine(path, "VPinMame", view == RegistryViewEx.Registry64 ? "VPinMAME64.dll" : "VPinMAME.dll");
            if (!ShouldRegisterVPinMame(dllPath, view))
                return;

            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring VpinMame is registered.");

                Process px = new Process
                {
                    EnableRaisingEvents = true
                };
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = Path.Combine(FileTools.GetSystemDirectory(view), "regsvr32.exe");
                px.StartInfo.Arguments = "/s \"" + dllPath + "\"";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }

        private void SetupOptions(string path, string romPath, ScreenResolution resolution)
        {
            if (_version >= new Version(10, 8, 0, 0))
                SetupOptionsIniFile(path, resolution);
            else
                SetupOptionsRegistry(resolution);

            SetupVPinMameOptions(path, romPath);
        }

        private void SetupOptionsIniFile(string path, ScreenResolution resolution)
        {
            string iniFile = Path.Combine(path, "VPinballX.ini");

            using (var ini = new IniFile(iniFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                SimpleLogger.Instance.Info("[Generator] Writing config to VPinballX.ini file.");

                if (Screen.AllScreens.Length > 1 && (!SystemConfig.isOptSet("enableb2s") || SystemConfig.getOptBoolean("enableb2s")) && !SystemInformation.TerminalServerSession)
                    ini.WriteValue("Controller", "ForceDisableB2S", "0");
                else if (SystemConfig.getOptBoolean("enableb2s"))
                    ini.WriteValue("Controller", "ForceDisableB2S", "0");
                else
                    ini.WriteValue("Controller", "ForceDisableB2S", "1");

                if (string.IsNullOrEmpty(ini.GetValue("Controller", "DOFContactors")))
                {
                    ini.WriteValue("Controller", "DOFContactors", "2");
                    ini.WriteValue("Controller", "DOFKnocker", "2");
                    ini.WriteValue("Controller", "DOFChimes", "2");
                    ini.WriteValue("Controller", "DOFBell", "2");
                    ini.WriteValue("Controller", "DOFGear", "2");
                    ini.WriteValue("Controller", "DOFShaker", "2");
                    ini.WriteValue("Controller", "DOFFlippers", "2");
                    ini.WriteValue("Controller", "DOFTargets", "2");
                    ini.WriteValue("Controller", "DOFDropTargets", "2");
                }

                ini.WriteValue("Player", "DisableESC", "1");

                // Get monitor index 
                int monitorIndex = SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]) ? SystemConfig["MonitorIndex"].ToInteger() - 1 : 0;
                if (monitorIndex >= Screen.AllScreens.Length)
                    monitorIndex = 0;

                // Get bounds based on screen or resolution
                Size bounds = resolution == null ? Screen.PrimaryScreen.Bounds.Size : new Size(resolution.Width, resolution.Height);
                if (monitorIndex != 0 && resolution == null && monitorIndex < Screen.AllScreens.Length)
                    bounds = Screen.AllScreens[monitorIndex].Bounds.Size;

                // Resolution and fullscreen
                ini.WriteValue("Player", "Width", bounds.Width.ToString());
                ini.WriteValue("Player", "Height", bounds.Height.ToString());
                ini.WriteValue("Player", "FullScreen", "0"); // resolution == null ? "0" : "1" -> Let desktop resolution handle
                ini.WriteValue("Player", "Display", monitorIndex.ToString());

                // Vertical sync
                if (SystemConfig.isOptSet("video_vsync") && SystemConfig["video_vsync"] == "adaptative")
                    ini.WriteValue("Player", "SyncMode", "2");
                else if (SystemConfig.isOptSet("video_vsync") && SystemConfig["video_vsync"] == "false")
                    ini.WriteValue("Player", "SyncMode", "0");
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
                
                ini.WriteValue("Player", "BGSet", SystemConfig.getOptBoolean("arcademode") ? "1" : "0");

                bool aniFilter = !SystemConfig.isOptSet("vp_anisotropic_filtering") || SystemConfig.getOptBoolean("vp_anisotropic_filtering");
                ini.WriteValue("Player", "ForceAnisotropicFiltering", aniFilter ? "1" : "0");
                ini.WriteValue("Player", "UseNVidiaAPI", SystemConfig.getOptBoolean("vp_nvidia") ? "1" : "0");
                ini.WriteValue("Player", "SoftwareVertexProcessing", SystemConfig.getOptBoolean("vp_vertex") ? "1" : "0");

                // Audio
                ini.WriteValue("Player", "PlayMusic", SystemConfig.getOptBoolean("vp_music_off") ? "0" : "1");

                // Controls

                if (!SystemConfig.getOptBoolean("disableautocontrollers"))
                {
                    ini.WriteValue("Player", "LRAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? "0" : "1");
                    ini.WriteValue("Player", "UDAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? "0" : "2");
                    ini.WriteValue("Player", "PlungerAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? "0" : "3");
                    BindIniFeatureSlider(ini, "Player", "DeadZone", "joy_deadzone", "15");
                }

                ini.WriteValue("Editor", "WindowTop", (Screen.PrimaryScreen.Bounds.Height / 2 - 300).ToString());
                ini.WriteValue("Editor", "WindowBottom", (Screen.PrimaryScreen.Bounds.Height / 2 + 300).ToString());
                ini.WriteValue("Editor", "WindowLeft", (Screen.PrimaryScreen.Bounds.Width / 2 - 400).ToString());
                ini.WriteValue("Editor", "WindowRight", (Screen.PrimaryScreen.Bounds.Width / 2 + 400).ToString());
                ini.WriteValue("Editor", "WindowMaximized", "0");
                ini.WriteValue("Editor", "SelectTableOnStart", "");
                ini.WriteValue("Editor", "SelectTableOnPlayerClose", "");
            }
        }

        private void SetupOptionsRegistry(ScreenResolution resolution)
        {
            //HKEY_CURRENT_USER\Software\Visual Pinball\VP10\Player

            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);

            RegistryKey vp = regKeyc.CreateSubKey("Visual Pinball");
            if (vp == null)
                return;

            regKeyc = vp.CreateSubKey("Controller");
            if (regKeyc != null)
            {
                SimpleLogger.Instance.Info("[Generator] Writing config to registry.");

                if (Screen.AllScreens.Length > 1 && (!SystemConfig.isOptSet("enableb2s") || SystemConfig.getOptBoolean("enableb2s")) && !SystemInformation.TerminalServerSession)
                    SetOption(regKeyc, "ForceDisableB2S", 0);
                else
                    SetOption(regKeyc, "ForceDisableB2S", 1);

                SetupOptionIfNotExists(regKeyc, "DOFContactors", 2);
                SetupOptionIfNotExists(regKeyc, "DOFKnocker", 2);
                SetupOptionIfNotExists(regKeyc, "DOFChimes", 2);
                SetupOptionIfNotExists(regKeyc, "DOFBell", 2);
                SetupOptionIfNotExists(regKeyc, "DOFGear", 2);
                SetupOptionIfNotExists(regKeyc, "DOFShaker", 2);
                SetupOptionIfNotExists(regKeyc, "DOFFlippers", 2);
                SetupOptionIfNotExists(regKeyc, "DOFTargets", 2);
                SetupOptionIfNotExists(regKeyc, "DOFDropTargets", 2);

                regKeyc.Close();
            }

            RegistryKey vp10 = vp.CreateSubKey("VP10");
            if (vp10 == null)
                return;

            regKeyc = vp10.CreateSubKey("Player");
            if (regKeyc != null)
            {
                SetOption(regKeyc, "DisableESC", 1);

                // Resolution and fullscreen
                SetOption(regKeyc, "Width", resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
                SetOption(regKeyc, "Height", resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);
                SetOption(regKeyc, "FullScreen", resolution == null ? 0 : 1);
                
                // Vertical sync
                if (SystemConfig.isOptSet("video_vsync") && SystemConfig["video_vsync"] == "adaptative")
                    SetOption(regKeyc, "AdaptiveVSync", 2);
                else if (SystemConfig.isOptSet("video_vsync") && SystemConfig["video_vsync"] == "false")
                    SetOption(regKeyc, "AdaptiveVSync", 0);
                else
                    SetOption(regKeyc, "AdaptiveVSync", 1);

                // Monitor index is 1-based
                if (SystemConfig.isOptSet("MonitorIndex") && !string.IsNullOrEmpty(SystemConfig["MonitorIndex"]))
                {
                    int monitor = SystemConfig["MonitorIndex"].ToInteger() - 1;
                    SetOption(regKeyc, "Display", monitor);
                }
                else
                    SetOption(regKeyc, "Display", 0);

                // Video options
                SetOption(regKeyc, "BallReflection", SystemConfig["vp_ballreflection"] == "1" ? 1 : 0);

                if (SystemConfig.isOptSet("vp_ambient_occlusion") && SystemConfig["vp_ambient_occlusion"] == "dynamic")
                {
                    SetOption(regKeyc, "DisableAO", 0);
                    SetOption(regKeyc, "DynamicAO", 1);
                }
                else
                {
                    SetOption(regKeyc, "DisableAO", SystemConfig["vp_ambient_occlusion"] == "0" ? 1 : 0);
                    SetOption(regKeyc, "DynamicAO", 0);
                }

                if (SystemConfig.isOptSet("vp_antialiasing") && !string.IsNullOrEmpty(SystemConfig["vp_antialiasing"]))
                {
                    int fxaa = SystemConfig["vp_antialiasing"].ToInteger();
                    SetOption(regKeyc, "FXAA", fxaa);
                }
                else
                    SetOption(regKeyc, "FXAA", 0);

                if (SystemConfig.isOptSet("vp_sharpen") && !string.IsNullOrEmpty(SystemConfig["vp_sharpen"]))
                {
                    int sharpen = SystemConfig["vp_sharpen"].ToInteger();
                    SetOption(regKeyc, "Sharpen", sharpen);
                }
                else
                    SetOption(regKeyc, "FXAA", 0);

                SetOption(regKeyc, "BGSet", SystemConfig.getOptBoolean("arcademode") ? 1 : 0);

                bool aniFilter = !SystemConfig.isOptSet("vp_anisotropic_filtering") || SystemConfig.getOptBoolean("vp_anisotropic_filtering");
                SetOption(regKeyc, "ForceAnisotropicFiltering", aniFilter ? 1 : 0);
                SetOption(regKeyc, "UseNVidiaAPI", SystemConfig.getOptBoolean("vp_nvidia") ? 1 : 0);
                SetOption(regKeyc, "SoftwareVertexProcessing", SystemConfig.getOptBoolean("vp_vertex") ? 1 : 0);

                // Audio
                SetOption(regKeyc, "PlayMusic", SystemConfig.getOptBoolean("vp_music_off") ? 0 : 1);

                // Controls
                if (!SystemConfig.getOptBoolean("disableautocontrollers"))
                {
                    SetOption(regKeyc, "LRAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? 0 : 1);
                    SetOption(regKeyc, "UDAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? 0 : 2);
                    SetOption(regKeyc, "PlungerAxis", SystemConfig.getOptBoolean("nouse_joyaxis") ? 0 : 3);

                    int deadzone = 15;

                    if (SystemConfig.isOptSet("joy_deadzone") && !string.IsNullOrEmpty(SystemConfig["joy_deadzone"]))
                        deadzone = SystemConfig["joy_deadzone"].ToIntegerString().ToInteger();

                    SetOption(regKeyc, "DeadZone", deadzone);
                }
                regKeyc.Close();
            }

            regKeyc = vp10.CreateSubKey("Editor");
            if (regKeyc != null)
            {
                SetOption(regKeyc, "WindowTop", Screen.PrimaryScreen.Bounds.Height / 2 - 300);
                SetOption(regKeyc, "WindowBottom", Screen.PrimaryScreen.Bounds.Height / 2 + 300);
                SetOption(regKeyc, "WindowLeft", Screen.PrimaryScreen.Bounds.Width / 2 - 400);
                SetOption(regKeyc, "WindowRight", Screen.PrimaryScreen.Bounds.Width / 2 + 400);
                SetOption(regKeyc, "WindowMaximized", 0);

                regKeyc.Close();
            }

            vp10.Close();
            vp.Close();
        }

        private static void SetupVPinMameOptions(string path, string romPath)
        {
            var softwareKey = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (softwareKey == null)
                return;
            
            var visualPinMame = softwareKey.CreateSubKey("Freeware").CreateSubKey("Visual PinMame");
            if (visualPinMame != null)
            {
                SimpleLogger.Instance.Info("[Generator] Writing VPinMame config to Registry.");

                DisableVPinMameLicenceDialogs(romPath, visualPinMame);

                visualPinMame.CreateSubKey("default");
                
                var globalKey = visualPinMame.CreateSubKey("globals");
                if (globalKey != null)
                {
                    string vPinMamePath = Path.Combine(path, "VPinMAME");

                    SetOption(globalKey, "rompath", string.IsNullOrEmpty(romPath) ? Path.Combine(vPinMamePath, "roms") : romPath);

                    SetOption(globalKey, "artwork_directory", Path.Combine(vPinMamePath, "artwork"));
                    SetOption(globalKey, "cfg_directory", Path.Combine(vPinMamePath, "cfg"));
                    SetOption(globalKey, "cheat_file", Path.Combine(vPinMamePath, "cheat.dat"));
                    SetOption(globalKey, "cpu_affinity_mask", 0);
                    SetOption(globalKey, "diff_directory", Path.Combine(vPinMamePath, "diff"));
                    SetOption(globalKey, "hiscore_directory", Path.Combine(vPinMamePath, "hi"));
                    SetOption(globalKey, "history_file", Path.Combine(vPinMamePath, "history.dat"));
                    SetOption(globalKey, "input_directory", Path.Combine(vPinMamePath, "inp"));
                    SetOption(globalKey, "joystick", 0);
                    SetOption(globalKey, "low_latency_throttle", 0);
                    SetOption(globalKey, "mameinfo_file", Path.Combine(vPinMamePath, "mameinfo.dat"));
                    SetOption(globalKey, "memcard_directory", Path.Combine(vPinMamePath, "memcard"));
                    SetOption(globalKey, "mouse", 0);
                    SetOption(globalKey, "nvram_directory", Path.Combine(vPinMamePath, "nvram"));
                    SetOption(globalKey, "samplepath", Path.Combine(vPinMamePath, "samples"));
                    SetOption(globalKey, "screen", "");
                    SetOption(globalKey, "snapshot_directory", Path.Combine(vPinMamePath, "snap"));
                    SetOption(globalKey, "state_directory", Path.Combine(vPinMamePath, "sta"));
                    SetOption(globalKey, "steadykey", 1);
                    SetOption(globalKey, "wave_directory", Path.Combine(vPinMamePath, "wave"));
                    SetOption(globalKey, "window", 1);

                    globalKey.Close();
                }
            }

            softwareKey.Close();
        }

        private static void DisableVPinMameLicenceDialogs(string romPath, RegistryKey visualPinMame)
        {
            if (romPath == null || !Directory.Exists(romPath))
                return;

            SimpleLogger.Instance.Info("[Generator] Disabling VPinMame Licence prompts for all available table roms.");

            string[] romList = Directory.GetFiles(romPath, "*.zip").Select(r => Path.GetFileNameWithoutExtension(r)).Distinct().ToArray();
            foreach (var rom in romList)
            {
                var romKey = visualPinMame.OpenSubKey(rom, true);
                if (romKey == null)
                {
                    romKey = visualPinMame.CreateSubKey(rom);
                    romKey?.SetValue(null, 1);
                }
                
                if (romKey != null)
                {
                    romKey.SetValue("cabinet_mode", 1);
                    romKey.SetValue("skip_disclaimer", 1);
                    romKey.SetValue("skip_gameinfo", 1);

                    romKey.Close();
                }
            }
        }

        private static void SetOption(RegistryKey regKeyc, string name, object value)
        {
            object o = regKeyc.GetValue(name);
            if (object.Equals(value, o))
                return;

            regKeyc.SetValue(name, value);
        }

        private static void SetupOptionIfNotExists(RegistryKey regKeyc, string name, object value)
        {
            object o = regKeyc.GetValue(name);
            if (o != null)
                return;

            regKeyc.SetValue(name, value);
        }

        class DirectB2sData
        {
            public static DirectB2sData FromFile(string file)
            {
                if (!File.Exists(file))
                    return null;


                XmlDocument document = new XmlDocument();
                document.Load(file);

                XmlElement element = (XmlElement)document.SelectSingleNode("DirectB2SData");
                if (element == null)
                    return null;

                var bulbs = new List<Bulb>();

                foreach (XmlElement bulb in element.SelectNodes("Illumination/Bulb"))
                {
                    if (!bulb.HasAttribute("Parent") || bulb.GetAttribute("Parent") != "Backglass")
                        continue;

                    try
                    {
                        Bulb item = new Bulb
                        {
                            ID = bulb.GetAttribute("ID").ToInteger(),
                            LightColor = bulb.GetAttribute("LightColor"),
                            LocX = bulb.GetAttribute("LocX").ToInteger(),
                            LocY = bulb.GetAttribute("LocY").ToInteger(),
                            Width = bulb.GetAttribute("Width").ToInteger(),
                            Height = bulb.GetAttribute("Height").ToInteger(),
                            Visible = bulb.GetAttribute("Visible") == "1",
                            IsImageSnippit = bulb.GetAttribute("IsImageSnippit") == "1",
                            Image = Misc.Base64ToImage(bulb.GetAttribute("Image"))
                        };

                        if (item.Visible && item.Image != null)
                            bulbs.Add(item);
                    }
                    catch { }
                }


                XmlElement element13 = (XmlElement)element.SelectSingleNode("Images/BackglassImage");
                if (element13 != null)
                {
                    try
                    {
                        var image = Misc.Base64ToImage(element13.Attributes["Value"].InnerText);
                        if (image != null)
                        {
                            return new DirectB2sData()
                            {
                                Bulbs = bulbs.ToArray(),
                                Image = image,
                            };
                        }
                    }
                    catch { }
                }

                return null;
            }

            public Image RenderBackglass(int index = 0)
            {
                var bitmap = new Bitmap(Image);

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    foreach (var bulb in Bulbs)
                    {
                        if (bulb.IsImageSnippit)
                            continue;

                        if (index == 0 && (bulb.ID & 1) == 0)
                            continue;

                        if (index == 1 && (bulb.ID & 1) == 1)
                            continue;

                        if (index == 3)
                            continue;

                        Color lightColor = Color.White;
                        var split = bulb.LightColor.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        if (split.Length == 3)
                            lightColor = Color.FromArgb(split[0].ToInteger(), split[1].ToInteger(), split[2].ToInteger());

                        using (ImageAttributes imageAttrs = new ImageAttributes())
                        {
                            var colorMatrix = new ColorMatrix(new float[][]
                                    {
                                        new float[] { lightColor.R / 255f, 0, 0, 0, 0 },
                                        new float[] { 0, lightColor.G / 255f, 0, 0, 0 },
                                        new float[] { 0, 0, lightColor.B / 255f, 0, 0 },
                                        new float[] { 0, 0, 0, 1, 0 },
                                        new float[] { 0, 0, 0, 0, 1 }
                                    });

                            imageAttrs.SetColorMatrix(colorMatrix);

                            Rectangle dest = new Rectangle(bulb.LocX, bulb.LocY, bulb.Width, bulb.Height);
                            g.DrawImage(bulb.Image, dest, 0, 0, bulb.Image.Width, bulb.Image.Height, GraphicsUnit.Pixel, imageAttrs, null, IntPtr.Zero);
                        }
                    }
                }

                return bitmap;
            }

            public Bulb[] Bulbs { get; private set; }
            public Image Image { get; private set; }

            public class Bulb
            {
                public int ID { get; set; }

                public string LightColor { get; set; }
                public int LocX { get; set; }
                public int LocY { get; set; }
                public int Width { get; set; }
                public int Height { get; set; }
                public bool Visible { get; set; }
                public bool IsImageSnippit { get; set; }

                public Image Image { get; set; }
            }
        }
    }
}
