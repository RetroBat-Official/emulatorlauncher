using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class ZQuestClassicGenerator : Generator
    {
        // Control scheme written in controls.cfg and assigned in zc.cfg
        private const string SchemeName = "RetroBat";

        // 1-11 = A, B, X, Y, LB, RB, Back, Start, Guide, LThumb, RThumb / 12-13 = LT, RT / 14-17 = Dpad Up, Down, Left, Right
        private const int ZcBtnA = 1;
        private const int ZcBtnB = 2;
        private const int ZcBtnX = 3;
        private const int ZcBtnY = 4;
        private const int ZcBtnLB = 5;
        private const int ZcBtnRB = 6;
        private const int ZcBtnBack = 7;
        private const int ZcBtnStart = 8;
        private const int ZcBtnGuide = 9;
        private const int ZcBtnLT = 12;
        private const int ZcBtnRT = 13;
        private const int ZcBtnDpadUp = 14;
        private const int ZcBtnDpadDown = 15;
        private const int ZcBtnDpadLeft = 16;
        private const int ZcBtnDpadRight = 17;

        /// <summary>
        /// ZC is single player and reads one joystick: the one at 'joystick_index' of the active control scheme.
        /// Joystick enumeration is done by ZC's statically linked SDL2 (2.32.10, Allegro SDL joystick driver),
        /// in SDL device index order : it matches EmulatorLauncher's SDL2 index and GUID.
        /// </summary>
        private void SetupControllers(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);
            if (c1 == null || c1.IsKeyboard)
            {
                SimpleLogger.Instance.Info("[INFO] No gamepad for player 1, ZC keyboard defaults are used.");
                return;
            }

            // SDL2 device index, same enumeration order as ZC (Allegro SDL driver)
            int joyIndex;
            if (c1.SdlController != null)
                joyIndex = c1.SdlController.Index;
            else
            {
                joyIndex = c1.DeviceIndex;
                SimpleLogger.Instance.Warning("[WARNING] ZQuest Classic : no SDL match for player 1, falling back to device index " + joyIndex);
            }

            if (joyIndex < 0)
                joyIndex = 0;

            bool swap = SystemConfig.getOptBoolean("zc_swap_buttons");

            WriteControlScheme(path, joyIndex, swap);
            AssignControlScheme(path, c1);
        }

        /// <summary>
        /// controls.cfg (Allegro 5 config) : one section per control scheme.
        /// Only joystick related keys are written, keyboard keys keep ZC defaults.
        /// </summary>
        private void WriteControlScheme(string path, int joyIndex, bool swap)
        {
            string controlsFile = Path.Combine(path, "controls.cfg");

            try
            {
                using (var ini = IniFile.FromFile(controlsFile, IniOptions.KeepEmptyLines))
                {
                    ini.WriteValue(SchemeName, "joystick_index", joyIndex.ToString());
                    ini.WriteValue(SchemeName, "analog_movement", "1");        // move with the left stick too
                    ini.WriteValue(SchemeName, "btn_menu", ZcBtnGuide.ToString());

                    ini.WriteValue(SchemeName, "btn_up", ZcBtnDpadUp.ToString());
                    ini.WriteValue(SchemeName, "btn_down", ZcBtnDpadDown.ToString());
                    ini.WriteValue(SchemeName, "btn_left", ZcBtnDpadLeft.ToString());
                    ini.WriteValue(SchemeName, "btn_right", ZcBtnDpadRight.ToString());

                    // Game A/B and EX1/EX2 (X/Y) on the buttons labelled A/B/X/Y, or swapped
                    ini.WriteValue(SchemeName, "btn_a", (swap ? ZcBtnB : ZcBtnA).ToString());
                    ini.WriteValue(SchemeName, "btn_b", (swap ? ZcBtnA : ZcBtnB).ToString());
                    ini.WriteValue(SchemeName, "btn_ex1", (swap ? ZcBtnY : ZcBtnX).ToString());
                    ini.WriteValue(SchemeName, "btn_ex2", (swap ? ZcBtnX : ZcBtnY).ToString());

                    ini.WriteValue(SchemeName, "btn_s", ZcBtnStart.ToString());     // start
                    ini.WriteValue(SchemeName, "btn_p", ZcBtnBack.ToString());      // map
                    ini.WriteValue(SchemeName, "btn_l", ZcBtnLB.ToString());
                    ini.WriteValue(SchemeName, "btn_r", ZcBtnRB.ToString());
                    ini.WriteValue(SchemeName, "btn_ex3", ZcBtnLT.ToString());
                    ini.WriteValue(SchemeName, "btn_ex4", ZcBtnRT.ToString());
                }
            }
            catch (System.Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Unable to write " + controlsFile + " : " + ex.Message);
            }
        }

        /// <summary>
        /// Scheme priority in ZC is quest-specific > per-gamepad > global (control_scheme.cpp).
        /// A per-gamepad scheme is auto-created the first time a pad is seen and keeps the joystick index of that moment,
        /// so the RetroBat scheme is also assigned to the player 1 pad identity : '[Controls] gamepad__<guid>'.
        /// Identity = raw SDL GUID in lowercase hexadecimal (CRC and driver byte included).
        /// </summary>
        private void AssignControlScheme(string path, Controller c1)
        {
            string cfgFile = Path.Combine(path, "zc.cfg");

            var guids = new HashSet<string>();

            if (c1.SdlController != null && (object)c1.SdlController.Guid != null)
                guids.Add(((string)c1.SdlController.Guid).ToLowerInvariant());

            try
            {
                foreach (var guid in c1.CompatibleSdlGuids)
                    guids.Add(guid.ToLowerInvariant());
            }
            catch { }

            try
            {
                using (var ini = IniFile.FromFile(cfgFile, IniOptions.KeepEmptyLines))
                {
                    ini.WriteValue("Controls", "global_control_scheme", SchemeName);

                    foreach (var guid in guids.Where(g => g.Length == 32))
                    {
                        ini.WriteValue("Controls", "gamepad__" + guid, SchemeName);
                        SimpleLogger.Instance.Info("[Generator] ZQuest Classic : scheme " + SchemeName + " assigned to gamepad " + guid);
                    }
                }
            }
            catch (System.Exception ex)
            {
                SimpleLogger.Instance.Error("[Generator] Unable to write " + cfgFile + " : " + ex.Message);
            }
        }
    }
}