using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Windows.Forms;
using EmulatorLauncher.PadToKeyboard;
using System.Security.Cryptography;
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

			string path = AppConfig.GetFullPath("xm6pro");

			string exe = Path.Combine(path, "XM6.exe");

			if (!File.Exists(exe))
				return null;           

			SetupConfiguration(path, rom, system);

            bool fullscreen = !IsEmulationStationWindowed();

            if (SystemConfig.isOptSet("68k_stretch") && SystemConfig["68k_stretch"] == "true")
                SystemConfig["bezel"] = "none";

            if (fullscreen)
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;

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
            string iniFile = Path.Combine(path, "XM6.ini");

            try
            {
                using (var ini = new IniFile(iniFile))
                {

                    if (!SystemConfig.getOptBoolean("disable_fullscreen"))
                    {
                        ini.WriteValue("Window", "Full", "1");
                        ini.WriteValue("Resume", "Screen", "1");
                    }

                    BindBoolIniFeature(ini, "Display", "FullScreenMaximum", "68k_stretch", "1", "0");
                    BindBoolIniFeature(ini, "Display", "FullScreenRescale", "68k_stretch", "1", "0");
                    BindBoolIniFeature(ini, "Display", "Scanlines", "68k_scanlines", "1", "0");
                    BindBoolIniFeature(ini, "Display", "Smoothing", "68k_smooth", "1", "0");
                    BindBoolIniFeature(ini, "Window", "StatusBar", "68k_statusbar", "1", "0");

                    // MIDI output
                    if (SystemConfig.isOptSet("68k_midi") && !string.IsNullOrEmpty("68k_midi"))
                    {
                        ini.WriteValue("MIDI", "ID", "1");
                        ini.WriteValue("MIDI", "IntLevel", "0");
                       
                        if (SystemConfig["68k_midi"] == "munt")
                        {
                            // Emulated Roland MT-32
                            ini.WriteValue("MIDI", "ResetCmd", "3");
                            ini.WriteValue("MIDI", "OutDevice", "4");
                        }
                        else if (SystemConfig["68k_midi"] == "virtual")
                        {
                            // Virtual MIDI
                            ini.WriteValue("MIDI", "ResetCmd", "1");
                            ini.WriteValue("MIDI", "OutDevice", "2");
                        }
                        else if (SystemConfig["68k_midi"] == "wave")
                        {
                            // Microsoft GS Wavetable
                            ini.WriteValue("MIDI", "ResetCmd", "1");
                            ini.WriteValue("MIDI", "OutDevice", "3");
                        }
                    }
                    else
                    {
                        // Microsoft GS Wavetable
                        ini.WriteValue("MIDI", "ID", "1");
                        ini.WriteValue("MIDI", "IntLevel", "0");
                        ini.WriteValue("MIDI", "ResetCmd", "1");
                        ini.WriteValue("MIDI", "OutDevice", "3");
                    }
                }
            }
            catch { }
        }

    }
}
