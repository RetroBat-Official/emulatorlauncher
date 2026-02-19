using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

namespace EmulatorLauncher
{
    partial class DolphinControllers
    {
        static readonly InputKeyMapping _wiiMapping = new InputKeyMapping
        {
            { InputKey.x,               "Buttons/2" },
            { InputKey.b,               "Buttons/A" },
            { InputKey.y,               "Buttons/1" },
            { InputKey.a,               "Buttons/B" },
            { InputKey.pageup,          "Buttons/-" },
            { InputKey.pagedown,        "Buttons/+" },
            { InputKey.select,          "Buttons/Home" },
            { InputKey.up,              "D-Pad/Up" },
            { InputKey.down,            "D-Pad/Down" },
            { InputKey.left,            "D-Pad/Left" },
            { InputKey.right,           "D-Pad/Right" },
            { InputKey.joystick1up,     "IR/Up" },
            { InputKey.joystick1left,   "IR/Left" },
            { InputKey.joystick2up,     "Tilt/Forward" },
            { InputKey.joystick2left,   "Tilt/Left" },
            { InputKey.l3,              "IR/Relative Input Hold" },
            { InputKey.r3,              "Tilt/Modifier" }
        };

        static readonly Dictionary<string, string> wiiReverseAxes = new Dictionary<string, string>()
        {
            { "IR/Up",      "IR/Down"},
            { "IR/Left",    "IR/Right"},
            { "Swing/Up",   "Swing/Down"},
            { "Swing/Left", "Swing/Right"},
            { "Tilt/Left",  "Tilt/Right"},
            { "Tilt/Forward", "Tilt/Backward"},
            { "Nunchuk/Stick/Up" ,  "Nunchuk/Stick/Down"},
            { "Nunchuk/Stick/Left", "Nunchuk/Stick/Right"},
            { "Classic/Right Stick/Up" , "Classic/Right Stick/Down"},
            { "Classic/Right Stick/Left" , "Classic/Right Stick/Right"},
            { "Classic/Left Stick/Up" , "Classic/Left Stick/Down"},
            { "Classic/Left Stick/Left" , "Classic/Left Stick/Right" },
            { "uDraw/Stylus/Up" , "uDraw/Stylus/Down"},
            { "uDraw/Stylus/Left" , "uDraw/Stylus/Right" },
            { "Drawsome/Stylus/Up" , "Drawsome/Stylus/Down"},
            { "Drawsome/Stylus/Left" , "Drawsome/Stylus/Right" }
        };

