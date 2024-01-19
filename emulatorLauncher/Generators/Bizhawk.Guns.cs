using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        /// <summary>
        /// Injects guns settings
        /// </summary>
        /// <param name="json"></param>
        /// <param name="deviceType"></param>
        /// <param name="core"></param>
        /// <param name="playerIndex"></param>
        private void SetupLightGuns(DynamicJson json, string deviceType, string core, string system, int playerIndex = 1)
        {
            if (!SystemConfig.getOptBoolean("use_guns"))
                return;

            var trollers = json.GetOrCreateContainer("AllTrollers");
            var analogtrollers = json.GetOrCreateContainer("AllTrollersAnalog");

            if (system == "mastersystem")
            {
                var smsTroller = trollers.GetOrCreateContainer("SMS Light Phaser Controller");

                smsTroller["P1 Trigger"] = "WMouse L";
                smsTroller["P2 B1"] = "WMouse R";      // Grenades for operation wolf

                var smsAnalog = analogtrollers.GetOrCreateContainer("SMS Light Phaser Controller");
                var p1X = smsAnalog.GetOrCreateContainer("P1 X");
                var p1Y = smsAnalog.GetOrCreateContainer("P1 Y");

                p1X["Value"] = "WMouse X";
                p1X.SetObject("Mult", 1.0);
                p1X.SetObject("Deadzone", 0.1);

                p1Y["Value"] = "WMouse Y";
                p1Y.SetObject("Mult", 1.0);
                p1Y.SetObject("Deadzone", 0.1);
            }

            if (system == "nes")
            {
                var nesTroller = trollers.GetOrCreateContainer(systemController[system]);

                nesTroller["P" + playerIndex + " Fire"] = "WMouse L";

                var nesAnalog = analogtrollers.GetOrCreateContainer(systemController[system]);
                var p2zapperX = nesAnalog.GetOrCreateContainer("P2 Zapper X");
                var p2zapperY = nesAnalog.GetOrCreateContainer("P2 Zapper Y");

                p2zapperX["Value"] = "WMouse X";
                p2zapperX.SetObject("Mult", 1.0);
                p2zapperX.SetObject("Deadzone", 0.1);

                p2zapperY["Value"] = "WMouse Y";
                p2zapperY.SetObject("Mult", 1.0);
                p2zapperY.SetObject("Deadzone", 0.1);
            }

            if (system == "snes")
            {
                var snesTroller = trollers.GetOrCreateContainer(systemController[system]);

                snesTroller["P" + playerIndex + " Trigger"] = "WMouse L";
                snesTroller["P" + playerIndex + " Cursor"] = "WMouse M";
                snesTroller["P" + playerIndex + " Turbo"] = "WMouse R";

                var snesAnalog = analogtrollers.GetOrCreateContainer(systemController[system]);
                var p2scopeX = snesAnalog.GetOrCreateContainer("P2 Scope X");
                var p2scopeY = snesAnalog.GetOrCreateContainer("P2 Scope Y");

                p2scopeX["Value"] = "WMouse X";
                p2scopeX.SetObject("Mult", 1.0);
                p2scopeX.SetObject("Deadzone", 0.1);

                p2scopeY["Value"] = "WMouse Y";
                p2scopeY.SetObject("Mult", 1.0);
                p2scopeY.SetObject("Deadzone", 0.1);
            }

            if (system == "saturn")
            {
                var saturnTroller = trollers.GetOrCreateContainer(systemController[system]);

                saturnTroller["P" + playerIndex + " Trigger"] = "WMouse L";
                saturnTroller["P" + playerIndex + " Offscreen Shot"] = "WMouse R";
                saturnTroller["P" + playerIndex + " Start"] = "WMouse M";

                var saturnAnalog = analogtrollers.GetOrCreateContainer(systemController[system]);
                var p1X = saturnAnalog.GetOrCreateContainer("P2 X Axis");
                var p1Y = saturnAnalog.GetOrCreateContainer("P2 Y Axis");

                p1X["Value"] = "WMouse X";
                p1X.SetObject("Mult", 1.0);
                p1X.SetObject("Deadzone", 0.1);

                p1Y["Value"] = "WMouse Y";
                p1Y.SetObject("Mult", 1.0);
                p1Y.SetObject("Deadzone", 0.1);
            }
        }
    }
}