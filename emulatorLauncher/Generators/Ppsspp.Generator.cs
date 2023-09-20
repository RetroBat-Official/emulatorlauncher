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

            var commandArray = new List<string>();
            //commandArray.Add("--escape-exit");
            commandArray.Add("-fullscreen");
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,
            };
        }

        private void SetupConfig(string path)
        {
            string iniFile = Path.Combine(path, "memstick", "PSP", "SYSTEM", "ppsspp.ini");
            bool cheevosEnable = Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements");

            if (cheevosEnable)
            {
                string cheevosTokenFile = Path.Combine(path, "memstick", "PSP", "SYSTEM", "ppsspp_retroachievements.dat");
                string cheevosToken = SystemConfig["retroachievements.token"];
                try 
                { 
                    File.WriteAllText(cheevosTokenFile, cheevosToken); 
                } 
                catch { }
            }

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    // Retroachievements
                    if (cheevosEnable)
                    {
                        ini.WriteValue("Achievements", "AchievementsUserName", SystemConfig["retroachievements.username"]);
                        ini.WriteValue("Achievements", "AchievementsEnable", "True");
                        ini.WriteValue("Achievements", "AchievementsEncoreMode", "False");
                        ini.WriteValue("Achievements", "AchievementsUnofficial", "False");
                        ini.WriteValue("Achievements", "AchievementsSoundEffects", "True");
                        ini.WriteValue("Achievements", "AchievementsLogBadMemReads", "False");
                        ini.WriteValue("Achievements", "AchievementsChallengeMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "True" : "False");
                    }
                    else
                    {
                        ini.WriteValue("Achievements", "AchievementsUserName", "");
                        ini.WriteValue("Achievements", "AchievementsEnable", "False");
                        ini.WriteValue("Achievements", "AchievementsEncoreMode", "False");
                        ini.WriteValue("Achievements", "AchievementsUnofficial", "False");
                        ini.WriteValue("Achievements", "AchievementsSoundEffects", "False");
                        ini.WriteValue("Achievements", "AchievementsLogBadMemReads", "False");
                        ini.WriteValue("Achievements", "AchievementsChallengeMode", "False");
                    }
                    
                    // Graphics
                    ini.WriteValue("Graphics", "FullScreen", "True");

                    if (SystemConfig.isOptSet("ppsspp_resolution") && !string.IsNullOrEmpty(SystemConfig["ppsspp_resolution"]))
                        ini.WriteValue("Graphics", "InternalResolution", SystemConfig["ppsspp_resolution"]);
                    else
                        ini.WriteValue("Graphics", "InternalResolution", "0");

                    if (SystemConfig.isOptSet("ppsspp_ratio") && !string.IsNullOrEmpty(SystemConfig["ppsspp_ratio"]) && SystemConfig["ppsspp_ratio"] == "stretch")
                    {
                        ini.WriteValue("Graphics", "DisplayStretch", "True");
                        ini.WriteValue("Graphics", "DisplayAspectRatio", "1.000000");
                    }
                    else if (SystemConfig.isOptSet("ppsspp_ratio") && !string.IsNullOrEmpty(SystemConfig["ppsspp_ratio"]))
                    {
                        ini.WriteValue("Graphics", "DisplayStretch", "False");
                        ini.WriteValue("Graphics", "DisplayAspectRatio", SystemConfig["ppsspp_ratio"]);
                    }
                    else
                    {
                        ini.WriteValue("Graphics", "DisplayStretch", "False");
                        ini.WriteValue("Graphics", "DisplayAspectRatio", "1.000000");
                    }

                    if (SystemConfig.isOptSet("ppsspp_backend") && !string.IsNullOrEmpty(SystemConfig["ppsspp_backend"]))
                        ini.WriteValue("Graphics", "GraphicsBackend", SystemConfig["ppsspp_backend"]);
                    else
                        ini.WriteValue("Graphics", "GraphicsBackend", "0 (OPENGL)");

                    if (SystemConfig.isOptSet("ppsspp_msaa") && !string.IsNullOrEmpty(SystemConfig["ppsspp_msaa"]))
                        ini.WriteValue("Graphics", "MultiSampleLevel", SystemConfig["ppsspp_msaa"]);
                    else
                        ini.WriteValue("Graphics", "MultiSampleLevel", "0");

                    if (SystemConfig.isOptSet("ppsspp_vsync") && !SystemConfig.getOptBoolean("ppsspp_vsync"))
                        ini.WriteValue("Graphics", "VSyncInterval", "False");
                    else
                        ini.WriteValue("Graphics", "VSyncInterval", "True");

                    if (SystemConfig.isOptSet("ppsspp_frame_skipping") && !string.IsNullOrEmpty(SystemConfig["ppsspp_frame_skipping"]))
                        ini.WriteValue("Graphics", "FrameSkip", SystemConfig["ppsspp_frame_skipping"]);
                    else
                        ini.WriteValue("Graphics", "FrameSkip", "0");

                    if (SystemConfig.isOptSet("ppsspp_frameskip_type") && !string.IsNullOrEmpty(SystemConfig["ppsspp_frameskip_type"]))
                        ini.WriteValue("Graphics", "FrameSkipType", SystemConfig["ppsspp_frameskip_type"]);
                    else
                        ini.WriteValue("Graphics", "FrameSkipType", "0");

                    if (SystemConfig.isOptSet("ppsspp_textureenhancement") && !string.IsNullOrEmpty(SystemConfig["ppsspp_textureenhancement"]) && SystemConfig["ppsspp_textureenhancement"].Contains("Tex"))
                    {
                        ini.WriteValue("Graphics", "TexHardwareScaling", "True");
                        ini.WriteValue("Graphics", "TextureShader", SystemConfig["ppsspp_textureenhancement"]);
                        ini.WriteValue("Graphics", "TexScalingLevel", "1");
                        ini.WriteValue("Graphics", "TexScalingType", "0");
                    }
                    else if (SystemConfig.isOptSet("ppsspp_textureenhancement") && !string.IsNullOrEmpty(SystemConfig["ppsspp_textureenhancement"]))
                    {
                        ini.WriteValue("Graphics", "TexScalingType", SystemConfig["ppsspp_textureenhancement"]);
                        ini.WriteValue("Graphics", "TexHardwareScaling", "False");
                        ini.WriteValue("Graphics", "TextureShader", "Off");
                        
                        if (SystemConfig.isOptSet("ppsspp_textureenhancement_level") && !string.IsNullOrEmpty(SystemConfig["ppsspp_textureenhancement_level"]) && SystemConfig["ppsspp_textureenhancement_level"] != "1")
                        {
                            ini.WriteValue("Graphics", "TexScalingLevel", SystemConfig["ppsspp_textureenhancement_level"]);
                            ini.WriteValue("Graphics", "TexDeposterize", "True");
                        }
                    }
                    else
                    {
                        ini.WriteValue("Graphics", "TexHardwareScaling", "False");
                        ini.WriteValue("Graphics", "TextureShader", "Off");
                        ini.WriteValue("Graphics", "TexScalingLevel", "1");
                        ini.WriteValue("Graphics", "TexScalingType", "0");
                        ini.WriteValue("Graphics", "TexDeposterize", "False");
                    }

                    if (SystemConfig.isOptSet("ppsspp_anisotropicfilter") && !string.IsNullOrEmpty(SystemConfig["ppsspp_anisotropicfilter"]))
                        ini.WriteValue("Graphics", "AnisotropyLevel", SystemConfig["ppsspp_anisotropicfilter"]);
                    else
                        ini.WriteValue("Graphics", "AnisotropyLevel", "0");

                    if (SystemConfig.isOptSet("ppsspp_texture_filtering") && !string.IsNullOrEmpty(SystemConfig["ppsspp_texture_filtering"]))
                        ini.WriteValue("Graphics", "TextureFiltering", SystemConfig["ppsspp_texture_filtering"]);
                    else
                        ini.WriteValue("Graphics", "TextureFiltering", "1");

                    if (SystemConfig.isOptSet("Integer_Scaling") && SystemConfig.getOptBoolean("Integer_Scaling"))
                    { 
                        ini.WriteValue("Graphics", "DisplayIntegerScale", "True");
                        ini.WriteValue("Graphics", "DisplayAspectRatio", "1.000000");
                    }
                    else
                        ini.WriteValue("Graphics", "DisplayIntegerScale", "False");

                    // Controls
                    if (SystemConfig.isOptSet("ppsspp_mouse") && SystemConfig.getOptBoolean("ppsspp_mouse"))
                        ini.WriteValue("Control", "UseMouse", "True");
                    else
                        ini.WriteValue("Control", "UseMouse", "False");

                    // Audio
                    if (SystemConfig.isOptSet("ppsspp_audiobackend") && !string.IsNullOrEmpty(SystemConfig["ppsspp_audiobackend"]))
                        ini.WriteValue("Sound", "AudioBackend", SystemConfig["ppsspp_audiobackend"]);
                    else
                        ini.WriteValue("Sound", "AudioBackend", "0");

                    // System Param
                    if (SystemConfig.isOptSet("ppsspp_confirmbutton") && SystemConfig.getOptBoolean("ppsspp_confirmbutton"))
                        ini.WriteValue("SystemParam", "ButtonPreference", "0");
                    else
                        ini.WriteValue("SystemParam", "ButtonPreference", "1");

                    // Discord
                    if (SystemConfig.isOptSet("discord") && SystemConfig.getOptBoolean("discord"))
                        ini.WriteValue("General", "DiscordPresence", "True");
                    else
                        ini.WriteValue("General", "DiscordPresence", "False");

                    // Shader Set
                    if (SystemConfig.isOptSet("ppsspp_shader") && !string.IsNullOrEmpty(SystemConfig["ppsspp_shader"]))
                        ini.WriteValue("PostShaderList", "PostShader1", SystemConfig["ppsspp_shader"]);
                    else if (Features.IsSupported("ppsspp_shader"))
                        ini.ClearSection("PostShaderList");
                }
            }
            catch { }
        }
    }
}
