using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;

namespace emulatorLauncher
{
    class Pcsx2Generator : Generator
    {
        public Pcsx2Generator()
        {
            DependsOnDesktopResolution = true;
        }

        private string _path;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            _path = AppConfig.GetFullPath("pcsx2");

            string exe = Path.Combine(_path, "pcsx2.exe");
            if (!File.Exists(exe))
                return null;

            SetupPaths();
            SetupVM();
            SetupGSDx(resolution);

            /*
            romName = Path.GetFileNameWithoutExtension(rom);

            RestoreIni(path, null, "GSdx.ini", true);
            RestoreIni(path, null, "PCSX2_vm.ini", true);

            SaveIni(path, romName, "GSdx.ini");
            SaveIni(path, romName, "PCSX2_vm.ini");
            */

            List<string> commandArray = new List<string>();
            commandArray.Add("--portable");
            commandArray.Add("--fullscreen");
            commandArray.Add("--nogui");

            if (SystemConfig.isOptSet("fullboot") && SystemConfig.getOptBoolean("fullboot"))
                commandArray.Add("--fullboot");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = _path,
                Arguments = args + " \"" + rom + "\"", 
            };
        }

        private void SetupPaths()
        {
            string iniFile = Path.Combine(_path, "inis", "PCSX2_ui.ini");
            if (File.Exists(iniFile))
            {
                try
                {
                    using (var ini = new IniFile(iniFile))
                    {
                        Uri relRoot = new Uri(_path, UriKind.Absolute);

                        string biosPath = AppConfig.GetFullPath("bios");
                        if (!string.IsNullOrEmpty(biosPath))
                        {                            
                            ini.WriteValue("Folders", "UseDefaultBios", "disabled");
                            ini.WriteValue("Folders", "Bios", biosPath.Replace("\\", "\\\\"));
                        }

                        string savesPath = AppConfig.GetFullPath("saves");
                        if (!string.IsNullOrEmpty(savesPath))
                        {
                            savesPath = Path.Combine(savesPath, "pcsx2");
                            if (!Directory.Exists(savesPath))
                                try { Directory.CreateDirectory(savesPath); }
                                catch { }

                            ini.WriteValue("Folders", "Savestates", savesPath.Replace("\\", "\\\\")); // Path.Combine(relPath, "pcsx2")
                        }

                        if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                            ini.WriteValue("GSWindow", "AspectRatio", SystemConfig["ratio"]);
                        else
                            ini.WriteValue("GSWindow", "AspectRatio", "Stretch");

                        ini.WriteValue("GSWindow", "IsFullscreen", "enabled");
                    }
                }
                catch { }
            }
        }

        private void SetupVM()
        {
            string iniFile = Path.Combine(_path, "inis", "PCSX2_vm.ini");
            if (File.Exists(iniFile))
            {
                try
                {
                    using (var ini = new IniFile(iniFile))
                    {
                        string negdivhack = SystemConfig["negdivhack"] == "1" ? "enabled" : "disabled";

                        ini.WriteValue("EmuCore/Speedhacks", "vuThread", negdivhack);

                        ini.WriteValue("EmuCore/CPU/Recompiler", "vuExtraOverflow", negdivhack);
                        ini.WriteValue("EmuCore/CPU/Recompiler", "vuSignOverflow", negdivhack);
                        ini.WriteValue("EmuCore/CPU/Recompiler", "fpuExtraOverflow", negdivhack);
                        ini.WriteValue("EmuCore/CPU/Recompiler", "fpuFullMode", negdivhack);

                        ini.WriteValue("EmuCore/Gamefixes", "VuClipFlagHack", negdivhack);
                        ini.WriteValue("EmuCore/Gamefixes", "FpuNegDivHack", negdivhack);
                    }
                }
                catch { }
            }
        }
                        
        private void SetupGSDx(ScreenResolution resolution)
        {
            string iniFile = Path.Combine(_path, "inis", "GSdx.ini");
            if (File.Exists(iniFile))
            {
                try
                {
                    using (var ini = new IniFile(iniFile))
                    {
                        if (!string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                            ini.WriteValue("Settings", "upscale_multiplier", SystemConfig["internalresolution"]);
                        else
                            ini.WriteValue("Settings", "upscale_multiplier", "0");

                        if (string.IsNullOrEmpty(SystemConfig["internalresolution"]) || SystemConfig["internalresolution"] == "0")
                        {
                            if (resolution != null)
                            {
                                ini.WriteValue("Settings", "resx", resolution.Width.ToString());
                                ini.WriteValue("Settings", "resy", (resolution.Height * 2).ToString());
                            }
                            else
                            {
                                ini.WriteValue("Settings", "resx", Screen.PrimaryScreen.Bounds.Width.ToString());
                                ini.WriteValue("Settings", "resy", (Screen.PrimaryScreen.Bounds.Height * 2).ToString());
                            }
                        }

                        if (SystemConfig.isOptSet("interlace") && !string.IsNullOrEmpty(SystemConfig["interlace"]))
                            ini.WriteValue("Settings", "interlace", SystemConfig["interlace"]);
                        else
                            ini.WriteValue("Settings", "interlace", "7");



                        if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                        {
                            ini.WriteValue("Settings", "osd_monitor_enabled", "1");
                            ini.WriteValue("Settings", "osd_indicator_enabled", "1");
                        }
                        else
                        {
                            ini.WriteValue("Settings", "osd_monitor_enabled", "0");
                            ini.WriteValue("Settings", "osd_indicator_enabled", "0");
                        }
                    }

                }
                catch { }
            }
        }


        /*
        private string romName;
        private const string savDirName = "tmp";
        
        public override void Cleanup()
        {
            RestoreIni(path, romName, "GSdx.ini");
            RestoreIni(path, romName, "PCSX2_vm.ini");

            try
            {
                string savDir = Path.Combine(path, "inis", savDirName);
                if (Directory.Exists(savDir))
                    Directory.Delete(savDir);
            }
            catch { }
        }
     
        static void SaveIni(string path, string romName, string iniName)
        {
            string ini = Path.Combine(path, "inis", romName, iniName);
            if (!File.Exists(ini))
                return;

            string originalIni = Path.Combine(path, "inis", iniName);
            if (File.Exists(originalIni))
            {
                string savDir = Path.Combine(path, "inis", savDirName);
                if (!Directory.Exists(savDir))
                    Directory.CreateDirectory(savDir);

                string savIni = Path.Combine(path, "inis", savDirName, iniName);

                try { File.Copy(originalIni, savIni, true); }
                catch { return; }
            }

            try { File.Copy(ini, originalIni, true); }
            catch { }

        }

        static void RestoreIni(string path, string romName, string iniName, bool force = false)
        {
            if (string.IsNullOrEmpty(romName))
                return;

            if (!force)
            {
                string ini = Path.Combine(path, "inis", romName, iniName);
                if (!File.Exists(ini))
                    return;
            }

            string originalIni = Path.Combine(path, "inis", iniName);
            if (File.Exists(originalIni))
            {
                string savIni = Path.Combine(path, "inis", savDirName, iniName);
                if (File.Exists(savIni))
                {
                    try { File.Move(savIni, originalIni); }
                    catch { }

                    try { File.Delete(savIni); }
                    catch { }

                }
            }
        }   */
    }
}
