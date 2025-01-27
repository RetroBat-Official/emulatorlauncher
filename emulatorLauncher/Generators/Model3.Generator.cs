﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class Model3Generator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        private void ConfigureMameHook()
        {
            // Kill MameHook if option is not enabled
            if (!SystemConfig.isOptSet("use_mamehooker") || !SystemConfig.getOptBoolean("use_mamehooker"))
            {
                SimpleLogger.Instance.Info("[INFO] MameHook option not enabled, killing any running instance");
                MameHooker.KillMameHooker();
                return;
            }

            // Get ROM name
            string romName = Path.GetFileNameWithoutExtension(SystemConfig["rom"]).ToLower();
            SimpleLogger.Instance.Info($"[INFO] ROM Name: {romName}");

            // Configure Model3 specific MameHooker settings
            if (!MameHooker.Model3.ConfigureModel3(romName))
                return;

            // Start MameHooker process after config update
            Process mameHookProcess = MameHooker.StartMameHooker();
            if (mameHookProcess == null)
            {
                SimpleLogger.Instance.Error("[ERROR] Failed to start MameHooker");
                return;
            }

            // Wait for MameHook to start
            SimpleLogger.Instance.Info("[INFO] Waiting for MameHook to initialize");
            mameHookProcess.WaitForInputIdle(2000); // Wait up to 2 seconds for the process to be ready
            System.Threading.Thread.Sleep(2000); // Additional wait to ensure full initialization
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            ConfigureMameHook();

            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("supermodel");

            string exe = Path.Combine(path, "supermodel.exe");            
            if (!File.Exists(exe))
                return null;

            bool isWideScreen;

            List<string> commandArray = new List<string>();

            if (resolution != null)
            {
                isWideScreen = ((float)resolution.Width / (float)resolution.Height) > 1.75f;
                commandArray.Add("-res=" + resolution.Width + "," + resolution.Height);
            }
            else
            {
                isWideScreen = ((float)Screen.PrimaryScreen.Bounds.Width / (float)Screen.PrimaryScreen.Bounds.Height) >= 1.75f;
                commandArray.Add("-res=" + Screen.PrimaryScreen.Bounds.Width + "," + Screen.PrimaryScreen.Bounds.Height);
            }

            _resolution = resolution;

            bool wideScreen = SystemConfig["widescreen"] == "1" || SystemConfig["widescreen"] == "2" || (!SystemConfig.isOptSet("widescreen") && isWideScreen);
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if ((fullscreen && !SystemConfig.isOptSet("videomode")) || SystemConfig.getOptBoolean("forcefullscreen"))
                commandArray.Add("-fullscreen");

            if (wideScreen)
            {
                SystemConfig["forceNoBezel"] = "1";

                ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator);

                if (SystemConfig["widescreen"] == "2")
                    commandArray.Add("-stretch");
                else
                    commandArray.Add("-wide-screen");
            }
            else
            {
                if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }

            // outputs for mamehook
            if (SystemConfig.isOptSet("supermodel_output") && !string.IsNullOrEmpty(SystemConfig["supermodel_output"]))
                commandArray.Add("-outputs=" + SystemConfig["supermodel_output"]);

            // crosshairs
            if (SystemConfig.isOptSet("crosshairs"))
                commandArray.Add("-crosshairs=" + SystemConfig["crosshairs"]);
            
            // force feedback
            if (SystemConfig.isOptSet("forceFeedback") && SystemConfig.getOptBoolean("forceFeedback"))
                commandArray.Add("-force-feedback");

            // SuperSampling
            if (SystemConfig.isOptSet("m3_supersampling") && !string.IsNullOrEmpty(SystemConfig["m3_supersampling"]) && SystemConfig["m3_supersampling"].ToIntegerString() != "0")
                commandArray.Add("-ss=" + SystemConfig["m3_supersampling"].ToIntegerString());

            //Write config in supermodel.ini
            SetupConfiguration(path, wideScreen, fullscreen);

            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = path,                
            };            
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            try
            {
                var px = Process.Start(path);

                while (!px.HasExited)
                {
                    if (px.WaitForExit(10))
                        break;

                    if (_bezelFileInfo != null)
                    {
                        IntPtr hWnd = User32.FindHwnds(px.Id).FirstOrDefault(h => User32.GetWindowText(h).StartsWith("Supermodel"));
                        if (hWnd != IntPtr.Zero)
                        {
                            var style = User32.GetWindowStyle(hWnd);
                            if (style.HasFlag(WS.CAPTION))
                            {
                                int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                                int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                                User32.SetWindowStyle(hWnd, style & ~WS.CAPTION);
                                User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);

                                if (_bezelFileInfo != null && bezel == null)
                                    bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
                            }
                        }
                    }
                    Application.DoEvents();
                }
                return px.ExitCode;
            }
            catch { }
            finally
            {
                bezel?.Dispose();
            }
            return -1;
        }
        private void SetupConfiguration(string path, bool wideScreen, bool fullscreen)
        {
            try
            {
                string iniPath = Path.Combine(path, "Config", "Supermodel.ini");
                if (File.Exists(iniPath))
                {
                    using (IniFile ini = new IniFile(iniPath, IniOptions.UseSpaces))
                    {
                        //Fullscreen and widescreen values (should we keep these as commandline take precedent ?
                        ini.WriteValue(" Global ", "BorderlessWindow", "True");
                        if ((fullscreen && !SystemConfig.isOptSet("videomode")) || SystemConfig.getOptBoolean("forcefullscreen"))
                            ini.WriteValue(" Global ", "FullScreen", "1");
                        else
                        {
                            ini.WriteValue(" Global ", "FullScreen", "0");
                            ini.WriteValue(" Global ", "XResolution", _resolution == null ? Screen.PrimaryScreen.Bounds.Width.ToString() : _resolution.Width.ToString());
                            ini.WriteValue(" Global ", "YResolution", _resolution == null ? Screen.PrimaryScreen.Bounds.Height.ToString() : _resolution.Height.ToString());
                            ini.WriteValue(" Global ", "WindowXPosition", "0");
                            ini.WriteValue(" Global ", "WindowYPosition", "0");
                        }

                        ini.WriteValue(" Global ", "WideScreen", wideScreen ? "1" : "0");

                        BindIniFeatureSlider(ini, " Global ", "Crosshairs", "crosshairs", "1");
                        BindBoolIniFeatureOn(ini, " Global ", "Throttle", "throttle", "1", "0");          //throttle - default on
                        BindBoolIniFeatureOn(ini, " Global ", "New3DEngine", "new3Dengine", "1", "0");    //New3DEngine - setting to OFF will use legacy 3D engine, fixes OpenGL error on older GPUs
                        BindBoolIniFeature(ini, " Global ", "MultiThreaded", "m3_thread", "0", "1");
                        BindIniFeatureSlider(ini, " Global ", "PowerPCFrequency", "m3_ppc_frequency", "50");
                        BindBoolIniFeature(ini, " Global ", "ShowFrameRate", "m3_fps", "1", "0");
                        BindBoolIniFeature(ini, " Global ", "WideBackground", "widescreen", "true", "false");
                        BindBoolIniFeature(ini, " Global ", "QuadRendering", "quadRendering", "true", "false");
                        BindBoolIniFeatureOn(ini, " Global ", "VSync", "m3_vsync", "true", "false");

                        if (SystemConfig.isOptSet("supermodel_output") && !string.IsNullOrEmpty(SystemConfig["supermodel_output"]))
                            ini.WriteValue(" Global ", "Outputs", SystemConfig["supermodel_output"]);
                        else
                            ini.Remove(" Global ", "Outputs");

                        //force rompath in GUI
                        string rompath = Path.Combine(AppConfig.GetFullPath("roms"), "model3");
                        ini.WriteValue(" Supermodel3 UI ", "Dir", rompath);
                        
                        //Create controller configuration
                        CreateControllerConfiguration(ini);
                    }
                }
            }
            catch { }
        }
    }
}
