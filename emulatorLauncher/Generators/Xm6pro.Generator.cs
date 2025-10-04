﻿using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class Xm6proGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public Xm6proGenerator()
        {
            DependsOnDesktopResolution = true;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{
            SimpleLogger.Instance.Info("[Generator] Getting " + emulator + " path and executable name.");

            string path = AppConfig.GetFullPath("xm6pro");

			string exe = Path.Combine(path, "XM6.exe");

			if (!File.Exists(exe))
				return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            if (SystemConfig.isOptSet("68k_stretch") && SystemConfig["68k_stretch"] == "true")
                SystemConfig["bezel"] = "none";

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            SetupConfiguration(path, fullscreen);

            return new ProcessStartInfo()
			{
				FileName = exe,
				WorkingDirectory = path,
				Arguments = "\"" + rom + "\"",
			};

		}

        private void SetupConfiguration(string path, bool fullscreen)
        {
            string iniFile = Path.Combine(path, "XM6.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {
                    ini.Remove("SASI", "File0");
                    ini.WriteValue("Window", "Full", fullscreen ? "1" : "0");  
                    ini.WriteValue("Resume", "Screen", "1");
                    ini.WriteValue("Basic", "AutoMemSw", "1");
                    ini.WriteValue("MIDI", "ID", "1");
                 
                    BindBoolIniFeature(ini, "Display", "FullScreenMaximum", "68k_stretch", "1", "0");
                    BindBoolIniFeature(ini, "Display", "FullScreenRescale", "68k_stretch", "1", "0");
                    BindBoolIniFeature(ini, "Display", "Scanlines", "68k_scanlines", "1", "0"); // Works with few games
                    BindBoolIniFeature(ini, "Display", "Smoothing", "68k_smooth", "1", "0");
                    BindBoolIniFeature(ini, "Window", "StatusBar", "68k_statusbar", "1", "0");
                    BindBoolIniFeature(ini, "Misc", "FloppySpeed", "68k_floppy", "1", "0"); // Improves loading
                    BindIniFeature(ini, "MIDI", "OutDevice", "68k_midi_index", "0"); // MIDI output device index
                    BindIniFeature(ini, "MIDI", "IntLevel", "68k_midi_interrupt", "0"); // MIDI interrupt level
                    BindIniFeature(ini, "Basic", "Clock", "68k_clock", "0");
                    BindIniFeature(ini, "Basic", "Memory", "68k_ram", "0");
                    
                    ini.Save();
                }
            }
            catch { }
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            if (ret == 1)
                return 0;

            return ret;
        }
    }
}
