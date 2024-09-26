using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System.Windows.Forms;

namespace EmulatorLauncher
{
    partial class GemRBGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("gemrb");

            string exe = Path.Combine(path, "gemrb.exe");
            if (!File.Exists(exe))
                return null;

            string conf = Path.Combine(path, "GemRB.cfg");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            SetupConfiguration(rom, conf, system, fullscreen, resolution);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = "-c \"" + conf + "\"",
            };
        }

        private void SetupConfiguration(string rom, string conf, string system, bool fullscreen, ScreenResolution resolution)
        {
            string gamePath = AppConfig.GetFullPath(rom);          
            string gameExtension = Path.GetExtension(rom).Replace(".", "");
            string gameName = new DirectoryInfo(gamePath).Name.Replace("." + gameExtension, "");

            string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), system, gameName);
            if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                catch { }

            using (var ini = IniFile.FromFile(conf))
            {                
                ini.WriteValue("", "GameType", gameExtension);
                ini.WriteValue("", "Fullscreen", fullscreen ? "1" : "0");
                ini.WriteValue("", "GamePath", gamePath);
                ini.WriteValue("", "SavePath", savesPath);
                ini.WriteValue("", "AudioDriver", "openal");
                ini.WriteValue("", "CapFPS", "0");

                if (SystemConfig.isOptSet("resolution") && !string.IsNullOrEmpty(SystemConfig["resolution"]))
                {                                       

                    if (SystemConfig["resolution"].ToLower().Contains("x"))
                    {
                        string[] gameResolution = SystemConfig["resolution"].ToLower().Split('x');
                        ini.WriteValue("", "Width", gameResolution[0]);
                        ini.WriteValue("", "Height", gameResolution[1]);

                    }
                    else if (SystemConfig["resolution"].ToLower() == "desktop")
                    {
                        ini.WriteValue("", "Width", (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString());
                        ini.WriteValue("", "Height", (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString());
                    }
                    else
                    {
                        ini.WriteValue("", "Width", "640");
                        ini.WriteValue("", "Height", "480");
                    }
                }
                else
                {
                    ini.WriteValue("", "Width", "640");
                    ini.WriteValue("", "Height", "480");
                }

                BindBoolIniFeature(ini, "", "Logging", "Logging", "1", "0");
                BindBoolIniFeature(ini, "", "SkipIntroVideos", "SkipIntroVideos", "1", "0");
                BindBoolIniFeature(ini, "", "DrawFPS", "DrawFPS", "1", "0");
                BindBoolIniFeature(ini, "", "EnableCheatKeys", "EnableCheatKeys", "1", "0");
                BindIniFeature(ini, "", "DebugMode", "DebugMode", "0");
                BindIniFeature(ini, "", "Encoding", "Encoding", "default");
                BindIniFeature(ini, "", "Bpp", "Bpp", "32");
                BindIniFeature(ini, "", "DebugMode", "DebugMode", "0");
                BindIniFeature(ini, "", "SpriteFogOfWar", "SpriteFogOfWar", "0");

            }

        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            int ret = base.RunAndWait(path);
            return ret;
        }
    }
}