        private static void GenerateControllerConfig_emulatedwiimotes(string path, string rom)
        {
            var extraOptions = new Dictionary<string, string>
            {
                ["Source"] = "1"
            };

            string romName = Path.GetFileName(rom);

            var wiiMapping = new InputKeyMapping(_wiiMapping);
            var extraMapping = new Dictionary<string, InputKey>();

            if (Program.SystemConfig["controller_mode"] != "cc" && !romName.Contains(".cc."))
            {
                if (Program.SystemConfig["controller_mode"] == "side" || romName.Contains(".side."))
                {
                    extraOptions["Options/Sideways Wiimote"] = "1";
                    wiiMapping[InputKey.x] = "Buttons/A";
                    wiiMapping[InputKey.y] = "Buttons/1";
                    wiiMapping[InputKey.b] = "Buttons/2";
                    wiiMapping[InputKey.a] = "Buttons/B";
                    wiiMapping[InputKey.l2] = "Shake/X";
                    wiiMapping[InputKey.l2] = "Shake/Y";
                    wiiMapping[InputKey.l2] = "Shake/Z";
                    wiiMapping[InputKey.select] = "Buttons/-";
                    wiiMapping[InputKey.start] = "Buttons/+";
                    wiiMapping[InputKey.pageup] = "Tilt/Left";
                    wiiMapping[InputKey.pagedown] = "Tilt/Right";
                    wiiMapping.RemoveAll(x => x.Key == InputKey.joystick1up);
                    wiiMapping.RemoveAll(x => x.Key == InputKey.joystick1left);
                    wiiMapping.RemoveAll(x => x.Key == InputKey.joystick2up);
                    wiiMapping.RemoveAll(x => x.Key == InputKey.joystick2left);
                }

                // i: infrared, s: swing, t: tilt, n: nunchuk
                // 12 possible combinations : is si / it ti / in ni / st ts / sn ns / tn nt

                // i
                string[] infraredFirstTags = { ".is.", ".it.", ".in." };
                if (Program.SystemConfig["controller_mode"] == "is" || Program.SystemConfig["controller_mode"] == "it" || Program.SystemConfig["controller_mode"] == "in" || infraredFirstTags.Any(r => romName.Contains(r)))
                {
                    wiiMapping[InputKey.joystick1up] = "IR/Up";
                    wiiMapping[InputKey.joystick1left] = "IR/Left";
                    wiiMapping[InputKey.l3] = "IR/Relative Input Hold";
                }

                string[] infraredLastTags = { ".si.", ".ti.", ".ni." };
                if (Program.SystemConfig["controller_mode"] == "si" || Program.SystemConfig["controller_mode"] == "ti" || Program.SystemConfig["controller_mode"] == "ni" || infraredLastTags.Any(r => romName.Contains(r)))
                {
                    wiiMapping[InputKey.joystick2up] = "IR/Up";
                    wiiMapping[InputKey.joystick2left] = "IR/Left";
                    wiiMapping[InputKey.r3] = "IR/Relative Input Hold";
                }

                // s
                string[] swingFirstTags = { ".si.", ".st.", ".sn." };
                if (Program.SystemConfig["controller_mode"] == "si" || Program.SystemConfig["controller_mode"] == "st" || Program.SystemConfig["controller_mode"] == "sn" || swingFirstTags.Any(r => romName.Contains(r)))
                {
                    wiiMapping[InputKey.joystick1up] = "Swing/Up";
                    wiiMapping[InputKey.joystick1left] = "Swing/Left";
                }

                string[] swingLastTags = { ".is.", ".ts.", ".ns." };
                if (Program.SystemConfig["controller_mode"] == "is" || Program.SystemConfig["controller_mode"] == "ts" || Program.SystemConfig["controller_mode"] == "ns" || swingLastTags.Any(r => romName.Contains(r)))
                {
                    wiiMapping[InputKey.joystick2up] = "Swing/Up";
                    wiiMapping[InputKey.joystick2left] = "Swing/Left";
                }

                // t
                string[] tiltFirstTags = { ".ti.", ".ts.", ".tn." };
                if (Program.SystemConfig["controller_mode"] == "ti" || Program.SystemConfig["controller_mode"] == "ts" || Program.SystemConfig["controller_mode"] == "tn" || tiltFirstTags.Any(r => romName.Contains(r)))
                {
                    wiiMapping[InputKey.joystick1up] = "Tilt/Forward";
                    wiiMapping[InputKey.joystick1left] = "Tilt/Left";
                    wiiMapping[InputKey.l3] = "Tilt/Modifier";
                }

                string[] tiltLastTags = { ".it.", ".st.", ".nt." };
                if (Program.SystemConfig["controller_mode"] == "it" || Program.SystemConfig["controller_mode"] == "st" || Program.SystemConfig["controller_mode"] == "nt" || tiltLastTags.Any(r => romName.Contains(r)))
                {
                    wiiMapping[InputKey.joystick2up] = "Tilt/Forward";
                    wiiMapping[InputKey.joystick2left] = "Tilt/Left";
                    wiiMapping[InputKey.r3] = "Tilt/Modifier";
                }

                // n
                string[] nunchukFirstTags = { ".ni.", ".ns.", ".nt." };
                if (Program.SystemConfig["controller_mode"] == "ni" || Program.SystemConfig["controller_mode"] == "ns" || Program.SystemConfig["controller_mode"] == "nt" || nunchukFirstTags.Any(r => romName.Contains(r)))
                {
                    extraOptions["Extension"] = "Nunchuk";
                    wiiMapping[InputKey.l1] = "Nunchuk/Buttons/C";
                    wiiMapping[InputKey.r1] = "Nunchuk/Buttons/Z";
                    wiiMapping[InputKey.joystick1up] = "Nunchuk/Stick/Up";
                    wiiMapping[InputKey.joystick1left] = "Nunchuk/Stick/Left";
                    wiiMapping[InputKey.l3] = "Nunchuk/Stick/Modifier";
                    wiiMapping[InputKey.select] = "Buttons/-";
                    wiiMapping[InputKey.start] = "Buttons/+";

                    if (Program.SystemConfig.getOptBoolean("nunchuk_swing"))
                    {
                        wiiMapping[InputKey.l2] = "Nunchuk/Swing/Left";
                        wiiMapping[InputKey.l2] = "Nunchuk/Swing/Up";
                        wiiMapping[InputKey.l2] = "Nunchuk/Swing/Forward";
                    }
                    else
                    {
                        wiiMapping[InputKey.l2] = "Nunchuk/Shake/X";
                        wiiMapping[InputKey.l2] = "Nunchuk/Shake/Y";
                        wiiMapping[InputKey.l2] = "Nunchuk/Shake/Z";
                    }

                    wiiMapping[InputKey.r2] = "Shake/X";
                    wiiMapping[InputKey.r2] = "Shake/Y";
                    wiiMapping[InputKey.r2] = "Shake/Z";
                }

                string[] nunchukLastTags = { ".in.", ".sn.", ".tn." };
                if (Program.SystemConfig["controller_mode"] == "in" || Program.SystemConfig["controller_mode"] == "sn" || Program.SystemConfig["controller_mode"] == "tn" || nunchukLastTags.Any(r => romName.Contains(r)))
                {
                    extraOptions["Extension"] = "Nunchuk";
                    wiiMapping[InputKey.l1] = "Nunchuk/Buttons/C";
                    wiiMapping[InputKey.r1] = "Nunchuk/Buttons/Z";
                    wiiMapping[InputKey.joystick2up] = "Nunchuk/Stick/Up";
                    wiiMapping[InputKey.joystick2left] = "Nunchuk/Stick/Left";
                    wiiMapping[InputKey.r3] = "Nunchuk/Stick/Modifier";
                    wiiMapping[InputKey.select] = "Buttons/-";
                    wiiMapping[InputKey.start] = "Buttons/+";
                    wiiMapping[InputKey.l2] = "Shake/X";
                    wiiMapping[InputKey.l2] = "Shake/Y";
                    wiiMapping[InputKey.l2] = "Shake/Z";

                    if (Program.SystemConfig.getOptBoolean("nunchuk_swing"))
                    {
                        wiiMapping[InputKey.r2] = "Nunchuk/Swing/Left";
                        wiiMapping[InputKey.r2] = "Nunchuk/Swing/Up";
                        wiiMapping[InputKey.r2] = "Nunchuk/Swing/Forward";
                    }
                    else
                    {
                        wiiMapping[InputKey.r2] = "Nunchuk/Shake/X";
                        wiiMapping[InputKey.r2] = "Nunchuk/Shake/Y";
                        wiiMapping[InputKey.r2] = "Nunchuk/Shake/Z";
                    }
                }

                // udraw : uDraw tablet
                else if (Program.SystemConfig["controller_mode"] == "udraw" || romName.Contains(".udraw."))
                {
                    extraOptions["Extension"] = "uDraw";
                    wiiMapping[InputKey.b] = "uDraw/Touch/Pressure";
                    wiiMapping[InputKey.a] = "uDraw/Touch/Lift";
                    wiiMapping[InputKey.joystick1up] = "uDraw/Stylus/Up";
                    wiiMapping[InputKey.joystick1left] = "uDraw/Stylus/Left";
                    wiiMapping[InputKey.pagedown] = "uDraw/Buttons/Rocker Up";
                    wiiMapping[InputKey.pageup] = "uDraw/Buttons/Rocker Down";
                }

                // tatacon : TaikoDrum
                else if (Program.SystemConfig["controller_mode"] == "tatacon" || romName.Contains(".tatacon."))
                {
                    extraOptions["Extension"] = "TaTaCon";
                    wiiMapping[InputKey.l2] = "TaTaCon/Center/Left";
                    wiiMapping[InputKey.r2] = "TaTaCon/Center/Right";
                    wiiMapping[InputKey.pageup] = "TaTaCon/Rim/Left";
                    wiiMapping[InputKey.pagedown] = "TaTaCon/Rim/Right";
                }

                // guitar : Guitar
                else if (Program.SystemConfig["controller_mode"] == "guitar" || romName.Contains(".guitar."))
                {
                    extraOptions["Extension"] = "Guitar";
                    wiiMapping[InputKey.b] = "Guitar/Frets/Green";
                    wiiMapping[InputKey.pageup] = "Guitar/Frets/Red";
                    wiiMapping[InputKey.pagedown] = "Guitar/Frets/Yellow";
                    wiiMapping[InputKey.r2] = "Guitar/Frets/Blue";
                    wiiMapping[InputKey.y] = "Guitar/Frets/Orange";
                    wiiMapping[InputKey.l2] = "Guitar/Strum/Up";
                    wiiMapping[InputKey.select] = "Guitar/Buttons/-";
                    wiiMapping[InputKey.start] = "Guitar/Buttons/+";
                    wiiMapping[InputKey.up] = "Guitar/Stick/Up";
                    wiiMapping[InputKey.down] = "Guitar/Stick/Down";
                    wiiMapping[InputKey.left] = "Guitar/Stick/Left";
                    wiiMapping[InputKey.right] = "Guitar/Stick/Right";
                    wiiMapping[InputKey.joystick1left] = "Guitar/Whammy/Bar";

                    
                    extraMapping["Guitar/Frets/Green_2"] = InputKey.l2;
                    extraMapping["Guitar/Frets/Red_2"] = InputKey.a;
                    extraMapping["Guitar/Strum/Up_2"] = InputKey.pageup;
                    extraMapping["Guitar/Strum/Down_2"] = InputKey.down;
                    extraMapping["Guitar/Strum/Up_3"] = InputKey.pagedown;
                    extraMapping["Guitar/Strum/Up_4"] = InputKey.r2;
                    extraMapping["Guitar/Strum/Up_5"] = InputKey.y;
                    extraMapping["Guitar/Strum/Up_6"] = InputKey.up;
                    extraMapping["Guitar/Buttons/-_2"] = InputKey.joystick1up;
                    extraMapping["Guitar/Buttons/-_3"] = InputKey.joystick1down;
                    extraMapping["Guitar/Buttons/-_4"] = InputKey.joystick2up;
                    extraMapping["Guitar/Buttons/-_5"] = InputKey.joystick2down;
                    extraMapping["Guitar/Whammy/Bar_2"] = InputKey.joystick1right;
                    extraMapping["Guitar/Whammy/Bar_3"] = InputKey.joystick2left;
                    extraMapping["Guitar/Whammy/Bar_4"] = InputKey.joystick2right;
                }

                // drumsrb & drumsgh : Drums
                else if (Program.SystemConfig["controller_mode"] == "drumsrb" || romName.Contains(".drumsrb."))
                {
                    extraOptions["Extension"] = "Drums";
                    wiiMapping[InputKey.l2] = "Drums/Pads/Red";
                    wiiMapping[InputKey.pageup] = "Drums/Pads/Yellow";
                    wiiMapping[InputKey.pagedown] = "Drums/Pads/Blue";
                    wiiMapping[InputKey.r2] = "Drums/Pads/Green";
                    wiiMapping[InputKey.joystick1left] = "Drums/Pads/Bass";
                    wiiMapping[InputKey.select] = "Drums/Buttons/-";
                    wiiMapping[InputKey.start] = "Drums/Buttons/+";
                    wiiMapping[InputKey.up] = "Drums/Stick/Up";
                    wiiMapping[InputKey.down] = "Drums/Stick/Down";
                    wiiMapping[InputKey.left] = "Drums/Stick/Left";
                    wiiMapping[InputKey.right] = "Drums/Stick/Right";

                    extraMapping["Drums/Pads/Red_2"] = InputKey.a;
                    extraMapping["Drums/Pads/Yellow_2"] = InputKey.up;
                    extraMapping["Drums/Pads/Yellow_3"] = InputKey.left;
                    extraMapping["Drums/Pads/Blue_2"] = InputKey.down;
                    extraMapping["Drums/Pads/Blue_3"] = InputKey.right;
                    extraMapping["Drums/Pads/Green_2"] = InputKey.b;
                    extraMapping["Drums/Pads/Bass_2"] = InputKey.joystick1right;
                    extraMapping["Drums/Pads/Bass_3"] = InputKey.joystick2left;
                    extraMapping["Drums/Pads/Bass_4"] = InputKey.joystick2right;
                }

                else if (Program.SystemConfig["controller_mode"] == "drumsgh" || romName.Contains(".drumsgh."))
                {
                    extraOptions["Extension"] = "Drums";
                    wiiMapping[InputKey.l2] = "Drums/Pads/Red";
                    wiiMapping[InputKey.pageup] = "Drums/Pads/Yellow";
                    wiiMapping[InputKey.pagedown] = "Drums/Pads/Blue";
                    wiiMapping[InputKey.r2] = "Drums/Pads/Orange";
                    wiiMapping[InputKey.b] = "Drums/Pads/Green";
                    wiiMapping[InputKey.joystick1left] = "Drums/Pads/Bass";
                    wiiMapping[InputKey.select] = "Drums/Buttons/-";
                    wiiMapping[InputKey.start] = "Drums/Buttons/+";
                    wiiMapping[InputKey.up] = "Drums/Stick/Up";
                    wiiMapping[InputKey.down] = "Drums/Stick/Down";
                    wiiMapping[InputKey.left] = "Drums/Stick/Left";
                    wiiMapping[InputKey.right] = "Drums/Stick/Right";

                    extraMapping["Drums/Pads/Red_2"] = InputKey.a;
                    extraMapping["Drums/Pads/Yellow_2"] = InputKey.joystick1down;
                    extraMapping["Drums/Pads/Yellow_3"] = InputKey.joystick2down;
                    extraMapping["Drums/Pads/Yellow_4"] = InputKey.joystick1up;
                    extraMapping["Drums/Pads/Yellow_5"] = InputKey.joystick2up;
                    extraMapping["Drums/Pads/Orange_2"] = InputKey.joystick1down;
                    extraMapping["Drums/Pads/Orange_3"] = InputKey.joystick2down;
                    extraMapping["Drums/Pads/Orange_4"] = InputKey.joystick1up;
                    extraMapping["Drums/Pads/Orange_5"] = InputKey.joystick2up;
                    extraMapping["Drums/Pads/Bass_2"] = InputKey.joystick1right;
                    extraMapping["Drums/Pads/Bass_3"] = InputKey.joystick2left;
                    extraMapping["Drums/Pads/Bass_4"] = InputKey.joystick2right;
                }

                else if (Program.SystemConfig["controller_mode"] == "drawsome" || romName.Contains(".ds."))
                {
                    extraOptions["Extension"] = "Drawsome";
                    wiiMapping[InputKey.b] = "Drawsome/Touch/Pressure";
                    wiiMapping[InputKey.a] = "Drawsome/Touch/Lift";
                    wiiMapping[InputKey.joystick1up] = "Drawsome/Stylus/Up";
                    wiiMapping[InputKey.joystick1left] = "Drawsome/Stylus/Left";
                }

                else if (Program.SystemConfig["controller_mode"] == "turntable" || romName.Contains(".tt."))
                {
                    extraOptions["Extension"] = "Turntable";
                    wiiMapping[InputKey.pageup] = "Turntable/Buttons/Green Left";
                    wiiMapping[InputKey.pagedown] = "Turntable/Buttons/Red Left";
                    wiiMapping[InputKey.y] = "Turntable/Buttons/Blue Left";
                    wiiMapping[InputKey.select] = "Turntable/Buttons/-";
                    wiiMapping[InputKey.start] = "Turntable/Buttons/+";
                    wiiMapping[InputKey.joystick1left] = "Turntable/Table Left/Left";
                    wiiMapping[InputKey.up] = "Turntable/Stick/Up";
                    wiiMapping[InputKey.down] = "Turntable/Stick/Down";
                    wiiMapping[InputKey.left] = "Turntable/Stick/Left";
                    wiiMapping[InputKey.right] = "Turntable/Stick/Right";
                    wiiMapping[InputKey.l3] = "Turntable/Effect/Left";
                    wiiMapping[InputKey.r3] = "Turntable/Effect/Right";
                    wiiMapping[InputKey.l2] = "Turntable/Crossfade/Left";
                    wiiMapping[InputKey.r2] = "Turntable/Crossfade/Right";

                    extraMapping["Turntable/Buttons/Green Left_2"] = InputKey.b;
                    extraMapping["Turntable/Buttons/Red Left_2"] = InputKey.a;
                    extraMapping["Turntable/Table Left/Left_3"] = InputKey.joystick1up;
                    extraMapping["Turntable/Table Left/Left_4"] = InputKey.joystick2left;
                    extraMapping["Turntable/Table Left/Left_5"] = InputKey.joystick2up;
                    extraMapping["Turntable/Table Left/Right_2"] = InputKey.joystick1down;
                    extraMapping["Turntable/Table Left/Right_3"] = InputKey.joystick2down;
                    extraMapping["Turntable/Table Left/Right_4"] = InputKey.joystick1right;
                    extraMapping["Turntable/Table Left/Right_5"] = InputKey.joystick2right;
                }
            }

            // cc : Classic Controller Settings
            else if (Program.SystemConfig["controller_mode"] == "cc" || Program.SystemConfig["controller_mode"] == "ccp" || romName.Contains(".cc.") || romName.Contains(".ccp."))
            {
                bool revertall = Program.Features.IsSupported("wii_cc_buttons") && Program.SystemConfig.isOptSet("wii_cc_buttons") && Program.SystemConfig["wii_cc_buttons"] == "xbox";

                extraOptions["Extension"] = "Classic";

                if (revertall)
                {
                    wiiMapping[InputKey.y] = "Classic/Buttons/X";
                    wiiMapping[InputKey.x] = "Classic/Buttons/Y";
                    wiiMapping[InputKey.a] = "Classic/Buttons/B";
                    wiiMapping[InputKey.b] = "Classic/Buttons/A";
                }
                else
                {
                    wiiMapping[InputKey.x] = "Classic/Buttons/X";
                    wiiMapping[InputKey.y] = "Classic/Buttons/Y";
                    wiiMapping[InputKey.b] = "Classic/Buttons/B";
                    wiiMapping[InputKey.a] = "Classic/Buttons/A";
                }
                wiiMapping[InputKey.select] = "Classic/Buttons/-";
                wiiMapping[InputKey.start] = "Classic/Buttons/+";

                wiiMapping[InputKey.pageup] = "Classic/Buttons/ZL";
                wiiMapping[InputKey.pagedown] = "Classic/Buttons/ZR";

                wiiMapping[InputKey.l2] = "Classic/Triggers/L-Analog";
                wiiMapping[InputKey.r2] = "Classic/Triggers/R-Analog";
                wiiMapping.Add(InputKey.l2, "Classic/Triggers/L");
                wiiMapping.Add(InputKey.r2, "Classic/Triggers/R");

                wiiMapping[InputKey.up] = "Classic/D-Pad/Up";
                wiiMapping[InputKey.down] = "Classic/D-Pad/Down";
                wiiMapping[InputKey.left] = "Classic/D-Pad/Left";
                wiiMapping[InputKey.right] = "Classic/D-Pad/Right";
                wiiMapping[InputKey.joystick1up] = "Classic/Left Stick/Up";
                wiiMapping[InputKey.joystick1left] = "Classic/Left Stick/Left";
                wiiMapping[InputKey.joystick2up] = "Classic/Right Stick/Up";
                wiiMapping[InputKey.joystick2left] = "Classic/Right Stick/Left";
                wiiMapping[InputKey.l3] = "Classic/Left Stick/Modifier";
                wiiMapping[InputKey.r3] = "Classic/Right Stick/Modifier";
            }

            GenerateControllerConfig_wii(path, wiiMapping, wiiReverseAxes, extraOptions, extraMapping);
        }

