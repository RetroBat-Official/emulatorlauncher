using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;
using System.Xml;
using System.Xml.Linq;

namespace emulatorLauncher
{
    partial class Mame64Generator
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

            string inputConfig = Path.Combine(path, "retrobat_auto.cfg");
            if (File.Exists(inputConfig))
                File.Delete(inputConfig);

            XDocument xdoc = new XDocument
                (
                    new XDeclaration("1.0", null, null),
                    new XElement("mameconfig", new XAttribute("version", "10"),
                        new XElement("system", new XAttribute("name", "default"),
                            new XElement("input")
                )));

            var input = xdoc.Element("input");

            foreach(var controller in this.Controllers.OrderBy(i => i.PlayerIndex))
            {
                int cIndex = controller.DirectInput.DeviceIndex + 1;
                string joy = "JOYCODE_" + cIndex + "_";

                var mapping = genericMapping;

                int i = controller.PlayerIndex;

                var guns = RawLightgun.GetRawLightguns();
                bool multigun = guns.Length >= 2;

                if (controller.IsXInputDevice)
                    mapping = xInputMapping;

                else if (controller.VendorID == USB_VENDOR.NINTENDO)
                    mapping = nintendoMapping;

                // Add UI mapping for player 1 to control MAME UI + Service menu
                if (i == 1)
                {
                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_MENU"), 
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["south"] + " OR KEYCODE_TAB")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_SELECT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR KEYCODE_ENTER")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_BACK"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR KEYCODE_ESC")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_CANCEL"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["start"] + " OR KEYCODE_ESC")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_UP"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR " + joy + mapping["lsup"] + " OR KEYCODE_UP")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_DOWN"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR " + joy + mapping["lsdown"] + " OR KEYCODE_DOWN")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_LEFT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR " + joy + mapping["lsleft"] + " OR KEYCODE_LEFT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_RIGHT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR " + joy + mapping["lsright"] + " OR KEYCODE_RIGHT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_PAUSE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["east"] + " OR KEYCODE_P")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_REWIND_SINGLE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["left"] + " OR KEYCODE_TILDE KEYCODE_LSHIFT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_FAST_FORWARD"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["right"] + " OR KEYCODE_INSERT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_SAVE_STATE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["west"] + " OR KEYCODE_F7 KEYCODE_LSHIFT")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "UI_LOAD_STATE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["north"] + " OR KEYCODE_F7")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "SERVICE"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " " + joy + mapping["r3"] + " OR KEYCODE_F2")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "SERVICE1"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " " + joy + mapping["r3"] + " OR KEYCODE_9")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "TILT"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["r1"] + " OR KEYCODE_T")));

                    input.Add(new XElement
                        ("port", new XAttribute("type", "TILT1"),
                            new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " " + joy + mapping["r1"] + " OR KEYCODE_T")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"] + " OR KEYCODE_UP")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"] + " OR KEYCODE_DOWN")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"] + " OR KEYCODE_LEFT")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"] + " OR KEYCODE_RIGHT")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsup"] + " OR KEYCODE_I")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsdown"] + " OR KEYCODE_K")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsleft"] + " OR KEYCODE_J")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsright"] + " OR KEYCODE_L")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsup"] + " OR KEYCODE_E")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsdown"] + " OR KEYCODE_D")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsleft"] + " OR KEYCODE_S")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsright"] + " OR KEYCODE_F")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR KEYCODE_LCONTROL OR MOUSECODE_" + i + "_BUTTON1 OR GUNCODE_" + i + "_BUTTON1")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR KEYCODE_LALT OR MOUSECODE_" + i + "_BUTTON3 OR GUNCODE_" + i + "_BUTTON2")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"] + " OR KEYCODE_SPACE OR MOUSECODE_" + i + "_BUTTON2")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["north"] + " OR KEYCODE_LSHIFT")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l1"] + " OR KEYCODE_Z")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r1"] + " OR KEYCODE_X")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"] + " OR KEYCODE_C")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r3"] + " OR KEYCODE_V")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_1")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_5")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "START" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"] + " OR KEYCODE_1")));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"] + " OR KEYCODE_5")));
                }

                // Max 8 players for mame
                else if (i >= 8)
                {
                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["up"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["down"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["left"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICK_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["right"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsup"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsdown"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsleft"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKRIGHT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["rsright"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_UP"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsup"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_DOWN"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsdown"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_LEFT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsleft"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_JOYSTICKLEFT_RIGHT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["lsright"])));

                    // Case of 2 guns only for now, cannot test more than 2 guns so stop here
                    if (multigun && (i == 2))
                    {
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"] + " OR MOUSECODE_" + i + "_BUTTON1 OR GUNCODE_" + i + "_BUTTON1")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"] + " OR MOUSECODE_" + i + "_BUTTON3 OR GUNCODE_" + i + "_BUTTON2")));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"] + " OR MOUSECODE_" + i + "_BUTTON2")));
                    }
                    else
                    {
                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON1"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["south"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON2"),
                                 new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["east"])));

                        input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON3"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["west"])));
                    }

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON4"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["north"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON5"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l1"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON6"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r1"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON7"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["l3"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_BUTTON8"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["r3"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_START"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "P" + i + "_SELECT"),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "START" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["start"])));

                    input.Add(new XElement
                            ("port", new XAttribute("type", "COIN" + i),
                                new XElement("newseq", new XAttribute("type", "standard"), joy + mapping["select"])));
                }
            }

            xdoc.Save(inputConfig);

            if (!File.Exists(inputConfig))
                return false;

            return true;
        }

        static Dictionary<string, string> xInputMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON7"},
            { "r3",             "BUTTON8"},
            { "l2",             "SLIDER1" },
            { "r2",             "SLIDER2"},
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "START" },
            { "select",         "SELECT" },
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "HAT1UP" },
            { "down",           "HAT1DOWN" },
            { "left",           "HAT1LEFT" },
            { "right",          "HAT1RIGHT" },
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH"},
            { "rsup",           "RZAXIS_NEG_SWITCH" },
            { "rsdown",         "RZAXIS_POS_SWITCH" },
            { "rsleft",         "ZAXIS_NEG_SWITCH" },
            { "rsright",        "ZAXIS_POS_SWITCH"},
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };

        static Dictionary<string, string> nintendoMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON7"},
            { "r3",             "BUTTON8"},
            { "l2",             "SLIDER1" },
            { "r2",             "SLIDER2"},
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "START" },
            { "select",         "SELECT" },
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "HAT1UP" },
            { "down",           "HAT1DOWN" },
            { "left",           "HAT1LEFT" },
            { "right",          "HAT1RIGHT" },
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH"},
            { "rsup",           "RZAXIS_NEG_SWITCH" },
            { "rsdown",         "RZAXIS_POS_SWITCH" },
            { "rsleft",         "ZAXIS_NEG_SWITCH" },
            { "rsright",        "ZAXIS_POS_SWITCH"},
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };

        static Dictionary<string, string> genericMapping = new Dictionary<string, string>()
        {
            { "l3",             "BUTTON7"},
            { "r3",             "BUTTON8"},
            { "l2",             "SLIDER1" },
            { "r2",             "SLIDER2"},
            { "north",          "BUTTON4" },
            { "south",          "BUTTON1" },
            { "west",           "BUTTON3" },
            { "east",           "BUTTON2" },
            { "start",          "START" },
            { "select",         "SELECT" },
            { "l1",             "BUTTON5" },
            { "r1",             "BUTTON6" },
            { "up",             "HAT1UP" },
            { "down",           "HAT1DOWN" },
            { "left",           "HAT1LEFT" },
            { "right",          "HAT1RIGHT" },
            { "lsup",           "YAXIS_UP_SWITCH" },
            { "lsdown",         "YAXIS_DOWN_SWITCH" },
            { "lsleft",         "XAXIS_LEFT_SWITCH" },
            { "lsright",        "XAXIS_RIGHT_SWITCH"},
            { "rsup",           "RZAXIS_NEG_SWITCH" },
            { "rsdown",         "RZAXIS_POS_SWITCH" },
            { "rsleft",         "ZAXIS_NEG_SWITCH" },
            { "rsright",        "ZAXIS_POS_SWITCH"},
            { "ls_x",           "XAXIS" },
            { "ls_y",           "YAXIS" },
            { "rs_x",           "ZAXIS" },
            { "rs_y",           "RZAXIS"}
        };
    }
}
