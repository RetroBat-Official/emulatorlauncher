using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace EmulatorLauncher
{
   partial class DesmumeGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private SaveStatesWatcher _saveStatesWatcher;
        private ScreenResolution _resolution;
        private bool _pad2Keyoverride = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath(emulator);

            string exe = Path.Combine(path, "DeSmuME-VS2022-x64-Release.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            //Applying bezels
            if (fullscreen)
            {
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            }
            _resolution = resolution;

            // Savestate
            int saveStateSlot = 0;
            if (Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
                string emulatorPath = Path.Combine(path, "StateSlots");

                _saveStatesWatcher = new DesmumeSaveStatesMonitor(rom, emulatorPath, localPath, Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png"));
                _saveStatesWatcher.PrepareEmulatorRepository();
                saveStateSlot = _saveStatesWatcher.Slot;
            }
            else
                _saveStatesWatcher = null;

            // settings
            SetupConfiguration(path, rom);

            // command line parameters
            List<string> commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("--windowed-fullscreen");

            if (_saveStatesWatcher != null && !string.IsNullOrEmpty(SystemConfig["state_file"]) && File.Exists(SystemConfig["state_file"]))
            {
                commandArray.Add("--load-slot");
                commandArray.Add(saveStateSlot.ToString());
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
            string settingsFile = Path.Combine(path, "desmume.ini");

            using (IniFile ini = IniFile.FromFile(settingsFile, IniOptions.KeepEmptyValues))
            {
                CreateControllerConfiguration(ini);

                ini.WriteValue("Console", "Show", "0");

                // BIOS and firmware
                ini.WriteValue("BIOS", "UseExtBIOS", "0");
                if (SystemConfig.isOptSet("desmume_UseExtBIOS") && SystemConfig.getOptBoolean("desmume_UseExtBIOS"))
                {
                    string bios7 = Path.Combine(AppConfig.GetFullPath("bios"), "bios7.bin");
                    string bios9 = Path.Combine(AppConfig.GetFullPath("bios"), "bios9.bin");

                    if (File.Exists(bios7) && File.Exists(bios9))
                    {
                        ini.WriteValue("BIOS", "ARM9BIOSFile", bios9);
                        ini.WriteValue("BIOS", "ARM7BIOSFile", bios7);
                        ini.WriteValue("BIOS", "UseExtBIOS", "1");
                    }
                }

                ini.WriteValue("Firmware", "UseExtFirmware", "0");
                if (SystemConfig.isOptSet("desmume_UseExtFirmware") && SystemConfig.getOptBoolean("desmume_UseExtFirmware"))
                {
                    string firmware = Path.Combine(AppConfig.GetFullPath("bios"), "firmware.bin");

                    if (SystemConfig.isOptSet("desmume_firmwareFile") && !string.IsNullOrEmpty(SystemConfig["desmume_firmwareFile"]))
                    {
                        string newFirmware = SystemConfig["desmume_firmwareFile"];
                        if (File.Exists(newFirmware))
                            firmware = newFirmware;
                    }

                    if (File.Exists(firmware))
                    {
                        ini.WriteValue("Firmware", "FirmwareFile", firmware);
                        ini.WriteValue("Firmware", "UseExtFirmware", "1");
                        if (SystemConfig.isOptSet("desmume_BootFromFirmware") && SystemConfig.getOptBoolean("desmume_BootFromFirmware"))
                            ini.WriteValue("Firmware", "BootFromFirmware", "1");
                    }
                }
                BindBoolIniFeature(ini, "BIOS", "PatchSWI3", "desmume_PatchSWI3", "1", "0");

                // Define paths
                string savesPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds", "DeSmuME");
                if (!Directory.Exists(savesPath)) try { Directory.CreateDirectory(savesPath); }
                    catch { }
                if (!string.IsNullOrEmpty(savesPath) && Directory.Exists(savesPath))
                    ini.WriteValue("PathSettings", "Battery", savesPath);

                bool newSaveStates = Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported("desmume");
                if (!newSaveStates)
                {
                    string saveStatesPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds", "DeSmuME", "states");
                    if (!Directory.Exists(saveStatesPath)) try { Directory.CreateDirectory(saveStatesPath); }
                        catch { }
                    if (!string.IsNullOrEmpty(saveStatesPath) && Directory.Exists(saveStatesPath))
                        ini.WriteValue("PathSettings", "States", saveStatesPath);

                    string saveStatesSlotsPath = Path.Combine(AppConfig.GetFullPath("saves"), "nds", "DeSmuME", "stateslots");
                    if (!Directory.Exists(saveStatesSlotsPath)) try { Directory.CreateDirectory(saveStatesSlotsPath); }
                        catch { }
                    if (!string.IsNullOrEmpty(saveStatesSlotsPath) && Directory.Exists(saveStatesSlotsPath))
                        ini.WriteValue("PathSettings", "StateSlots", saveStatesSlotsPath);
                }
                else
                {
                    ini.WriteValue("PathSettings", "States", ".\\States");
                    ini.WriteValue("PathSettings", "StateSlots", ".\\StateSlots");
                }

                string cheatsPath = Path.Combine(AppConfig.GetFullPath("cheats"), "desmume");
                if (!Directory.Exists(cheatsPath)) try { Directory.CreateDirectory(cheatsPath); }
                    catch { }
                if (!string.IsNullOrEmpty(cheatsPath) && Directory.Exists(cheatsPath))
                    ini.WriteValue("PathSettings", "Cheats", cheatsPath);

                string screenshotsPath = Path.Combine(AppConfig.GetFullPath("screenshots"), "DeSmuME");
                if (!Directory.Exists(screenshotsPath)) try { Directory.CreateDirectory(screenshotsPath); }
                    catch { }
                if (!string.IsNullOrEmpty(screenshotsPath) && Directory.Exists(screenshotsPath))
                    ini.WriteValue("PathSettings", "Screenshots", screenshotsPath);

                string aviPath = Path.Combine(AppConfig.GetFullPath("records"), "output", "DeSmuME");
                if (!Directory.Exists(aviPath)) try { Directory.CreateDirectory(aviPath); }
                    catch { }
                if (!string.IsNullOrEmpty(aviPath) && Directory.Exists(aviPath))
                    ini.WriteValue("PathSettings", "AviFiles", aviPath);

                // Frameskipping
                bool manualSkip = SystemConfig.isOptSet("desmume_frameskip_type") && SystemConfig["desmume_frameskip_type"] == "manual";
                if (SystemConfig.isOptSet("desmume_FrameSkip") && !string.IsNullOrEmpty(SystemConfig["desmume_FrameSkip"]))
                {
                    string frameskipRatio = SystemConfig["desmume_FrameSkip"];
                    if (manualSkip)
                        ini.WriteValue("Video", "FrameSkip", frameskipRatio);
                    else
                        ini.WriteValue("Video", "FrameSkip", "AUTO" + frameskipRatio);
                }
                else
                    ini.WriteValue("Video", "FrameSkip", "AUTO0");

                // Video options
                BindIniFeature(ini, "Video", "LCDsLayout", "desmume_layout", "0");
                BindBoolIniFeature(ini, "Video", "LCDsSwap", "desmume_LCDsSwap", "1", "0");
                BindBoolIniFeature(ini, "Video", "Window Pad To Integer", "integerscale", "1", "0");
                BindBoolIniFeatureOn(ini, "Video", "VSync", "VSync", "1", "0");
                BindBoolIniFeatureOn(ini, "Video", "Window Force Ratio", "desmume_forceratio", "1", "0");
                BindBoolIniFeatureOn(ini, "Video", "Display Method Filter", "smooth", "1", "0");
                BindIniFeature(ini, "Video", "Window Rotate Set", "desmume_rotate", "0");
                BindIniFeature(ini, "Video", "Filter", "desmume_Filter", "0");
                BindIniFeature(ini, "Video", "Display Method", "desmume_display_method", "3");
                
                // Emulation
                BindIniFeature(ini, "Emulation", "LCDsLayout", "desmume_layout", "0");

                // 3D settings
                BindIniFeature(ini, "3D", "Renderer", "desmume_Renderer", "2");
                BindBoolIniFeature(ini, "3D", "EnableTXTHack", "desmume_EnableTXTHack", "1", "0");
                BindBoolIniFeature(ini, "3D", "EnableLineHack", "desmume_EnableLineHack", "1", "0");
                BindBoolIniFeatureOn(ini, "3D", "TextureSmooth", "desmume_TextureSmooth", "1", "0");
                BindIniFeature(ini, "3D", "MultisampleSize", "desmume_MultisampleSize", "0");
                BindIniFeature(ini, "3D", "PrescaleHD", "desmume_PrescaleHD", "1");
                BindIniFeature(ini, "3D", "GpuBpp", "desmume_GpuBpp", "24");
                BindIniFeature(ini, "3D", "TextureScalingFactor", "desmume_TextureScalingFactor", "1");
                BindBoolIniFeatureOn(ini, "3D", "EnableDepthLEqualPolygonFacing", "desmume_EnableDepthLEqualPolygonFacing", "1", "0");

                // Audio settings
                BindIniFeature(ini, "Sound", "SoundCore2", "desmume_SoundCore2", "2");

                // Display
                BindBoolIniFeature(ini, "Display", "Display Fps", "desmume_fps", "1", "0");
                BindBoolIniFeature(ini, "Display", "Show Menu In Fullscreen Mode", "desmume_menu", "1", "0");
                BindIniFeature(ini, "Display", "Screen Size Ratio", "desmume_hratio", "1.0");
                ini.WriteValue("Display", "Show Toolbar", "0");

                ini.Save();
            }
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
                PadToKey.AddOrUpdateKeyMapping(mapping, "DeSmuME-VS2022-x64-Release", InputKey.hotkey | InputKey.start, "(%{CLOSE})");
            }

            return mapping;
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
                return 0;
            }

            return ret;
        }

        public override void Cleanup()
        {
            if (_saveStatesWatcher != null)
            {
                _saveStatesWatcher.Dispose();
                _saveStatesWatcher = null;
            }

            base.Cleanup();
        }
    }
}
