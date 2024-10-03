using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class FlycastGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _fullscreen;
        private string _romName;
        private bool _isArcade;
        private SaveStatesWatcher _saveStatesWatcher;
        private int _saveStateSlot;

        public FlycastGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("flycast");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "flycast.exe");
            if (!File.Exists(exe))
                return null;

            _isArcade = (system != "dreamcast" && system != "dc");
            _romName = Path.GetFileNameWithoutExtension(rom);
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            bool wide = SystemConfig.isOptSet("flycast_ratio") && SystemConfig["flycast_ratio"] != "normal";

            //Applying bezels
            if (_fullscreen && !wide)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            if (!_isArcade && Program.HasEsSaveStates && Program.EsSaveStates.IsEmulatorSupported(emulator))
            {
                string localPath = Program.EsSaveStates.GetSavePath(system, emulator, core);
                string emulatorPath = Path.Combine(path, "data");

                _saveStatesWatcher = new FlycastSaveStatesMonitor(rom, emulatorPath, localPath, Path.Combine(AppConfig.GetFullPath("retrobat"), "system", "resources", "savestateicon.png"));
                _saveStatesWatcher.PrepareEmulatorRepository();
                _saveStateSlot = _saveStatesWatcher.Slot != -1 ? (_saveStatesWatcher.Slot != 0 ? _saveStatesWatcher.Slot : 1) : 1;
            }
            else
                _saveStatesWatcher = null;

            SetupConfiguration(path, system, resolution);

            List<string> commandArray = new List<string>
            {
                "\"" + rom + "\""
            };

            string args = string.Join(" ", commandArray);

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };
        }

        //Configuration file emu.cfg
        private void SetupConfiguration(string path, string system, ScreenResolution resolution = null)
        {
            string configfile = Path.Combine(path, "emu.cfg");

            SimpleLogger.Instance.Info("[INFO] Writing configuration to 'emu.cfg' file.");

            using (var ini = IniFile.FromFile(configfile, IniOptions.UseSpaces | IniOptions.KeepEmptyValues | IniOptions.KeepEmptyLines))
            {
                // RetroAchievements
                if (Features.IsSupported("cheevos") && SystemConfig.getOptBoolean("retroachievements"))
                {
                    ini.WriteValue("achievements", "Enabled", "yes");
                    ini.WriteValue("achievements", "HardcoreMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "yes" : "no");

                    // Inject credentials
                    if (SystemConfig.isOptSet("retroachievements.username") && SystemConfig.isOptSet("retroachievements.token"))
                    {
                        ini.WriteValue("achievements", "Token", SystemConfig["retroachievements.token"]);
                        ini.WriteValue("achievements", "UserName", SystemConfig["retroachievements.username"]);
                    }
                }
                else
                {
                    ini.WriteValue("achievements", "Enabled", "no");
                    ini.WriteValue("achievements", "HardcoreMode", "no");
                    ini.WriteValue("achievements", "Token", null);
                    ini.WriteValue("achievements", "UserName", null);
                }

                // General
                BindIniFeature(ini, "config", "Dreamcast.Language", "flycast_language", "6");
                BindIniFeature(ini, "config", "Dreamcast.Broadcast", "flycast_broadcast", "4");
                BindIniFeature(ini, "config", "Dreamcast.Region", "flycast_region", "3");
                BindIniFeature(ini, "config", "Dreamcast.Cable", "flycast_cable", "0");
                ini.WriteValue("config", "Dreamcast.HideLegacyNaomiRoms", "yes");
                BindBoolIniFeatureOn(ini, "config", "ForceFreePlay", "flycast_freeplay", "yes", "no");

                // Autoload savestate
                if (_saveStatesWatcher != null && !string.IsNullOrEmpty(SystemConfig["state_file"]) && File.Exists(SystemConfig["state_file"]))
                {
                    ini.WriteValue("config", "Dreamcast.AutoLoadState", "yes");
                    ini.WriteValue("config", "Dreamcast.SavestateSlot", _saveStateSlot.ToString());
                }
                else if (_saveStatesWatcher != null)
                {
                    ini.WriteValue("config", "Dreamcast.AutoLoadState", "no");
                    ini.WriteValue("config", "Dreamcast.SavestateSlot", _saveStateSlot.ToString());
                }
                else
                {
                    ini.WriteValue("config", "Dreamcast.AutoLoadState", "no");
                    ini.WriteValue("config", "Dreamcast.SavestateSlot", "1");
                }

                if (SystemConfig.isOptSet("autosave") && SystemConfig.getOptBoolean("autosave"))
                {
                    ini.WriteValue("config", "Dreamcast.AutoSaveState", "yes");
                }
                else
                {
                    ini.WriteValue("config", "Dreamcast.AutoSaveState", "no");
                }

                string dcRomsPath = Path.Combine(AppConfig.GetFullPath("roms"), "dreamcast");
                string naomiRomsPath = Path.Combine(AppConfig.GetFullPath("roms"), "naomi");
                string naomi2RomsPath = Path.Combine(AppConfig.GetFullPath("roms"), "naomi2");
                string atomiwaveRomsPath = Path.Combine(AppConfig.GetFullPath("roms"), "atomiswave");

                ini.Remove("config", "Dreamcast.ContentPath");
                ini.WriteValue("config", "Dreamcast.ContentPath", dcRomsPath + ";" + naomiRomsPath + ";" + naomi2RomsPath + ";" + atomiwaveRomsPath);

                // video
                if (_fullscreen)
                    ini.WriteValue("window", "fullscreen", "yes");
                else
                {
                    ini.WriteValue("window", "fullscreen", "no");
                    if (resolution == null)
                    {
                        var res = ScreenResolution.CurrentResolution;
                        ini.WriteValue("window", "height", res.Height.ToString());
                        ini.WriteValue("window", "width", res.Width.ToString());
                    }
                    else
                    {
                        ini.WriteValue("window", "height", resolution.Height.ToString());
                        ini.WriteValue("window", "width", resolution.Width.ToString());
                    }
                }

                if (SystemConfig.isOptSet("flycast_transparent_sorting") && SystemConfig["flycast_transparent_sorting"] == "triangle")
                {
                    if (SystemConfig["flycast_video"] == "dx9")
                        ini.WriteValue("config", "pvr.rend", "1");
                    else if (SystemConfig["flycast_video"] == "dx11")
                        ini.WriteValue("config", "pvr.rend", "2");
                    else if (SystemConfig["flycast_video"] == "opengl")
                        ini.WriteValue("config", "pvr.rend", "0");
                    else
                        ini.WriteValue("config", "pvr.rend", "4");

                    ini.WriteValue("config", "rend.PerStripSorting", "no");
                }
                else if (SystemConfig.isOptSet("flycast_transparent_sorting") && SystemConfig["flycast_transparent_sorting"] == "strip")
                {
                    if (SystemConfig["flycast_video"] == "dx9")
                        ini.WriteValue("config", "pvr.rend", "1");
                    else if (SystemConfig["flycast_video"] == "dx11")
                        ini.WriteValue("config", "pvr.rend", "2");
                    else if (SystemConfig["flycast_video"] == "opengl")
                        ini.WriteValue("config", "pvr.rend", "0");
                    else
                        ini.WriteValue("config", "pvr.rend", "4");

                    ini.WriteValue("config", "rend.PerStripSorting", "yes");
                }
                else
                {
                    if (SystemConfig["flycast_video"] == "dx9")
                    {
                        ini.WriteValue("config", "pvr.rend", "1");
                        ini.WriteValue("config", "rend.PerStripSorting", "no");
                    }
                    else if (SystemConfig["flycast_video"] == "dx11")
                    {
                        ini.WriteValue("config", "pvr.rend", "6");
                        ini.WriteValue("config", "rend.PerStripSorting", "yes");
                    }
                    else if (SystemConfig["flycast_video"] == "opengl")
                    {
                        ini.WriteValue("config", "pvr.rend", "3");
                        ini.WriteValue("config", "rend.PerStripSorting", "yes");
                    }
                    else
                    {
                        ini.WriteValue("config", "pvr.rend", "5");
                        ini.WriteValue("config", "rend.PerStripSorting", "yes");
                    }
                }

                BindIniFeature(ini, "config", "pvr.AutoSkipFrame", "flycast_autoframeskip", "0");
                BindBoolIniFeature(ini, "config", "rend.ModifierVolumes", "flycast_shadows", "no", "yes");
                BindBoolIniFeature(ini, "config", "rend.Fog", "flycast_fog", "no", "yes");

                if (SystemConfig.isOptSet("flycast_ratio") && SystemConfig["flycast_ratio"] == "wide")
                {
                    ini.WriteValue("config", "rend.SuperWideScreen", "no");
                    ini.WriteValue("config", "rend.WideScreen", "yes");
                }
                else if (SystemConfig.isOptSet("flycast_ratio") && SystemConfig["flycast_ratio"] == "stretch")
                {
                    ini.WriteValue("config", "rend.SuperWideScreen", "yes");
                    ini.WriteValue("config", "rend.WideScreen", "yes");
                }
                else
                {
                    ini.WriteValue("config", "rend.SuperWideScreen", "no");
                    ini.WriteValue("config", "rend.WideScreen", "no");
                }
                
                BindBoolIniFeature(ini, "config", "rend.WidescreenGameHacks", "flycast_widescreen_hack", "yes", "no");
                BindIniFeature(ini, "config", "rend.AnisotropicFiltering", "flycast_fxaa", "1");
                BindIniFeature(ini, "config", "rend.TextureFiltering", "flycast_texture_filter", "0");
                
                if (SystemConfig.isOptSet("flycast_vsync") && !string.IsNullOrEmpty(SystemConfig["flycast_vsync"]))
                {
                    string fvsync = SystemConfig["flycast_vsync"];
                    switch (fvsync)
                    {
                        case "no":
                            ini.WriteValue("config", "rend.vsync", "no");
                            ini.WriteValue("config", "rend.DupeFrames", "no");
                            break;
                        case "yes":
                            ini.WriteValue("config", "rend.vsync", "yes");
                            ini.WriteValue("config", "rend.DupeFrames", "no");
                            break;
                        case "dupframes":
                            ini.WriteValue("config", "rend.vsync", "yes");
                            ini.WriteValue("config", "rend.DupeFrames", "yes");
                            break;
                    }
                }
                else
                {
                    ini.WriteValue("config", "rend.vsync", "yes");
                    ini.WriteValue("config", "rend.DupeFrames", "no");
                }

                BindBoolIniFeature(ini, "config", "rend.Rotate90", "flycast_rotate", "yes", "no");
                BindBoolIniFeature(ini, "config", "rend.ShowFPS", "flycast_fps", "yes", "no");
                BindBoolIniFeatureOn(ini, "config", "rend.RenderToTextureBuffer", "flycast_copytovram", "yes", "no");
                BindIniFeatureSlider(ini, "config", "rend.TextureUpscale", "flycast_texture_upscale", "1");
                BindIniFeatureSlider(ini, "config", "pvr.MaxThreads", "flycast_threads", "3");
                BindBoolIniFeature(ini, "config", "rend.CustomTextures", "flycast_custom_textures", "yes", "no");
                BindIniFeature(ini, "config", "rend.Resolution", "flycast_resolution", "480");

                // audio
                BindIniFeature(ini, "audio", "backend", "flycast_audio", "auto");
                BindBoolIniFeature(ini, "config", "aica.DSPEnabled", "flycast_dsp", "yes", "no");

                // Advanced
                ini.WriteValue("config", "rend.ThreadedRendering", "yes");
                BindIniFeature(ini, "config", "Dynarec.Enabled", "flycast_dynarec", "yes");
                BindBoolIniFeature(ini, "config", "UseReios", "flycast_hlebios", "yes", "no");
                BindBoolIniFeature(ini, "config", "Dreamcast.RamMod32MB", "flycast_ram32", "yes", "no");

                CreateControllerConfiguration(path, system, ini);

                BindBoolIniFeature(ini, "config", "PerGameVmu", "flycast_vmupergame", "yes", "no");

                // Network features
                BindBoolIniFeature(ini, "network", "NetworkOutput", "flycast_output", "yes", "no");
                BindBoolIniFeatureOn(ini, "network", "EnableUPnP", "flycast_upnp", "yes", "no");
                BindIniFeature(ini, "network", "server", "flycast_server", null);
                BindIniFeature(ini, "network", "LocalPort", "flycast_localport", "37391");
                BindBoolIniFeature(ini, "network", "ActAsServer", "flycast_host", "yes", "no");

                if (SystemConfig.isOptSet("flycast_network") && !string.IsNullOrEmpty(SystemConfig["flycast_network"]))
                {
                    string fnet = SystemConfig["flycast_network"];
                    switch (fnet)
                    {
                        case "disabled":
                            ini.WriteValue("network", "BattleCable", "no");
                            ini.WriteValue("network", "Enable", "no");
                            ini.WriteValue("network", "GGPO", "no");
                            break;
                        case "ggpo":
                            ini.WriteValue("network", "BattleCable", "no");
                            ini.WriteValue("network", "Enable", "no");
                            ini.WriteValue("network", "GGPO", "yes");
                            break;
                        case "naomi":
                            ini.WriteValue("network", "BattleCable", "no");
                            ini.WriteValue("network", "Enable", "yes");
                            ini.WriteValue("network", "GGPO", "no");
                            break;
                        case "battle":
                            ini.WriteValue("network", "BattleCable", "yes");
                            ini.WriteValue("network", "Enable", "no");
                            ini.WriteValue("network", "GGPO", "no");
                            break;
                    }
                }
                else
                {
                    ini.WriteValue("network", "Network", "no");
                }

                ini.Save();
            }
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

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            return ret;
        }
    }
}
