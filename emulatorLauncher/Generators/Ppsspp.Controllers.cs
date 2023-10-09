using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;

namespace EmulatorLauncher
{
    partial class PpssppGenerator
    {
        // see. github.com/batocera-linux/batocera.linux/blob/master/package/batocera/core/batocera-configgen/configgen/configgen/generators/ppsspp/ppssppControllers.py
        private void CreateControllerConfiguration(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            string iniFile = Path.Combine(path, "memstick", "PSP", "SYSTEM", "controls.ini");

            try
            {
                using (var ini = new IniFile(iniFile, IniOptions.UseSpaces))
                {
                    var controller = Program.Controllers.FirstOrDefault();
                    GenerateControllerConfig(ini, controller);

                    ini.WriteValue("ControlMapping", "Rewind", "1-131,20-109:20-21,10-109:10-21");
                    ini.WriteValue("ControlMapping", "Fast-forward", "1-132,20-109:20-22,10-109:10-22");
                    ini.WriteValue("ControlMapping", "Load State", "1-134,20-109:20-100,10-109:10-100");
                    ini.WriteValue("ControlMapping", "Save State", "1-133,20-109:20-99,10-109:10-99");

                    if (_saveStatesWatcher != null && _saveStatesWatcher.IncrementalMode)
                    {
                        ini.WriteValue("ControlMapping", "Previous Slot", "");
                        ini.WriteValue("ControlMapping", "Next Slot", "");
                    }
                    else
                    {
                        ini.WriteValue("ControlMapping", "Previous Slot", "1-135,20-109:20-19,10-109:10-19");
                        ini.WriteValue("ControlMapping", "Next Slot", "1-136,20-109:20-20,10-109:10-20");
                    }
                }
            }
            catch { }
        }

        private void GenerateControllerConfig(IniFile ini, Controller controller)
        {
            if (controller == null)
                return;

            var DEVICE_ID = 10 + controller.DeviceIndex;

            if (controller.IsKeyboard)
            {
                // TODO
                DEVICE_ID = 1;
                return;
            }
            else if (controller.IsXInputDevice)
                DEVICE_ID = 20 + controller.DeviceIndex;
            
            foreach (Input input in controller.Config.Input)
            {
                Dictionary<string, string> map;
                if (!ppssppMapping.TryGetValue(input.Name, out map))
                    continue;

                string name;
                if (!map.TryGetValue(input.Type, out name))
                    continue;

                switch (input.Type)
                {         
                    case "key":
                        // TODO
                        break;

                    case "button":
                        if (SDLNameToNKCode.ContainsKey(input.Name))
                            SetOption(ini, name, DEVICE_ID, (int)SDLNameToNKCode[input.Name]);                       
                        break;
                    
                    case "axis":
                        if (SDLJoyAxisMap.ContainsKey(input.Id))
                        {
                            var pspcode = AxisToCode(SDLJoyAxisMap[input.Id], input.Value);
                            SetOption(ini, name, DEVICE_ID, pspcode);

                            string revertAxisName;

                            if (input.Name == InputKey.joystick1up)
                                revertAxisName = ppssppMapping[InputKey.joystick1down][input.Type];
                            else if (input.Name == InputKey.joystick1left)
                                revertAxisName = ppssppMapping[InputKey.joystick1right][input.Type];
                            else
                                break;

                            pspcode = AxisToCode(SDLJoyAxisMap[input.Id], -input.Value);
                            SetOption(ini, revertAxisName, DEVICE_ID, pspcode);
                        }
                        break;
                    case "hat":         
                        if (SDLHatMap.ContainsKey(input.Name))
                        {
                            var pspcode = (int) SDLHatMap[input.Name];
                            SetOption(ini, name, DEVICE_ID, pspcode);
                        }
                        break;
                }
            }
        }

        private int AxisToCode(NKCODE axisId, long direction)
        {
            int AXIS_BIND_NKCODE_START = 4000;

            if (direction < 0)
                direction = 1;
            else
                direction = 0;

            return (int) (AXIS_BIND_NKCODE_START + ((int) axisId) * 2 + (int) direction);
        }

        private void SetOption(IniFile ini, string name, int dev, int code)
        {
            var val = string.Format("{0}-{1}", dev, code);

            var list = new List<string>();

            var current = ini.GetValue("ControlMapping", name) ?? "";
            foreach (var item in current.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var curDev = item.Split(new char[] { '-' }).FirstOrDefault().ToInteger();

                if (curDev - dev > 0 && curDev - dev < 9)
                    list.Add(val);
                else
                    list.Add(item);
            }

            var entry = string.Join(",", list.ToArray());
           // ini.WriteValue("ControlMapping", name, entry);
        }
        
