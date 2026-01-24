using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Compression.SevenZip;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static EmulatorLauncher.PadToKeyboard.SendKey;

namespace EmulatorLauncher
{
    partial class JZintvGenerator : Generator
    {
        private void ConfigureControllers(List<string> commandArray, string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Controller autoconfiguration disabled.");
                return;
            }

            if (Program.Controllers.Where(c => !c.IsKeyboard).Count() == 0)
            {
                SimpleLogger.Instance.Info("[INFO] No controller connected, skipping controller configuration.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for jzintv");

            string configFile = Path.Combine(path, "hackfile_retrobat.cfg");

            // Sort controllers by DirectInput joystick ID
            var controllersSorted = this.Controllers
            .Where(c => !c.IsKeyboard && c.DirectInput != null && c.DirectInput.JoystickID > -1)
            .OrderBy(c => c.DirectInput.JoystickID)   // Order by joystick ID
            .ToList();

            int index1 = -1;
            int index2 = -1;

            Dictionary<int, Controller> finalControllerList = new Dictionary<int, Controller>();

            List<string> lines = new List<string>();
            try
            {
                // Header of the file
                lines.Add("; RetroBat hackfile for jzintv, generated on " + DateTime.Now.ToString("dd/MM/yy - HH:mm"));
                
                Controller c1 = null;
                Controller c2 = null;

                if (controllersSorted.Count > 1)
                {
                    c1 = controllersSorted.FirstOrDefault(c => c.PlayerIndex == 1);
                    c2 = controllersSorted.FirstOrDefault(c => c.PlayerIndex == 2);

                    if (c1 != null)
                    {
                        index1 = controllersSorted.IndexOf(c1);
                        finalControllerList.Add(index1, c1);
                    }
                    if (c2 != null)
                    {
                        index2 = controllersSorted.IndexOf(c2);
                        finalControllerList.Add(index2, c2);
                    }
                }
                else if (Program.Controllers.Count == 1)
                {
                    c1 = controllersSorted.FirstOrDefault(c => c.PlayerIndex == 1);
                    if (c1 != null)
                    {
                        index1 = controllersSorted.IndexOf(c1);
                        finalControllerList.Add(index1, c1);
                    }
                }
                else
                    return;

                foreach (var c in finalControllerList)
                {
                    ConfigureJoystick(lines, c.Value, c.Key, c.Value.PlayerIndex, commandArray);
                }

                File.WriteAllLines(configFile, lines);

                commandArray.Add("--kbdhackfile=hackfile_retrobat.cfg");
            }
            catch { }
            
            return;
        }

        private void ConfigureJoystick(List<string> lines, Controller ctrl, int cIndex, int playerindex, List<string> commandArray)
        {
            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string joyNb = "JS" + cIndex;
            var mapping = GetFinalMapping();
            bool rightPad = SystemConfig.getOptBoolean("intv_right_pad");

            string targetPrefix = "PD0L_";
            string targetPrefix2 = "PD0R_";

            if (playerindex == 1 && rightPad)
            {
                targetPrefix = "PD0R_";
                targetPrefix2 = "PD0L_";
            }

            if (playerindex == 2)
            {
                targetPrefix = rightPad ? "PD0L_" : "PD0R_";
                targetPrefix2 = rightPad ? "PD0R_" : "PD0L_";
            }

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ". Fetching gamecontrollerdb.txt file with guid : " + guid);

            // Fetching controller mapping from gamecontrollerdb.txt file
            SdlToDirectInput c = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

