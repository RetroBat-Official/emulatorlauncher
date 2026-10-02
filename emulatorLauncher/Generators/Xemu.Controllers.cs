using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class XEmuGenerator
    {
        // The pad written to xemu port 1, set by the port loop in
        // SetupTOMLConfiguration. Every Chihiro JVS gamepad binding reads
        // bound_controllers[0], so it has to be that exact device.
        private Controller _chihiroPad;

        // Chihiro JVS binding codes (ui/xemu-input.h in the fork).
        // Gamepad button: 2001 + SDL_GamepadButton
        private const int PAD_A = 2001;   // SOUTH
        private const int PAD_B = 2002;   // EAST
        private const int PAD_X = 2003;   // WEST
        private const int PAD_Y = 2004;   // NORTH
        private const int PAD_BACK = 2005;
        private const int PAD_START = 2007;
        private const int PAD_LB = 2010;
        private const int PAD_RB = 2011;

        // Gamepad half axis: 3001 + axis * 2 + (positive ? 1 : 0)
        private const int PAD_LEFTX_NEG = 3001;
        private const int PAD_LEFTX_POS = 3002;
        private const int PAD_LEFTY_NEG = 3003;   // stick pushed up
        private const int PAD_LEFTY_POS = 3004;
        private const int PAD_RIGHTX_NEG = 3005;
        private const int PAD_RIGHTX_POS = 3006;
        private const int PAD_RIGHTY_NEG = 3007;
        private const int PAD_RIGHTY_POS = 3008;
        private const int PAD_LTRIGGER = 3010;
        private const int PAD_RTRIGGER = 3012;

        // Keyboard scancodes, kept for the operator buttons and for player 2
        private const int KEY_1 = 30;
        private const int KEY_2 = 31;
        private const int KEY_5 = 34;
        private const int KEY_6 = 35;
        private const int KEY_9 = 38;
        private const int KEY_0 = 39;

        private void ConfigureChihiroControllers(IniTomlFile ini)
        {
            if (SystemConfig.getOptBoolean("disableautocontrollers"))
            {
                SimpleLogger.Instance.Info("[Generator] Chihiro controller autoconfiguration disabled.");
                return;
            }

            ini.WriteValue("chihiro.jvs", "service", KEY_9.ToString());
            ini.WriteValue("chihiro.jvs", "test", KEY_0.ToString());
            ini.WriteValue("chihiro.jvs_p2", "start", KEY_2.ToString());
            ini.WriteValue("chihiro.jvs_p2", "coin", KEY_6.ToString());

            var pad = _chihiroPad;

            if (pad == null || pad.SdlController == null)
            {
                if (pad != null)
                    SimpleLogger.Instance.Info("[Generator] Port 1 device is not an SDL gamepad, keeping keyboard defaults for Chihiro.");

                ini.WriteValue("chihiro.jvs", "start", KEY_1.ToString());
                ini.WriteValue("chihiro.jvs", "coin", KEY_5.ToString());
                return;
            }

            SimpleLogger.Instance.Info("[Generator] Chihiro controller configuration for " + pad.ToShortString());

            // Face buttons, swapped for a pad with the Nintendo layout
            bool invertButtons = SystemConfig.getOptBoolean("buttonsInvert");
            int btnA = invertButtons ? PAD_B : PAD_A;
            int btnB = invertButtons ? PAD_A : PAD_B;
            int btnX = invertButtons ? PAD_Y : PAD_X;
            int btnY = invertButtons ? PAD_X : PAD_Y;

            // System buttons, shared by every game
            ini.WriteValue("chihiro.jvs", "start", PAD_START.ToString());
            ini.WriteValue("chihiro.jvs", "coin", PAD_BACK.ToString());
            ini.WriteValue("chihiro.jvs", "card_in", btnB.ToString());

            // Driving: left stick steers, triggers are the pedals, shoulders the
            // sequential shifter (Crazy Taxi, OutRun 2, Maximum Tune)
            ini.WriteValue("chihiro.jvs", "steer_left", PAD_LEFTX_NEG.ToString());
            ini.WriteValue("chihiro.jvs", "steer_right", PAD_LEFTX_POS.ToString());
            ini.WriteValue("chihiro.jvs", "gas", PAD_RTRIGGER.ToString());
            ini.WriteValue("chihiro.jvs", "brake", PAD_LTRIGGER.ToString());
            ini.WriteValue("chihiro.jvs", "gear_up", PAD_RB.ToString());
            ini.WriteValue("chihiro.jvs", "gear_down", PAD_LB.ToString());

            // Crazy Taxi High Roller
            ini.WriteValue("chihiro.jvs.ctx", "drive_gear", btnB.ToString());
            ini.WriteValue("chihiro.jvs.ctx", "reverse", btnX.ToString());
            ini.WriteValue("chihiro.jvs.ctx", "jump", btnA.ToString());

            // OutRun 2
            ini.WriteValue("chihiro.jvs.or2", "view_change", btnY.ToString());

            // Maximum Tune 2 - the H shifter positions (gear1..gear6) are left
            // unbound, they only make sense on a real shifter
            ini.WriteValue("chihiro.jvs.wmmt2", "view_change", btnY.ToString());
            ini.WriteValue("chihiro.jvs.wmmt2", "intrude_change", btnX.ToString());

            // Ollie King: the board tilts with the left stick, grabs on the shoulders
            ini.WriteValue("chihiro.jvs.ok", "swing_left", PAD_LEFTX_NEG.ToString());
            ini.WriteValue("chihiro.jvs.ok", "swing_right", PAD_LEFTX_POS.ToString());
            ini.WriteValue("chihiro.jvs.ok", "board_front", PAD_LEFTY_NEG.ToString());
            ini.WriteValue("chihiro.jvs.ok", "board_rear", PAD_LEFTY_POS.ToString());
            ini.WriteValue("chihiro.jvs.ok", "left_grab", PAD_LB.ToString());
            ini.WriteValue("chihiro.jvs.ok", "right_grab", PAD_RB.ToString());

            // Gundam Battle Operating Simulator: one stick per twin-stick lever
            ini.WriteValue("chihiro.jvs.gundam", "l_up", PAD_LEFTY_NEG.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "l_down", PAD_LEFTY_POS.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "l_left", PAD_LEFTX_NEG.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "l_right", PAD_LEFTX_POS.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "l_trigger", PAD_LTRIGGER.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "l_button", PAD_LB.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "r_up", PAD_RIGHTY_NEG.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "r_down", PAD_RIGHTY_POS.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "r_left", PAD_RIGHTX_NEG.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "r_right", PAD_RIGHTX_POS.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "r_trigger", PAD_RTRIGGER.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "r_button", PAD_RB.ToString());
            ini.WriteValue("chihiro.jvs.gundam", "pedal", btnA.ToString());
        }
    }
}