using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using System;

namespace EmulatorLauncher
{
    partial class FlycastGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;
        private bool _fullscreen;
        private string _romName;

        public FlycastGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("flycast");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "flycast.exe");
            if (!File.Exists(exe))
                return null;

            _romName = Path.GetFileNameWithoutExtension(rom);
            _fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");
            bool wide = SystemConfig.isOptSet("flycast_ratio") && SystemConfig["flycast_ratio"] != "normal";

            //Applying bezels
            if (_fullscreen && !wide)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

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
                BindIniFeature(ini, "config", "ForceFreePlay", "flycast_freeplay", "yes");

                if (SystemConfig.isOptSet("autosave") && SystemConfig.getOptBoolean("autosave"))
                {
                    ini.WriteValue("config", "Dreamcast.AutoLoadState", "yes");
                    ini.WriteValue("config", "Dreamcast.AutoSaveState", "yes");
                }
                else
                {
                    ini.WriteValue("config", "Dreamcast.AutoLoadState", "no");
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
                BindIniFeature(ini, "config", "rend.ModifierVolumes", "flycast_shadows", "yes");
                BindIniFeature(ini, "config", "rend.Fog", "flycast_fog", "yes");

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
                BindIniFeature(ini, "config", "rend.vsync", "flycast_vsync", "yes");
                BindBoolIniFeature(ini, "config", "rend.DupeFrames", "flycast_dupeframes", "yes", "no");
                BindBoolIniFeature(ini, "config", "rend.Rotate90", "flycast_rotate", "yes", "no");
                BindBoolIniFeature(ini, "config", "rend.ShowFPS", "flycast_fps", "yes", "no");
                BindIniFeature(ini, "config", "rend.RenderToTextureBuffer", "flycast_copytovram", "yes");
                BindIniFeature(ini, "config", "rend.TextureUpscale", "flycast_texture_upscale", "1");
                BindIniFeature(ini, "config", "pvr.MaxThreads", "flycast_threads", "3");
                BindBoolIniFeature(ini, "config", "rend.CustomTextures", "flycast_custom_textures", "yes", "no");
                BindIniFeature(ini, "config", "rend.Resolution", "flycast_resolution", "480");

                // audio
                BindIniFeature(ini, "audio", "backend", "flycast_audio", "auto");
                BindBoolIniFeature(ini, "config", "aica.DSPEnabled", "flycast_dsp", "yes", "no");

                // Advanced
                ini.WriteValue("config", "rend.ThreadedRendering", "yes");
                BindIniFeature(ini, "config", "Dynarec.Enabled", "flycast_dynarec", "yes");

                CreateControllerConfiguration(path, system, ini);

                BindBoolIniFeature(ini, "config", "PerGameVmu", "flycast_vmupergame", "yes", "no");

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

            return ret;
        }
    }
}