        private static void GenerateControllerConfig_realwiimotes(string path)
        {
            string iniFile = Path.Combine(path, "User", "Config", "WiimoteNew.ini");

            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                for (int i = 1; i < 5; i++)
                {
                    ini.ClearSection("Wiimote" + i.ToString());
                    ini.WriteValue("Wiimote" + i.ToString(), "Source", "2");
                }

                // Balance board
                if (Program.SystemConfig.isOptSet("wii_balanceboard") && Program.SystemConfig.getOptBoolean("wii_balanceboard"))
                {
                    ini.WriteValue("BalanceBoard", "Source", "2");
                }
                else
                    ini.WriteValue("BalanceBoard", "Source", "0");

                ini.Save();
            }
        }

        private static void GenerateControllerConfig_realEmulatedwiimotes(string path)
        {
            string iniFile = Path.Combine(path, "User", "Config", "WiimoteNew.ini");

            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                for (int i = 1; i < 5; i++)
                {
                    string section = "Wiimote" + i.ToString();
                    string btDevice = (i - 1).ToString();

                    ini.ClearSection(section);
                    ini.WriteValue(section, "Source", "1");
                    ini.WriteValue(section, "Device", "Bluetooth/" + btDevice + "/Wii Remote");

                    foreach (KeyValuePair<string, string> x in realEmulatedWiimote)
                        ini.WriteValue(section, x.Key, x.Value);

                    if (Program.SystemConfig["emulatedwiimotes"] == "3")
                        ini.WriteValue(section, "Extension", "Nunchuk");
                    else if (Program.SystemConfig["emulatedwiimotes"] == "4")
                        ini.WriteValue(section, "Extension", "Classic");
                }

                // Balance board
                if (Program.SystemConfig.isOptSet("wii_balanceboard") && Program.SystemConfig.getOptBoolean("wii_balanceboard"))
                {
                    ini.WriteValue("BalanceBoard", "Source", "2");
                }
                else
                    ini.WriteValue("BalanceBoard", "Source", "0");

                ini.Save();
            }

