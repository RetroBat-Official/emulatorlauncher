using System;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Windows.Forms;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    // Used for Visual Pinball versions prior to 10.8 - options stored in registry
    partial class VPinballGenerator : Generator
    {
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
    }
}
