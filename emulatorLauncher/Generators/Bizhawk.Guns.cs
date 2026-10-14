using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System.Linq;

namespace EmulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private bool _lightgunConfigured = false;

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

            _lightgunConfigured = true;

            var guns = RawLightgun.GetRawLightguns();
            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
            {
                Guns.StartSindenSoftware();
            }

            var trollers = json.GetOrCreateContainer("AllTrollers");
            var analogtrollers = json.GetOrCreateContainer("AllTrollersAnalog");

            if (system == "mastersystem")
            {
                var smsTroller = trollers.GetOrCreateContainer("SMS Light Phaser Controller");

                smsTroller["P1 Trigger"] = "WMouse L";
                smsTroller["P2 B1"] = "WMouse R";       // Grenades for operation wolf
                smsTroller["Pause"] = "Number1";        // Console pause = start (pad bindings are not read when the phaser is in port 1)

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

            if (system == "nes" || system == "famicom")
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

            if (system == "snes" || system == "superfamicom")
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
                var p1X = saturnAnalog.GetOrCreateContainer("P1 X Axis");
                var p1Y = saturnAnalog.GetOrCreateContainer("P1 Y Axis");

                p1X["Value"] = "WMouse X";
                p1X.SetObject("Mult", 1.0);
                p1X.SetObject("Deadzone", 0.1);

                p1Y["Value"] = "WMouse Y";
                p1Y.SetObject("Mult", 1.0);
                p1Y.SetObject("Deadzone", 0.1);
            }
        }

        /// <summary>
        /// DolphinBar Wiimote (mode 1 or 2) : the D-pad sends the arrow keys.
        /// D-pad Right = Start / Pause, D-pad Left = Select, D-pad Down = secondary gun button.
        /// BizHawk reads one mouse, so only one gun is used.
        /// Must be called after the pad configuration, which resets the pad bindings.
        /// </summary>
        private void ConfigureDolphinBarKeys(DynamicJson json, string system)
        {
            if (!_lightgunConfigured)
                return;

            if (!RawLightgun.GetRawLightguns().Any(g => g.Type == RawLighGunType.MayFlashWiimote))
                return;

            var trollers = json.GetOrCreateContainer("AllTrollers");

            switch (system)
            {
                case "nes":
                case "famicom":
                    {
                        // Zapper has only a trigger : start and select are on the player 1 pad
                        var nesTroller = trollers.GetOrCreateContainer(systemController[system]);
                        AppendBinding(nesTroller, "P1 Start", "Right");
                        AppendBinding(nesTroller, "P1 Select", "Left");
                    }
                    break;
                case "snes":
                case "superfamicom":
                    {
                        var snesTroller = trollers.GetOrCreateContainer(systemController[system]);
                        AppendBinding(snesTroller, "P2 Pause", "Right");
                        AppendBinding(snesTroller, "P2 Cursor", "Down");
                    }
                    break;
                case "saturn":
                    {
                        var saturnTroller = trollers.GetOrCreateContainer(systemController[system]);
                        AppendBinding(saturnTroller, "P1 Start", "Right");
                    }
                    break;
                case "mastersystem":
                    {
                        var smsTroller = trollers.GetOrCreateContainer("SMS Light Phaser Controller");
                        AppendBinding(smsTroller, "Pause", "Right");
                    }
                    break;
            }
        }

        /// <summary>
        /// Adds an input to a BizHawk binding list (inputs separated by ", "), without duplicates.
        /// </summary>
        private static void AppendBinding(DynamicJson container, string key, string input)
        {
            string value = container[key];

            if (string.IsNullOrEmpty(value))
                container[key] = input;
            else if (!value.Split(',').Select(v => v.Trim()).Contains(input))
                container[key] = value + ", " + input;
        }
    }
}