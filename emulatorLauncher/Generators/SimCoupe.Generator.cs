using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;

namespace EmulatorLauncher
{
    class SimCoupeGenerator : Generator
    {
        private BezelFiles _bezelFileInfo;
        private ScreenResolution _resolution;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("simcoupe");

            string exe = Path.Combine(path, "simcoupe.exe");
            if (!File.Exists(exe))
                return null;

            var platform = ReshadeManager.GetPlatformFromFile(exe);
            if (!ReshadeManager.Setup(ReshadeBezelType.dxgi, platform, system, rom, path, resolution))
                _bezelFileInfo = BezelFiles.GetBezelFiles(system, rom, resolution);

            _resolution = resolution;
            
            List<string> commandArray = new List<string>
            {
                rom
            };
            commandArray.AddRange(new string[] { "-drive1", "1" });
            commandArray.AddRange(new string[] { "-drive2", "0" });            
            commandArray.AddRange(new string[] { "-autoload", "yes" });
            commandArray.AddRange(new string[] { "-turbodisk", "yes" });
            commandArray.AddRange(new string[] { "-fullscreen", "yes" });
            commandArray.AddRange(new string[] { "-firstrun", "no" });
            commandArray.AddRange(new string[] { "-profile", "no" });

            if (Features.IsSupported("smooth") && SystemConfig.getOptBoolean("smooth"))
                commandArray.AddRange(new string[] { "-smooth", "yes" });
            else
                commandArray.AddRange(new string[] { "-smooth", "no" });

            for (int i = 0; i < 2; i++)
            {
                if (i >= Program.Controllers.Count || Program.Controllers[i].Config == null || Program.Controllers[i].Config.Type == "keyboard")
                {
                    commandArray.AddRange(new string[] { "-joydev" + (i + 1).ToString(), "None" });
                    continue;
                }

                var dxInfo = Program.Controllers[i].DirectInput;
                if (dxInfo == null || string.IsNullOrEmpty(dxInfo.Name))
                    commandArray.AddRange(new string[] { "-joydev" + (i + 1).ToString(), "None" });
                else
                    commandArray.AddRange(new string[] { "-joydev" + (i + 1).ToString(), dxInfo.Name });
            }

            var args = string.Join(" ", commandArray.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a).ToArray());

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args
            };
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            FakeBezelFrm bezel = null;

            if (_bezelFileInfo != null)
                bezel = _bezelFileInfo.ShowFakeBezel(_resolution);

            int ret = base.RunAndWait(path);

            bezel?.Dispose();

            ReshadeManager.UninstallReshader(ReshadeBezelType.dxgi, path.WorkingDirectory);

            return ret;
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, "simcoupe", InputKey.hotkey | InputKey.start, "(%{KILL})");
        }        
    }
}
