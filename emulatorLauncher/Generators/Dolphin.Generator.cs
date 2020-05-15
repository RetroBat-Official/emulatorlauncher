using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class DolphinGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "dolphin-triforce" || core == "dolphin-triforce") ? "dolphin-triforce" : "dolphin";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("dolphin");

            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("dolphin-emu");

            string exe = Path.Combine(path, "Dolphin.exe");
            if (!File.Exists(exe))
                exe = Path.Combine(path, "DolphinWX.exe");

            if (!File.Exists(exe))
                return null;

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

            SetupGeneralConfig(path, system);
            SetupGfxConfig(path);

            DolphinControllers.WriteControllersConfig(system, rom);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-b -e \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }

        private void SetupGfxConfig(string path)
        {
            string iniFile = Path.Combine(path, "User", "Config", "GFX.ini");
            if (!File.Exists(iniFile))
                return;

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    if (SystemConfig.isOptSet("ratio"))
                    {
                        if (SystemConfig["ratio"] == "4/3")
                            ini.WriteValue("Settings", "AspectRatio", "2");
                        else if (SystemConfig["ratio"] == "16/9")
                            ini.WriteValue("Settings", "AspectRatio", "1");
                        else
                            ini.WriteValue("Settings", "AspectRatio", "0");
                    }

                    // draw or not FPS
                    if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                        ini.WriteValue("Settings", "ShowFPS", "True");
                    else
                        ini.WriteValue("Settings", "ShowFPS", "False");
                    
                    ini.WriteValue("Hardware", "VSync", SystemConfig["VSync"] != "false" ? "True" : "False");                    

                    // search for custom textures
                    ini.WriteValue("Settings", "HiresTextures", "True");
                    ini.WriteValue("Settings", "CacheHiresTextures", "True");

                    if (SystemConfig.isOptSet("internalresolution") && !string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                        ini.WriteValue("Graphics", "InternalResolution", SystemConfig["internalresolution"]);
                    else
                        ini.WriteValue("Graphics", "InternalResolution", "0");
                }
            }

            catch { }
        }
    
        private string getGameCubeLangFromEnvironment()
        {
            Dictionary<string, int> availableLanguages = new Dictionary<string,int>() 
            { 
                {"en", 0 }, { "de", 1 }, { "fr", 2 }, { "es", 3 }, { "it", 4 }, { "nl", 5 } 
            };

            if (!SystemConfig.isOptSet("Language"))
                return "0";

            string lang = SystemConfig["Language"]??"";
            int idx = lang.IndexOf("_");
            if (idx >= 0)
                lang = lang.Substring(0, idx);

            int ret = 0;
            availableLanguages.TryGetValue(lang, out ret);
            return ret.ToString();
        }

        private void SetupGeneralConfig(string path, string system)
        {
            string iniFile = Path.Combine(path, "User", "Config", "Dolphin.ini");
            if (!File.Exists(iniFile))
                return;

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("Display", "Fullscreen", "True");

                    // draw or not FPS
                    if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                    {
                        ini.WriteValue("General", "ShowLag", "True");
                        ini.WriteValue("General", "ShowFPSCounter", "True");
                    }
                    else
                    {
                        ini.WriteValue("General", "ShowLag", "False");
                        ini.WriteValue("General", "ShowFrameCount", "False");
                    }

                    // don't ask about statistics
                    ini.WriteValue("Analytics", "PermissionAsked", "True");

                    // don't confirm at stop
                    ini.WriteValue("Interface", "ConfirmStop", "False");

                    // language (for gamecube at least)
                    ini.WriteValue("Core", "SelectedLanguage", getGameCubeLangFromEnvironment());
                    ini.WriteValue("Core", "GameCubeLanguage", getGameCubeLangFromEnvironment());

                    // wiimote scanning
                    ini.WriteValue("Core", "WiimoteContinuousScanning", "True");

                    // gamecube pads forced as standard pad
                    if (!((Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")))
                    {
                        bool emulatedWiiMote = (system == "wii" && Program.SystemConfig.isOptSet("emulatedwiimotes") && Program.SystemConfig.getOptBoolean("emulatedwiimotes"));

                        for (int i = 0; i < 4; i++)
                        {
                            var ctl = Controllers.FirstOrDefault(c => c.Index == i + 1);

                            if (ctl != null && ctl.Input != null && !emulatedWiiMote)
                            {
                                /*if (ctl.Input.Type == "keyboard")
                                    ini.WriteValue("Core", "SIDevice" + i, "7");
                                else*/
                                    ini.WriteValue("Core", "SIDevice" + i, "6");
                            }
                            else
                                ini.WriteValue("Core", "SIDevice" + i, "0");
                        }
                    }
                }
            }

            catch { }
        }
    }
}