            // Set hotkeys
            string hotkeyini = Path.Combine(path, "User", "Config", "Hotkeys.ini");
            if (File.Exists(hotkeyini))
                SetWiimoteHotkeys(hotkeyini);
        }

        private static void GenerateControllerConfig_wii(string path, InputKeyMapping anyMapping, Dictionary<string, string> anyReverseAxes, Dictionary<string, string> extraOptions = null, Dictionary<string, InputKey> extramapping = null)
        {
            //string path = Program.AppConfig.GetFullPath("dolphin");
            string iniFile = Path.Combine(path, "User", "Config", "WiimoteNew.ini");

            SimpleLogger.Instance.Info("[INFO] Writing Wiimote controller configuration in : " + iniFile);

            bool forceSDL = false;
            if (Program.SystemConfig.isOptSet("input_forceSDL") && Program.SystemConfig.getOptBoolean("input_forceSDL"))
                forceSDL = true;

            int nsamepad = 0;

            Dictionary<string, int> double_pads = new Dictionary<string, int>();
            Dictionary<string, string> specialHK = null;

            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                foreach (var pad in Program.Controllers.OrderBy(i => i.PlayerIndex).Take(4))
                {
                    bool xinputAsSdl = false;
                    bool isNintendo = pad.VendorID == USB_VENDOR.NINTENDO;
                    string gcpad = "Wiimote" + pad.PlayerIndex;
                    if (gcpad != null)
                        ini.ClearSection(gcpad);

                    if (pad.Config == null)
                        continue;

                    string guid = pad.GetSdlGuid(SdlVersion.SDL2_0_X).ToLowerInvariant();
                    var prod = pad.ProductID;
                    string gamecubepad = "gamecubepad" + (pad.PlayerIndex - 1);

                    if (ConfigureGCAdapter(gcpad, gamecubepad, guid, pad, ini, out specialHK))
                    {
                        if (specialHK != null)
                        {
                            foreach (var hk in specialHK)
                            {
                                if (!_gcSpecialHotkeys.ContainsKey(hk.Key))
                                    _gcSpecialHotkeys.Add(hk.Key, hk.Value);
                            }
                        }
                        continue;
                    }

                    string tech = "XInput";
                    string deviceName = "Gamepad";
                    int xIndex = 0;

                    if (pad.Config.Type == "keyboard")
                    {
                        tech = "DInput";
                        deviceName = "Keyboard Mouse";
                    }
                    else if (!pad.IsXInputDevice || forceSDL)
                    {
                        var s = pad.SdlController;
                        if (s == null)
                            continue;

                        tech = "SDL";

                        if (pad.IsXInputDevice)
                        {
                            xinputAsSdl = true;
                        }

                        deviceName = pad.Name ?? "";

                        string newNamePath = Path.Combine(Program.AppConfig.GetFullPath("tools"), "controllerinfo.yml");
                        if (File.Exists(newNamePath))
                        {
                            string newName = SdlJoystickGuid.GetNameFromFile(newNamePath, pad.Guid, "dolphin");

                            if (newName != null)
                                deviceName = newName;
                        }
                    }

                    if (double_pads.ContainsKey(tech + "/" + deviceName))
                        nsamepad = double_pads[tech + "/" + deviceName];
                    else
                        nsamepad = 0;

                    if (pad.PlayerIndex == 1)
                        _p1sdlindex = nsamepad;

                    double_pads[tech + "/" + deviceName] = nsamepad + 1;

                    if (pad.IsXInputDevice)
                        xIndex = pad.XInput != null ? pad.XInput.DeviceIndex : pad.DeviceIndex;

                    if (tech == "XInput" && !xinputAsSdl)
                        ini.WriteValue(gcpad, "Device", tech + "/" + xIndex + "/" + deviceName);
                    else if (xinputAsSdl)
                        ini.WriteValue(gcpad, "Device", "SDL" + "/" + nsamepad.ToString() + "/" + deviceName);
                    else
                        ini.WriteValue(gcpad, "Device", tech + "/" + nsamepad.ToString() + "/" + deviceName);

                    if (extraOptions != null)
                        foreach (var xtra in extraOptions)
                            ini.WriteValue(gcpad, xtra.Key, xtra.Value);

                    bool positional = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "position";
                    bool xboxLayout = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "xbox";
                    bool revertXY = Program.Features.IsSupported("gamecube_buttons") && Program.SystemConfig.isOptSet("gamecube_buttons") && Program.SystemConfig["gamecube_buttons"] == "reverse_ab";
                    bool rumble = !Program.SystemConfig.isOptSet("input_rumble") || Program.SystemConfig.getOptBoolean("input_rumble");

                    if (isNintendo && pad.PlayerIndex == 1)
                    {
                        string tempMapA = anyMapping[InputKey.a];
                        string tempMapB = anyMapping[InputKey.b];
                        string tempMapX = anyMapping[InputKey.x];
                        string tempMapY = anyMapping[InputKey.y];

                        if (tempMapB != null)
                            anyMapping[InputKey.a] = tempMapB;
                        if (tempMapA != null)
                            anyMapping[InputKey.b] = tempMapA;
                        if (tempMapY != null)
                            anyMapping[InputKey.x] = tempMapY;
                        if (tempMapX != null)
                            anyMapping[InputKey.y] = tempMapX;
                    }

                    foreach (var x in anyMapping)
                    {
                        string value = x.Value;

                        if (pad.Config.Type == "keyboard")
                        {
                            if (x.Key == InputKey.a)
                                value = "Buttons/A";
                            else if (x.Key == InputKey.b)
                                value = "Buttons/B";

                            if (x.Key == InputKey.joystick1left || x.Key == InputKey.joystick1up)
                                continue;

                            ini.WriteValue(gcpad, "IR/Up", "`Cursor Y-`");
                            ini.WriteValue(gcpad, "IR/Down", "`Cursor Y+`");
                            ini.WriteValue(gcpad, "IR/Left", "`Cursor X-`");
                            ini.WriteValue(gcpad, "IR/Right", "`Cursor X+`");

                            var input = pad.Config[x.Key];
                            if (input == null)
                                continue;

                            var name = ToDolphinKey(input.Id);
                            ini.WriteValue(gcpad, value, name);
                        }
                        else if (tech == "XInput")
                        {
                            var mapping = pad.GetXInputMapping(x.Key);
                            if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                ini.WriteValue(gcpad, value, xInputMapping[mapping]);

                            if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                mapping = pad.GetXInputMapping(x.Key, true);
                                if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                    ini.WriteValue(gcpad, reverseAxis, xInputMapping[mapping]);
                            }
                        }

                        else // SDL
                        {
                            var input = pad.GetSdlMapping(x.Key);

                            ini.WriteValue(gcpad, value, dolphinSDLMapping[x.Key]);

                            if(anyReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                if (joyRevertAxis.ContainsKey(x.Key))
                                {
                                    var revertKey = joyRevertAxis[x.Key];
                                    ini.WriteValue(gcpad, reverseAxis, dolphinSDLMapping[revertKey]);
                                }
                            }
                        }
                    }

                    foreach (var y in extramapping)
                    {
                        string value = y.Key;
                        if (value.Contains('_'))
                        {
                            string[] values = value.Split('_');
                            value = values[0];
                        }

                        if (pad.Config.Type == "keyboard")
                        {
                            if (y.Value == InputKey.a)
                                value = "Buttons/A";
                            else if (y.Value == InputKey.b)
                                value = "Buttons/B";

                            if (y.Value == InputKey.joystick1left || y.Value == InputKey.joystick1up)
                                continue;

                            var input = pad.Config[y.Value];
                            if (input == null)
                                continue;

                            var name = ToDolphinKey(input.Id);

                            string originalIni = ini.GetValue(gcpad, value)?? "";
                            string newIniValue;
                            if (string.IsNullOrEmpty(originalIni))
                                newIniValue = name;
                            else
                                newIniValue = originalIni + "|" + name;
                            ini.WriteValue(gcpad, value, name);
                        }
                        else if (tech == "XInput")
                        {
                            var mapping = pad.GetXInputMapping(y.Value);
                            if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                            {
                                string originalIni = ini.GetValue(gcpad, value) ?? "";
                                string newIniValue;
                                if (string.IsNullOrEmpty(originalIni))
                                    newIniValue = xInputMapping[mapping];
                                else
                                    newIniValue = originalIni + "|" + xInputMapping[mapping];
                                ini.WriteValue(gcpad, value, newIniValue);
                            }
                            else if (y.Value == InputKey.joystick1down || y.Value == InputKey.joystick2down || y.Value == InputKey.joystick1right || y.Value == InputKey.joystick2right)
                            {
                                string newIniValue;

                                switch (y.Value)
                                {
                                    case InputKey.joystick1down:
                                        string originalIni = ini.GetValue(gcpad, value) ?? "";
                                        if (string.IsNullOrEmpty(originalIni))
                                            newIniValue = xInputMapping[XINPUTMAPPING.LEFTANALOG_DOWN];
                                        else
                                            newIniValue = originalIni + "|" + xInputMapping[XINPUTMAPPING.LEFTANALOG_DOWN];
                                        ini.WriteValue(gcpad, value, newIniValue);
                                        break;
                                    case InputKey.joystick2down:
                                        string originalIni2 = ini.GetValue(gcpad, value) ?? "";
                                        if (string.IsNullOrEmpty(originalIni2))
                                            newIniValue = xInputMapping[XINPUTMAPPING.RIGHTANALOG_DOWN];
                                        else
                                            newIniValue = originalIni2 + "|" + xInputMapping[XINPUTMAPPING.RIGHTANALOG_DOWN];
                                        ini.WriteValue(gcpad, value, newIniValue);
                                        break;
                                    case InputKey.joystick1right:
                                        string originalIni3 = ini.GetValue(gcpad, value) ?? "";
                                        if (string.IsNullOrEmpty(originalIni3))
                                            newIniValue = xInputMapping[XINPUTMAPPING.LEFTANALOG_RIGHT];
                                        else
                                            newIniValue = originalIni3 + "|" + xInputMapping[XINPUTMAPPING.LEFTANALOG_RIGHT];
                                        ini.WriteValue(gcpad, value, newIniValue);
                                        break;
                                    case InputKey.joystick2right:
                                        string originalIni4 = ini.GetValue(gcpad, value) ?? "";
                                        if (string.IsNullOrEmpty(originalIni4))
                                            newIniValue = xInputMapping[XINPUTMAPPING.RIGHTANALOG_RIGHT];
                                        else
                                            newIniValue = originalIni4 + "|" + xInputMapping[XINPUTMAPPING.RIGHTANALOG_RIGHT];
                                        ini.WriteValue(gcpad, value, newIniValue);
                                        break;
                                }
                            }

                            if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                mapping = pad.GetXInputMapping(y.Value, true);
                                if (mapping != XINPUTMAPPING.UNKNOWN && xInputMapping.ContainsKey(mapping))
                                {
                                    string originalIni = ini.GetValue(gcpad, reverseAxis) ?? "";
                                    string newIniValue;
                                    if (string.IsNullOrEmpty(originalIni))
                                        newIniValue = xInputMapping[mapping];
                                    else
                                        newIniValue = originalIni + "|" + xInputMapping[mapping];
                                    ini.WriteValue(gcpad, reverseAxis, newIniValue);
                                }
                            }
                        }

                        else // SDL
                        {
                            var input = pad.GetSdlMapping(y.Value);

                            ini.WriteValue(gcpad, value, dolphinSDLMapping[y.Value]);

                            if (anyReverseAxes.TryGetValue(value, out string reverseAxis))
                            {
                                if (joyRevertAxis.ContainsKey(y.Value))
                                {
                                    var revertKey = joyRevertAxis[y.Value];
                                    ini.WriteValue(gcpad, reverseAxis, dolphinSDLMapping[revertKey]);
                                }
                            }
                        }
                    }

                    // DEAD ZONE
                    if (Program.SystemConfig.isOptSet("dolphin_wii_deadzone") && !string.IsNullOrEmpty(Program.SystemConfig["dolphin_wii_deadzone"]))
                    {
                        string deadzone = Program.SystemConfig["dolphin_wii_deadzone"].ToIntegerString() + ".0";
                        ini.WriteValue(gcpad, "Classic/Right Stick/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "Classic/Left Stick/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "IR/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "Tilt/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "Swing/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "IMUGyroscope/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "Nunchuk/Tilt/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "Nunchuk/Swing/Dead Zone", deadzone);
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Dead Zone", deadzone);
                    }
                    else
                    {
                        ini.WriteValue(gcpad, "Classic/Right Stick/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "Classic/Left Stick/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "IR/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "Tilt/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "Swing/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "IMUGyroscope/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "Nunchuk/Tilt/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "Nunchuk/Swing/Dead Zone", "15.0");
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Dead Zone", "15.0");
                    }

                    // SENSITIVITY
                    if (Program.SystemConfig.isOptSet("dolphin_wii_sensitivity") && !string.IsNullOrEmpty(Program.SystemConfig["dolphin_wii_sensitivity"]))
                    {
                        string sensitivity = Program.SystemConfig["dolphin_wii_sensitivity"].ToIntegerString() + ".0";
                        ini.WriteValue(gcpad, "IR/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "IR/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "IR/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "IR/Right/Range", sensitivity);
                        ini.WriteValue(gcpad, "Tilt/Forward/Range", sensitivity);
                        ini.WriteValue(gcpad, "Tilt/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "Tilt/Backward/Range", sensitivity);
                        ini.WriteValue(gcpad, "Tilt/Right/Range", sensitivity);
                        ini.WriteValue(gcpad, "Swing/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "Swing/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "Swing/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "Swing/Right/Range", sensitivity);
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Right/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Left Stick/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Left Stick/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Left Stick/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Left Stick/Right/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Right Stick/Up/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Right Stick/Down/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Right Stick/Left/Range", sensitivity);
                        ini.WriteValue(gcpad, "Classic/Right Stick/Right/Range", sensitivity);
                    }
                    else
                    {
                        ini.WriteValue(gcpad, "IR/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "IR/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "IR/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "IR/Right/Range", "100.0");
                        ini.WriteValue(gcpad, "Tilt/Forward/Range", "100.0");
                        ini.WriteValue(gcpad, "Tilt/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "Tilt/Backward/Range", "100.0");
                        ini.WriteValue(gcpad, "Tilt/Right/Range", "100.0");
                        ini.WriteValue(gcpad, "Swing/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "Swing/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "Swing/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "Swing/Right/Range", "100.0");
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "Nunchuk/Stick/Right/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Left Stick/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Left Stick/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Left Stick/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Left Stick/Right/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Right Stick/Up/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Right Stick/Down/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Right Stick/Left/Range", "100.0");
                        ini.WriteValue(gcpad, "Classic/Right Stick/Right/Range", "100.0");
                    }

                    if (Program.SystemConfig["controller_mode"] == "cc" || Program.SystemConfig["controller_mode"] == "ccp")
                    {
                        if (prod == USB_PRODUCT.NINTENDO_SWITCH_PRO)
                        {
                            ini.WriteValue(gcpad, "Classic/Left Stick/Calibration", "98.50 101.73 102.04 106.46 104.62 102.21 102.00 100.53 97.00 96.50 99.95 100.08 102.40 99.37 99.60 100.17 99.60 100.14 98.87 100.48 102.45 101.12 100.92 97.92 99.00 99.92 100.83 100.45 102.27 98.45 97.16 97.36");
                            ini.WriteValue(gcpad, "Classic/Right Stick/Calibration", "98.19 101.79 101.37 102.32 103.05 101.19 99.56 99.11 98.45 100.60 98.65 100.67 99.85 97.31 97.24 96.36 95.94 97.94 98.17 100.24 99.22 98.10 99.69 98.77 97.14 100.45 99.08 100.13 102.61 101.37 100.55 97.03");
                        }
                        else if (prod == USB_PRODUCT.SONY_DS3 ||
                        prod == USB_PRODUCT.SONY_DS4 ||
                        prod == USB_PRODUCT.SONY_DS4_DONGLE ||
                        prod == USB_PRODUCT.SONY_DS4_SLIM ||
                        prod == USB_PRODUCT.SONY_DS5)
                        {
                            ini.WriteValue(gcpad, "Classic/Left Stick/Calibration", "100.00 101.96 104.75 107.35 109.13 110.30 105.04 101.96 100.00 101.96 105.65 105.14 105.94 103.89 104.87 101.04 100.00 101.96 107.16 107.49 105.93 103.65 102.31 101.96 100.00 101.96 103.68 108.28 108.05 105.96 103.66 101.48");
                            ini.WriteValue(gcpad, "Classic/Right Stick/Calibration", "100.00 101.96 104.31 104.51 105.93 104.41 103.44 101.96 100.00 101.96 104.07 105.45 109.33 107.39 104.91 101.96 100.00 101.96 106.79 107.84 105.66 104.16 102.91 100.38 98.14 101.63 105.29 107.30 106.77 104.73 104.87 100.92");
                        }
                        else
                        {
                            ini.WriteValue(gcpad, "Classic/Left Stick/Calibration", "100.00 101.96 104.75 107.35 109.13 110.30 105.04 101.96 100.00 101.96 105.65 105.14 105.94 103.89 104.87 101.04 100.00 101.96 107.16 107.49 105.93 103.65 102.31 101.96 100.00 101.96 103.68 108.28 108.05 105.96 103.66 101.48");
                            ini.WriteValue(gcpad, "Classic/Right Stick/Calibration", "100.00 101.96 104.31 104.51 105.93 104.41 103.44 101.96 100.00 101.96 104.07 105.45 109.33 107.39 104.91 101.96 100.00 101.96 106.79 107.84 105.66 104.16 102.91 100.38 98.14 101.63 105.29 107.30 106.77 104.73 104.87 100.92");
                        }
                    }
                    if (Program.SystemConfig.isOptSet("wii_motionpad") && Program.SystemConfig.getOptBoolean("wii_motionpad"))
                    {
                        ini.WriteValue(gcpad, "IMUAccelerometer/Up", "`Accel Up`");
                        ini.WriteValue(gcpad, "IMUAccelerometer/Down", "`Accel Down`");
                        ini.WriteValue(gcpad, "IMUAccelerometer/Left", "`Accel Left`");
                        ini.WriteValue(gcpad, "IMUAccelerometer/Right", "`Accel Right`");
                        ini.WriteValue(gcpad, "IMUAccelerometer/Forward", "`Accel Forward`");
                        ini.WriteValue(gcpad, "IMUAccelerometer/Backward", "`Accel Backward`");
                        ini.WriteValue(gcpad, "IMUGyroscope/Pitch Up", "`Gyro Pitch Up`");
                        ini.WriteValue(gcpad, "IMUGyroscope/Pitch Down", "`Gyro Pitch Down`");
                        ini.WriteValue(gcpad, "IMUGyroscope/Roll Left", "`Gyro Roll Left`");
                        ini.WriteValue(gcpad, "IMUGyroscope/Roll Right", "`Gyro Roll Right`");
                        ini.WriteValue(gcpad, "IMUGyroscope/Yaw Left", "`Gyro Yaw Left`");
                        ini.WriteValue(gcpad, "IMUGyroscope/Yaw Right", "`Gyro Yaw Right`");
                        ini.Remove(gcpad, "Tilt/Forward");
                        ini.Remove(gcpad, "Tilt/Left");
                        ini.Remove(gcpad, "Tilt/Right");
                        ini.Remove(gcpad, "Tilt/Backward");
                        ini.Remove(gcpad, "Shake/X");
                        ini.Remove(gcpad, "Shake/Y");
                        ini.Remove(gcpad, "Shake/Z");
                        ini.Remove(gcpad, "Swing/Down");
                        ini.Remove(gcpad, "Swing/Right");
                        ini.Remove(gcpad, "Swing/Up");
                        ini.Remove(gcpad, "Swing/Left");

                        if (Program.SystemConfig.isOptSet("wii_motion_deadzone") && !string.IsNullOrEmpty(Program.SystemConfig["wii_motion_deadzone"]))
                        {
                            string value = Program.SystemConfig["wii_motion_deadzone"].ToCleanDecimalString();
                            if (!value.Contains('.'))
                                value += ".0";
                            ini.WriteValue(gcpad, "IMUGyroscope/Dead Zone", value);
                        }
                        else
                            ini.Remove(gcpad, "IMUGyroscope/Dead Zone");
                    }

                    // Hide wiimote cursor
                    if (Program.SystemConfig.getOptBoolean("wii_hidecursor"))
                        ini.WriteValue(gcpad, "IR/Auto-Hide", "True");
                    else
                        ini.WriteValue(gcpad, "IR/Auto-Hide", "False");

                    // Relative input for IR cursor
                    if (Program.SystemConfig.getOptBoolean("wii_relativecursor") || pad.Config.Type == "keyboard")
                        ini.WriteValue(gcpad, "IR/Relative Input", "False");
                    else
                        ini.WriteValue(gcpad, "IR/Relative Input", "True");

                    SimpleLogger.Instance.Info("[INFO] Assigned controller " + pad.DevicePath + " to player : " + pad.PlayerIndex.ToString());
                }

                ini.Save();
            }

            // Reset hotkeys
            string hotkeyini = Path.Combine(path, "User", "Config", "Hotkeys.ini");
            ResetHotkeysToDefault(hotkeyini, _gcSpecialHotkeys);
        }

        private static void SetWiimoteHotkeys(string iniFile)
        {
            using (IniFile ini = new IniFile(iniFile, IniOptions.UseSpaces))
            {
                ini.WriteValue("Hotkeys", "Device", "Bluetooth/0/Wii Remote");
                ini.WriteValue("Hotkeys", "General/Toggle Pause", "B&`-`");
                ini.WriteValue("Hotkeys", "General/Toggle Fullscreen", "A&`-`");
                ini.WriteValue("Hotkeys", "General/Exit", "HOME&`-`");

                // SaveStates
                ini.WriteValue("Hotkeys", "General/Take Screenshot", "`-`&`1`"); // Use Same value as SaveState....
                ini.WriteValue("Hotkeys", "Save State/Save to Selected Slot", "`-`&`1`");
                ini.WriteValue("Hotkeys", "Load State/Load from Selected Slot", "`-`&`2`");
                ini.WriteValue("Hotkeys", "Other State Hotkeys/Increase Selected State Slot", "Up&`-`");
                ini.WriteValue("Hotkeys", "Other State Hotkeys/Decrease Selected State Slot", "Down&`-`");
            }
        }

        static readonly Dictionary<string, string> realEmulatedWiimote = new Dictionary<string, string>()
        {
            { "Tilt/Modifier/Range", "50." },
            { "Nunchuk/Stick/Modifier/Range", "50." },
            { "Nunchuk/Tilt/Modifier/Range", "50." },
            { "Classic/Left Stick/Modifier/Range", "50." },
            { "Classic/Right Stick/Modifier/Range", "50." },
            { "Guitar/Stick/Modifier/Range", "50." },
            { "Drums/Stick/Modifier/Range", "50." },
            { "Turntable/Stick/Modifier/Range", "50." },
            { "uDraw/Stylus/Modifier/Range", "50." },
            { "Drawsome/Stylus/Modifier/Rangee", "50." },
            { "Buttons/A", "`A`" },
            { "Buttons/B", "`B`" },
            { "Buttons/1", "`1`" },
            { "Buttons/2", "`2`" },
            { "Buttons/-", "`-`" },
            { "Buttons/+", "`+`" },
            { "Buttons/Home", "`HOME`" },
            { "D-Pad/Up", "`Up`" },
            { "D-Pad/Down", "`Down`" },
            { "D-Pad/Left", "`Left`" },
            { "D-Pad/Right", "`Right`" },
            { "IMUAccelerometer/Up", "`Accel Up`" },
            { "IMUAccelerometer/Down", "`Accel Down`" },
            { "IMUAccelerometer/Left", "`Accel Left`" },
            { "IMUAccelerometer/Right", "`Accel Right`" },
            { "IMUAccelerometer/Forward", "`Accel Forward`" },
            { "IMUAccelerometer/Backward", "`Accel Backward`" },
            { "IMUGyroscope/Dead Zone", "3." },
            { "IMUGyroscope/Pitch Up", "`Gyro Pitch Up`" },
            { "IMUGyroscope/Pitch Down", "`Gyro Pitch Down`" },
            { "IMUGyroscope/Roll Left", "`Gyro Roll Left`" },
            { "IMUGyroscope/Roll Right", "`Gyro Roll Right`" },
            { "IMUGyroscope/Yaw Left", "`Gyro Yaw Left`" },
            { "IMUGyroscope/Yaw Right", "`Gyro Yaw Right`" },
            { "Extension/Attach MotionPlus", "`Attached MotionPlus`" },
            { "Nunchuk/Buttons/C", "`Nunchuk C`" },
            { "Nunchuk/Buttons/Z", "`Nunchuk Z`" },
            { "Nunchuk/Stick/Up", "`Nunchuk Y+`" },
            { "Nunchuk/Stick/Down", "`Nunchuk Y-`" },
            { "Nunchuk/Stick/Left", "`Nunchuk X-`" },
            { "Nunchuk/Stick/Right", "`Nunchuk X+`" },
            { "Nunchuk/Stick/Calibration", "100.00 100.00 100.00 100.00 100.00 100.00 100.00 100.00" },
            { "Nunchuk/IMUAccelerometer/Up", "`Nunchuk Accel Up`" },
            { "Nunchuk/IMUAccelerometer/Down", "`Nunchuk Accel Down`" },
            { "Nunchuk/IMUAccelerometer/Left", "`Nunchuk Accel Left`" },
            { "Nunchuk/IMUAccelerometer/Right", "`Nunchuk Accel Right`" },
            { "Nunchuk/IMUAccelerometer/Forward", "`Nunchuk Accel Forward`" },
            { "Nunchuk/IMUAccelerometer/Backward", "`Nunchuk Accel Backward`" },
            { "Classic/Buttons/A", "`Classic A`" },
            { "Classic/Buttons/B", "`Classic B`" },
            { "Classic/Buttons/X", "`Classic X`" },
            { "Classic/Buttons/Y", "`Classic Y`" },
            { "Classic/Buttons/ZL", "`Classic ZL`" },
            { "Classic/Buttons/ZR", "`Classic ZR`" },
            { "Classic/Buttons/-", "`Classic -`" },
            { "Classic/Buttons/+", "`Classic +`" },
            { "Classic/Buttons/Home", "`Classic HOME`" },
            { "Classic/Left Stick/Up", "`Classic Left Y+`" },
            { "Classic/Left Stick/Down", "`Classic Left Y-`" },
            { "Classic/Left Stick/Left", "`Classic Left X-`" },
            { "Classic/Left Stick/Right", "`Classic Left X+`" },
            { "Classic/Left Stick/Calibration", "100.00 100.00 100.00 100.00 100.00 100.00 100.00 100.00" },
            { "Classic/Right Stick/Up", "`Classic Right Y+`" },
            { "Classic/Right Stick/Down", "`Classic Right Y-`" },
            { "Classic/Right Stick/Left", "`Classic Right X-`" },
            { "Classic/Right Stick/Right", "`Classic Right X+`" },
            { "Classic/Right Stick/Calibration", "100.00 100.00 100.00 100.00 100.00 100.00 100.00 100.00" },
            { "Classic/Triggers/L", "`Classic L`" },
            { "Classic/Triggers/R", "`Classic R`" },
            { "Classic/Triggers/L-Analog", "`Classic L-Analog`" },
            { "Classic/Triggers/R-Analog", "`Classic R-Analog`" },
            { "Classic/D-Pad/Up", "`Classic Up`" },
            { "Classic/D-Pad/Down", "`Classic Down`" },
            { "Classic/D-Pad/Left", "`Classic Left`" },
            { "Classic/D-Pad/Right", "`Classic Right`" },
            { "Rumble/Motor", "`Motor`" },
            { "Options/Battery", "`Battery`" },
        };
    }
}
