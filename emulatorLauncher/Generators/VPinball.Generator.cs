using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using VPinballLauncher;
using System.Xml;
using System.Drawing;
using emulatorLauncher.Tools;
using Microsoft.Win32;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class VPinballGenerator : Generator
    {
        private LoadingForm _splash;
        private string _rom;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("vpinball");
            if (path == null)
                return null;

            string exe = Path.Combine(path, "VPinballX.exe");
            if (!File.Exists(exe))
                return null;

            rom = this.TryUnZipGameIfNeeded(system, rom, true);
            if (Directory.Exists(rom))
            {
                rom = Directory.GetFiles(rom, "*.vpx").FirstOrDefault();
                if (string.IsNullOrEmpty(rom))
                    return null;
            }

            _rom = rom;
            _splash = ShowSplash(rom);

            ScreenResolution.SetHighDpiAware(exe);
            EnsureUltraDMDRegistered(path);
            EnsureBackglassServerRegistered(path);
            EnsureVPinMameRegistered(path);

            string romPath = Path.Combine(Path.GetDirectoryName(rom), "roms");
            if (!Directory.Exists(romPath))
                romPath = Path.Combine(Path.GetDirectoryName(rom), ".roms");
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

            List<string> commandArray = new List<string>();
                        
            Version version = new Version();
            if (Version.TryParse(FileVersionInfo.GetVersionInfo(exe).ProductVersion.Replace(",", ".").Replace(" ", ""), out version) && version >= new Version(10, 7, 0, 0))
                commandArray.Add("-ExtMinimized");            

            commandArray.Add("-play");
            commandArray.Add(rom);

            string args = string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WindowStyle = _splash != null ? ProcessWindowStyle.Minimized : ProcessWindowStyle.Normal,
                UseShellExecute = true
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            try
            {
                var px = Process.Start(path);

                using (new KeyboardManager(() => KillProcess(px, _rom)))
                {
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

        private static void KillProcess(Process px, string rom)
        {
            try
            {
                ScreenCapture.AddScreenCaptureToGameList(rom);
                px.Kill();
            }
            catch { }
        }

        private static LoadingForm ShowSplash(string rom)
        {
            if (rom == null)
                return null;

            string fn = Path.ChangeExtension(rom, ".directb2s");
            if (File.Exists(fn))
            {
                try
                {
                    XmlDocument document = new XmlDocument();
                    document.Load(fn);

                    XmlElement element = (XmlElement)document.SelectSingleNode("DirectB2SData");

                    XmlElement element13 = (XmlElement)element.SelectSingleNode("Images/BackglassImage");
                    if (element13 != null)
                    {
                        Image image12 = Misc.Base64ToImage(element13.Attributes["Value"].InnerText);

                        LoadingForm frm = new LoadingForm();
                        frm.Image = image12;
                        frm.Show();

                        return frm;
                    }

                }
                catch { }
            }

            return null;
        }


        private static void EnsureUltraDMDRegistered(string path)
        {
            try
            {
                //                HKEY_CLASSES_ROOT\WOW6432Node\CLSID\{E1612654-304A-4E07-A236-EB64D6D4F511}
                RegistryKey regKeyc = Registry.ClassesRoot.OpenSubKey(@"CLSID\{E1612654-304A-4E07-A236-EB64D6D4F511}\LocalServer32", false);
                if (regKeyc != null)
                {
                    object pth = regKeyc.GetValue(null);
                    if (pth != null)
                    {
                        try
                        {
                            string localPath = new Uri(pth.ToString()).LocalPath;
                            if (File.Exists(localPath))
                            {
                                regKeyc.Close();
                                return;
                            }
                        }
                        catch { }
                    }

                    regKeyc.Close();
                }

                string ultraDMD = Path.Combine(path, "UltraDMD", "UltraDMD.exe");
                if (!File.Exists(ultraDMD))
                    ultraDMD = Path.Combine(path, "XDMD", "UltraDMD.exe");

                if (File.Exists(ultraDMD))
                {
                    Process px = new Process();
                    px.EnableRaisingEvents = true;
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

        private static void EnsureBackglassServerRegistered(string path)
        {
            try
            {
                RegistryKey regKeyc = Registry.ClassesRoot.OpenSubKey(@"Record\{080561AA-C3D7-3933-AF39-15A780324DB1}", false);
                if (regKeyc != null)
                {
                    var sk = regKeyc.OpenSubKey(@"1.0.0.0");
                    if (sk != null)
                    {
                        object pth = sk.GetValue("CodeBase");
                        if (pth != null)
                        {
                            try
                            {
                                string localPath = new Uri(pth.ToString()).LocalPath;
                                if (File.Exists(localPath))
                                {
                                    regKeyc.Close();
                                    return;
                                }
                            }
                            catch { }
                        }
                    }
                    regKeyc.Close();
                }

                Process px = new Process();
                px.EnableRaisingEvents = true;
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = Path.Combine(GetRegAsmPath(), "regasm.exe");
                px.StartInfo.Arguments = "\"" + Path.Combine(path, "BackglassServer", "B2SBackglassServer.dll") + "\" /codebase";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }


        private static string GetRegAsmPath()
        {
            string installRoot = string.Empty;
            string str2 = null;

            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NetFramework", false);
            if (key != null)
            {
                object oInstallRoot = key.GetValue("InstallRoot");
                if (oInstallRoot != null)
                    installRoot = oInstallRoot.ToString();

                key.Close();
            }

            if (string.IsNullOrEmpty(installRoot))
                return null;

            key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\.NetFramework\Policy\v4.0", false);
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

        private static void EnsureVPinMameRegistered(string path)
        {
            try
            {
                RegistryKey regKeyc = Registry.ClassesRoot.OpenSubKey(@"TypeLib\{57270B76-C846-4B1E-88D4-53C8337A0623}", false);
                if (regKeyc != null)
                {
                    var sk = regKeyc.OpenSubKey(@"1.0\0\win32");
                    if (sk != null)
                    {
                        object pth = sk.GetValue(null);
                        if (pth != null)
                        {
                            if (File.Exists(pth.ToString()))
                            {
                                regKeyc.Close();
                                return;
                            }
                        }
                    }
                    regKeyc.Close();
                }

                Process px = new Process();
                px.EnableRaisingEvents = true;
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = Path.Combine(FileTools.GetSystemDirectory(), "regsvr32.exe");
                px.StartInfo.Arguments = "/s \"" + Path.Combine(path, "VPinMame", "VPinMAME.dll") + "\"";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }

        private void SetupOptions(string path, string romPath, ScreenResolution resolution)
        {
            //HKEY_CURRENT_USER\Software\Visual Pinball\VP10\Player

            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);

            RegistryKey vp = regKeyc.CreateSubKey("Visual Pinball");
            if (vp == null)
                return;

            regKeyc = vp.CreateSubKey("Controller");
            if (regKeyc != null)
            {
                if (Screen.AllScreens.Length >= 1 && SystemConfig["enableb2s"] != "0" && !SystemInformation.TerminalServerSession)
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
                SetOption(regKeyc, "Width", resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width);
                SetOption(regKeyc, "Height", resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height);
                SetOption(regKeyc, "FullScreen", resolution == null ? 0 : 1);
                SetOption(regKeyc, "AdaptiveVSync", SystemConfig["VSync"] != "false" ? 1 : 0);
                SetOption(regKeyc, "BGSet", SystemConfig["arcademode"] == "1" ? 1 : 0);

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

            SetupVPinMameOptions(path, romPath);
        }

        private static void SetupVPinMameOptions(string path, string romPath)
        {
            string vPinMamePath = Path.Combine(path, "VPinMAME");

            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
            {
                RegistryKey visualPinMame = regKeyc.CreateSubKey("Freeware").CreateSubKey("Visual PinMame");
                if (visualPinMame == null)
                    return;

                regKeyc = visualPinMame.CreateSubKey("globals");

                DisableVPinMameLicenceDialogs(romPath, visualPinMame);
            }

            if (regKeyc != null)
            {
                SetOption(regKeyc, "rompath", string.IsNullOrEmpty(romPath) ? Path.Combine(vPinMamePath, "roms") : romPath);

                SetOption(regKeyc, "nvram_directory", Path.Combine(vPinMamePath, "nvram"));
                SetOption(regKeyc, "snapshot_directory", Path.Combine(vPinMamePath, "snap"));
                SetOption(regKeyc, "samplepath", Path.Combine(vPinMamePath, "samples"));
                SetOption(regKeyc, "state_directory", Path.Combine(vPinMamePath, "sta"));
                SetOption(regKeyc, "wave_directory", Path.Combine(vPinMamePath, "wave"));
                SetOption(regKeyc, "memcard_directory", Path.Combine(vPinMamePath, "memcard"));
                SetOption(regKeyc, "artwork_directory", Path.Combine(vPinMamePath, "artwork"));
                SetOption(regKeyc, "cfg_directory", Path.Combine(vPinMamePath, "cfg"));
                SetOption(regKeyc, "diff_directory", Path.Combine(vPinMamePath, "diff"));
                SetOption(regKeyc, "hiscore_directory", Path.Combine(vPinMamePath, "hi"));
                SetOption(regKeyc, "input_directory", Path.Combine(vPinMamePath, "inp"));
                SetOption(regKeyc, "mameinfo_file", Path.Combine(vPinMamePath, "mameinfo.dat"));
                SetOption(regKeyc, "cheat_file", Path.Combine(vPinMamePath, "cheat.dat"));
                SetOption(regKeyc, "history_file", Path.Combine(vPinMamePath, "history.dat"));

                regKeyc.Close();
            }
        }

        private static void DisableVPinMameLicenceDialogs(string romPath, RegistryKey visualPinMame)
        {
            if (romPath == null)
                return;

            string[] romList = Directory.GetFiles(romPath, "*.zip");
            foreach (var rom in romList)
            {
                RegistryKey romKey = visualPinMame.OpenSubKey(Path.GetFileNameWithoutExtension(rom), true);
                if (romKey == null)
                {
                    romKey = visualPinMame.CreateSubKey(Path.GetFileNameWithoutExtension(rom));
                    if (romKey != null)
                    {
                        romKey.SetValue(null, 1);
                        romKey.SetValue("cabinet_mode", 1);
                        romKey.Close();
                    }
                }
                else
                {
                    romKey.SetValue("cabinet_mode", 1);
                    romKey.Close();
                }
            }
        }

        private static Dictionary<string, object> _oldValues = new Dictionary<string, object>();

        private static void SetOption(RegistryKey regKeyc, string name, object value)
        {
            object o = regKeyc.GetValue(name);
            if (object.Equals(value, o))
                return;

            if (o != null)
                _oldValues[name] = o;

            regKeyc.SetValue(name, value);
        }

        private static void SetupOptionIfNotExists(RegistryKey regKeyc, string name, object value)
        {
            object o = regKeyc.GetValue(name);
            if (o != null)
                return;

            regKeyc.SetValue(name, value);
        }

        private static void RestoreOptions(string path)
        {
            if (_oldValues.Count == 0)
                return;

            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software\Freeware\Visual PinMame\globals", true);
            if (regKeyc != null)
            {
                foreach (var key in _oldValues)
                {
                    try
                    {
                        if (key.Value == null)
                            regKeyc.DeleteValue(key.Key);
                        else
                            regKeyc.SetValue(key.Key, key.Value);
                    }
                    catch { }
                }

                regKeyc.Close();
            }
        }
    }
}
