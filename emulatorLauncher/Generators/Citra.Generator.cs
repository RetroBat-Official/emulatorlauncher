using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace emulatorLauncher
{
	class CitraGenerator : Generator
	{
		public CitraGenerator()
		{
			DependsOnDesktopResolution = true;
		}

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{
			string path = AppConfig.GetFullPath("citra");

			string exe = Path.Combine(path, "citra-qt.exe");
			if (!File.Exists(exe))
				return null;

            string portableFile = Path.Combine(path, "portable.txt");
            if (!File.Exists(portableFile))
                File.WriteAllText(portableFile, "");

			if (core == "citra-sdl")
			{
				exe = Path.Combine(path, "citra.exe");
				if (!File.Exists(exe))
				    return null;
				
				return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
					Arguments = "\"" + rom + "\"",
				};			
			}

            SetupConfiguration(path);

			return new ProcessStartInfo()
				{
					FileName = exe,
					WorkingDirectory = path,
					Arguments = "\"" + rom + "\" -f",
				};
        }

        private void SetupConfiguration(string path)
        {
            string userconfigPath = Path.Combine(path, "user", "config");
            if (!Directory.Exists(userconfigPath))
                Directory.CreateDirectory(userconfigPath);

            string conf = Path.Combine(userconfigPath, "qt-config.ini");

            var ini = new IniFile(conf);
            ini.WriteValue("UI", "fullscreen\\default", "false");
            ini.WriteValue("UI", "fullscreen", "true");

            ini.WriteValue("UI", "confirmClose\\default", "false");
            ini.WriteValue("UI", "confirmClose", "false");

            ini.WriteValue("WebService", "enable_telemetry\\default", "false");
            ini.WriteValue("WebService", "enable_telemetry", "false");

            ini.WriteValue("UI", "firstStart\\default", "false");
            ini.WriteValue("UI", "firstStart", "false");

            ini.WriteValue("UI", "calloutFlags\\default", "false");
            ini.WriteValue("UI", "calloutFlags", "1");

            if (SystemConfig.isOptSet("smooth") && SystemConfig.getOptBoolean("smooth"))
            {
                ini.WriteValue("Layout", "filter_mode\\default", "true");
                ini.WriteValue("Layout", "filter_mode", "true");
            }
            else
            {
                ini.WriteValue("Layout", "filter_mode\\default", "false");
                ini.WriteValue("Layout", "filter_mode", "false");
            }

            if (ini.IsDirty)
                ini.Save();           
        }
    }
}
