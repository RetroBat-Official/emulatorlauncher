using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class Model2Generator : Generator
    {
        private void ConfigureControllers(byte[] bytes, IniFile ini, string parentRom, int hexLength)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            if (Program.SystemConfig.isOptSet("m2_joystick_autoconfig") && Program.SystemConfig["m2_joystick_autoconfig"] == "template")
                return;

            if (Program.Controllers.Count > 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                var c2 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 2);
                if (c1.IsKeyboard)
                    return;
                else
                    WriteJoystickMapping(bytes, parentRom, hexLength, c1, c2);
            }
            else if (Program.Controllers.Count == 1)
            {
                var c1 = Program.Controllers.FirstOrDefault(c => c.PlayerIndex == 1);

                if (c1.IsKeyboard)
                    return;
                else
                    WriteJoystickMapping(bytes, parentRom, hexLength, c1);
            }
            else if (Program.Controllers.Count == 0)
                return;
        }

        private void WriteJoystickMapping(byte[] bytes, string parentRom, int hexLength, Controller c1, Controller c2 = null)
        {
            if (c1 == null || c1.Config == null)
                return;

            //initialize controller index, supermodel uses directinput controller index (+1)
            //only index of player 1 is initialized as there might be only 1 controller at that point
            int j2index;
            int j1index = c1.DirectInput != null ? c1.DirectInput.DeviceIndex + 1 : c1.DeviceIndex + 1;

            //If a secod controller is connected, get controller index of player 2, if there is no 2nd controller, just increment the index
            if (c2 != null && c2.Config != null && !c2.IsKeyboard)
                j2index = c2.DirectInput != null ? c2.DirectInput.DeviceIndex + 1 : c2.DeviceIndex + 1;
            else
                j2index = 0;

            string tech1 = "xinput";
            string tech2 = "xinput";

            if (_dinput)
            {
                if (c1.VendorID == USB_VENDOR.SONY)
                    tech1 = "dualshock";
                else if (c1.VendorID == USB_VENDOR.MICROSOFT)
                    tech1 = "microsoft";
                else if (c1.VendorID == USB_VENDOR.NINTENDO)
                    tech1 = "nintendo";
                else
                    tech1 = "dinput";

                if (c2 != null && c2.Config != null && _dinput)
                {
                    if (c2.VendorID == USB_VENDOR.SONY)
                        tech2 = "dualshock";
                    else if (c2.VendorID == USB_VENDOR.MICROSOFT)
                        tech2 = "microsoft";
                    else if (c2.VendorID == USB_VENDOR.NINTENDO)
                        tech2 = "nintendo";
                    else
                        tech2 = "dinput";
                }
                else
                    tech2 = tech1;
            }

            // Write end of binary file for service buttons, test buttons and keyboard buttons for stats display
            WriteServiceBytes(bytes, j1index, c1, tech1, serviceByte[parentRom]);
            WriteStatsBytes(bytes, serviceByte[parentRom] + 8);

            // Per game category mapping
            #region  shooters
            if (shooters.Contains(parentRom))
            {
                // Player index bytes
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = Convert.ToByte(j1index);
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j2index);

                bytes[0] = _dinput ? GetInputCode(InputKey.up, c1, tech1) : (byte)0x02;
                bytes[4] = _dinput ? GetInputCode(InputKey.down, c1, tech1) : (byte)0x03;
                bytes[8] = _dinput ? GetInputCode(InputKey.left, c1, tech1) : (byte)0x00;
                bytes[12] = _dinput ? GetInputCode(InputKey.right, c1, tech1) : (byte)0x01;
                bytes[16] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                bytes[20] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                bytes[24] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                bytes[28] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                bytes[32] = (byte)0x02;
                bytes[36] = (byte)0x04;
                if (c2 != null && !c2.IsKeyboard)
                {
                    bytes[40] = _dinput ? GetInputCode(InputKey.up, c2, tech2) : (byte)0x02;
                    bytes[44] = _dinput ? GetInputCode(InputKey.down, c2, tech2) : (byte)0x03;
                    bytes[48] = _dinput ? GetInputCode(InputKey.left, c2, tech2) : (byte)0x00;
                    bytes[52] = _dinput ? GetInputCode(InputKey.right, c2, tech2) : (byte)0x01;
                    bytes[56] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                    bytes[60] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                    bytes[64] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                    bytes[68] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                }
                else
                {
                    bytes[40] = (byte)0x00;
                    bytes[44] = (byte)0x00;
                    bytes[48] = (byte)0x00;
                    bytes[52] = (byte)0x00;
                    bytes[56] = (byte)0x00;
                    bytes[60] = (byte)0x00;
                    bytes[64] = (byte)0x00;
                    bytes[68] = (byte)0x00;
                }

                bytes[72] = (byte)0x03;
                    bytes[76] = (byte)0x05;

                bytes[80] = (byte)0x3B;
                bytes[81] = (byte)0x00;
                bytes[84] = (byte)0x3C;
                bytes[85] = (byte)0x00;
            }
            #endregion
            #region driving
            else if (drivingshiftupdown.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = bytes[45] = bytes[49] = Convert.ToByte(j1index);

                bytes[0] = _dinput ? GetInputCode(InputKey.up, c1, tech1) : (byte)0x02;
                bytes[4] = _dinput ? GetInputCode(InputKey.down, c1, tech1) : (byte)0x03;
                bytes[8] = _dinput ? GetInputCode(InputKey.left, c1, tech1) : (byte)0x00;
                bytes[12] = _dinput ? GetInputCode(InputKey.right, c1, tech1) : (byte)0x01;

                bytes[16] = _dinput ? (byte)0x00 : (byte)0x02;
                bytes[19] = 0xFF;

                bytes[20] = _dinput ? GetInputCode(InputKey.r2, c1, tech1, true) : (byte)0x07;
                if (parentRom == "indy500" || parentRom == "stcc" || parentRom == "motoraid" || parentRom.StartsWith("manxtt"))
                {
                    if (_dinput && tech1 != "dualshock")
                        bytes[21] = Convert.ToByte(j1index + 16);
                }
                else if (!_dinput || tech1 == "dualshock")
                    bytes[21] = Convert.ToByte(j1index + 16);
                bytes[23] = 0xFF;

                bytes[24] = _dinput ? GetInputCode(InputKey.l2, c1, tech1, true) : (byte)0x06;
                if (parentRom != "indy500" && parentRom != "stcc" && parentRom != "motoraid" && !parentRom.StartsWith("manxtt"))
                {
                    bytes[25] = Convert.ToByte(j1index + 16);
                }
                bytes[27] = 0xFF;

                if (parentRom == "motoraid")
                {
                    bytes[28] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[32] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[36] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[40] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;
                    bytes[44] = (byte)0x00;
                    bytes[45] = (byte)0x00;

                    bytes[68] = (byte)0x01;
                    bytes[69] = (byte)0x01;
                    bytes[70] = (byte)0x01;
                }
                else if (parentRom != "manxtt" && parentRom != "manxttc")
                {
                    bytes[28] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[32] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[36] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[40] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                    bytes[44] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[48] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                    bytes[72] = (byte)0x01;
                    bytes[73] = (byte)0x01;
                    bytes[74] = (byte)0x01;
                }
                else
                {
                    bytes[28] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[32] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[36] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[40] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;
                    bytes[44] = (byte)0x00;
                    bytes[45] = (byte)0x00;

                    bytes[68] = (byte)0x01;
                    bytes[69] = (byte)0x01;
                    bytes[70] = (byte)0x01;
                }

            }
            #endregion
            #region driving gear stick
            else if (drivingshiftlever.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = Convert.ToByte(j1index);
                
                bytes[0] = _dinput ? GetInputCode(InputKey.up, c1, tech1) : (byte)0x02;
                bytes[4] = _dinput ? GetInputCode(InputKey.down, c1, tech1) : (byte)0x03;
                bytes[8] = _dinput ? GetInputCode(InputKey.left, c1, tech1) : (byte)0x00;
                bytes[12] = _dinput ? GetInputCode(InputKey.right, c1, tech1) : (byte)0x01;

                bytes[16] = _dinput ? (byte)0x00 : (byte)0x02;  // Steering
                bytes[19] = 0xFF;

                bytes[20] = _dinput ? GetInputCode(InputKey.r2, c1, tech1, true) : (byte)0x07;  // Accelerate (R2)
                bytes[23] = 0xFF;
                bytes[24] = _dinput ? GetInputCode(InputKey.l2, c1, tech1, true) : (byte)0x06;  // Brake (L2)
                bytes[27] = 0xFF;
                bytes[28] = _dinput ? GetInputCode(InputKey.rightanalogup, c1, tech1) : (byte)0x0A;
                bytes[32] = _dinput ? GetInputCode(InputKey.rightanalogdown, c1, tech1) : (byte)0x0B;
                bytes[36] = _dinput ? GetInputCode(InputKey.rightanalogleft, c1, tech1) : (byte)0x08;
                bytes[40] = _dinput ? GetInputCode(InputKey.rightanalogright, c1, tech1) : (byte)0x09;

                bytes[44] = _dinput ? GetInputCode(InputKey.pagedown, c1, tech1) : (byte)0x60;
                bytes[48] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;

                if (parentRom == "daytona")
                {
                    bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j1index);
                    bytes[52] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[56] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[60] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[64] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[68] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                    bytes[96] = (byte)0x01;
                    bytes[97] = (byte)0x01;
                    bytes[98] = (byte)0x01;
                }

                else if (parentRom.StartsWith("srally"))
                {
                    bytes[52] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[56] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                    bytes[84] = (byte)0x01;
                    bytes[85] = (byte)0x01;
                    bytes[86] = (byte)0x01;
                }

            }
            #endregion
            #region fighters
            else if (fighters.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = Convert.ToByte(j1index);
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = Convert.ToByte(j2index);

                // Player 1
                bytes[0] = _dinput ? GetInputCode(InputKey.up, c1, tech1) : (byte)0x02;
                bytes[4] = _dinput ? GetInputCode(InputKey.down, c1, tech1) : (byte)0x03;
                bytes[8] = _dinput ? GetInputCode(InputKey.left, c1, tech1) : (byte)0x00;
                bytes[12] = _dinput ? GetInputCode(InputKey.right, c1, tech1) : (byte)0x01;

                if (parentRom == "doa")
                {
                    bytes[16] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[20] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                }
                else
                {
                    bytes[16] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[20] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                }

                if (parentRom == "fvipers")
                    bytes[24] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                else
                    bytes[24] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;

                if (parentRom == "doa")
                    bytes[28] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                else if (parentRom == "fvipers")
                    bytes[28] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                else
                    bytes[28] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;

                bytes[32] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                bytes[36] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                // Player 2
                if (c2 != null)
                {
                    bytes[40] = _dinput ? GetInputCode(InputKey.up, c2, tech2) : (byte)0x02;
                    bytes[44] = _dinput ? GetInputCode(InputKey.down, c2, tech2) : (byte)0x03;
                    bytes[48] = _dinput ? GetInputCode(InputKey.left, c2, tech2) : (byte)0x00;
                    bytes[52] = _dinput ? GetInputCode(InputKey.right, c2, tech2) : (byte)0x01;

                    if (parentRom == "doa")
                    {
                        bytes[56] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                        bytes[60] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                    }
                    else
                    {
                        bytes[56] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                        bytes[60] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                    }

                    if (parentRom == "fvipers")
                        bytes[64] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                    else
                        bytes[64] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;

                    if (parentRom == "doa")
                        bytes[68] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                    else if (parentRom == "fvipers")
                        bytes[68] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                    else
                        bytes[68] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;

                    bytes[72] = _dinput ? GetInputCode(InputKey.start, c2, tech2) : (byte)0xB0;
                    bytes[76] = _dinput ? GetInputCode(InputKey.select, c2, tech2) : (byte)0xC0;
                }
                else
                    bytes[40] = bytes[44] = bytes[48] = bytes[52] = bytes[56] = bytes[60] = bytes[64] = bytes[68] = bytes[72] = bytes[76] = 0x00;
            }
            #endregion
            #region standard
            else if (standard.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = Convert.ToByte(j1index);
                bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = Convert.ToByte(j2index);

                // Player 1
                bytes[0] = _dinput ? GetInputCode(InputKey.leftanalogup, c1, tech1) : (byte)0x06;
                bytes[4] = _dinput ? GetInputCode(InputKey.leftanalogdown, c1, tech1) : (byte)0x07;
                bytes[8] = _dinput ? GetInputCode(InputKey.leftanalogleft, c1, tech1) : (byte)0x04;
                bytes[12] = _dinput ? GetInputCode(InputKey.leftanalogright, c1, tech1) : (byte)0x05;

                if (parentRom == "vstriker")
                {
                    bytes[16] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[20] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[24] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[28] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                }
                else if (parentRom == "dynamcop")
                {
                    bytes[16] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[20] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                    bytes[24] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[28] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                }
                else if (parentRom == "pltkids")
                {
                    bytes[16] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[20] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[24] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                    bytes[28] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                }
                else if (parentRom == "zerogun")
                {
                    bytes[16] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[20] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[24] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[28] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                }

                bytes[32] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                bytes[36] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                // Player 2
                if (c2 != null)
                {
                    bytes[40] = _dinput ? GetInputCode(InputKey.leftanalogup, c2, tech2) : (byte)0x06;
                    bytes[44] = _dinput ? GetInputCode(InputKey.leftanalogdown, c2, tech2) : (byte)0x07;
                    bytes[48] = _dinput ? GetInputCode(InputKey.leftanalogleft, c2, tech2) : (byte)0x04;
                    bytes[52] = _dinput ? GetInputCode(InputKey.leftanalogright, c2, tech2) : (byte)0x05;

                    if (parentRom == "vstriker")
                    {
                        bytes[56] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                        bytes[60] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                        bytes[64] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                        bytes[68] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                    }
                    else if (parentRom == "dynamcop")
                    {
                        bytes[56] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                        bytes[60] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                        bytes[64] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                        bytes[68] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                    }
                    else if (parentRom == "pltkids")
                    {
                        bytes[56] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                        bytes[60] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                        bytes[64] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                        bytes[68] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                    }
                    else if (parentRom == "zerogun")
                    {
                        bytes[56] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                        bytes[60] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                        bytes[64] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                        bytes[68] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                    }

                    bytes[72] = _dinput ? GetInputCode(InputKey.start, c2, tech2) : (byte)0xB0;
                    bytes[76] = _dinput ? GetInputCode(InputKey.select, c2, tech2) : (byte)0xC0;
                }
                else
                    bytes[40] = bytes[44] = bytes[48] = bytes[52] = bytes[56] = bytes[60] = bytes[64] = bytes[68] = bytes[72] = bytes[76] = 0x00;
            }
            #endregion
            #region sports
            else if (sports.Contains(parentRom))
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = Convert.ToByte(j1index);
                if (parentRom != "segawski")
                    bytes[41] = Convert.ToByte(j1index);
                if (parentRom != "segawski" && parentRom != "waverunr")
                    bytes[45] = Convert.ToByte(j1index);

                if (parentRom == "waverunr")
                {
                    bytes[0] = _dinput ? GetInputCode(InputKey.rightanalogleft, c1, tech1) : (byte)0x08;
                    bytes[4] = _dinput ? GetInputCode(InputKey.rightanalogright, c1, tech1) : (byte)0x09;
                    bytes[8] = _dinput ? GetInputCode(InputKey.leftanalogleft, c1, tech1) : (byte)0x04;
                    bytes[12] = _dinput ? GetInputCode(InputKey.leftanalogright, c1, tech1) : (byte)0x05;
                    bytes[16] = _dinput ? GetInputCode(InputKey.r2, c1, tech1) : (byte)0x80;
                    bytes[20] = _dinput ? (byte)0x00 : (byte)0x02;
                    bytes[23] = 0xFF;
                    bytes[24] = _dinput ? (byte)0x03 : (byte)0x04;
                    bytes[27] = 0xFF;
                    bytes[28] = _dinput ? GetInputCode(InputKey.r2, c1, tech1, true) : (byte)0x07;
                    bytes[31] = 0xFF;
                    bytes[32] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                    bytes[36] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[40] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                    bytes[64] = (byte)0x01;
                    bytes[65] = (byte)0x01;
                    bytes[66] = (byte)0x01;
                }

                else if (parentRom == "skisuprg")
                {
                    bytes[0] = _dinput ? GetInputCode(InputKey.leftanalogleft, c1, tech1) : (byte)0x04;
                    bytes[4] = _dinput ? GetInputCode(InputKey.leftanalogright, c1, tech1) : (byte)0x05;
                    bytes[8] = _dinput ? GetInputCode(InputKey.rightanalogleft, c1, tech1) : (byte)0x08;
                    bytes[12] = _dinput ? GetInputCode(InputKey.rightanalogright, c1, tech1) : (byte)0x09;

                    bytes[16] = _dinput ? (byte)0x03 : (byte)0x04;
                    bytes[19] = 0xFF;
                    bytes[20] = _dinput ? (byte)0x00 : (byte)0x02;
                    bytes[21] = Convert.ToByte(j1index + 16);
                    bytes[23] = 0xFF;

                    bytes[24] = _dinput ? GetInputCode(InputKey.down, c1, tech1) : (byte)0x03;
                    bytes[28] = _dinput ? GetInputCode(InputKey.up, c1, tech1) : (byte)0x02;

                    bytes[32] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[36] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[40] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[44] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                    bytes[68] = (byte)0x01;
                    bytes[69] = (byte)0x01;
                }

                else if (parentRom == "topskatr" || parentRom == "segawski")
                {
                    bytes[0] = _dinput ? GetInputCode(InputKey.leftanalogup, c1, tech1) : (byte)0x06;
                    bytes[4] = _dinput ? GetInputCode(InputKey.leftanalogdown, c1, tech1) : (byte)0x07;
                    bytes[8] = _dinput ? GetInputCode(InputKey.leftanalogleft, c1, tech1) : (byte)0x04;
                    bytes[12] = _dinput ? GetInputCode(InputKey.leftanalogright, c1, tech1) : (byte)0x05;
                    bytes[16] = _dinput ? (byte)0x00 : (byte)0x02;
                    bytes[17] = Convert.ToByte(j1index + 16);
                    bytes[19] = 0xFF;

                    if (parentRom == "topskatr")
                    {
                        bytes[20] = _dinput ? (byte)0x03 : (byte)0x04;
                        bytes[23] = 0xFF;
                        bytes[24] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                        bytes[28] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                        bytes[32] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                        bytes[36] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                        bytes[40] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                        bytes[44] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                        bytes[68] = (byte)0x01;
                        bytes[69] = (byte)0x01;
                    }

                    if (parentRom == "segawski")
                    {
                        bytes[20] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                        bytes[24] = _dinput ? GetInputCode(InputKey.l2, c1, tech1) : (byte)0x70;
                        bytes[28] = _dinput ? GetInputCode(InputKey.r2, c1, tech1) : (byte)0x80;
                        bytes[32] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                        bytes[36] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                        bytes[60] = (byte)0x01;
                    }
                }
            }
            #endregion

            // Games with completely specific schemes
            // Desert Tank
            else if (parentRom == "desert")
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = Convert.ToByte(j1index);
                bytes[0] = _dinput ? GetInputCode(InputKey.rightanalogleft, c1, tech1) : (byte)0x08;
                bytes[4] = _dinput ? GetInputCode(InputKey.rightanalogright, c1, tech1) : (byte)0x09;
                bytes[8] = _dinput ? GetInputCode(InputKey.rightanalogup, c1, tech1) : (byte)0x0A;
                bytes[12] = _dinput ? GetInputCode(InputKey.rightanalogdown, c1, tech1) : (byte)0x0B;
                bytes[16] = _dinput ? GetInputCode(InputKey.leftanalogup, c1, tech1) : (byte)0x06;
                bytes[20] = _dinput ? GetInputCode(InputKey.leftanalogdown, c1, tech1) : (byte)0x07;
                bytes[24] = _dinput ? (byte)0x03 : (byte)0x04;
                bytes[27] = 0xFF;
                bytes[28] = _dinput ? (byte)0x02 : (byte)0x05;
                bytes[31] = 0xFF;
                bytes[32] = _dinput ? (byte)0x01 : (byte)0x03;
                bytes[35] = 0xFF;
                bytes[36] = _dinput ? GetInputCode(InputKey.l2, c1, tech1) : (byte)0x70;
                bytes[40] = _dinput ? GetInputCode(InputKey.pagedown, c1, tech1) : (byte)0x60;
                bytes[44] = _dinput ? GetInputCode(InputKey.r2, c1, tech1) : (byte)0x80;
                bytes[48] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                bytes[52] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                bytes[56] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                bytes[60] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                bytes[64] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;
                bytes[68] = 0x07;
                bytes[69] = 0x00;
            }

            else
            {
                bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = bytes[41] = Convert.ToByte(j1index);
                
                bytes[0] = _dinput ? GetInputCode(InputKey.leftanalogup, c1, tech1) : (byte)0x06;
                bytes[4] = _dinput ? GetInputCode(InputKey.leftanalogdown, c1, tech1) : (byte)0x07;
                bytes[8] = _dinput ? GetInputCode(InputKey.leftanalogleft, c1, tech1) : (byte)0x04;
                bytes[12] = _dinput ? GetInputCode(InputKey.leftanalogright, c1, tech1) : (byte)0x05;

                // Dynamite Baseball '97
                if (parentRom == "dynabb97")
                {
                    if (c2 != null)
                        bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = bytes[81] = bytes[85] = Convert.ToByte(j2index);
                    else
                        bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = bytes[81] = bytes[85] = 0x00;

                    bytes[16] = _dinput ? GetInputCode(InputKey.r2, c1, tech1, true) : (byte)0x07;
                    if (!_dinput)
                        bytes[17] = Convert.ToByte(j1index + 16);

                    bytes[19] = 0xFF;
                    bytes[20] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[24] = _dinput ? GetInputCode(InputKey.a, c1, tech1) : (byte)0x30;
                    bytes[28] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                    bytes[32] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                    bytes[36] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[40] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;

                    if (c2 != null)
                    {
                        bytes[44] = _dinput ? GetInputCode(InputKey.leftanalogup, c2, tech2) : (byte)0x06;
                        bytes[48] = _dinput ? GetInputCode(InputKey.leftanalogdown, c2, tech2) : (byte)0x07;
                        bytes[52] = _dinput ? GetInputCode(InputKey.leftanalogleft, c2, tech2) : (byte)0x04;
                        bytes[56] = _dinput ? GetInputCode(InputKey.leftanalogright, c2, tech2) : (byte)0x05;

                        bytes[60] = _dinput ? GetInputCode(InputKey.r2, c2, tech2, true) : (byte)0x07;
                        bytes[63] = 0xFF;
                        bytes[64] = _dinput ? GetInputCode(InputKey.y, c2, tech2) : (byte)0x10;
                        bytes[68] = _dinput ? GetInputCode(InputKey.a, c2, tech2) : (byte)0x30;
                        bytes[72] = _dinput ? GetInputCode(InputKey.b, c2, tech2) : (byte)0x40;
                        bytes[76] = _dinput ? GetInputCode(InputKey.x, c2, tech2) : (byte)0x20;
                        bytes[80] = _dinput ? GetInputCode(InputKey.start, c2, tech2) : (byte)0xB0;
                        bytes[84] = _dinput ? GetInputCode(InputKey.select, c2, tech2) : (byte)0xC0;
                    }

                    bytes[108] = (byte)0x01;
                    if (c2 != null && !c2.IsKeyboard)
                        bytes[109] = (byte)0x01;
                    else
                        bytes[109] = (byte)0x00;
                }
                // Sky Target
                else if (parentRom == "skytargt")
                {
                    bytes[16] = _dinput ? (byte)0x00 : (byte)0x02;
                    bytes[19] = 0xFF;
                    bytes[20] = _dinput ? (byte)0x01 : (byte)0x03;
                    bytes[23] = 0xFF;
                    bytes[24] = _dinput ? GetInputCode(InputKey.r2, c1, tech1) : (byte)0x80;
                    bytes[28] = _dinput ? GetInputCode(InputKey.l2, c1, tech1) : (byte)0x70;
                    bytes[32] = _dinput ? GetInputCode(InputKey.x, c1, tech1) : (byte)0x20;
                    bytes[36] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[40] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;
                    bytes[44] = 0x07;
                    bytes[45] = 0x00;

                    bytes[68] = (byte)0x01;
                }
                else if (parentRom == "von")
                {
                    bytes[45] = bytes[49] = bytes[53] = Convert.ToByte(j1index);
                    bytes[16] = _dinput ? GetInputCode(InputKey.l2, c1, tech1) : (byte)0x70;
                    bytes[20] = _dinput ? GetInputCode(InputKey.y, c1, tech1) : (byte)0x10;
                    bytes[24] = _dinput ? GetInputCode(InputKey.start, c1, tech1) : (byte)0xB0;
                    bytes[28] = _dinput ? GetInputCode(InputKey.select, c1, tech1) : (byte)0xC0;
                    bytes[32] = _dinput ? GetInputCode(InputKey.rightanalogup, c1, tech1) : (byte)0x0A;
                    bytes[36] = _dinput ? GetInputCode(InputKey.rightanalogdown, c1, tech1) : (byte)0x0B;
                    bytes[40] = _dinput ? GetInputCode(InputKey.rightanalogleft, c1, tech1) : (byte)0x08;
                    bytes[44] = _dinput ? GetInputCode(InputKey.rightanalogright, c1, tech1) : (byte)0x09;
                    bytes[48] = _dinput ? GetInputCode(InputKey.r2, c1, tech1) : (byte)0x80;
                    bytes[52] = _dinput ? GetInputCode(InputKey.b, c1, tech1) : (byte)0x40;
                }
            }
        }

        private static byte GetInputCode(InputKey key, Controller c, string tech, bool trigger = false)
        {
            Int64 pid = -1;

            bool revertAxis = false;
            key = key.GetRevertedAxis(out revertAxis);

            var dinput = c.GetDirectInputMapping(key);
            if (dinput == null)
                return 0x00;

            if (dinput.Type == "button")
            {
                pid = dinput.Id;
                switch (pid)
                {
                    case 0: return 0x10;
                    case 1: return 0x20;
                    case 2: return 0x30;
                    case 3: return 0x40;
                    case 4: return 0x50;
                    case 5: return 0x60;
                    case 6: return 0x70;
                    case 7: return 0x80;
                    case 8: return 0x90;
                    case 9: return 0xA0;
                    case 10: return 0xB0;
                    case 11: return 0xC0;
                    case 12: return 0xD0;
                    case 13: return 0xE0;
                    case 14: return 0xF0;
                }
            }

            else if (dinput.Type == "axis")
            {
                pid = dinput.Id;
                switch (pid)
                {
                    case 0:
                        if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x01;
                        else return 0x00;
                    case 1:
                        if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x03;
                        else return 0x02;
                    case 2:
                        if (tech == "dualshock")
                        {
                            if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x07;
                            else return 0x06;
                        }
                        else
                        {
                            if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x05;
                            else return 0x04;
                        }
                    case 3:
                        if (tech == "dualshock")
                        {
                            if (trigger) return 0x04;
                            else return 0x70;
                        }
                        else if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x07;
                        else return 0x06;
                    case 4:
                        if (tech == "dualshock")
                        {
                            if (trigger) return 0x05;
                            else return 0x80;
                        }
                        else if (tech == "microsoft")
                            return 0x03;
                        else if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x09;
                        else return 0x08;
                    case 5:
                        if (tech == "dualshock")
                        {
                            if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x05;
                            else return 0x04;
                        }
                        else if (tech == "microsoft")
                            return 0x03;
                        else if ((!revertAxis && dinput.Value > 0) || (revertAxis && dinput.Value < 0)) return 0x0B;
                        else return 0x0A;
                }
            }

            else if (dinput.Type == "hat")
            {
                pid = dinput.Value;
                switch (pid)
                {
                    case 1: return 0x0E;
                    case 2: return 0x0D;
                    case 4: return 0x0F;
                    case 8: return 0x0C;
                }
            }
            return 0x00;
        }

        private void WriteServiceBytes(byte[] bytes, int index, Controller c, string tech, int startByte)
        {
            if (SystemConfig.isOptSet("m2_enable_service") && SystemConfig.getOptBoolean("m2_enable_service"))
            {
                bytes[startByte] = (byte)0x3B;
                bytes[startByte + 1] = 0x00;
                bytes[startByte + 4] = (byte)0x3C;
                bytes[startByte + 5] = 0x00;
            }
            else
            {
                bytes[startByte] = _dinput ? GetInputCode(InputKey.l3, c, tech) : (byte)0x90;
                bytes[startByte + 1] = (byte)index;
                bytes[startByte + 4] = _dinput ? GetInputCode(InputKey.r3, c, tech) : (byte)0xA0;
                bytes[startByte + 5] = (byte)index;
            }
        }

        static Dictionary<string, int> serviceByte = new Dictionary<string, int>()
        { 
            { "bel", 80 },
            { "daytona", 76 },
            { "desert", 72 },
            { "doa", 80 },
            { "dynabb97", 88 },
            { "dynamcop", 80 },
            { "fvipers", 80 },
            { "gunblade", 80 },
            { "hotd", 80 },
            { "indy500", 52 },
            { "lastbrnx", 80 },
            { "manxtt", 48 },
            { "manxttc", 48 },
            { "motoraid", 48 },
            { "overrev", 52 },
            { "pltkids", 80 },
            { "rchase2", 80 },
            { "schamp", 80 },
            { "segawski", 40 },
            { "sgt24h", 52 },
            { "skisuprg", 48 },
            { "skytargt", 48 },
            { "srallyc", 64 },
            { "srallyp", 64 },
            { "stcc", 52 },
            { "topskatr", 48 },
            { "vcop", 80 },
            { "vcop2", 80 },
            { "vf2", 80 },
            { "von", 56 },
            { "vstriker", 80 },
            { "waverunr", 44 },
            { "zerogun", 80 }
        };

        private void WriteStatsBytes(byte[] bytes, int startByte)
        {
            bytes[startByte] = (byte)0x42;
            bytes[startByte + 1] = (byte)0x00;
            bytes[startByte + 4] = (byte)0x41;
            bytes[startByte + 5] = (byte)0x00;
            bytes[startByte + 8] = (byte)0x40;
            bytes[startByte + 9] = (byte)0x00;
        }

        static List<string> shooters = new List<string>() { "bel", "gunblade", "hotd", "rchase2", "vcop", "vcop2" };
        static List<string> fighters = new List<string>() { "doa", "fvipers", "lastbrnx", "schamp", "vf2" };
        static List<string> standard = new List<string>() { "dynamcop", "pltkids", "vstriker", "zerogun" };
        static List<string> drivingshiftupdown = new List<string>() { "indy500", "motoraid", "overrev", "sgt24h", "stcc", "manxtt", "manxttc" };
        static List<string> drivingshiftlever = new List<string>() { "daytona", "srallyc" };
        static List<string> sports = new List<string>() { "segawski", "skisuprg", "topskatr", "waverunr" };
    }
}
