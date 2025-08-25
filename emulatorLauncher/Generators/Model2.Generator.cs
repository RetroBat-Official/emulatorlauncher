﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class Model2Generator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _dinput;
        private string _destFile;
        private string _destParent;
        private string _path;
        private string _rom;

        public Model2Generator()
        {
            DependsOnDesktopResolution = false;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("m2emulator");
            if (!Directory.Exists(path))
                return null;

            _path = path;

            string exe = Path.Combine(path, "emulator_multicpu.exe");
            if (core != null && core.ToLower().Contains("singlecpu"))
                exe = Path.Combine(path, "emulator.exe");

            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            string pakDir = Path.Combine(path, "roms");
            if (!Directory.Exists(pakDir))
                Directory.CreateDirectory(pakDir);

            foreach (var file in Directory.GetFiles(pakDir))
            {
                if (Path.GetFileName(file) == Path.GetFileName(rom))
                    continue;

                File.Delete(file);
            }

            _destFile = Path.Combine(pakDir, Path.GetFileName(rom));
            if (!File.Exists(_destFile))
            {
                File.Copy(rom, _destFile);

                try { new FileInfo(_destFile).Attributes &= ~FileAttributes.ReadOnly; }
                catch { }
            }

            if (parentRoms.TryGetValue(Path.GetFileNameWithoutExtension(rom).ToLowerInvariant(), out string parentRom))
            {
                parentRom = Path.Combine(Path.GetDirectoryName(rom), parentRom + ".zip");
                _destParent = Path.Combine(pakDir, Path.GetFileName(parentRom));

                if (!File.Exists(_destParent))
                {
                    File.Copy(parentRom, _destParent);

                    try { new FileInfo(_destParent).Attributes &= ~FileAttributes.ReadOnly; }
                    catch { }
                }
            }

            _resolution = resolution;

            if (!fullscreen)
                SystemConfig["bezel"] = "none";

            if (!ReshadeManager.Setup(ReshadeBezelType.d3d9, ReshadePlatform.x86, system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _dinput = false;
            if (SystemConfig.isOptSet("m2_joystick_driver") && SystemConfig["m2_joystick_driver"] == "dinput")
                _dinput = true;

            _rom = Path.GetFileNameWithoutExtension(rom);

            SetupConfig(path, resolution, rom, fullscreen);
            SetupLUAScript(path, rom);

            string arg = Path.GetFileNameWithoutExtension(_destFile);

            return new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = arg,
                WorkingDirectory = path,
            };
        }

        private void SetupConfig(string path, ScreenResolution resolution, string rom, bool fullscreen)
        {
            string iniFile = Path.Combine(path, "Emulator.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    // rompath
                    string rompath = "..\\..\\roms\\model2";
                    ini.WriteValue("RomDirs", "Dir1", rompath);

                    ini.WriteValue("Renderer", "FullMode", "4");

                    if (fullscreen)
                        ini.WriteValue("Renderer", "AutoFull", "1");
                    else
                        ini.WriteValue("Renderer", "AutoFull", "0");

                    if (SystemConfig["bezel"] == null || SystemConfig["bezel"] == "none")
                        ini.WriteValue("Renderer", "WideScreenWindow", "1");
                    else
                        ini.WriteValue("Renderer", "WideScreenWindow", "0");

                    if (SystemConfig.isOptSet("m2_widescreen") && SystemConfig.getOptBoolean("m2_widescreen"))
                        ini.WriteValue("Renderer", "WideScreenWindow", "1");

                    ini.WriteValue("Renderer", "FullScreenWidth", (resolution == null ? Screen.PrimaryScreen.Bounds.Width : resolution.Width).ToString());
                    ini.WriteValue("Renderer", "FullScreenHeight", (resolution == null ? Screen.PrimaryScreen.Bounds.Height : resolution.Height).ToString());
                    
                    BindBoolIniFeatureOn(ini, "Renderer", "ForceSync", "VSync", "1", "0");
                    BindBoolIniFeatureOn(ini, "Renderer", "Bilinear", "bilinear_filtering", "1", "0");
                    BindBoolIniFeature(ini, "Renderer", "Trilinear", "trilinear_filtering", "1", "0");
                    BindBoolIniFeature(ini, "Renderer", "ForceManaged", "m2_ForceManaged", "1", "0");
                    BindBoolIniFeature(ini, "Renderer", "AutoMip", "m2_AutoMip", "1", "0");
                    BindBoolIniFeature(ini, "Renderer", "FSAA", "m2_fsaa", "1", "0");
                    if (SystemConfig.isOptSet("m2_crosshair") && !string.IsNullOrEmpty(SystemConfig["m2_crosshair"]))
                    {
                        if (SystemConfig["m2_crosshair"] == "0")
                            ini.WriteValue("Renderer", "DrawCross", "0");
                        else
                            ini.WriteValue("Renderer", "DrawCross", "1");
                    }
                    else
                        ini.WriteValue("Renderer", "DrawCross", "0");

                    // Input Drivers
                    if (SystemConfig.isOptSet("m2_joystick_driver") && SystemConfig["m2_joystick_driver"] == "dinput")
                        ini.WriteValue("Input", "XInput", "0");
                    else
                        ini.WriteValue("Input", "XInput", "1");

                    BindBoolIniFeature(ini, "Input", "EnableFF", "m2_force_feedback", "1", "0");
                    BindBoolIniFeature(ini, "Input", "HoldGears", "m2_HoldGears", "1", "0");
                    BindBoolIniFeature(ini, "Input", "UseRawInput", "m2_rawinput", "1", "0");

                    if (SystemConfig.isOptSet("m2_deadzone") && !string.IsNullOrEmpty(SystemConfig["m2_deadzone"]))
                    {
                        string deadzone = SystemConfig["m2_deadzone"].ToIntegerString() + "00";
                        ini.WriteValue("Input", "FE_CENTERING_Deadband", deadzone);
                    }
                    else
                    {
                        ini.WriteValue("Input", "FE_CENTERING_Deadband", "1000");
                    }

                    ConfigureInput(path, ini, rom);
                }
            }
            catch { }
        }

        private void ConfigureInput(string path, IniFile ini, string rom)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (!Controllers.Any(c => c.IsXInputDevice) && SystemConfig["m2_joystick_driver"] != "xinput")
            {
                SimpleLogger.Instance.Info("No XInput controllers found, switching to DirectInput");
                ini.WriteValue("Input", "XInput", "0");
            }

            if (!SystemConfig.getOptBoolean("use_guns") && Program.SystemConfig.isOptSet("m2_joystick_autoconfig") && Program.SystemConfig["m2_joystick_autoconfig"] == "template")
            {
                string inputCFGpath = Path.Combine(path, "CFG");
                if (!Directory.Exists(inputCFGpath)) try { Directory.CreateDirectory(inputCFGpath); }
                    catch { }

                string game = Path.GetFileNameWithoutExtension(rom).ToLowerInvariant();
                string parentGame = game;
                if (parentRoms.ContainsKey(game))
                    parentGame = parentRoms[game];

                string sourceInputCfgFile = Path.Combine(path, "templatescfg", "xinput", parentGame + ".input");
                if (_dinput)
                    sourceInputCfgFile = Path.Combine(path, "templatescfg", "dinput", parentGame + ".input");

                string targetInputCfgFile = Path.Combine(inputCFGpath, game + ".input");

                if (File.Exists(targetInputCfgFile))
                    File.Delete(targetInputCfgFile);

                if (File.Exists(sourceInputCfgFile))
                    File.Copy(sourceInputCfgFile, targetInputCfgFile);
            }
            else
            {
                string inputCFGpath = Path.Combine(path, "CFG");
                if (!Directory.Exists(inputCFGpath)) try { Directory.CreateDirectory(inputCFGpath); }
                    catch { }

                string inputFilename = Path.GetFileNameWithoutExtension(rom);
                string inputFile = Path.Combine(inputCFGpath, inputFilename + ".input");

                string parentRom = parentRoms.ContainsKey(inputFilename) ? parentRoms[inputFilename] : inputFilename;
                int hexLength = 107;
                if (byteLength.ContainsKey(parentRom))
                    hexLength = byteLength[parentRom];

                byte[] bytes;

                SimpleLogger.Instance.Info("[INFO] Configuring input file " + inputFile);

                if (File.Exists(inputFile))
                    bytes = File.ReadAllBytes(inputFile);
                else
                    bytes = new byte[hexLength];

                ConfigureControllers(bytes, ini, parentRom);

                SimpleLogger.Instance.Info("[INFO] Saving input file " + inputFile);
                File.WriteAllBytes(inputFile, bytes);
            }
        }

        public override void Cleanup()
        {
            if (_sindenSoft)
                Guns.KillSindenSoftware();

            if (_demulshooter)
                Demulshooter.KillDemulShooter();

            if (_destFile != null && File.Exists(_destFile))
                File.Delete(_destFile);

            if (_destParent != null && File.Exists(_destParent))
                File.Delete(_destParent);

            base.Cleanup();
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
                        IntPtr hWnd = User32.FindHwnds(px.Id).FirstOrDefault(h => User32.GetClassName(h) == "MYWIN");
                        if (hWnd != IntPtr.Zero)
                        {
                            var style = User32.GetWindowStyle(hWnd);
                            if (style.HasFlag(WS.CAPTION))
                            {
                                int resX = (_resolution == null ? Screen.PrimaryScreen.Bounds.Width : _resolution.Width);
                                int resY = (_resolution == null ? Screen.PrimaryScreen.Bounds.Height : _resolution.Height);

                                User32.SetWindowStyle(hWnd, style & ~WS.CAPTION);
                                User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, resX, resY, SWP.NOZORDER | SWP.FRAMECHANGED);
                                User32.SetMenu(hWnd, IntPtr.Zero);

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

        static readonly Dictionary<string, string> parentRoms = new Dictionary<string, string>()
        { 
            // Daytona USA
            { "dayton93", "daytona" },
            { "daytonam", "daytona" },
            { "daytonas", "daytona" },
            { "daytonase", "daytona" },
            { "daytonat", "daytona" },
            { "daytonata", "daytona" },
            { "daytonagtx", "daytona" },
            // Dead Or Alive
            { "doaa", "doa" },
            // Dynamite Cop
            { "dynmcopb", "dynamcop" },
            { "dynmcopc", "dynamcop" },
            { "dyndeka2", "dynamcop" },
            { "dyndek2b", "dynamcop" },
            // Indianapolis 500
            { "indy500d", "indy500" },
            { "indy500to", "indy500" },
            // Last Bronx
            { "lastbrnxj", "lastbrnx" },
            { "lastbrnxu", "lastbrnx" },
            // Pilot Kids
            { "pltkidsa", "pltkids" },            
            // Over Rev
            { "overrevb", "overrev" }, 
            // Sega Rally Championship
            { "srallycb", "srallyc" },
            { "srallyp", "srallyc" },
            // Sega Touring Car Championship
            { "stcca", "stcc" },
            { "stccb", "stcc" },
            // Top Skater
            { "topskatrj", "topskatr" },
            { "topskatru", "topskatr" },
            // Sonic The Fighters
            { "sfight", "schamp" },            
            // Virtua Cop
            { "vcopa", "vcop" },            
            // Virtua Fighter 2
            { "vf2o", "vf2" },
            { "vf2a", "vf2" },
            { "vf2b", "vf2" },
            // Virtua Striker
            { "vstrikro", "vstriker" },
            // Virtual On Cybertroopers
            { "vonj", "von" },
            // Zero Gunner
            { "zerogunaj", "zerogun" },
            { "zerogunj", "zerogun" },
            { "zeroguna", "zerogun" },
        };

        static readonly Dictionary<string, int> byteLength = new Dictionary<string, int>()
        {
            { "bel", 108 },
            { "daytona", 104 },
            { "desert", 100 },
            { "doa", 108 },
            { "dynabb97", 116 },
            { "dynamcop", 108 },
            { "fvipers", 108 },
            { "gunblade", 108 },
            { "hotd", 108 },
            { "indy500", 80 },
            { "lastbrnx", 108 },
            { "manxtt", 76 },
            { "manxttc", 76 },
            { "motoraid", 76 },
            { "overrev", 80 },
            { "pltkids", 108 },
            { "rchase2", 108 },
            { "schamp", 108 },
            { "segawski", 68 },
            { "sgt24h", 80 },
            { "skisuprg", 76 },
            { "skytargt", 76 },
            { "srallyc", 92 },
            { "stcc", 80 },
            { "topskatr", 76 },
            { "vcop", 108 },
            { "vcop2", 108 },
            { "vf2", 108 },
            { "von", 84 },
            { "vstriker", 108 },
            { "waverunr", 72 },
            { "zerogun", 108 }
        };

        private void SetupLUAScript(string path, string rom)
        {
            string luaFile = Path.Combine(path, "scripts", Path.GetFileNameWithoutExtension(rom) + ".lua");

            if (File.Exists(luaFile))
            {
                try
                {
                    var lines = File.ReadAllLines(luaFile);

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];

                        if (lines[i].Contains("wide=false") || lines[i].Contains("wide=true"))
                        {
                            if (SystemConfig.getOptBoolean("m2_widescreen"))
                                lines[i] = line.Replace("wide=false", "wide=true");
                            else
                                lines[i] = line.Replace("wide=true", "wide=false");

                            break;
                        }
                            
                    }

                    File.WriteAllLines(luaFile, lines);
                }
                catch { }
            }
        }
    }
}
