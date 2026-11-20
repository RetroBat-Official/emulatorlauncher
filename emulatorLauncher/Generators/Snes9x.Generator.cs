using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.PadToKeyboard;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EmulatorLauncher
{
	partial class Snes9xGenerator : Generator
	{
        protected override bool UseGenericScreenPlacement { get { return true; } }
        private BezelFiles _bezelFileInfo;
		private ScreenResolution _resolution;
        private static readonly ReshadeBezelType[] _reshadeTypes = new ReshadeBezelType[] { ReshadeBezelType.opengl, ReshadeBezelType.d3d9, ReshadeBezelType.vulkan };
        private List<string> _commandArray = new List<string>();
		private bool _pad2Keyoverride = false;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("snes9x");

			string exe = Path.Combine(path, "snes9x-x64.exe");
			if (!File.Exists(exe) || !Environment.Is64BitOperatingSystem)
				exe = Path.Combine(path, "snes9x.exe");

			if (!File.Exists(exe))
				return null;

            bool fullscreen = ShouldRunFullscreen();

            //Applying bezels
            if (!fullscreen)
                SystemConfig["forceNoBezel"] = "true";

            // ReShade must match the video driver (OutputMethod: 0 DirectDraw, 1 Direct3D 9, 2 OpenGL, 3 Vulkan)
            ReshadeBezelType? reshadeType = ReshadeBezelType.d3d9;
            switch (SystemConfig["snes9x_renderer"])
            {
                case "0": // DirectDraw: not supported by ReShade
                case "3": // Vulkan: ReShade layer not validated with Snes9x
                    reshadeType = null;
                    break;
                case "2":
                    reshadeType = ReshadeBezelType.opengl;
                    break;
            }

            // Remove the ReShade of the other video drivers
            foreach (var type in _reshadeTypes)
            {
                if (type != reshadeType)
                    ReshadeManager.UninstallReshader(type, path);
            }

            if (reshadeType == null || !ReshadeManager.Setup(reshadeType.Value, ReshadeManager.GetPlatformFromFile(exe), system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);
            _resolution = resolution;

            SetupConfiguration(path, rom, system, fullscreen);

            _commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", _commandArray);

            return new ProcessStartInfo()
			{
				FileName = exe,
				WorkingDirectory = path,
				Arguments = args,
			};
			
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();

            foreach (var type in _reshadeTypes)
                ReshadeManager.UninstallReshader(type, path.WorkingDirectory);

            if (ret == 1)
                return 0;

            return ret;
        }

        static string EncodeToFakeCJK(string input)
        {
            var sb = new StringBuilder();
            byte[] bytes = Encoding.ASCII.GetBytes(input);
            for (int i = 0; i < bytes.Length - 1; i += 2)
            {
                // low byte first, high byte second — straight UTF-16 LE interpretation
                ushort codepoint = (ushort)(bytes[i] | (bytes[i + 1] << 8));
                sb.Append((char)codepoint);
            }
            // handle odd-length strings
            if (bytes.Length % 2 != 0)
                sb.Append((char)bytes[bytes.Length - 1]);
            return sb.ToString();
        }

        private void SetupConfiguration(string path, string rom, string system, bool fullscreen)
        {
            string conf = Path.Combine(path, "snes9x.conf");
			if (!File.Exists(conf))
				return;

            using (var ini = IniFile.FromFile(conf, IniOptions.KeepEmptyLines))
			{
				string username = SystemConfig["retroachievements.username"];
				string token = SystemConfig["retroachievements.token"];

				string finalUsername = EncodeToFakeCJK(username);
                string finalToken = EncodeToFakeCJK(token);

                //retroachievements
                ini.WriteValue("RetroAchievements", "Enabled", SystemConfig.getOptBoolean("retroachievements") ? "TRUE" : "FALSE");
                ini.WriteValue("RetroAchievements", "HardcoreMode", SystemConfig.getOptBoolean("retroachievements.hardcore") ? "TRUE" : "FALSE");
                ini.WriteValue("RetroAchievements", "Username", finalUsername);
                ini.WriteValue("RetroAchievements", "ApiToken", finalToken);
                ini.WriteValue("RetroAchievements", "EmulatorName", "SuperSnes9x");

                // Inject path
                Dictionary<string, string> userPath = new Dictionary<string, string>
				{
					{ "Dir:Roms", Path.Combine(AppConfig.GetFullPath("roms"), system) },
					{ "Dir:Screenshots", Path.Combine(AppConfig.GetFullPath("screenshots"), "snes9x") },
					{ "Dir:Movies", Path.Combine(AppConfig.GetFullPath("records"), "snes9x") },
					{ "Dir:Cheats", Path.Combine(AppConfig.GetFullPath("cheats"), "snes9x") },
					{ "Dir:Patches", Path.Combine(AppConfig.GetFullPath("roms"), system) },
					{ "Dir:Savestates", Path.Combine(AppConfig.GetFullPath("saves"), system, "snes9x", "sstates") },
					{ "Dir:SRAM", Path.Combine(AppConfig.GetFullPath("saves"), system) },
					{ "Dir:SatData", Path.Combine(AppConfig.GetFullPath("saves"), system, "snes9x", "satdata") },
					{ "Dir:Bios", Path.Combine(AppConfig.GetFullPath("bios")) }
				};
				foreach (KeyValuePair<string, string> pair in userPath)
				{
					if (!Directory.Exists(pair.Value)) try { Directory.CreateDirectory(pair.Value); }
						catch { }
					if (!string.IsNullOrEmpty(pair.Value) && Directory.Exists(pair.Value))
						ini.WriteValue(@"Settings\Win\Files", pair.Key, pair.Value);
				}

                // General settings
                ini.WriteValue(@"Settings\Win", "PauseWhenInactive", SystemConfig.getOptBoolean("nopauseonlostfocus") ? "FALSE" : "TRUE");
                ini.WriteValue(@"Sound\Win", "SoundDriver", "4"); // Force XAudio
				ini.WriteValue(@"Sound\Win", "BufferSize", "64");
				BindBoolIniFeature(ini, @"Display\Win", "HideMenu", "snes9x_showmenu", "FALSE", "TRUE");    // Hide menu at startup, ESC to toggle
                ini.WriteValue(@"Display\Win", "FullscreenOnOpen", "FALSE");
				ini.WriteValue(@"Display\Win", "Fullscreen:Enabled", fullscreen ? "TRUE" : "FALSE");
				ini.WriteValue(@"Display\Win", "Fullscreen:EmulateFullscreen", SystemConfig.getOptBoolean("exclusivefs") ? "FALSE" : "TRUE");
				ini.WriteValue(@"Display\Win", "Window:Maximized", "TRUE");
				ini.WriteValue(@"Display\Win", "BlendHiRes", "TRUE");
				BindBoolIniFeature(ini, "Settings\\Win", "RewindBufferSize", "rewind", "15", "0");

                // Bilinear filtering
                if (SystemConfig.isOptSet("snes9x_bilinear") && SystemConfig.getOptBoolean("snes9x_bilinear"))
					ini.WriteValue(@"Display\Win", "Stretch:BilinearFilter", "TRUE");
				else
					ini.WriteValue(@"Display\Win", "Stretch:BilinearFilter", "FALSE");

                // Ratio
                ini.WriteValue(@"Display\Win", "Stretch:Enabled", "TRUE");
                if (SystemConfig.isOptSet("snes9x_ratio") && !string.IsNullOrEmpty(SystemConfig["snes9x_ratio"]))
                {

					if (SystemConfig["snes9x_ratio"] == "stretch")
					{
                        ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "FALSE");
					}
					else if (SystemConfig["snes9x_ratio"] == "full_43")
                    {
                        ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "TRUE");
                        ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "299");
                    }

					else if (SystemConfig["snes9x_ratio"] == "full_87")
					{
                        ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "TRUE");
                        ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "256");
                    }

					else if (SystemConfig["snes9x_ratio"] == "8/7")
					{
                        ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "TRUE");
                        ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "256");
					}
                    else if (SystemConfig["snes9x_ratio"] == "4/3")
                    {
                        ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "TRUE");
                        ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "299");
                    }
                }
				else
                {
                    
                    ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "TRUE");
					ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "299");
				}

				// Integer scale
				if (SystemConfig.isOptSet("snes9x_integer") && SystemConfig.getOptBoolean("snes9x_integer"))
					ini.WriteValue(@"Display\Win", "Stretch:IntegerScaling", "TRUE");
				else
					ini.WriteValue(@"Display\Win", "Stretch:IntegerScaling", "FALSE");

				// VSync
				if (SystemConfig.isOptSet("snes9x_vsync") && !SystemConfig.getOptBoolean("snes9x_vsync"))
					ini.WriteValue(@"Display\Win", "Vsync", "FALSE");
				else
					ini.WriteValue(@"Display\Win", "Vsync", "TRUE");

				// NTSC filters
				if (SystemConfig.isOptSet("snes9x_ntsc_filters") && !string.IsNullOrEmpty(SystemConfig["snes9x_ntsc_filters"]))
				{
					ini.WriteValue(@"Display\Win", "FilterType", SystemConfig["snes9x_ntsc_filters"]);
                }
				else
				{
					ini.WriteValue(@"Display\Win", "FilterType", "0");
                }

				// Video drivers
				BindIniFeature(ini, @"Display\Win", "OutputMethod", "snes9x_renderer", "1");

				// Filters and stuff
                BindIniFeature(ini, @"Display\Win", "FilterType", "snes9x_ntsc_filters", "0");
                BindBoolIniFeature(ini, @"Display\Win", "NTSCScanlines", "snes9x_scanlines", "TRUE", "FALSE");

                // Framerate
                BindBoolIniFeature(ini, "Display", "FrameRate", "snes9x_framerate", "ON", "OFF");

                CreateControllerConfiguration(ini);
            }
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            if (_pad2Keyoverride && File.Exists(Path.Combine(Path.GetTempPath(), "padToKey.xml")))
            {
                mapping = PadToKey.Load(Path.Combine(Path.GetTempPath(), "padToKey.xml"));
            }

            return mapping;
        }

        public override void Cleanup()
        {
            base.Cleanup();
        }
    }
}
