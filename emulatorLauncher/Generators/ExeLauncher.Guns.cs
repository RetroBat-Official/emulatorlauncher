using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class ExeLauncherGenerator : Generator
    {
        private bool _demulshooter;

        private void ConfigureExeLauncherGuns(string system, string rom)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            // Get number of guns connected
            var guns = RawLightgun.GetRawLightguns();
            if (guns.Length == 0)
                return;

            string gameName = Path.GetFileNameWithoutExtension(rom).Replace(" ", "").Replace("_", "").ToLowerInvariant();

            // Check if the game is in the exeLauncherGames list (compatible games)
            if (!Demulshooter.exeLauncherGames.ContainsKey(gameName))
            {
                gameName = Path.GetFileNameWithoutExtension(new DirectoryInfo(Path.GetDirectoryName(rom)).Name).Replace(" ", "").Replace("_", "").ToLowerInvariant();

                if (!Demulshooter.exeLauncherGames.ContainsKey(gameName))
                    return;
            }

            // Enable demulshooter if use_demulshooter is set to auto and there are multiple guns, or if it's explicitly set to true
            _demulshooter = (SystemConfig.isOptSet("use_demulshooter") && SystemConfig["use_demulshooter"] == "1") ||
                           (!SystemConfig.isOptSet("use_demulshooter") && guns.Length > 1);

            if (!_demulshooter)
                return;

            SimpleLogger.Instance.Info("[GUNS] Game is compatible with DemulShooter, configuring...");

            // Start DemulShooter with appropriate parameters
            Demulshooter.StartDemulshooter("exelauncher", system, gameName, guns.ElementAtOrDefault(0), guns.ElementAtOrDefault(1));
        }

        public override void Cleanup()
        {
            if (_demulshooter)
                Demulshooter.KillDemulShooter();

            base.Cleanup();
        }
    }
} 