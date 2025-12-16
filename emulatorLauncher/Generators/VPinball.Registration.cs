using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class VPinballGenerator : Generator
    {
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

        private bool ShouldRegisterBackglassServer(string path, RegistryViewEx view)
        {
            try
            {
                var clsid = ReadRegistryValue(RegistryKeyEx.ClassesRoot, @"B2S.B2SPlayer\CLSID", null);
                if (string.IsNullOrEmpty(clsid))
                {
                    SimpleLogger.Instance.Info("[INFO] B2S not registered: registering.");
                    return true;
                }

                var codeBase = ReadRegistryValue(RegistryKeyEx.ClassesRoot, @"CLSID\" + clsid + @"\InprocServer32", "CodeBase", view);
                if (string.IsNullOrEmpty(codeBase))
                {
                    SimpleLogger.Instance.Info("[INFO] B2S not registered: registering.");
                    return true;
                }

                string localPath = new Uri(codeBase).LocalPath;
                if (!File.Exists(localPath))
                {
                    SimpleLogger.Instance.Info("[INFO] B2S registering, local Path does not exist.");
                    return true;
                }

                // Path has changed ?
                if (!localPath.Equals(path, StringComparison.InvariantCultureIgnoreCase))
                {
                    SimpleLogger.Instance.Info("[INFO] B2S dll registered in different path, re-registering.");
                    return true;
                }

                // Version changed ?
                var assembly = ReadRegistryValue(RegistryKeyEx.ClassesRoot, @"CLSID\" + clsid + @"\InprocServer32", "Assembly", view);
                var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(localPath).FullName;

                if (assembly != assemblyName)
                {
                    SimpleLogger.Instance.Info("[INFO] Registered B2S dll assembly version different from RetroBat, re-registering.");
                    return true;
                }

                return false;
            }
            catch
            {
                SimpleLogger.Instance.Warning("[WARNING] Unable to determine if Backglass Server is registered.");
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

        private void EnsurePinupPlayerRegistered(string path)
        {
            string keyPath = @"TypeLib\{D50F2477-84E8-4CED-9409-3735CA67FDE3}\1.0\0\win32";
            string PinupPlayerPath = Path.Combine(path, "PinUPSystem", "PinUpPlayer.exe");

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        string value = key.GetValue(null) as string;
                        if (value != null)
                        {
                            if (value != PinupPlayerPath)
                                RegisterPinupPlayer(PinupPlayerPath);
                            else
                                return;
                        }
                        else
                            RegisterPinupPlayer(PinupPlayerPath);
                    }
                    else
                        RegisterPinupPlayer(PinupPlayerPath);
                }
            }
            catch
            { }
        }

        private void RegisterPinupPlayer(string path)
        {
            if (!File.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] PinUpPlayer.exe not found.");
                return;
            }

            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring PinupPlayer is registered.");

                Process px = new Process
                {
                    EnableRaisingEvents = true
                };
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = path;
                px.StartInfo.Arguments = "/regserver";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }

        private void EnsurePinupDOFRegistered(string path)
        {
            string keyPath = @"TypeLib\{02B4C318-12D3-48C6-AA69-CEE342FF9D15}\1.0\0\win32";
            string PinupDOFPath = Path.Combine(path, "PinUPSystem", "PinUpDOF.exe");

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        string value = key.GetValue(null) as string;
                        if (value != null)
                        {
                            if (value != PinupDOFPath)
                                RegisterPinupDOF(PinupDOFPath);
                            else
                                return;
                        }
                        else
                            RegisterPinupDOF(PinupDOFPath);
                    }
                    else
                        RegisterPinupDOF(PinupDOFPath);
                }
            }
            catch
            { }
        }

        private void RegisterPinupDOF(string path)
        {
            if (!File.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] PinUpDOF.exe not found.");
                return;
            }

            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring PinUpDOF is registered.");

                Process px = new Process
                {
                    EnableRaisingEvents = true
                };
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = path;
                px.StartInfo.Arguments = "/regserver";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }

        private void EnsurePupServerRegistered(string path)
        {
            string keyPath = @"TypeLib\{5EC048E8-EF55-40B8-902D-D6ECD1C8FF4E}\1.0\0\win32";
            string PinupDOFPath = Path.Combine(path, "PinUPSystem", "PuPServer.exe");

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        string value = key.GetValue(null) as string;
                        if (value != null)
                        {
                            if (value != PinupDOFPath)
                                RegisterPupServer(PinupDOFPath);
                            else
                                return;
                        }
                        else
                            RegisterPupServer(PinupDOFPath);
                    }
                    else
                        RegisterPupServer(PinupDOFPath);
                }
            }
            catch
            { }
        }

        private void RegisterPupServer(string path)
        {
            if (!File.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] PuPServer.exe not found.");
                return;
            }

            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring PuPServer is registered.");

                Process px = new Process
                {
                    EnableRaisingEvents = true
                };
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = path;
                px.StartInfo.Arguments = "/regserver";
                px.StartInfo.UseShellExecute = true;
                px.StartInfo.CreateNoWindow = true;
                px.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                px.Start();
                px.WaitForExit();
            }
            catch { }
        }

        private void EnsurePupDMDControlRegistered(string path)
        {
            string keyPath = @"TypeLib\{5049E487-2802-46B0-A511-8B198B274E1B}\1.0\0\win32";
            string PUPDMDControl = Path.Combine(path, "VPinMAME", "PUPDMDControl.exe");

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.OpenSubKey(keyPath))
                {
                    if (key != null)
                    {
                        string value = key.GetValue(null) as string;
                        if (value != null)
                        {
                            if (value != PUPDMDControl)
                                RegisterPupDMDControl(PUPDMDControl);
                            else
                                return;
                        }
                        else
                            RegisterPupDMDControl(PUPDMDControl);
                    }
                    else
                        RegisterPupDMDControl(PUPDMDControl);
                }
            }
            catch
            { }
        }

        private void RegisterPupDMDControl(string path)
        {
            if (!File.Exists(path))
            {
                SimpleLogger.Instance.Warning("[WARNING] PUPDMDControl.exe not found.");
                return;
            }

            try
            {
                SimpleLogger.Instance.Info("[Generator] Ensuring PUPDMDControl is registered.");

                Process px = new Process
                {
                    EnableRaisingEvents = true
                };
                px.StartInfo.Verb = "RunAs";
                px.StartInfo.FileName = path;
                px.StartInfo.Arguments = "/regserver";
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

        private static void BindBoolRegistryFeature(RegistryKey key, string name, string featureName, object truevalue, object falsevalue, bool defaultOn)
        {
            if (Program.Features.IsSupported(featureName))
            {
                if (Program.SystemConfig.isOptSet(featureName) && Program.SystemConfig.getOptBoolean(featureName))
                    SetOption(key, name, truevalue);
                else
                {
                    if (Program.SystemConfig.isOptSet(featureName) && !Program.SystemConfig.getOptBoolean(featureName))
                        SetOption(key, name, falsevalue);
                    else if (defaultOn)
                        SetOption(key, name, truevalue);
                    else
                        SetOption(key, name, falsevalue);
                }
            }
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
    }
}
