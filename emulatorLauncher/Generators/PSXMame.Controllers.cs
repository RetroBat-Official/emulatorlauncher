using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using EmulatorLauncher.Common.Joysticks;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class PSXMameGenerator
    {
        private bool ConfigureMameControllers(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return false;

            if (Controllers.Count == 0)
                return false;

            else if (Controllers.Count == 1)
            {
                var c1 = Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                if (c1.IsKeyboard)
                    return false;
            }
            
            // Delete existing default file if any
            string defaultCtrl = Path.Combine(path, "ctrlr", "retrobat.cfg");
            if (File.Exists(defaultCtrl))
                File.Delete(defaultCtrl);

            var mameconfig = new XElement("mameconfig", new XAttribute("version", "10"));
            var system = new XElement("system", new XAttribute("name", "default"));
            var input = new XElement("input");

            var mameControllers = this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).ToList();
            int controllerCount = mameControllers.Count;

            foreach (var controller in mameControllers)
            {
                int i = controller.PlayerIndex;

                int tempIndex = mameControllers.OrderBy(c => c.DirectInput.DeviceIndex).ToList().IndexOf(controller) + 1;
                int cIndex = tempIndex + controllerCount;

                string joy = "JOYCODE_" + cIndex + "_";
                bool xinputCtrl = controller.IsXInputDevice;
                string guid = (controller.Guid.ToString()).Substring(0, 24) + "00000000";
                SdlToDirectInput ctrlr = null;
                string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
                bool analogtodpad = SystemConfig.getOptBoolean("psxmame_analog_to_dpad");

                // Get dinput mapping information
                if (!File.Exists(gamecontrollerDB))
                {
                    SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                    gamecontrollerDB = null;
                }
                if (gamecontrollerDB != null)
                {
                    SimpleLogger.Instance.Info("[INFO] Player "+ i + " . Fetching gamecontrollerdb.txt file with guid : " + guid);

                    try { ctrlr = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid); }
                    catch { }

                    if (ctrlr == null || ctrlr.ButtonMappings == null)
                        SimpleLogger.Instance.Info("[INFO] Player "+ i + ". No controller mapping found in gamecontrollerdb.txt file for guid : " + guid);
                    else
                        SimpleLogger.Instance.Info("[INFO] Player " + i + ": controller mapping found in gamecontrollerDB file.");
                }

                #region player1
                // Add UI mapping for player 1 to control MAME UI + Service menu
                if (i == 1)
                {
                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_SELECT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_ENTER")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_CANCEL"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_ESC")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR KEYCODE_UP")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_DOWN"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR KEYCODE_DOWN")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_LEFT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_LEFT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_RIGHT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_RIGHT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_PAUSE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_P")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P1_GAMBLE_SERVICE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_F2")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "SERVICE1"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftstick", xinputCtrl) + " " + joy + GetDinputMapping(ctrlr, "rightstick", xinputCtrl) + " OR KEYCODE_9")));

                    // Standard joystick buttons and directions
                    if (analogtodpad)
                    {
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR " + joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR " + joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR KEYCODE_DOWN")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR " + joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR " + joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_RIGHT")));
                    }

                    else
                    {
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl) + " OR KEYCODE_UP")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl) + " OR KEYCODE_DOWN")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl) + " OR KEYCODE_LEFT")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl) + " OR KEYCODE_RIGHT")));
                    }

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1) + " OR KEYCODE_I")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1) + " OR KEYCODE_K")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1) + " OR KEYCODE_J")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1) + " OR KEYCODE_L")));

                    if (!analogtodpad)
                    {
                        input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1) + " OR KEYCODE_E")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1) + " OR KEYCODE_D")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1) + " OR KEYCODE_S")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1) + " OR KEYCODE_F")));
                    }

                    else
                    {
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_E")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), "KEYCODE_D")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + "KEYCODE_S")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + "KEYCODE_F")));
                    }

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl) + " OR KEYCODE_LCONTROL")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl) + " OR KEYCODE_LALT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl) + " OR KEYCODE_SPACE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "y", xinputCtrl) + " OR KEYCODE_LSHIFT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl) + " OR KEYCODE_Z")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl) + " OR KEYCODE_X")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON9"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON10"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON11"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON12"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON13"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON14"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON15"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON16"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_START"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_SELECT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5")));

                    // Start & coin
                    input.Add(new XElement
                        ("port", new XAttribute("type", "START" + i),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl) + " OR KEYCODE_1")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "COIN" + i),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl) + " OR KEYCODE_5")));

                    var analogList = new[] {
                          new { type = "standard", value = "NONE" },
                          new { type = "decrement", value = "NONE" },
                          new { type = "increment", value = "NONE" }
                        };

                    foreach(string a in AnalogToNone)
                    {
                        input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_" + a),
                          analogList.Select(
                            x => new XElement("newseq", new XAttribute("type", x.type), x.value))));
                    }
                }
                #endregion

                #region other players
                // Max 8 players for mame
                else if (i <= 8)
                {
                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpup", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpdown", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpleft", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "dpright", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, -1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "righty", xinputCtrl, 1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, -1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightx", xinputCtrl, 1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, -1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "lefty", xinputCtrl, 1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, -1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftx", xinputCtrl, 1))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "a", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "b", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "x", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "y", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "leftshoulder", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "rightshoulder", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON9"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON10"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON11"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON12"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON13"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON14"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON15"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_BUTTON16"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_START"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_SELECT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl))));

                    // Start & coin
                    input.Add(new XElement
                        ("port", new XAttribute("type", "START" + i),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "start", xinputCtrl))));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "COIN" + i),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + GetDinputMapping(ctrlr, "back", xinputCtrl))));

                    var analogList = new[] {
                          new { type = "standard", value = "NONE" },
                          new { type = "decrement", value = "NONE" },
                          new { type = "increment", value = "NONE" }
                        };

                    foreach (string a in AnalogToNone)
                    {
                        input.Add(new XElement
                        ("port", new XAttribute("type", "P" + i + "_" + a),
                          analogList.Select(
                            x => new XElement("newseq", new XAttribute("type", x.type), x.value))));
                    }
                }

                if (mameControllers.Count < 8)
                {
                    for (int j = mameControllers.Count + 1; j <= 8; j++)
                    {
                        input.Add(new XElement
                        ("port", new XAttribute("type", "P" + j + "_JOYSTICK_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICK_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICK_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICK_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON1"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON2"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON3"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON9"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON10"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON11"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON12"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON13"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON14"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON15"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_BUTTON16"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "START" + j),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + j),
                                new XElement("newseq", new XAttribute("type", "standard"), "NONE")));

                        var analogList = new[] {
                          new { type = "standard", value = "NONE" },
                          new { type = "decrement", value = "NONE" },
                          new { type = "increment", value = "NONE" }
                        };

                        foreach (string a in AnalogToNone)
                        {
                            input.Add(new XElement
                            ("port", new XAttribute("type", "P" + j + "_" + a),
                              analogList.Select(
                                x => new XElement("newseq", new XAttribute("type", x.type), x.value))));
                        }
                    }
                }
            }
            #endregion

            XDocument xdoc = new XDocument
                (
                    new XDeclaration("1.0", null, null)
                );
            
            xdoc.Add(mameconfig);
            mameconfig.Add(system);
            system.Add(input);

            xdoc.Save(defaultCtrl);

            if (!File.Exists(defaultCtrl))
                return false;

            return true;
        }

        private string GetDinputMapping(SdlToDirectInput c, string buttonkey, bool isXinput, int axisDirection = 0)
        {
            if (c == null)
                return "NONE";

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return "NONE";
            }

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return "NONE";
            }

            string button = c.ButtonMappings[buttonkey];

            // For xInput : specific treatment of axis
            if (isXinput && button == "a5")
                button = "a2";
            if (isXinput && button == "a2")
                axisDirection = 1;

            if (button.StartsWith("b"))
            {
                int buttonID = (button.Substring(1).ToInteger()) + 1;
                return "BUTTON" + buttonID;
            }

            else if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();

                switch (hatID)
                {
                    case 1:
                        return "HATSWITCHU";
                    case 2:
                        return "HATSWITCHR";
                    case 4:
                        return "HATSWITCHD";
                    case 8:
                        return "HATSWITCHL";
                }
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    axisDirection = -1;
                }
                if (button.StartsWith("+a"))
                {
                    axisID = button.Substring(2).ToInteger();
                    axisDirection = 1;
                }

                switch (axisID)
                {
                    case 0:
                        if (axisDirection == 1) return "XAXIS_RIGHT_SWITCH";
                        else if (axisDirection == -1) return "XAXIS_LEFT_SWITCH";
                        else return "XAXIS";
                    case 1:
                        if (axisDirection == 1) return "YAXIS_DOWN_SWITCH";
                        else if (axisDirection == -1) return "YAXIS_UP_SWITCH";
                        else return "YAXIS";
                    case 2:
                        if (axisDirection == 1) return "ZAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "ZAXIS_NEG_SWITCH";
                        else return "ZAXIS";
                    case 3:
                        if (axisDirection == 1) return "RXAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RXAXIS_NEG_SWITCH";
                        else return "RXAXIS";
                    case 4:
                        if (axisDirection == 1) return "RYAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RYAXIS_NEG_SWITCH";
                        else return "RYAXIS";
                    case 5:
                        if (axisDirection == 1) return "RZAXIS_POS_SWITCH";
                        else if (axisDirection == -1) return "RZAXIS_NEG_SWITCH";
                        else return "RZAXIS";
                }
            }

            return "NONE";
        }

        static readonly List<string> AnalogToNone = new List<string>()
        {
            "PEDAL", "PEDAL2", "PEDAL3", "PADDLE", "PADDLE_V", "POSITIONAL", "POSITIONAL_V", "DIAL", "DIAL_V", "TRACKBALL_X", "TRACKBALL_Y", "AD_STICK_X", "AD_STICK_Y", "AD_STICK_Z",
              "LIGHTGUN_X", "LIGHTGUN_Y", "MOUSE_X", "MOUSE_Y"
        };
    }
}
