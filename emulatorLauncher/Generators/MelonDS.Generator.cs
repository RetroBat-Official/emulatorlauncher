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

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("melonds");

            string exe = Path.Combine(path, "melonDS.exe");
            if (!File.Exists(exe))
                return null;

            bool bootToDSINand = Path.GetExtension(rom).ToLowerInvariant() == ".bin";

            //Applying bezels
            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);
            else
                _bezelsEnabled = true;

            _resolution = resolution;
            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            // settings
            SetupConfiguration(path, rom, bootToDSINand);

            // command line parameters
            var commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-f");

            if (Path.GetExtension(rom).ToLowerInvariant() == ".zip")
            {
                var entries = Zip.ListEntries(rom).Where(e => !e.IsDirectory).Select(e => e.Filename).ToArray();
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
            }
            else
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
            string settingsFile = Path.Combine(path, "melonDS.ini");

            using (var ini = IniFile.FromFile(settingsFile))
            {
                CreateControllerConfiguration(ini);

                bool dsi = SystemConfig.isOptSet("melonds_console") && SystemConfig["melonds_console"] == "dsi";

                if (bootToDSINand)
                    dsi = true;

                ini.WriteValue("", "ExternalBIOSEnable", "0");

                if (!dsi)
                    ini.WriteValue("", "ConsoleType", "0");

                // Define paths
                string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds");
                if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                    catch { }
                if (!string.IsNullOrEmpty(savesPath) && Directory.Exists(savesPath))
                    ini.WriteValue("", "SaveFilePath", savesPath.Replace("\\", "/"));

                string saveStatesPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds", "melonds", "sstates");
                if (!Directory.Exists(saveStatesPath)) try { Directory.CreateDirectory(saveStatesPath); }
                    catch { }
                if (!string.IsNullOrEmpty(saveStatesPath) && Directory.Exists(saveStatesPath))
                    ini.WriteValue("", "SavestatePath", saveStatesPath.Replace("\\", "/"));

                string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "melonds");
                if (!Directory.Exists(cheatsPath)) try { Directory.CreateDirectory(cheatsPath); }
                    catch { }
                if (!string.IsNullOrEmpty(cheatsPath) && Directory.Exists(cheatsPath))
                    ini.WriteValue("", "CheatFilePath", cheatsPath.Replace("\\", "/"));

                BindBoolIniFeature(ini, "", "DirectBoot", "melonds_boottobios", "0", "1");

                if (SystemConfig.isOptSet("melonds_externalBIOS") && SystemConfig.getOptBoolean("melonds_externalBIOS") && !dsi)
                {
                    string firmware = Path.Combine(AppConfig.GetFullPath("bios"), "firmware.bin");
                    string bios7 = Path.Combine(AppConfig.GetFullPath("bios"), "bios7.bin");
                    string bios9 = Path.Combine(AppConfig.GetFullPath("bios"), "bios9.bin");

                    if (File.Exists(firmware) && File.Exists(bios7) && File.Exists(bios9))
                        ini.WriteValue("", "ExternalBIOSEnable", "1");

                    if (File.Exists(firmware))
                        ini.WriteValue("", "FirmwarePath", firmware.Replace("\\", "/"));

                    if (File.Exists(bios7))
                        ini.WriteValue("", "BIOS7Path", bios7.Replace("\\", "/"));

                    if (File.Exists(bios9))
                        ini.WriteValue("", "BIOS9Path", bios9.Replace("\\", "/"));
                }

                if (dsi)
                {
                    ini.WriteValue("", "ConsoleType", "1");
                    string dsibios9 = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_bios9.bin");
                    string dsifirmware = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_firmware.bin");
                    string dsibios7 = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_bios7.bin");
                    string dsinand = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_nand.bin");

                    if (bootToDSINand)
                    {
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
                        ini.WriteValue("", "DSiBIOS9Path", dsibios9.Replace("\\", "/"));
                        ini.WriteValue("", "DSiBIOS7Path", dsibios7.Replace("\\", "/"));
                        ini.WriteValue("", "DSiFirmwarePath", dsifirmware.Replace("\\", "/"));
                        ini.WriteValue("", "DSiNANDPath", dsinand.Replace("\\", "/"));
                    }
                }

                BindBoolIniFeature(ini, "", "EnableCheats", "melonds_cheats", "1", "0");
                BindIniFeature(ini, "", "FirmwareLanguage", "melonds_firmware_language", "1");

                ini.WriteValue("", "Threaded3D", "1");
                ini.WriteValue("", "LimitFPS", "1");
                BindIniFeature(ini, "", "3DRenderer", "melonds_renderer", "1");
                BindBoolIniFeature(ini, "", "ScreenVSync", "melonds_vsync", "0", "1");
                BindIniFeature(ini, "", "GL_ScaleFactor", "melonds_internal_resolution", "1");
                BindBoolIniFeature(ini, "", "GL_BetterPolygons", "melonds_polygon", "1", "0");
                BindIniFeature(ini, "", "ScreenLayout", "melonds_screen_layout", "1");
                BindBoolIniFeature(ini, "", "ScreenSwap", "melonds_swapscreen", "1", "0");
                BindIniFeature(ini, "", "ScreenSizing", "melonds_screen_sizing", "0");
                BindBoolIniFeature(ini, "", "IntegerScaling", "integerscale", "1", "0");
                
                if (_bezelsEnabled)
                {
                    BindIniFeature(ini, "", "ScreenAspectTop", "melonds_ratio_top", "3");
                    BindIniFeature(ini, "", "ScreenAspectBot", "melonds_ratio_bottom", "3");
                }
                else
                {
                    BindIniFeature(ini, "", "ScreenAspectTop", "melonds_ratio_top", "0");
                    BindIniFeature(ini, "", "ScreenAspectBot", "melonds_ratio_bottom", "0");
                }
                BindIniFeature(ini, "", "ScreenGap", "melonds_screengap", "0");
                BindIniFeature(ini, "", "ScreenRotation", "melonds_rotate", "0");
            }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
