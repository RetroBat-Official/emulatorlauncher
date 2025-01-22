using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class DemulGenerator : Generator
    {
        private bool _demulshooter;

        private void ConfigureDemulGuns(string system, string rom)
        {
            // If system is not compatible with guns, return
            if (system != "atomiswave" && system != "naomi" && system != "naomi2")
                return;

            // Get rom name without extension
            string romName = Path.GetFileNameWithoutExtension(rom).ToLower();

            // Check if the game is in the demulRoms list (compatible games)
            if (!demulRoms.Contains(romName))
                return;

            // Get number of guns connected
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Count == 0)
                return;

            // Enable demulshooter if use_demulshooter is set to auto and there are multiple guns, or if it's explicitly set to true
            _demulshooter = (SystemConfig.isOptSet("use_demulshooter") && SystemConfig["use_demulshooter"] == "1") ||
                           (SystemConfig.isOptSet("use_demulshooter") && SystemConfig["use_demulshooter"] == "auto" && guns.Count > 1);

            if (!_demulshooter)
                return;

            SimpleLogger.Instance.Info("[GUNS] Game is compatible with DemulShooter, disabling native controls.");

            // Start DemulShooter with appropriate parameters
            Demulshooter.StartDemulshooter("demul", "demul07a", rom, guns.ElementAtOrDefault(0), guns.ElementAtOrDefault(1));
        }

        public override void Cleanup()
        {
            if (_demulshooter)
                Demulshooter.KillDemulShooter();

            base.Cleanup();
        }
    }
} 