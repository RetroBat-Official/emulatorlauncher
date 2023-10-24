using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
	class Snes9xGenerator : Generator
	{
		private BezelFiles _bezelFileInfo;
		private ScreenResolution _resolution;

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			string path = AppConfig.GetFullPath("snes9x");

			string exe = Path.Combine(path, "snes9x-x64.exe");
			if (!File.Exists(exe) || !Environment.Is64BitOperatingSystem)
				exe = Path.Combine(path, "snes9x.exe");

			if (!File.Exists(exe))
				return null;

			//Applying bezels
			if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution))
				_bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

			_resolution = resolution;

            SetupConfiguration(path, rom, system);

            //List<string> commandArray = new List<string>();

            return new ProcessStartInfo()
			{
				FileName = exe,
				WorkingDirectory = path,
				Arguments = "\"" + rom + "\"",
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

			if (ret == 1)
				return 0;

			return ret;
		}

		private void SetupConfiguration(string path, string rom, string system)
        {
            string conf = Path.Combine(path, "snes9x.conf");
			if (!File.Exists(conf))
				return;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            using (var ini = IniFile.FromFile(conf, IniOptions.KeepEmptyLines))
			{

				// Inject path loop
				Dictionary<string, string> userPath = new Dictionary<string, string>
				{
					{ "Dir:Roms", Path.Combine(AppConfig.GetFullPath("roms"), system) },
					{ "Dir:Screenshots", Path.Combine(AppConfig.GetFullPath("screenshots"), "snes9x") },
					{ "Dir:Movies", Path.Combine(AppConfig.GetFullPath("records"), "snes9x") },
					{ "Dir:Cheats", Path.Combine(AppConfig.GetFullPath("cheats"), "snes9x") },
					{ "Dir:Patches", Path.Combine(AppConfig.GetFullPath("roms"), system) },
					{ "Dir:Savestates", Path.Combine(AppConfig.GetFullPath("saves"), system, "snes9x", "sstates") },
					{ "Dir:SRAM", Path.Combine(AppConfig.GetFullPath("saves"), system, "snes9x", "sram") },
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
				ini.WriteValue(@"Sound\Win", "SoundDriver", "4"); // Force XAudio
				ini.WriteValue(@"Sound\Win", "BufferSize", "64");

				ini.WriteValue(@"Display\Win", "OutputMethod", "2"); // Force OpenGL renderer to get bezel and shader to work
				ini.WriteValue(@"Display\Win", "HideMenu", "TRUE"); // Hide menu at startup, ESC to toggle
				ini.WriteValue(@"Display\Win", "FullscreenOnOpen", "FALSE");
				ini.WriteValue(@"Display\Win", "Fullscreen:Enabled", fullscreen ? "TRUE" : "FALSE");
				ini.WriteValue(@"Display\Win", "Fullscreen:EmulateFullscreen", fullscreen ? "TRUE" : "FALSE");
				ini.WriteValue(@"Display\Win", "Window:Maximized", "TRUE");
				ini.WriteValue(@"Display\Win", "BlendHiRes", "TRUE");

				// Bilinear filtering
				if (SystemConfig.isOptSet("snes9x_bilinear") && SystemConfig.getOptBoolean("snes9x_bilinear"))
					ini.WriteValue(@"Display\Win", "Stretch:BilinearFilter", "TRUE");
				else
					ini.WriteValue(@"Display\Win", "Stretch:BilinearFilter", "FALSE");

				// Ratio
				if (SystemConfig.isOptSet("snes9x_ratio") && !string.IsNullOrEmpty(SystemConfig["snes9x_ratio"]))
                {

					if (SystemConfig["snes9x_ratio"] != "full")
						ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "TRUE");
					else
						ini.WriteValue(@"Display\Win", "Stretch:MaintainAspectRatio", "FALSE");

					if (SystemConfig["snes9x_ratio"] == "4/3")
						ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "299");

					if (SystemConfig["snes9x_ratio"] == "8/7")
						ini.WriteValue(@"Display\Win", "Stretch:AspectRatioBaseWidth", "256");
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
					ini.WriteValue(@"Display\Win", "FilterType", SystemConfig["snes9x_ntsc_filters"]);
				else
					ini.WriteValue(@"Display\Win", "FilterType", "0");

				// Shaders
				if (SystemConfig.isOptSet("snes9x_shader") && !string.IsNullOrEmpty(SystemConfig["snes9x_shader"]))
                {
					ini.WriteValue(@"Display\Win", "ShaderEnabled", "TRUE");
					ini.WriteValue(@"Display\Win", "NTSCScanlines", "TRUE");

					string shaderPath = Path.Combine(path, "shaders", "shaders_glsl", SystemConfig["snes9x_shader"]);
					if (!File.Exists(shaderPath))
						shaderPath = Path.Combine(AppConfig.GetFullPath("retroarch"), "shaders", "shaders_glsl", SystemConfig["snes9x_shader"]);
					if (!File.Exists(shaderPath))
						shaderPath = Path.Combine(AppConfig.GetFullPath("system"), "shaders", "shaders_glsl", SystemConfig["snes9x_shader"]);
					if (File.Exists(shaderPath))
						ini.WriteValue(@"Display\Win", "OpenGL:OGLShader", shaderPath);
					else
						ini.WriteValue(@"Display\Win", "OpenGL:OGLShader", "");                 
                }
                else
                {
					ini.WriteValue(@"Display\Win", "ShaderEnabled", "FALSE");
					ini.WriteValue(@"Display\Win", "NTSCScanlines", "FALSE");
					ini.WriteValue(@"Display\Win", "OpenGL:OGLShader", "");
				}            
				
			}

        }

    }
}
