using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Compression;

namespace EmulatorLauncher
{
   partial class MelonDSGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _bezelsEnabled = false;
        private string _path;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("melonds");

            string exe = Path.Combine(path, "melonDS.exe");
            if (!File.Exists(exe))
                return null;

            _path = path;

            bool bootToDSINand = Path.GetExtension(rom).ToLowerInvariant() == ".bin";
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //Applying bezels (reshade is not working well and unglazed not aligned for standalone)
            if (fullscreen)
            {
                //if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                    _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
                _bezelsEnabled = true;
            }

            _resolution = resolution;
            

            // settings
            SetupConfiguration(path, rom, bootToDSINand);

            // command line parameters
            List<string> commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-f");

            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                string[] entries = Zip.ListEntries(rom).Where(e => !e.IsDirectory).Select(e => e.Filename).ToArray();
                string ndsFile = entries.Where(e => Path.GetExtension(e).ToLowerInvariant() == ".nds").FirstOrDefault();

                if (ndsFile != null)
                {
                    commandArray.Add("-a");
                    commandArray.Add("\"" + ndsFile + "\"");
                }
            }

            if (bootToDSINand)
            {
                commandArray.Add("-b");
                commandArray.Add("always");

                // Get a rom to launch
                string romPath = Path.Combine(AppConfig.GetFullPath("roms"), "nds");
                string[] files = Directory.GetFiles(romPath, "*.nds", SearchOption.AllDirectories);

                if (files.Length > 0)
                {
                    // Get the first .nds file
                    string firstNDSFile = files[0];
                    rom = firstNDSFile;
                }
                else
                {
                    throw new ApplicationException("Unable to find any .nds game to launch, at least one file is required to boot to dsi BIOS.");
                }
            }
            
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }


        private void SetupConfiguration(string path, string rom, bool bootToDSINand = false)
        {
            string settingsFile = Path.Combine(path, "melonDS.toml");

            using (IniFile ini = IniFile.FromFile(settingsFile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues))
            {
                CreateControllerConfiguration(ini);

                bool dsi = SystemConfig.isOptSet("melonds_console") && SystemConfig["melonds_console"] == "dsi";

                if (bootToDSINand)
                    dsi = true;

                ini.WriteValue("Emu", "ExternalBIOSEnable", "false");

                if (!dsi)
                    ini.WriteValue("Emu", "ConsoleType", "0");

                // Define paths
                string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds");
                if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                    catch { }
                if (!string.IsNullOrEmpty(savesPath) && Directory.Exists(savesPath))
                    ini.WriteValue("Instance0", "SaveFilePath", "\"" + savesPath.Replace("\\", "/") + "\"");

                string saveStatesPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds", "melonds", "sstates");
                if (!Directory.Exists(saveStatesPath)) try { Directory.CreateDirectory(saveStatesPath); }
                    catch { }
                if (!string.IsNullOrEmpty(saveStatesPath) && Directory.Exists(saveStatesPath))
                    ini.WriteValue("Instance0", "SavestatePath", "\"" + saveStatesPath.Replace("\\", "/") + "\"");

                string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "melonds");
                if (!Directory.Exists(cheatsPath)) try { Directory.CreateDirectory(cheatsPath); }
                    catch { }
                if (!string.IsNullOrEmpty(cheatsPath) && Directory.Exists(cheatsPath))
                    ini.WriteValue("Instance0", "CheatFilePath", "\"" + cheatsPath.Replace("\\", "/") + "\"");

                BindBoolIniFeature(ini, "Emu", "DirectBoot", "melonds_boottobios", "false", "true");

                if (SystemConfig.isOptSet("melonds_externalBIOS") && SystemConfig.getOptBoolean("melonds_externalBIOS"))
                {
                    string firmware = Path.Combine(AppConfig.GetFullPath("bios"), "firmware.bin");
                    string bios7 = Path.Combine(AppConfig.GetFullPath("bios"), "bios7.bin");
                    string bios9 = Path.Combine(AppConfig.GetFullPath("bios"), "bios9.bin");

                    if (File.Exists(firmware) && File.Exists(bios7) && File.Exists(bios9))
                        ini.WriteValue("Emu", "ExternalBIOSEnable", "true");

                    if (File.Exists(firmware))
                        ini.WriteValue("DS", "FirmwarePath", "\"" + firmware.Replace("\\", "/") + "\"");

                    if (File.Exists(bios7))
                        ini.WriteValue("DS", "BIOS7Path", "\"" + bios7.Replace("\\", "/") + "\"");

                    if (File.Exists(bios9))
                        ini.WriteValue("DS", "BIOS9Path", "\"" + bios9.Replace("\\", "/") + "\"");
                }

                if (dsi)
                {
                    ini.WriteValue("Emu", "ConsoleType", "1");
                    string dsibios9 = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_bios9.bin");
                    string dsifirmware = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_firmware.bin");
                    string dsibios7 = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_bios7.bin");
                    string dsinand = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_nand.bin");

                    if (bootToDSINand)
                    {
                        ini.WriteValue("Emu", "DirectBoot", "false");
                        ini.WriteValue("DSi", "FullBIOSBoot", "true");

                        // Copy the loaded nand to the bios folder before loading, so that multiple nand files can be used.
                        string biosPath = Path.Combine(AppConfig.GetFullPath("bios"));
                        if (!string.IsNullOrEmpty(biosPath))
                        {
                            string nandFileTarget = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_nand.bin");
                            string nandFileSource = rom;

                            if (File.Exists(nandFileTarget) && File.Exists(nandFileSource))
                                File.Delete(nandFileTarget);

                            if (File.Exists(nandFileSource))
                                File.Copy(nandFileSource, nandFileTarget);
                        }
                    }

                    if (!File.Exists(dsifirmware) || !File.Exists(dsibios7) || !File.Exists(dsibios9) || !File.Exists(dsinand))
                        throw new ApplicationException("Cannot run dsi system, dsi bios files are missing");
                    else
                    {
                        ini.WriteValue("DSi", "BIOS9Path", "\"" + dsibios9.Replace("\\", "/") + "\"");
                        ini.WriteValue("DSi", "BIOS7Path", "\"" + dsibios7.Replace("\\", "/") + "\"");
                        ini.WriteValue("DSi", "FirmwarePath", "\"" + dsifirmware.Replace("\\", "/") + "\"");
                        ini.WriteValue("DSi", "NANDPath", "\"" + dsinand.Replace("\\", "/") + "\"");
                    }
                }

                BindBoolIniFeature(ini, "Instance0", "EnableCheats", "melonds_cheats", "true", "false");
                BindIniFeature(ini, "Instance0.Firmware", "Language", "melonds_firmware_language", "1");

                ini.WriteValue("3D.Soft", "Threaded", "true");
                ini.WriteValue("", "LimitFPS", "true");
                BindIniFeature(ini, "3D", "Renderer", "melonds_renderer", "2");
                BindBoolIniFeatureOn(ini, "Screen", "VSync", "melonds_vsync", "true", "false");
                BindIniFeatureSlider(ini, "3D.GL", "ScaleFactor", "melonds_internal_resolution", "1");
                BindBoolIniFeature(ini, "3D.GL", "BetterPolygons", "melonds_polygon", "true", "false");
                BindIniFeature(ini, "Instance0.Window0", "ScreenLayout", "melonds_screen_layout", "1");
                BindBoolIniFeature(ini, "Instance0.Window0", "ScreenSwap", "melonds_swapscreen", "true", "false");
                BindIniFeature(ini, "Instance0.Window0", "ScreenSizing", "melonds_screen_sizing", "0");
                BindBoolIniFeature(ini, "Instance0.Window0", "IntegerScaling", "integerscale", "true", "false");
                
                if (_bezelsEnabled)
                {
                    if (SystemConfig.isOptSet("melonds_screen_sizing") && (SystemConfig["melonds_screen_sizing"] == "4" || SystemConfig["melonds_screen_sizing"] == "5"))
                    {
                        BindIniFeature(ini, "Instance0.Window0", "ScreenAspectTop", "melonds_ratio_top", "0");
                        BindIniFeature(ini, "Instance0.Window0", "ScreenAspectBot", "melonds_ratio_bottom", "0");
                    }
                    else
                    {
                        BindIniFeature(ini, "Instance0.Window0", "ScreenAspectTop", "melonds_ratio_top", "3");
                        BindIniFeature(ini, "Instance0.Window0", "ScreenAspectBot", "melonds_ratio_bottom", "3");
                    }
                }
                else
                {
                    BindIniFeature(ini, "Instance0.Window0", "ScreenAspectTop", "melonds_ratio_top", "0");
                    BindIniFeature(ini, "Instance0.Window0", "ScreenAspectBot", "melonds_ratio_bottom", "0");
                }
                BindIniFeatureSlider(ini, "Instance0.Window0", "ScreenGap", "melonds_screengap", "0");
                BindIniFeature(ini, "Instance0.Window0", "ScreenRotation", "melonds_rotate", "0");

                ini.Save();
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
            {
                ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, _path);
                return 0;
            }

            ReshadeManager.UninstallReshader(ReshadeBezelType.opengl, _path);

            return ret;
        }
    }
}
