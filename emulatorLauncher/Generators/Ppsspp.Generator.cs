using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
    class PpssppGenerator : Generator
    {
        public PpssppGenerator()
        {
            DependsOnDesktopResolution = true;
        }
        
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("ppsspp");

            string exe = Path.Combine(path, "PPSSPPWindows64.exe");
            if (!File.Exists(exe) || !Environment.Is64BitOperatingSystem)
                exe = Path.Combine(path, "PPSSPPWindows.exe");

            if (!File.Exists(exe))
                return null;

            SetupConfig(path);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = "-escapeexitsemu -fullscreen \"" + rom + "\"",
                WorkingDirectory = path,
            };
        }

        private void SetupConfig(string path)
        {
            string iniFile = Path.Combine(path, "memstick", "PSP", "SYSTEM", "ppsspp.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    ini.WriteValue("Graphics", "FullScreen", "True");

                    //if (SystemConfig.isOptSet("showFPS") && SystemConfig.getOptBoolean("showFPS"))
                    if (SystemConfig.isOptSet("DrawFramerate") && SystemConfig.getOptBoolean("DrawFramerate"))
                        ini.WriteValue("Graphics", "ShowFPSCounter", "3");
                    else
                        ini.WriteValue("Graphics", "ShowFPSCounter", "0");

                    if (SystemConfig.isOptSet("frameskip") && SystemConfig.getOptBoolean("frameskip"))
                        ini.WriteValue("Graphics", "FrameSkip", SystemConfig["frameskip"]);
                    else
                        ini.WriteValue("Graphics", "FrameSkip", "0");

                    if (SystemConfig.isOptSet("frameskiptype") && !string.IsNullOrEmpty(SystemConfig["frameskiptype"]))
                        ini.WriteValue("Graphics", "FrameSkipType", SystemConfig["frameskiptype"]);
                    else
                        ini.WriteValue("Graphics", "FrameSkipType", "0");

                    if (SystemConfig.isOptSet("internalresolution") && !string.IsNullOrEmpty(SystemConfig["internalresolution"]))
                        ini.WriteValue("Graphics", "InternalResolution", SystemConfig["internalresolution"]);
                    else
                        ini.WriteValue("Graphics", "InternalResolution", "0");

                    if (SystemConfig.isOptSet("rewind") && SystemConfig.getOptBoolean("rewind"))
                        ini.WriteValue("General", "RewindFlipFrequency", "300");
                    else
                        ini.WriteValue("General", "RewindFlipFrequency", "0");
                }
            }

            catch { }
        }
    }
}