            if (c == null)
            {
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ". No controller found in gamecontrollerdb.txt file for guid : " + guid);
                return;
            }
            else
                SimpleLogger.Instance.Info("[INFO] Player " + playerindex + ": " + guid + " found in gamecontrollerDB file.");

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return;
            }

            for (int i = 0; i < 2; i++)
            {
                lines.Add(string.Empty);
                lines.Add("MAP " + i);
                string vprefix = (i == 0) ? targetPrefix : targetPrefix2;
                string vprefixopp = (i == 0) ? targetPrefix2 : targetPrefix;
                int js0cCounter = 0;

                AddKeyBoardLines(lines);

                foreach (var map in mapping)
                {
                    string value = mapping[map.Key];

                    if (string.IsNullOrEmpty(value))
                        continue;

                    if (map.Key == "leftjoy")
                    {
                        string dirNr = "16dir";
                        string jsxbcommand = "";

                        if (!c.ButtonMappings.ContainsKey("leftx") || !c.ButtonMappings.ContainsKey("lefty"))
                        {
                            SimpleLogger.Instance.Info("[INFO] No left joystick mapping found for the controller of player " + playerindex);
                            continue;
                        }
                        else if (!c.ButtonMappings["leftx"].StartsWith("a") || !c.ButtonMappings["lefty"].StartsWith("a"))
                        {
                            SimpleLogger.Instance.Info("[INFO] Left joystick is not analog for the controller of player " + playerindex);
                            continue;
                        }
                        else
                        {
                            string axisX = "0";
                            string axisY = "1";
                            jsxbcommand += "--js" + cIndex + "=\"xaxis=" + axisX + ",yaxis=" + axisY + ",noac";
                        }

                        switch (value)
                        {
                            case "directions":
                                lines.Add(joyNb + "_N " + vprefix + "J_N");
                                lines.Add(joyNb + "_NE " + vprefix + "J_NE");
                                lines.Add(joyNb + "_E " + vprefix + "J_E");
                                lines.Add(joyNb + "_SE " + vprefix + "J_SE");
                                lines.Add(joyNb + "_S " + vprefix + "J_S");
                                lines.Add(joyNb + "_SW " + vprefix + "J_SW");
                                lines.Add(joyNb + "_W " + vprefix + "J_W");
                                lines.Add(joyNb + "_NW " + vprefix + "J_NW");
                                lines.Add(joyNb + "_NNE " + vprefix + "J_NNE");
                                lines.Add(joyNb + "_ENE " + vprefix + "J_ENE");
                                lines.Add(joyNb + "_ESE " + vprefix + "J_ESE");
                                lines.Add(joyNb + "_SSE " + vprefix + "J_SSE");
                                lines.Add(joyNb + "_SSW " + vprefix + "J_SSW");
                                lines.Add(joyNb + "_WSW " + vprefix + "J_WSW");
                                lines.Add(joyNb + "_WNW " + vprefix + "J_WNW");
                                lines.Add(joyNb + "_NNW " + vprefix + "J_NNW");
                                break;
                            case "directionsopposite":
                                lines.Add(joyNb + "_N " + vprefixopp + "J_N");
                                lines.Add(joyNb + "_NE " + vprefixopp + "J_NE");
                                lines.Add(joyNb + "_E " + vprefixopp + "J_E");
                                lines.Add(joyNb + "_SE " + vprefixopp + "J_SE");
                                lines.Add(joyNb + "_S " + vprefixopp + "J_S");
                                lines.Add(joyNb + "_SW " + vprefixopp + "J_SW");
                                lines.Add(joyNb + "_W " + vprefixopp + "J_W");
                                lines.Add(joyNb + "_NW " + vprefixopp + "J_NW");
                                lines.Add(joyNb + "_NNE " + vprefixopp + "J_NNE");
                                lines.Add(joyNb + "_ENE " + vprefixopp + "J_ENE");
                                lines.Add(joyNb + "_ESE " + vprefixopp + "J_ESE");
                                lines.Add(joyNb + "_SSE " + vprefixopp + "J_SSE");
                                lines.Add(joyNb + "_SSW " + vprefixopp + "J_SSW");
                                lines.Add(joyNb + "_WSW " + vprefixopp + "J_WSW");
                                lines.Add(joyNb + "_WNW " + vprefixopp + "J_WNW");
                                lines.Add(joyNb + "_NNW " + vprefixopp + "J_NNW");
                                break;
                            case "na":
                                lines.Add(joyNb + "_N NA");
                                lines.Add(joyNb + "_NE NA");
                                lines.Add(joyNb + "_E NA");
                                lines.Add(joyNb + "_SE NA");
                                lines.Add(joyNb + "_S NA");
                                lines.Add(joyNb + "_SW NA");
                                lines.Add(joyNb + "_W NA");
                                lines.Add(joyNb + "_NW NA");
                                lines.Add(joyNb + "_NNE NA");
                                lines.Add(joyNb + "_ENE NA");
                                lines.Add(joyNb + "_ESE NA");
                                lines.Add(joyNb + "_SSE NA");
                                lines.Add(joyNb + "_SSW NA");
                                lines.Add(joyNb + "_WSW NA");
                                lines.Add(joyNb + "_WNW NA");
                                lines.Add(joyNb + "_NNW NA");
                                break;
                        }

                        if (!string.IsNullOrEmpty(jsxbcommand))
                        {
                            string push = "20";
                            if (SystemConfig.isOptSet("intv_joystick_push") && !string.IsNullOrEmpty(SystemConfig["intv_joystick_push"]))
                                push = SystemConfig["intv_joystick_push"].ToIntegerString();

                            string rels = "10";
                            if (SystemConfig.isOptSet("intv_joystick_rels") && !string.IsNullOrEmpty(SystemConfig["intv_joystick_rels"]))
                                rels = SystemConfig["intv_joystick_rels"].ToIntegerString();

                            jsxbcommand += ",push=" + push + ",rels=" + rels + "," + "xrng=-49:50,yrng=-49:50," + dirNr + "\"";

                            if (i==0)
                                commandArray.Add(jsxbcommand);
                        }

                    }

                    else if (map.Key == "rightjoy")
                    {
                        string dirNr = "16dir";
                        string jsxbcommand = "";

                        if (!c.ButtonMappings.ContainsKey("rightx") || !c.ButtonMappings.ContainsKey("righty"))
                        {
                            SimpleLogger.Instance.Info("[INFO] No right joystick mapping found for the controller of player " + playerindex);
                            continue;
                        }
                        else if (!c.ButtonMappings["rightx"].StartsWith("a") || !c.ButtonMappings["righty"].StartsWith("a"))
                        {
                            SimpleLogger.Instance.Info("[INFO] Right joystick is not analog for the controller of player " + playerindex);
                            continue;
                        }
                        else
                        {
                            string axisX = ctrl.IsXInputDevice ? "4" : "2";
                            string axisY = ctrl.IsXInputDevice ? "3" : "3";
                            jsxbcommand += "--js" + cIndex + "b=\"xaxis=" + axisX + ",yaxis=" + axisY + ",noac";
                        }

                        switch (value)
                        {
                            case "directions":
                                lines.Add(joyNb + "B_N " + vprefix + "J_N");
                                lines.Add(joyNb + "B_NE " + vprefix + "J_NE");
                                lines.Add(joyNb + "B_E " + vprefix + "J_E");
                                lines.Add(joyNb + "B_SE " + vprefix + "J_SE");
                                lines.Add(joyNb + "B_S " + vprefix + "J_S");
                                lines.Add(joyNb + "B_SW " + vprefix + "J_SW");
                                lines.Add(joyNb + "B_W " + vprefix + "J_W");
                                lines.Add(joyNb + "B_NW " + vprefix + "J_NW");
                                lines.Add(joyNb + "B_NNE " + vprefix + "J_NNE");
                                lines.Add(joyNb + "B_ENE " + vprefix + "J_ENE");
                                lines.Add(joyNb + "B_ESE " + vprefix + "J_ESE");
                                lines.Add(joyNb + "B_SSE " + vprefix + "J_SSE");
                                lines.Add(joyNb + "B_SSW " + vprefix + "J_SSW");
                                lines.Add(joyNb + "B_WSW " + vprefix + "J_WSW");
                                lines.Add(joyNb + "B_WNW " + vprefix + "J_WNW");
                                lines.Add(joyNb + "B_NNW " + vprefix + "J_NNW");
                                break;
                            case "directionsopposite":
                                lines.Add(joyNb + "B_N " + vprefixopp + "J_N");
                                lines.Add(joyNb + "B_NE " + vprefixopp + "J_NE");
                                lines.Add(joyNb + "B_E " + vprefixopp + "J_E");
                                lines.Add(joyNb + "B_SE " + vprefixopp + "J_SE");
                                lines.Add(joyNb + "B_S " + vprefixopp + "J_S");
                                lines.Add(joyNb + "B_SW " + vprefixopp + "J_SW");
                                lines.Add(joyNb + "B_W " + vprefixopp + "J_W");
                                lines.Add(joyNb + "B_NW " + vprefixopp + "J_NW");
                                lines.Add(joyNb + "B_NNE " + vprefixopp + "J_NNE");
                                lines.Add(joyNb + "B_ENE " + vprefixopp + "J_ENE");
                                lines.Add(joyNb + "B_ESE " + vprefixopp + "J_ESE");
                                lines.Add(joyNb + "B_SSE " + vprefixopp + "J_SSE");
                                lines.Add(joyNb + "B_SSW " + vprefixopp + "J_SSW");
                                lines.Add(joyNb + "B_WSW " + vprefixopp + "J_WSW");
                                lines.Add(joyNb + "B_WNW " + vprefixopp + "J_WNW");
                                lines.Add(joyNb + "B_NNW " + vprefixopp + "J_NNW");
                                break;
                            case "na":
                                lines.Add(joyNb + "B_N NA");
                                lines.Add(joyNb + "B_NE NA");
                                lines.Add(joyNb + "B_E NA");
                                lines.Add(joyNb + "B_SE NA");
                                lines.Add(joyNb + "B_S NA");
                                lines.Add(joyNb + "B_SW NA");
                                lines.Add(joyNb + "B_W NA");
                                lines.Add(joyNb + "B_NW NA");
                                lines.Add(joyNb + "B_NNE NA");
                                lines.Add(joyNb + "B_ENE NA");
                                lines.Add(joyNb + "B_ESE NA");
                                lines.Add(joyNb + "B_SSE NA");
                                lines.Add(joyNb + "B_SSW NA");
                                lines.Add(joyNb + "B_WSW NA");
                                lines.Add(joyNb + "B_WNW NA");
                                lines.Add(joyNb + "B_NNW NA");
                                break;
                        }

                        if (!string.IsNullOrEmpty(jsxbcommand))
                        {
                            string push = "20";
                            if (SystemConfig.isOptSet("intv_joystick_push") && !string.IsNullOrEmpty(SystemConfig["intv_joystick_push"]))
                                push = SystemConfig["intv_joystick_push"].ToIntegerString();

                            string rels = "10";
                            if (SystemConfig.isOptSet("intv_joystick_rels") && !string.IsNullOrEmpty(SystemConfig["intv_joystick_rels"]))
                                rels = SystemConfig["intv_joystick_rels"].ToIntegerString();

                            jsxbcommand += ",push=" + push + ",rels=" + rels + "," + "xrng=-49:50,yrng=-49:50," + dirNr + "\"";

                            if (i == 0)
                                commandArray.Add(jsxbcommand);
                        }
                    }
                    
                    else if (map.Key == "dpad")
                    {
                        switch (value)
                        {
                            case "directions":
                                lines.Add(joyNb + "_HAT0_N " + vprefix + "D_N");
                                lines.Add(joyNb + "_HAT0_NE " + vprefix + "D_NE");
                                lines.Add(joyNb + "_HAT0_E " + vprefix + "D_E");
                                lines.Add(joyNb + "_HAT0_SE " + vprefix + "D_SE");
                                lines.Add(joyNb + "_HAT0_S " + vprefix + "D_S");
                                lines.Add(joyNb + "_HAT0_SW " + vprefix + "D_SW");
                                lines.Add(joyNb + "_HAT0_W " + vprefix + "D_W");
                                lines.Add(joyNb + "_HAT0_NW " + vprefix + "D_NW");
                                break;
                            case "directionsopposite":
                                lines.Add(joyNb + "_HAT0_N " + vprefixopp + "D_N");
                                lines.Add(joyNb + "_HAT0_NE " + vprefixopp + "D_NE");
                                lines.Add(joyNb + "_HAT0_E " + vprefixopp + "D_E");
                                lines.Add(joyNb + "_HAT0_SE " + vprefixopp + "D_SE");
                                lines.Add(joyNb + "_HAT0_S " + vprefixopp + "D_S");
                                lines.Add(joyNb + "_HAT0_SW " + vprefixopp + "D_SW");
                                lines.Add(joyNb + "_HAT0_W " + vprefixopp + "D_W");
                                lines.Add(joyNb + "_HAT0_NW " + vprefixopp + "D_NW");
                                break;
                            case "keypad":
                                lines.Add(joyNb + "_HAT0_N " + vprefix + "KP2");
                                lines.Add(joyNb + "_HAT0_NE " + vprefix + "KP3");
                                lines.Add(joyNb + "_HAT0_E " + vprefix + "KP6");
                                lines.Add(joyNb + "_HAT0_SE " + vprefix + "KP9");
                                lines.Add(joyNb + "_HAT0_S " + vprefix + "KP8");
                                lines.Add(joyNb + "_HAT0_SW " + vprefix + "KP7");
                                lines.Add(joyNb + "_HAT0_W " + vprefix + "KP4");
                                lines.Add(joyNb + "_HAT0_NW " + vprefix + "KP1");
                                break;
                            case "na":
                                lines.Add(joyNb + "_HAT0_N NA");
                                lines.Add(joyNb + "_HAT0_NE NA");
                                lines.Add(joyNb + "_HAT0_E NA");
                                lines.Add(joyNb + "_HAT0_SE NA");
                                lines.Add(joyNb + "_HAT0_S NA");
                                lines.Add(joyNb + "_HAT0_SW NA");
                                lines.Add(joyNb + "_HAT0_W NA");
                                lines.Add(joyNb + "_HAT0_NW NA");
                                break;
                        }
                    }

                    else
                    {
                        string keyPrefix = joyNb;
                        if (!GetDinputMapping(c, map.Key, ctrl, out bool triggerbutton, out string id))
                        {
                            SimpleLogger.Instance.Info("[INFO] No mapping found for " + map.Key + " in gamecontrollerdb file for player " + playerindex);
                            continue;
                        }
                        else
                        {
                            if (triggerbutton)
                            {
                                string direction = map.Key == "lefttrigger" ? "E" : "W";
                                keyPrefix = joyNb + "C_" + direction;

                                if (ctrl.IsXInputDevice)
                                {
                                    if (js0cCounter < 2)
                                    {
                                        commandArray.Add("--js" + cIndex + "c=\"xaxis=2,ac,push=60,rels=50,button\"");
                                        js0cCounter = 2;
                                    }
                                }
                                else
                                {
                                    if (map.Key == "lefttrigger")
                                    {
                                        commandArray.Add("--js" + cIndex + "c=\"xaxis=5,ac,push=60,rels=50,button\"");
                                        keyPrefix = joyNb + "C_E";
                                        string cancelW = joyNb + "C_W";
                                        lines.Add(cancelW + " NA");
                                    }
                                    else if (value == "righttrigger")
                                    {
                                        commandArray.Add("--js" + cIndex + "d=\"xaxis=4,ac,push=60,rels=50,button\"");
                                        keyPrefix = joyNb + "D_E";
                                        string cancelW = joyNb + "D_W";
                                        lines.Add(cancelW + " NA");
                                    }
                                }
                            }
                            else
                            {
                                keyPrefix = joyNb + "_BTN_" + id;
                            }
                        }
                        
                        if (value.ToLowerInvariant().StartsWith("kp") || value.ToLowerInvariant().StartsWith("a_"))
                        {
                            string v = vprefix + value;
                            lines.Add(keyPrefix + " " + v);
                        }
                        else if (value.ToLowerInvariant() == "switchpad")
                        {
                            if (i == 0)
                                lines.Add(keyPrefix + " " + "SETMAP1");
                            else
                                lines.Add(keyPrefix + " " + "SETMAP0");
                        }
                        else
                        {
                            lines.Add(keyPrefix + " " + value);
                        }
                    }
                }
            }
        }


        private bool GetDinputMapping(SdlToDirectInput c, string buttonkey, Controller ctrl, out bool triggerbutton, out string id)
        {
            id = "";
            triggerbutton = false;
            bool isXinput = ctrl.IsXInputDevice;

            if (c == null)
                return false;

            int direction = 1;

            if (c.ButtonMappings == null)
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for the controller.");
                return false;
            }

            if (buttonkey == "lefttrigger" && c.ButtonMappings.ContainsKey("lefttriggerbutton"))
                buttonkey = "lefttriggerbutton";
            if (buttonkey == "righttrigger" && c.ButtonMappings.ContainsKey("righttriggerbutton"))
                buttonkey = "righttriggerbutton";

            if (!c.ButtonMappings.ContainsKey(buttonkey))
            {
                SimpleLogger.Instance.Info("[INFO] No mapping found for " + buttonkey + " in gamecontrollerdb file");
                return false;
            }

            string button = c.ButtonMappings[buttonkey];

            if (button.StartsWith("b"))
            {
                string buttonID = (button.Substring(1));
                id = doubleDigitString(buttonID);
                return true;
            }

            else if (button.StartsWith("h"))
            {
                return false;
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                if (buttonkey == "lefttrigger" || buttonkey == "righttrigger")
                    triggerbutton = true;

                return true;
            }

            return false;
        }

        private string GetAxis(string axis)
        {
            if (axis.StartsWith("-a") || axis.StartsWith("+a"))
            {
                return axis.Substring(2);
            }
            else if (axis.StartsWith("a"))
            {
                return axis.Substring(1);
            }
            
            return axis;
        }

        private string doubleDigitString(string x)
        {
            if (int.TryParse(x, out int n))
            {
                return n.ToString("D2");
            }
            else
            {
                return x;
            }
        }

        private readonly Dictionary<string, string> DefaultMapping = new Dictionary<string, string>()
        {
            { "a", "A_L" },             // SOUTH
            { "b", "A_R" },             // EAST
            { "x", "A_T" },             // WEST
            { "y", "KP0" },             // NORTH
            { "leftshoulder", "A_L" },
            { "rightshoulder", "A_R" },
            { "start", "PAUSE" },
            { "back", "switchpad" },
            { "rightstick", "KP5" },
            { "leftstick", "NA" },
            { "lefttrigger", "KPC" },
            { "righttrigger", "KPE" },
            // treat these separatlely
            { "dpad", "keypad" },
            { "leftjoy", "directions" },
            { "rightjoy", "directions" }
        };

        private Dictionary<string, string> GetFinalMapping ()
        {
            var ret = DefaultMapping;

            foreach (var key in DefaultMapping.Keys.ToList())
            {
                string featureName = "intv_button_" + key;
                if (SystemConfig.isOptSet(featureName) && !string.IsNullOrEmpty(SystemConfig[featureName]))
                    ret[key] = SystemConfig[featureName];
            }

            return ret;
        }


        private void AddKeyBoardLines(List<string> lines)
        {
            lines.Add("F1 NA");
            lines.Add("ESCAPE QUIT");
            lines.Add("F8 SHOT");
        }

        /*
        ---- F1 quits, need to set to PAUSE
        ---- ESCAPE needs to be set to QUIT
        ---- F8 to be set to SHOT
        ---- F to set to WTOG
        ---- Keys    
            ; JS0_xx_00 **Left joystick (N, NNE, NE, ENE, E, ESE, SE, SSE, S, SSW, SW, WSW, W, WNW, NW, NNW)
            ; JS0B_BTN_00 **right joystick (need axis) (N, NNE, NE, ENE, E, ESE, SE, SSE, S, SSW, SW, WSW, W, WNW, NW, NNW)
            ; JS0C_BTN_00 **used for triggers (when axis) (E, W) - use east (special for xbox) when both triggers have same ID
            ; JS0_HAT0_00 **DPAD (N, NE, E, SE, S, SW, W, NW)
            ; PD0L / PD0R Left and right Controller 1 & 2
        ---- values:   
            ; PD0L for left controller and PD0R for right controller
            ; PD0L_A_T **Top Side Buttons
            ; PD0L_A_L **Left Lower Button
            ; PD0L_A_R **Right Lower Button
            ; PD0L_KP0-KP9 **Number Pad 0-9
            ; PD0L_KPC **Clear
            ; PD0L_KPE **Enter
            ; Others: QUIT, PAUSE, RESET
            ; PD0L_J_(N, NNE, NE, ENE, E, ESE, SE, SSE, S, SSW, SW, WSW, W, WNW, NW, NNW)
        */
    }
}