        static Dictionary<InputKey, Dictionary<string, string>> ppssppMapping = new Dictionary<InputKey, Dictionary<string, string>>
        {
            { InputKey.a, new Dictionary<string, string> { { "button", "Circle" }, { "key", "Circle" } } },
            { InputKey.b, new Dictionary<string, string> { { "button", "Cross" }, { "key", "Cross" } } },
            { InputKey.x, new Dictionary<string, string> { { "button", "Triangle" }, { "key", "Triangle" } } },
            { InputKey.y, new Dictionary<string, string> { { "button", "Square" }, { "key", "Square" } } },
            { InputKey.start, new Dictionary<string, string> { { "button", "Start" }, { "key", "Start" } } },
            { InputKey.select, new Dictionary<string, string> { { "button", "Select" }, { "key", "Select" } } },
            { InputKey.hotkey, new Dictionary<string, string> { { "button", "Pause" }, { "key", "Pause" } } },
            { InputKey.pageup, new Dictionary<string, string> { { "button", "L" }, { "key", "L" } } },
            { InputKey.pagedown, new Dictionary<string, string> { { "button", "R" }, { "key", "R" } } },
            { InputKey.joystick1left, new Dictionary<string, string> { { "axis", "An.Left" } } },
            { InputKey.joystick1up, new Dictionary<string, string> { { "axis", "An.Up" } } },
            { InputKey.up, new Dictionary<string, string> { { "hat", "Up" }, { "axis", "Up" }, { "button", "Up" } } },
            { InputKey.down, new Dictionary<string, string> { { "hat", "Down" }, { "axis", "Down" }, { "button", "Down" } } },
            { InputKey.left, new Dictionary<string, string> { { "hat", "Left" }, { "axis", "Left" }, { "button", "Left" } } },
            { InputKey.right, new Dictionary<string, string> { { "hat", "Right" }, { "axis", "Right" }, { "button", "Right" } } },
            { InputKey.joystick1right, new Dictionary<string, string> { { "axis", "An.Right" } } },
            { InputKey.joystick1down, new Dictionary<string, string> { { "axis", "An.Down" } } }
        };
        
        enum NKCODE : int
        {
            BACK = 4,
            DPAD_UP = 19,
            DPAD_DOWN = 20,
            DPAD_LEFT = 21,
            DPAD_RIGHT = 22,
            AXIS_X = 0,
            AXIS_Y = 1,
            AXIS_HAT_X = 15,
            AXIS_HAT_Y = 16,
            AXIS_Z = 11,
            AXIS_RZ = 14,
            AXIS_LTRIGGER = 17,
            AXIS_RTRIGGER = 18,
            BUTTON_1 = 188,
            BUTTON_2 = 189,
            BUTTON_3 = 190,
            BUTTON_4 = 191,
            BUTTON_5 = 192,
            BUTTON_6 = 193,
            BUTTON_7 = 194,
            BUTTON_8 = 195,
            BUTTON_9 = 196,
            BUTTON_10 = 197,
            BUTTON_11 = 198,
            BUTTON_12 = 199,
            BUTTON_13 = 200,
            BUTTON_14 = 201,
            BUTTON_15 = 202,
            BUTTON_16 = 203
        }

        // SDL2 input ids conversion table to NKCodes
        // See https://hg.libsdl.org/SDL/file/e12c38730512/include/SDL_gamecontroller.h#l262
        static Dictionary<InputKey, NKCODE> SDLNameToNKCode = new Dictionary<InputKey, NKCODE>
        {
            { InputKey.b,  NKCODE.BUTTON_2 }, // A
            { InputKey.a,  NKCODE.BUTTON_3 }, // B
            { InputKey.y,  NKCODE.BUTTON_4 }, // X
            { InputKey.x,  NKCODE.BUTTON_1 }, // Y
            { InputKey.select,  NKCODE.BUTTON_9 }, // SELECT/BACK
            { InputKey.hotkey,  NKCODE.BACK }, // GUIDE
            { InputKey.start,  NKCODE.BUTTON_10 }, // START
            { InputKey.pageup,  NKCODE.BUTTON_6 }, // L
            { InputKey.pagedown,  NKCODE.BUTTON_5 }, // R
            { InputKey.up,  NKCODE.DPAD_UP }, 
            { InputKey.down,  NKCODE.DPAD_DOWN }, 
            { InputKey.left,  NKCODE.DPAD_LEFT }, 
            { InputKey.right,  NKCODE.DPAD_RIGHT }
        };

        static Dictionary<InputKey, NKCODE> SDLHatMap = new Dictionary<InputKey, NKCODE>
        {
            { InputKey.up, NKCODE.DPAD_UP }, 
            { InputKey.down, NKCODE.DPAD_DOWN }, 
            { InputKey.left, NKCODE.DPAD_LEFT }, 
            { InputKey.right, NKCODE.DPAD_RIGHT }
        };

        static Dictionary<long, NKCODE> SDLJoyAxisMap = new Dictionary<long, NKCODE>
        {
            { 0, NKCODE.AXIS_X }, 
            { 1, NKCODE.AXIS_Y }, 
            { 2, NKCODE.AXIS_Z }, 
            { 3, NKCODE.AXIS_RZ }, 
            { 4, NKCODE.AXIS_LTRIGGER }, 
            { 5, NKCODE.AXIS_RTRIGGER }
        };
    }

    


}
