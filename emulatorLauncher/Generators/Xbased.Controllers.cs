using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class XbasedGenerator : Generator
    {
        // Duke inputs as xbased names them (map.p<N>.<input>, app/ui/input_map.c)
        private static readonly string[] _xbasedInputs =
        {
            "a", "b", "x", "y", "black", "white", "lt", "rt", "start", "back", "ls", "rs",
            "up", "down", "left", "right", "lup", "ldown", "lleft", "lright", "rup", "rdown", "rleft", "rright"
        };

        /// <summary>
        /// pad.p<N>=pad<K>: K is the rank of the gamepad in the order xbased opens them (SDL_EVENT_GAMEPAD_ADDED),
        /// i.e. the SDL3 order of the recognized gamepads, then the joysticks xbased gives a generic mapping.
        /// The default xbased mapping is positional (A south, Black RB, White LB...): the map.* keys are removed.
        /// </summary>
        private void ConfigureControllers(XbasedIniFile ini)
        {
            if (SystemConfig.getOptBoolean("disableautocontrollers"))
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // xbased sets no SDL joystick hint
            Sdl3GameController.SetEnumerationHints(Sdl3GameController.Sdl3HintProfile.Sdl3Default);

            var sdl3 = Sdl3GameController.GetControllers();
            var xbasedOrder = sdl3.Where(c => c.IsGamepad).OrderBy(c => c.EnumerationIndex)
                .Concat(sdl3.Where(c => !c.IsGamepad).OrderBy(c => c.EnumerationIndex))
                .ToList();

            var pads = this.Controllers.Where(c => !c.IsKeyboard && c.Config != null).OrderBy(c => c.PlayerIndex).Take(4).ToList();

            for (int player = 1; player <= 4; player++)
            {
                foreach (string input in _xbasedInputs)
                    ini.Remove("map.p" + player + "." + input);

                ini.Set("pad.p" + player, GetPadDevice(player, pads, xbasedOrder));
            }

            // Stick dead zone and rumble
            ini.Set("deadzone", SystemConfig.GetValueOrDefault("xbased_deadzone", "10"));
            ini.Set("rumble", SystemConfig.isOptSet("xbased_rumble") && !SystemConfig.getOptBoolean("xbased_rumble") ? "0" : "1");
        }

        private static string GetPadDevice(int player, List<Controller> pads, List<Sdl3GameController> xbasedOrder)
        {
            // No controller: player 1 on keyboard (auto also takes the first gamepad plugged later)
            if (player > pads.Count)
                return player == 1 ? "auto" : "none";

            var controller = pads[player - 1];
            var sdl3 = controller.Sdl3Controller;
            int rank = sdl3 == null ? -1 : xbasedOrder.IndexOf(sdl3);

            if (rank < 0)
            {
                // auto gives player N the N-th gamepad opened by xbased
                SimpleLogger.Instance.Warning("[Generator] xbased : no SDL3 match for " + controller.ToShortString() + ", player " + player + " set to auto.");
                return "auto";
            }

            SimpleLogger.Instance.Info("[Generator] xbased player " + player + " = " + controller.ToShortString() + " -> pad" + (rank + 1));
            return "pad" + (rank + 1);
        }
    }
}
