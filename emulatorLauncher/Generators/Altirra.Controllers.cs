using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Joysticks;
using System.Collections.Generic;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class AltirraGenerator : Generator
    {
        private Dictionary<string, string> _inputMaps = new Dictionary<string, string>();
        private int _xinputCount;
        private string _activeProfiles = "";

        private void ConfigureControllers(IniFile ini, string system, AltirraProfile profile)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (Program.Controllers.Count < 1)
                return;

            // Get count of xinput controllers (will be used later for index of gamepad)
            _xinputCount = Controllers.Where(c => c.IsXInputDevice).Count();

            // Save existing mapping profiles to restore them later
            string inputMapsSection = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000\\Input maps";
            var maps = ini.EnumerateKeys(inputMapsSection);

            if (maps != null && maps.Length > 0)
            {
                foreach (var map in maps)
                {
                    _inputMaps.Add(map, ini.GetValue(inputMapsSection, map));
                }
            }

            ini.ClearSection("User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000\\Input maps");

            // Inject controllers
            if (!Controllers.Any(c => !c.IsKeyboard))
            {
                ConfigureKeyboard(ini, system, inputMapsSection);
                return;
            }
            else
            {
                foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(c => c.PlayerIndex).Take(2))
                    ConfigureInput(ini, controller, system, inputMapsSection);
            }

            ini.WriteValue("User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000", "Input: Active map names", "\"" + _activeProfiles + "\"");
            ini.WriteValue("User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000", "Input: Quick map names", "\"\"");
        }

        private void ConfigureInput(IniFile ini, Controller ctrl, string system, string inputMapsSection)
        {
            if (ctrl == null || ctrl.Config == null)
                return;

            int playerIndex = ctrl.PlayerIndex;
            bool isXinput = ctrl.IsXInputDevice;
            
            SdlToDirectInput sdlCtrl = null;
            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";

            // Define port to use
            int joyPort = playerIndex;

            if (playerIndex == 1)
            {
                if (SystemConfig.getOptBoolean("altirra_joyport2"))
                    joyPort = 2;
                else
                    joyPort = 1;
            }
            else if (playerIndex == 2)
            {
                if (SystemConfig.getOptBoolean("altirra_joyport2"))
                    joyPort = 1;
                else
                    joyPort = 2;
            }

            // Get mapping for dinput controller
            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Info("[INFO] gamecontrollerdb.txt file not found in tools folder. Controller mapping will not be available.");
                gamecontrollerDB = null;
            }

            if (gamecontrollerDB != null)
            {
                SimpleLogger.Instance.Info("[INFO] Player 1. Fetching gamecontrollerdb.txt file with guid : " + guid);

                sdlCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);

                if (sdlCtrl == null)
                    SimpleLogger.Instance.Info("[INFO] Player 1. No controller found in gamecontrollerdb.txt file for guid : " + guid);
                else
                    SimpleLogger.Instance.Info("[INFO] Player 1: " + guid + " found in gamecontrollerDB file.");
            }

            List<byte> byteList = new List<byte>();
            byteList.AddRange(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x00 });

            // This byte is the number of mapped controllers, Player 1 will have 2 mappings: joystick + console
            if (playerIndex == 1 && system != "atari5200")
                byteList.Add(0x02);
            else
                byteList.Add(0x01);

            byteList.AddRange(new byte[] { 0x00, 0x00, 0x00 });

            // Next byte represents the number of mappings in the profile
            if (playerIndex == 1 && (system == "atari5200"))
                byteList.Add(0x17);
            else if (playerIndex == 1)
                byteList.Add(0x07);
            else if (system == "atari5200")
                byteList.Add(0x17);
            else
                byteList.Add(0x05);

            byteList.AddRange(new byte[] { 0x00, 0x00, 0x00 });

            // index of gamepad
            if (ctrl.IsXInputDevice && SystemConfig.isOptSet("altirra_inputdriver") && SystemConfig["altirra_inputdriver"] == "xinput")
            {
                int xIndex = ctrl.PlayerIndex - 1;

                if (ctrl.XInput != null)
                    xIndex = ctrl.XInput.DeviceIndex;

                if (SystemConfig.isOptSet("altirra_forcepadindex" + playerIndex) && !string.IsNullOrEmpty(SystemConfig["altirra_forcepadindex" + playerIndex]))
                    xIndex = SystemConfig["altirra_forcepadindex" + playerIndex].ToInteger() - 1;

                byteList.Add((byte)xIndex);
            }
            else
            {
                int dIndex = ctrl.DirectInput.DeviceIndex + _xinputCount;
                byteList.Add((byte)dIndex);
            }
            byteList.AddRange(new byte[] { 0x00, 0x00, 0x00 });

            // Add the bytes corresponding to the name of the controller
            byteList.AddRange(GetControllerNameByteRange(system, playerIndex, joyPort));

            byteList.AddRange(new byte[] { 0x00, 0x00 });

            // Add the bytes for type of joystick
            if (system == "atari5200")
                byteList.Add(0x05);         // 5200 controller
            else
                byteList.Add(0x01);         // CX40 Joystick

            byteList.AddRange(new byte[] { 0x00, 0x00, 0x00 });

            // Port for the controller
            if (joyPort == 1)
                byteList.Add(0x00);
            else if (joyPort == 2)
                byteList.Add(0x01);

            byteList.AddRange(new byte[] { 0x00, 0x00, 0x00 });

            // Add console mapping for non 5200 to get start and select
            if (playerIndex == 1 && system != "atari5200")
            {
                byteList.Add(0x04);
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
            }

            // Start button mapping
            if (isXinput)
            {
                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "x"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "dpleft"));
                byteList.AddRange(GetByteRangeForButton(system, 1, playerIndex));   // button 1 - left

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "a"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "dpright"));
                byteList.AddRange(GetByteRangeForButton(system, 2, playerIndex));   // button 2 - right

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "dpleft"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "dpup"));
                byteList.AddRange(GetByteRangeForButton(system, 3, playerIndex));   // left - up

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "dpright"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "dpdown"));
                byteList.AddRange(GetByteRangeForButton(system, 4, playerIndex));   // right - down

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "dpup"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "a"));
                byteList.AddRange(GetByteRangeForButton(system, 5, playerIndex));   // up - button 1

                if (playerIndex == 1 && system != "atari5200")
                    goto finish;

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "dpdown"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "start"));
                byteList.AddRange(GetByteRangeForButton(system, 6, playerIndex));   // down - start

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForXInput(system, "leftx"));
                else
                    byteList.AddRange(GetByteRangeForXInput(system, "back"));
                byteList.AddRange(GetByteRangeForButton(system, 7, playerIndex));   // analog H - select

                if (system != "atari5200")
                    goto finish;

                byteList.AddRange(GetByteRangeForXInput(system, "lefty"));
                byteList.AddRange(GetByteRangeForButton(system, 8, playerIndex));   // analog Y

                // 5200 controller num keys (not mapped)
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 9, playerIndex));   // 0 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 10, playerIndex));   // 1 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 11, playerIndex));   // 2 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 12, playerIndex));   // 3 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 13, playerIndex));   // 4 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 14, playerIndex));   // 5 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 15, playerIndex));   // 6 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 16, playerIndex));   // 7 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 17, playerIndex));   // 8 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 18, playerIndex));   // 9 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 19, playerIndex));   // * key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 20, playerIndex));   // # key

                // Start, reset and pause
                byteList.AddRange(GetByteRangeForXInput(system, "start"));
                byteList.AddRange(GetByteRangeForButton(system, 21, playerIndex));   // start
                byteList.AddRange(GetByteRangeForXInput(system, "rightstick"));
                byteList.AddRange(GetByteRangeForButton(system, 22, playerIndex));   // reset
                byteList.AddRange(GetByteRangeForXInput(system, "back"));
                byteList.AddRange(GetByteRangeForButton(system, 23, playerIndex));   // pause
            }
            else if (sdlCtrl != null)
            {
                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "x", sdlCtrl));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "dpleft", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 1, playerIndex));   // button 1 - left

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "a", sdlCtrl));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "dpright", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 2, playerIndex));   // button 2 - right

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "dpleft", sdlCtrl));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "dpup", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 3, playerIndex));   // left - up

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "dpright", sdlCtrl));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "dpdown", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 4, playerIndex));   // right - down

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "dpup", sdlCtrl));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "a", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 5, playerIndex));   // up - button 1

                if (playerIndex == 1 && system != "atari5200")
                    goto finish;

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "dpdown", sdlCtrl));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "start", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 6, playerIndex));   // down - start

                if (system == "atari5200")
                    byteList.AddRange(GetByteRangeForInput(system, "leftx", sdlCtrl, true));
                else
                    byteList.AddRange(GetByteRangeForInput(system, "back", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 7, playerIndex));   // analog H - select

                if (system != "atari5200")
                    goto finish;

                byteList.AddRange(GetByteRangeForInput(system, "lefty", sdlCtrl, true));
                byteList.AddRange(GetByteRangeForButton(system, 8, playerIndex));   // analog Y

                // 5200 controller num keys (not mapped)
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 9, playerIndex));   // 0 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 10, playerIndex));   // 1 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 11, playerIndex));   // 2 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 12, playerIndex));   // 3 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 13, playerIndex));   // 4 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 14, playerIndex));   // 5 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 15, playerIndex));   // 6 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 16, playerIndex));   // 7 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 17, playerIndex));   // 8 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 18, playerIndex));   // 9 key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 19, playerIndex));   // * key
                byteList.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                byteList.AddRange(GetByteRangeForButton(system, 20, playerIndex));   // # key

                // Start, reset and pause
                byteList.AddRange(GetByteRangeForInput(system, "start", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 21, playerIndex));   // start
                byteList.AddRange(GetByteRangeForInput(system, "rightstick", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 22, playerIndex));   // reset
                byteList.AddRange(GetByteRangeForInput(system, "back", sdlCtrl));
                byteList.AddRange(GetByteRangeForButton(system, 23, playerIndex));   // pause
            }

            finish:
            // Write to input maps
            byte[] byteArray = byteList.ToArray();
            string newHexString = string.Join(" ", byteArray.Select(b => b.ToString("X2")));
            ini.WriteValue(inputMapsSection, "Input map " + (playerIndex - 1).ToString(), "[" + newHexString + "]");

            // Write active Profiles
            if (playerIndex == 1 && system == "atari5200")
                _activeProfiles = "Retrobat -> 5200 (port " + joyPort + ")";
            else if (playerIndex == 1)
                _activeProfiles = "Retrobat -> CX40 (port " + joyPort + ")";
            else if (system == "atari5200")
                _activeProfiles = _activeProfiles + "\\n" + "Retrobat -> 5200 (port " + joyPort + ")";
            else
                _activeProfiles = _activeProfiles + "\\n" + "Retrobat -> CX40 (port " + joyPort + ")";
        }

        private void ConfigureKeyboard(IniFile ini, string system, string inputMapsSection)
        {
            string defaultProfileSection = "User\\Software\\virtualdub.org\\Altirra\\Profiles\\00000000";

            int joyPort = 1;
            if (SystemConfig.getOptBoolean("altirra_joyport2"))
                joyPort = 2;

            int i = 0;
            foreach (var kbP in altirraDefaultKeyboardProfiles)
            {
                ini.WriteValue(inputMapsSection, "Input map " + i, "[" + kbP.ByteArray + "]");
                i++;
            }

            AltirraKBProfile kbProfile = altirraDefaultKeyboardProfiles.FirstOrDefault(p => p.System.Contains(system) && p.Port == joyPort);

            if (kbProfile != null)
                ini.WriteValue(defaultProfileSection, "Input: Active map names", "\"" + kbProfile.Name + "\"");
        }

        private byte[] GetByteRangeForInput(string system, string buttonkey, SdlToDirectInput ctrl, bool isAxis = false)
        {
            byte[] byteArray = new byte[6];
            string direction = "left";

            if (SystemConfig.getOptBoolean("altirra_usestick"))
            {
                if (buttonkey == "dpleft")
                {
                    buttonkey = "leftx";
                }
                else if (buttonkey == "dpright")
                {
                    buttonkey = "leftx";
                    direction = "right";
                }
                else if (buttonkey == "dpup")
                {
                    buttonkey = "lefty";
                }
                else if (buttonkey == "dpdown")
                {
                    buttonkey = "lefty";
                    direction = "right";
                }
            }

            if (ctrl == null || string.IsNullOrEmpty(buttonkey))
                return byteArray;

            if (!ctrl.ButtonMappings.ContainsKey(buttonkey))
                return byteArray;

            string button = ctrl.ButtonMappings[buttonkey];

            if (button.StartsWith("h"))
            {
                int hatID = button.Substring(3).ToInteger();
                switch (hatID)
                {
                    case 1:
                        byteArray[0] = 0x0E;
                        break;
                    case 2:
                        byteArray[0] = 0x0D;
                        break;
                    case 4:
                        byteArray[0] = 0x0F;
                        break;
                    case 8:
                        byteArray[0] = 0x0C;
                        break;
                };
                byteArray[1] = 0x21;
            }

            else if (button.StartsWith("b"))
            {
                int buttonID = button.Substring(1).ToInteger();
                buttonID++;
                byteArray[0] = (byte)buttonID;
                byteArray[1] = 0x28;
            }

            else if (button.StartsWith("a") || button.StartsWith("-a") || button.StartsWith("+a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                else if (button.StartsWith("a"))
                    axisID = button.Substring(1).ToInteger();

                if (isAxis)
                {
                    byteArray[0] = (byte)axisID;
                    byteArray[1] = 0x20;
                }
                else
                {
                    switch (axisID)
                    {
                        case 0:
                            if (direction == "right")
                                byteArray[0] = 0x01;
                            else
                                byteArray[0] = 0x00;
                            break;
                        case 1:
                            if (direction == "right")
                                byteArray[0] = 0x03;
                            else
                                byteArray[0] = 0x02;
                            break;
                        case 3:
                            if (direction == "right")
                                byteArray[0] = 0x05;
                            else
                                byteArray[0] = 0x04;
                            break;
                        case 4:
                            if (direction == "right")
                                byteArray[0] = 0x0B;
                            else
                                byteArray[0] = 0x0A;
                            break;
                    }
                    byteArray[1] = 0x21;
                }
            }

            return byteArray;
        }

        private byte[] GetByteRangeForXInput(string system, string buttonkey)
        {
            byte[] byteArray = new byte[6];

            if (SystemConfig.getOptBoolean("altirra_usestick") && buttonkey.StartsWith("dp"))
            {
                if (buttonkey == "dpleft")
                {
                    byteArray[0] = 0x00;
                }
                else if (buttonkey == "dpright")
                {
                    byteArray[0] = 0x01;
                }
                else if (buttonkey == "dpup")
                {
                    byteArray[0] = 0x02;
                }
                else if (buttonkey == "dpdown")
                {
                    byteArray[0] = 0x03;
                }
                byteArray[1] = 0x21;
            }
            else
            {
                switch (buttonkey)
                {
                    case "a":
                        byteArray[0] = 0x00;
                        byteArray[1] = 0x28;
                        break;
                    case "b":
                        byteArray[0] = 0x01;
                        byteArray[1] = 0x28;
                        break;
                    case "x":
                        byteArray[0] = 0x02;
                        byteArray[1] = 0x28;
                        break;
                    case "y":
                        byteArray[0] = 0x03;
                        byteArray[1] = 0x28;
                        break;
                    case "dpleft":
                        byteArray[0] = 0x0C;
                        byteArray[1] = 0x21;
                        break;
                    case "dpright":
                        byteArray[0] = 0x0D;
                        byteArray[1] = 0x21;
                        break;
                    case "dpup":
                        byteArray[0] = 0x0E;
                        byteArray[1] = 0x21;
                        break;
                    case "dpdown":
                        byteArray[0] = 0x0F;
                        byteArray[1] = 0x21;
                        break;
                    case "leftx":
                        byteArray[0] = 0x00;
                        byteArray[1] = 0x20;
                        break;
                    case "lefty":
                        byteArray[0] = 0x01;
                        byteArray[1] = 0x20;
                        break;
                    case "start":
                        byteArray[0] = 0x07;
                        byteArray[1] = 0x28;
                        break;
                    case "rightstick":
                        byteArray[0] = 0x09;
                        byteArray[1] = 0x28;
                        break;
                    case "back":
                        byteArray[0] = 0x06;
                        byteArray[1] = 0x28;
                        break;
                }
            }

            return byteArray;
        }

        private byte[] GetByteRangeForButton(string system, int id, int playerIndex)
        {
            byte[] byteArray = new byte[6];

            if (system == "atari5200" && px_5200.ContainsKey(id))
                byteArray = px_5200[id];

            else
            {
                if (playerIndex == 1 && p1_800.ContainsKey(id))
                    byteArray = p1_800[id];
                else if (p2_800.ContainsKey(id))
                    byteArray = p2_800[id];
            }

            return byteArray;
        }

        private byte[] GetControllerNameByteRange(string system, int playerIndex, int joyPort)
        {
            byte[] byteArray = new byte[50];

            byteArray[0] = 0x52; byteArray[1] = 0x00;  // 'R'
            byteArray[2] = 0x65; byteArray[3] = 0x00;  // 'e'
            byteArray[4] = 0x74; byteArray[5] = 0x00;  // 't'
            byteArray[6] = 0x72; byteArray[7] = 0x00;  // 'r'
            byteArray[8] = 0x6F; byteArray[9] = 0x00;  // 'o'
            byteArray[10] = 0x62; byteArray[11] = 0x00;  // 'b'
            byteArray[12] = 0x61; byteArray[13] = 0x00;  // 'a'
            byteArray[14] = 0x74; byteArray[15] = 0x00;  // 't'
            byteArray[16] = 0x20; byteArray[17] = 0x00;  // ' '
            byteArray[18] = 0x2D; byteArray[19] = 0x00;  // '-'
            byteArray[20] = 0x3E; byteArray[21] = 0x00;  // '>'
            byteArray[22] = 0x20; byteArray[23] = 0x00;  // ' '

            if (system == "atari5200")
            {
                byteArray[24] = 0x35; byteArray[25] = 0x00;  // '5'
                byteArray[26] = 0x32; byteArray[27] = 0x00;  // '2'
                byteArray[28] = 0x30; byteArray[29] = 0x00;  // '0'
                byteArray[30] = 0x30; byteArray[31] = 0x00;  // '0'
            }
            else
            {
                byteArray[24] = 0x43; byteArray[25] = 0x00;  // 'C'
                byteArray[26] = 0x58; byteArray[27] = 0x00;  // 'X'
                byteArray[28] = 0x34; byteArray[29] = 0x00;  // '4'
                byteArray[30] = 0x30; byteArray[31] = 0x00;  // '0'
            }

            byteArray[32] = 0x20; byteArray[33] = 0x00;  // ' '
            byteArray[34] = 0x28; byteArray[35] = 0x00;  // '('
            byteArray[36] = 0x70; byteArray[37] = 0x00;  // 'p'
            byteArray[38] = 0x6F; byteArray[39] = 0x00;  // 'o'
            byteArray[40] = 0x72; byteArray[41] = 0x00;  // 'r'
            byteArray[42] = 0x74; byteArray[43] = 0x00;  // 't'
            byteArray[44] = 0x20; byteArray[45] = 0x00;  // ' '

            if (joyPort == 1)
            {
                byteArray[46] = 0x31; byteArray[47] = 0x00;  // '1'
            }
            else if (joyPort == 2)
            {
                byteArray[46] = 0x32; byteArray[47] = 0x00;  // '2'
            }

            byteArray[48] = 0x29; byteArray[49] = 0x00;  // ')'

            return byteArray;
        }

        internal class AltirraKBProfile
        {
            public string System { get; set; }
            public string Name { get; set; }
            public int Port { get; set; }
            public string ByteArray { get; set; }
            public string Type { get; set; }
        }

        static readonly AltirraKBProfile[] altirraDefaultKeyboardProfiles = new AltirraKBProfile[]
        {
            new AltirraKBProfile { System = "atari800,xegs", Name = "Arrow Keys -> Joystick (port 1)", Port = 1, Type = "Joystick(CX40)", ByteArray = "02 00 00 00 1F 00 00 00 01 00 00 00 05 00 00 00 FF FF FF FF 41 00 72 00 72 00 6F 00 77 00 20 00 4B 00 65 00 79 00 73 00 20 00 2D 00 3E 00 20 00 4A 00 6F 00 79 00 73 00 74 00 69 00 63 00 6B 00 20 00 28 00 70 00 6F 00 72 00 74 00 20 00 31 00 29 00 00 00 01 00 00 00 00 00 00 00 25 00 00 00 00 00 00 00 02 01 00 00 27 00 00 00 00 00 00 00 03 01 00 00 26 00 00 00 00 00 00 00 00 01 00 00 28 00 00 00 00 00 00 00 01 01 00 00 A2 00 00 00 00 00 00 00 00 00 00 00" },
            new AltirraKBProfile { System = "atari800,xegs", Name = "Arrow Keys -> Joystick (port 2)", Port = 2, Type = "Joystick(CX40)", ByteArray = "02 00 00 00 1F 00 00 00 01 00 00 00 05 00 00 00 FF FF FF FF 41 00 72 00 72 00 6F 00 77 00 20 00 4B 00 65 00 79 00 73 00 20 00 2D 00 3E 00 20 00 4A 00 6F 00 79 00 73 00 74 00 69 00 63 00 6B 00 20 00 28 00 70 00 6F 00 72 00 74 00 20 00 31 00 29 00 00 00 01 00 00 00 01 00 00 00 25 00 00 00 00 00 00 00 02 01 00 00 27 00 00 00 00 00 00 00 03 01 00 00 26 00 00 00 00 00 00 00 00 01 00 00 28 00 00 00 00 00 00 00 01 01 00 00 A2 00 00 00 00 00 00 00 00 00 00 00" },
            new AltirraKBProfile { System = "atari5200", Name = "Arrow Keys -> Joystick5200 (port 1)", Port = 1, Type = "5200 Controller", ByteArray = "02 00 00 00 23 00 00 00 01 00 00 00 17 00 00 00 FF FF FF FF 41 00 72 00 72 00 6F 00 77 00 20 00 4B 00 65 00 79 00 73 00 20 00 2D 00 3E 00 20 00 4A 00 6F 00 79 00 73 00 74 00 69 00 63 00 6B 00 35 00 32 00 30 00 30 00 20 00 28 00 70 00 6F 00 72 00 74 00 20 00 31 00 29 00 00 00 05 00 00 00 00 00 00 00 A2 00 00 00 00 00 00 00 00 00 00 00 A0 00 00 00 00 00 00 00 01 00 00 00 25 00 00 00 00 00 00 00 02 01 00 00 27 00 00 00 00 00 00 00 03 01 00 00 26 00 00 00 00 00 00 00 00 01 00 00 28 00 00 00 00 00 00 00 01 01 00 00 00 00 00 00 00 00 00 00 00 08 00 00 00 00 00 00 00 00 00 00 01 08 00 00 60 00 00 00 00 00 00 00 00 04 00 00 61 00 00 00 00 00 00 00 01 04 00 00 62 00 00 00 00 00 00 00 02 04 00 00 63 00 00 00 00 00 00 00 03 04 00 00 64 00 00 00 00 00 00 00 04 04 00 00 65 00 00 00 00 00 00 00 05 04 00 00 66 00 00 00 00 00 00 00 06 04 00 00 67 00 00 00 00 00 00 00 07 04 00 00 68 00 00 00 00 00 00 00 08 04 00 00 69 00 00 00 00 00 00 00 09 04 00 00 6A 00 00 00 00 00 00 00 0A 04 00 00 6F 00 00 00 00 00 00 00 0B 04 00 00 0D 00 00 00 00 00 00 00 0C 04 00 00 74 00 00 00 00 00 00 00 0E 04 00 00 50 00 00 00 00 00 00 00 0D 04 00 00" },
            new AltirraKBProfile { System = "atari5200", Name = "Arrow Keys -> Joystick5200 (port 2)", Port = 2, Type = "5200 Controller", ByteArray = "02 00 00 00 23 00 00 00 01 00 00 00 17 00 00 00 FF FF FF FF 41 00 72 00 72 00 6F 00 77 00 20 00 4B 00 65 00 79 00 73 00 20 00 2D 00 3E 00 20 00 4A 00 6F 00 79 00 73 00 74 00 69 00 63 00 6B 00 35 00 32 00 30 00 30 00 20 00 28 00 70 00 6F 00 72 00 74 00 20 00 32 00 29 00 00 00 05 00 00 00 01 00 00 00 A2 00 00 00 00 00 00 00 00 00 00 00 A0 00 00 00 00 00 00 00 01 00 00 00 25 00 00 00 00 00 00 00 02 01 00 00 27 00 00 00 00 00 00 00 03 01 00 00 26 00 00 00 00 00 00 00 00 01 00 00 28 00 00 00 00 00 00 00 01 01 00 00 00 00 00 00 00 00 00 00 00 08 00 00 00 00 00 00 00 00 00 00 01 08 00 00 60 00 00 00 00 00 00 00 00 04 00 00 61 00 00 00 00 00 00 00 01 04 00 00 62 00 00 00 00 00 00 00 02 04 00 00 63 00 00 00 00 00 00 00 03 04 00 00 64 00 00 00 00 00 00 00 04 04 00 00 65 00 00 00 00 00 00 00 05 04 00 00 66 00 00 00 00 00 00 00 06 04 00 00 67 00 00 00 00 00 00 00 07 04 00 00 68 00 00 00 00 00 00 00 08 04 00 00 69 00 00 00 00 00 00 00 09 04 00 00 6A 00 00 00 00 00 00 00 0A 04 00 00 6F 00 00 00 00 00 00 00 0B 04 00 00 0D 00 00 00 00 00 00 00 0C 04 00 00 74 00 00 00 00 00 00 00 0E 04 00 00 50 00 00 00 00 00 00 00 0D 04 00 00" }
        };

        private static readonly Dictionary<int, byte[]> px_5200 = new Dictionary<int, byte[]>
        {
            { 1, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } },
            { 2, new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 } },
            { 3, new byte[] { 0x00, 0x00, 0x02, 0x01, 0x00, 0x00 } },
            { 4, new byte[] { 0x00, 0x00, 0x03, 0x01, 0x00, 0x00 } },
            { 5, new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 } },
            { 6, new byte[] { 0x00, 0x00, 0x01, 0x01, 0x00, 0x00 } },
            { 7, new byte[] { 0x00, 0x00, 0x00, 0x08, 0x00, 0x00 } },
            { 8, new byte[] { 0x00, 0x00, 0x01, 0x08, 0x00, 0x00 } },
            { 9, new byte[] { 0x00, 0x00, 0x00, 0x04, 0x00, 0x00 } },
            { 10, new byte[] { 0x00, 0x00, 0x01, 0x04, 0x00, 0x00 } },
            { 11, new byte[] { 0x00, 0x00, 0x02, 0x04, 0x00, 0x00 } },
            { 12, new byte[] { 0x00, 0x00, 0x03, 0x04, 0x00, 0x00 } },
            { 13, new byte[] { 0x00, 0x00, 0x04, 0x04, 0x00, 0x00 } },
            { 14, new byte[] { 0x00, 0x00, 0x05, 0x04, 0x00, 0x00 } },
            { 15, new byte[] { 0x00, 0x00, 0x06, 0x04, 0x00, 0x00 } },
            { 16, new byte[] { 0x00, 0x00, 0x07, 0x04, 0x00, 0x00 } },
            { 17, new byte[] { 0x00, 0x00, 0x08, 0x04, 0x00, 0x00 } },
            { 18, new byte[] { 0x00, 0x00, 0x09, 0x04, 0x00, 0x00 } },
            { 19, new byte[] { 0x00, 0x00, 0x0A, 0x04, 0x00, 0x00 } },
            { 20, new byte[] { 0x00, 0x00, 0x0B, 0x04, 0x00, 0x00 } },
            { 21, new byte[] { 0x00, 0x00, 0x0C, 0x04, 0x00, 0x00 } },
            { 22, new byte[] { 0x00, 0x00, 0x0E, 0x04, 0x00, 0x00 } },
            { 23, new byte[] { 0x00, 0x00, 0x0D, 0x04, 0x00, 0x00 } },
        };

        private static readonly Dictionary<int, byte[]> p1_800 = new Dictionary<int, byte[]>
        {
            { 1, new byte[] { 0x00, 0x00, 0x02, 0x01, 0x00, 0x00 } },
            { 2, new byte[] { 0x00, 0x00, 0x03, 0x01, 0x00, 0x00 } },
            { 3, new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 } },
            { 4, new byte[] { 0x00, 0x00, 0x01, 0x01, 0x00, 0x00 } },
            { 5, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } },
            { 6, new byte[] { 0x00, 0x00, 0x01, 0x01, 0x00, 0x00 } },
            { 7, new byte[] { 0x00, 0x00, 0x00, 0x02, 0x00, 0x00 } },
            { 8, new byte[] { 0x00, 0x00, 0x01, 0x02, 0x00, 0x00 } },
        };

        private static readonly Dictionary<int, byte[]> p2_800 = new Dictionary<int, byte[]>
        {
            { 1, new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 } },
            { 2, new byte[] { 0x00, 0x00, 0x01, 0x00, 0x00, 0x00 } },
            { 3, new byte[] { 0x00, 0x00, 0x02, 0x01, 0x00, 0x00 } },
            { 4, new byte[] { 0x00, 0x00, 0x03, 0x01, 0x00, 0x00 } },
            { 5, new byte[] { 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 } },
        };
    }
}
