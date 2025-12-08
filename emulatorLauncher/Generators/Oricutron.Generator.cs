using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.PadToKeyboard;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using System.Linq;

namespace EmulatorLauncher
{
    class OricutronGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

		public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
		{
			string path = AppConfig.GetFullPath("oricutron");

            string exe = Path.Combine(path, "oricutron.exe");
			if (!File.Exists(exe))
				return null;

            if (!ReshadeManager.Setup(ReshadeBezelType.opengl, ReshadePlatform.x64, system, rom, path, resolution, emulator))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution, emulator);

            _resolution = resolution;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            ConfigureOricutron(path, rom, fullscreen);

            List<string> commandArray = new List<string>();
            
            if (fullscreen)
                commandArray.Add("--fullscreen");

            if (Path.GetExtension(rom).ToLower() == ".dsk")
                commandArray.Add("--disk");
            else
            {
                commandArray.Add("--turbotape");
                commandArray.Add("on");
                commandArray.Add("--tape");
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

        private void ConfigureOricutron(string path, string rom, bool fullscreen)
        {
            string configPath = Path.Combine(path, "oricutron.cfg");

            try
            {
                using (var ini = new IniFile(configPath))
                {
                    BindIniFeature(ini, "", "machine", "oricutron_machine", "atmos");
                    BindIniFeature(ini, "", "rendermode", "oricutron_rendermode", "atmos");
                    BindBoolIniFeature(ini, "", "aratio", "oricutron_aratio", "yes", "no");
                    BindBoolIniFeature(ini, "", "scanlines", "oricutron_scanlines", "yes", "no");
                    BindBoolIniFeature(ini, "", "hstretch", "oricutron_hstretch", "yes", "no");
                    BindBoolIniFeature(ini, "", "palghosting", "oricutron_palghosting", "yes", "no");
                    ini.WriteValue("", "fullscreen", fullscreen ? "yes" : "no");
                    BindIniFeature(ini, "", "joyinterface", "oricutron_joyinterface", "none");

                    ConfigureControllers(ini);

                    ini.Save();
                }
            }
            catch { }
        }

        private void ConfigureControllers(IniFile ini)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            if (this.Controllers.Count == 0 || !this.Controllers.Any(c => !c.IsKeyboard && c.SdlController != null) || SystemConfig.getOptBoolean("oricutron_forceKeyboard"))
            {
                SimpleLogger.Instance.Info("[INFO] No controllers connected, skipping controller configuration.");

                ini.WriteValue("", "joystick_a", "kbjoy1");
                ini.WriteValue("", "joystick_b", "kbjoy2");
                ini.WriteValue("", "telejoy_a", "kbjoy1");
                ini.WriteValue("", "telejoy_b", "kbjoy2");
                return;
            }
            else
            {
                var c1 = this.Controllers.OrderBy(i => i.PlayerIndex).FirstOrDefault(c => !c.IsKeyboard && c.SdlController != null);
                if (c1 != null)
                {
                    string c1Index = c1.SdlController.Index.ToString();
                    ini.WriteValue("", "joystick_a", "sdljoy" + c1Index);
                    ini.WriteValue("", "telejoy_a", "sdljoy" + c1Index);
                }
                var c2 = this.Controllers.OrderBy(i => i.PlayerIndex).FirstOrDefault(c => !c.IsKeyboard && c.SdlController != null && c != c1);
                if (c2 != null)
                {
                    string c2Index = c2.SdlController.Index.ToString();
                    ini.WriteValue("", "joystick_b", "sdljoy" + c2Index);
                    ini.WriteValue("", "telejoy_b", "sdljoy" + c2Index);
                }
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

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "oricutron", InputKey.hotkey | InputKey.start, "(%{KILL})");
        }       
    }
}
