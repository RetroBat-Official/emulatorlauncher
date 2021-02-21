using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;

namespace emulatorLauncher
{
	class DuckstationGenerator : Generator
	{
		public DuckstationGenerator()
		{
			DependsOnDesktopResolution = true;
		}
		
		public override void RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);
        
            base.RunAndWait(path);

            if (bezel != null)
                bezel.Dispose();
        }

        private string _path;
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{

			_path = AppConfig.GetFullPath("duckstation");
            _resolution = resolution;

			string exe = Path.Combine(_path, "duckstation-nogui-x64-ReleaseLTCG.exe");
			if (!File.Exists(exe))
				return null;
				
			SetupSettings();
			
			if (SystemConfig["ratio"] == "4:3")
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

			return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = _path,
					Arguments = "\"" + rom + "\"",
				};
        }
		
		private void SetupSettings()
        {
            string iniFile = Path.Combine(_path, "settings.ini");
            if (File.Exists(iniFile))
            {
                try
                {
                    using (var ini = new IniFile(iniFile))
                    {
                        Uri relRoot = new Uri(_path, UriKind.Absolute);

                        string biosPath = AppConfig.GetFullPath("bios");
                        if (!string.IsNullOrEmpty(biosPath))
                        {
                            ini.WriteValue("BIOS", "SearchDirectory", biosPath.Replace("\\", "\\\\"));
                        }

                        if (SystemConfig.isOptSet("ratio") && !string.IsNullOrEmpty(SystemConfig["ratio"]))
                            ini.WriteValue("Display", "AspectRatio", SystemConfig["ratio"]);
                        else
                            ini.WriteValue("Display", "AspectRatio", "Auto (Game Native)");
							
						if (SystemConfig.isOptSet("internal_resolution") && !string.IsNullOrEmpty(SystemConfig["internal_resolution"]))
                            ini.WriteValue("GPU", "ResolutionScale", SystemConfig["internal_resolution"]);
                        else
                            ini.WriteValue("GPU", "ResolutionScale", "5");
							
						if (SystemConfig.isOptSet("gfxbackend") && !string.IsNullOrEmpty(SystemConfig["gfxbackend"]))
                            ini.WriteValue("GPU", "Renderer", SystemConfig["gfxbackend"]);
                        else
                            ini.WriteValue("GPU", "Renderer", "Vulkan");
							
						if (SystemConfig.isOptSet("Texture_Enhancement") && !string.IsNullOrEmpty(SystemConfig["Texture_Enhancement"]))
                            ini.WriteValue("GPU", "TextureFilter", SystemConfig["Texture_Enhancement"]);
                        else
                            ini.WriteValue("GPU", "TextureFilter", "Nearest");
							
						if (SystemConfig.isOptSet("interlace") && !string.IsNullOrEmpty(SystemConfig["interlace"]))
                            ini.WriteValue("GPU", "DisableInterlacing", SystemConfig["interlace"]);
                        else
                            ini.WriteValue("GPU", "DisableInterlacing", "true");
							
						if (SystemConfig.isOptSet("NTSC_Timings") && !string.IsNullOrEmpty(SystemConfig["NTSC_Timings"]))
                            ini.WriteValue("GPU", "ForceNTSCTimings", SystemConfig["NTSC_Timings"]);
                        else
                            ini.WriteValue("GPU", "ForceNTSCTimings", "false");
						
						if (SystemConfig.isOptSet("Widescreen_Hack") && !string.IsNullOrEmpty(SystemConfig["Widescreen_Hack"]))
                            ini.WriteValue("GPU", "WidescreenHack", SystemConfig["Widescreen_Hack"]);
                        else
                            ini.WriteValue("GPU", "WidescreenHack", "false");
						
						if (SystemConfig.isOptSet("Disable_Dithering") && !string.IsNullOrEmpty(SystemConfig["Disable_Dithering"]))
                            ini.WriteValue("GPU", "TrueColor", SystemConfig["Disable_Dithering"]);
                        else
                            ini.WriteValue("GPU", "TrueColor", "false");
							
						if (SystemConfig.isOptSet("Scaled_Dithering") && !string.IsNullOrEmpty(SystemConfig["Scaled_Dithering"]))
                            ini.WriteValue("GPU", "ScaledDithering", SystemConfig["Scaled_Dithering"]);
                        else
                            ini.WriteValue("GPU", "ScaledDithering", "false");
						
						if (SystemConfig.isOptSet("VSync") && !string.IsNullOrEmpty(SystemConfig["VSync"]))
                            ini.WriteValue("Display", "VSync", SystemConfig["VSync"]);
                        else
                            ini.WriteValue("Display", "VSync", "true");
						
						if (SystemConfig.isOptSet("Linear_Filtering") && !string.IsNullOrEmpty(SystemConfig["Linear_Filtering"]))
                            ini.WriteValue("Display", "LinearFiltering", SystemConfig["Linear_Filtering"]);
                        else
                            ini.WriteValue("Display", "LinearFiltering", "true");
							
						if (SystemConfig.isOptSet("Integer_Scaling") && !string.IsNullOrEmpty(SystemConfig["Integer_Scaling"]))
                            ini.WriteValue("Display", "IntegerScaling", SystemConfig["Integer_Scaling"]);
                        else
                            ini.WriteValue("Display", "IntegerScaling", "false");
							
                        ini.WriteValue("Main", "ConfirmPowerOff", "false");
                        ini.WriteValue("Main", "StartFullscreen", "true");
                        ini.WriteValue("Main", "ApplyGameSettings", "true");
                        ini.WriteValue("Main", "RenderToMainWindow", "true");
                        ini.WriteValue("Main", "EnableDiscordPresence", "false");
                        ini.WriteValue("Display", "Fullscreen", "true");
                    }
                }
                catch { }
            }
        }
		
    }
}
