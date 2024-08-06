using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Common.Joysticks
{
    public class N64Controller
    {
        static readonly N64Controller[] N64Controllers;

        static N64Controller()
        {
            N64Controllers = new N64Controller[]
            {
                new N64Controller("mupen64", "Nintendo Switch Online N64 Controller", "030000007e050000192000000000680c", mupen64_nso, mupen64_nso_hk),
                new N64Controller("mupen64", "Raphnet N64 Adapter", "030000009b2800006300000000000000", mupen64_raphnet, mupen64_raphnet_hk),
                new N64Controller("mupen64", "Mayflash N64 Adapter", "03000000d620000010a7000000000000", mupen64_mayflash, mupen64_mayflash_hk),
                new N64Controller("simple64", "Nintendo Switch Online N64 Controller", "030000007e050000192000000000680c", simple64_nso),
                new N64Controller("simple64", "Raphnet N64 Adapter", "030000009b2800006300000000000000", simple64_raphnet),
                new N64Controller("simple64", "Mayflash N64 Adapter", "03000000d620000010a7000000000000", simple64_mayflash),
                new N64Controller("ares", "Nintendo Switch Online N64 Controller", "030000007e050000192000000000680c", ares_nso),
                new N64Controller("ares", "Raphnet N64 Adapter", "030000009b2800006300000000000000", ares_raphnet),
                new N64Controller("ares", "Mayflash N64 Adapter", "03000000d620000010a7000000000000", ares_mayflash),
                new N64Controller("bizhawk", "Nintendo Switch Online N64 Controller", "030000007e050000192000000000680c", bizhawk_nso, null, bizhawk_nso_info),
                new N64Controller("bizhawk", "Raphnet N64 Adapter", "030000009b2800006300000000000000", bizhawk_raphnet, null, bizhawk_raphnet_info),
                new N64Controller("bizhawk", "Mayflash N64 Adapter", "03000000d620000010a7000000000000", bizhawk_mayflash, null, bizhawk_mayflash_info),
                new N64Controller("libretro", "Nintendo Switch Online N64 Controller", "030000007e050000192000000000680c", libretro_nso, libretro_nso_hk, libretro_nso_info),
                new N64Controller("libretro", "Raphnet N64 Adapter", "030000009b2800006300000000000000", libretro_raphnet, libretro_raphnet_hk),
                new N64Controller("libretro", "Mayflash N64 Adapter", "03000000d620000010a7000000000000", libretro_mayflash, libretro_mayflash_hk),
            };
        }

        public string Emulator { get; private set; }
        public string Name { get; private set; }
        public string Guid { get; private set; }
        public Dictionary<string,string> Mapping { get; private set; }
        public Dictionary<string, string> HotKeyMapping { get; private set; }
        public Dictionary<string, string> ControllerInfo { get; private set; }

        #region Private methods
        private N64Controller(string emulator, string name, string guid, Dictionary<string, string> mapping, Dictionary<string, string> hotkeymapping = null, Dictionary<string, string> controllerInfo = null)
        {
            Emulator = emulator;
            Name = name;
            Guid = guid;
            Mapping = mapping;
            HotKeyMapping = hotkeymapping;
            ControllerInfo = controllerInfo;
        }
        #endregion

        #region public methods
        public static N64Controller GetN64Controller(string emulator, string guid)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid))
                return null;

            return N64Controllers.FirstOrDefault(c =>
                emulator.Equals(c.Emulator, StringComparison.InvariantCultureIgnoreCase) &&
                guid.Equals(c.Guid, StringComparison.InvariantCultureIgnoreCase));
        }

        public static N64Controller GetN64Controller(string emulator, string guid, List<N64Controller> controllers)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid) || controllers == null)
                return null;

            return controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(c.Guid, guid, StringComparison.InvariantCultureIgnoreCase));
        }

        public static List<N64Controller> LoadControllersFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException($"The JSON file '{jsonFilePath}' was not found.");

            var jsonString = File.ReadAllText(jsonFilePath);
            var controllerModels = JsonSerializer.DeserializeFile<List<N64ControllerJsonModel>>(jsonString);

            if (controllerModels == null)
                throw new Exception("Deserialization returned null. Ensure that the JSON mapping file is correctly formatted.");

            return controllerModels.Select(model => new N64Controller(
                model.Emulator,
                model.Name,
                model.Guid,
                model.Mapping,
                model.HotKeyMapping,
                model.ControllerInfo)).ToList();
        }
        #endregion

        #region Dictionaries mappings
        static readonly Dictionary<string, string> mupen64_nso = new Dictionary<string, string>()
        {
            { "A_InputType", "0" },
            { "A_Name", "a" },
            { "A_Data", "0" },
            { "A_ExtraData", "0" },
            { "B_InputType", "0" },
            { "B_Name", "b" },
            { "B_Data", "1" },
            { "B_ExtraData", "0" },
            { "Start_InputType", "0" },
            { "Start_Name", "start" },
            { "Start_Data", "6" },
            { "Start_ExtraData", "0" },
            { "DpadUp_InputType", "0" },
            { "DpadUp_Name", "dpup" },
            { "DpadUp_Data", "11" },
            { "DpadUp_ExtraData", "0" },
            { "DpadDown_InputType", "0" },
            { "DpadDown_Name", "dpdown" },
            { "DpadDown_Data", "12" },
            { "DpadDown_ExtraData", "0" },
            { "DpadLeft_InputType", "0" },
            { "DpadLeft_Name", "dpleft" },
            { "DpadLeft_Data", "13" },
            { "DpadLeft_ExtraData", "0" },
            { "DpadRight_InputType", "0" },
            { "DpadRight_Name", "dpright" },
            { "DpadRight_Data", "14" },
            { "DpadRight_ExtraData", "0" },
            { "CButtonUp_InputType", "0" },
            { "CButtonUp_Name", "y" },
            { "CButtonUp_Data", "3" },
            { "CButtonUp_ExtraData", "0" },
            { "CButtonDown_InputType", "1" },
            { "CButtonDown_Name", "righttrigger+" },
            { "CButtonDown_Data", "5" },
            { "CButtonDown_ExtraData", "1" },
            { "CButtonLeft_InputType", "0" },
            { "CButtonLeft_Name", "x" },
            { "CButtonLeft_Data", "2" },
            { "CButtonLeft_ExtraData", "0" },
            { "CButtonRight_InputType", "0" },
            { "CButtonRight_Name", "back" },
            { "CButtonRight_Data", "4" },
            { "CButtonRight_ExtraData", "0" },
            { "LeftTrigger_InputType", "0" },
            { "LeftTrigger_Name", "leftshoulder" },
            { "LeftTrigger_Data", "9" },
            { "LeftTrigger_ExtraData", "0" },
            { "RightTrigger_InputType", "0" },
            { "RightTrigger_Name", "rightshoulder" },
            { "RightTrigger_Data", "10" },
            { "RightTrigger_ExtraData", "0" },
            { "ZTrigger_InputType", "1" },
            { "ZTrigger_Name", "lefttrigger+" },
            { "ZTrigger_Data", "4" },
            { "ZTrigger_ExtraData", "1" },
            { "AnalogStickUp_InputType", "1" },
            { "AnalogStickUp_Name", "lefty-" },
            { "AnalogStickUp_Data", "1" },
            { "AnalogStickUp_ExtraData", "0" },
            { "AnalogStickDown_InputType", "1" },
            { "AnalogStickDown_Name", "lefty+" },
            { "AnalogStickDown_Data", "1" },
            { "AnalogStickDown_ExtraData", "1" },
            { "AnalogStickLeft_InputType", "1" },
            { "AnalogStickLeft_Name", "leftx-" },
            { "AnalogStickLeft_Data", "0" },
            { "AnalogStickLeft_ExtraData", "0" },
            { "AnalogStickRight_InputType", "1" },
            { "AnalogStickRight_Name", "leftx+" },
            { "AnalogStickRight_Data", "0" },
            { "AnalogStickRight_ExtraData", "1" },
        };
        static readonly Dictionary<string, string> mupen64_raphnet = new Dictionary<string, string>()
        {
            { "DeviceName", "Raphnet N64 Adapter" },
            { "A_InputType", "0" },
            { "A_Name", "a" },
            { "A_Data", "0" },
            { "A_ExtraData", "0" },
            { "B_InputType", "0" },
            { "B_Name", "b" },
            { "B_Data", "1" },
            { "B_ExtraData", "0" },
            { "Start_InputType", "0" },
            { "Start_Name", "start" },
            { "Start_Data", "6" },
            { "Start_ExtraData", "0" },
            { "DpadUp_InputType", "0" },
            { "DpadUp_Name", "dpup" },
            { "DpadUp_Data", "11" },
            { "DpadUp_ExtraData", "0" },
            { "DpadDown_InputType", "0" },
            { "DpadDown_Name", "dpdown" },
            { "DpadDown_Data", "12" },
            { "DpadDown_ExtraData", "0" },
            { "DpadLeft_InputType", "0" },
            { "DpadLeft_Name", "dpleft" },
            { "DpadLeft_Data", "13" },
            { "DpadLeft_ExtraData", "0" },
            { "DpadRight_InputType", "0" },
            { "DpadRight_Name", "dpright" },
            { "DpadRight_Data", "14" },
            { "DpadRight_ExtraData", "0" },
            { "CButtonUp_InputType", "1" },
            { "CButtonUp_Name", "righty-" },
            { "CButtonUp_Data", "3" },
            { "CButtonUp_ExtraData", "0" },
            { "CButtonDown_InputType", "1" },
            { "CButtonDown_Name", "righty+" },
            { "CButtonDown_Data", "3" },
            { "CButtonDown_ExtraData", "1" },
            { "CButtonLeft_InputType", "1" },
            { "CButtonLeft_Name", "rightx-" },
            { "CButtonLeft_Data", "2" },
            { "CButtonLeft_ExtraData", "0" },
            { "CButtonRight_InputType", "1" },
            { "CButtonRight_Name", "rightx+" },
            { "CButtonRight_Data", "2" },
            { "CButtonRight_ExtraData", "1" },
            { "LeftTrigger_InputType", "0" },
            { "LeftTrigger_Name", "leftshoulder" },
            { "LeftTrigger_Data", "9" },
            { "LeftTrigger_ExtraData", "0" },
            { "RightTrigger_InputType", "0" },
            { "RightTrigger_Name", "rightshoulder" },
            { "RightTrigger_Data", "10" },
            { "RightTrigger_ExtraData", "0" },
            { "ZTrigger_InputType", "1" },
            { "ZTrigger_Name", "lefttrigger+" },
            { "ZTrigger_Data", "4" },
            { "ZTrigger_ExtraData", "1" },
            { "AnalogStickUp_InputType", "1" },
            { "AnalogStickUp_Name", "lefty-" },
            { "AnalogStickUp_Data", "1" },
            { "AnalogStickUp_ExtraData", "0" },
            { "AnalogStickDown_InputType", "1" },
            { "AnalogStickDown_Name", "lefty+" },
            { "AnalogStickDown_Data", "1" },
            { "AnalogStickDown_ExtraData", "1" },
            { "AnalogStickLeft_InputType", "1" },
            { "AnalogStickLeft_Name", "leftx-" },
            { "AnalogStickLeft_Data", "0" },
            { "AnalogStickLeft_ExtraData", "0" },
            { "AnalogStickRight_InputType", "1" },
            { "AnalogStickRight_Name", "leftx+" },
            { "AnalogStickRight_Data", "0" },
            { "AnalogStickRight_ExtraData", "1" },
        };
        static readonly Dictionary<string, string> mupen64_mayflash = new Dictionary<string, string>()
        {
            { "DeviceName", "Mayflash Magic NS" },
            { "A_InputType", "0" },
            { "A_Name", "a" },
            { "A_Data", "0" },
            { "A_ExtraData", "0" },
            { "B_InputType", "0" },
            { "B_Name", "b" },
            { "B_Data", "1" },
            { "B_ExtraData", "0" },
            { "Start_InputType", "0" },
            { "Start_Name", "start" },
            { "Start_Data", "6" },
            { "Start_ExtraData", "0" },
            { "DpadUp_InputType", "0" },
            { "DpadUp_Name", "dpup" },
            { "DpadUp_Data", "11" },
            { "DpadUp_ExtraData", "0" },
            { "DpadDown_InputType", "0" },
            { "DpadDown_Name", "dpdown" },
            { "DpadDown_Data", "12" },
            { "DpadDown_ExtraData", "0" },
            { "DpadLeft_InputType", "0" },
            { "DpadLeft_Name", "dpleft" },
            { "DpadLeft_Data", "13" },
            { "DpadLeft_ExtraData", "0" },
            { "DpadRight_InputType", "0" },
            { "DpadRight_Name", "dpright" },
            { "DpadRight_Data", "14" },
            { "DpadRight_ExtraData", "0" },
            { "CButtonUp_InputType", "1" },
            { "CButtonUp_Name", "righty-" },
            { "CButtonUp_Data", "3" },
            { "CButtonUp_ExtraData", "0" },
            { "CButtonDown_InputType", "1" },
            { "CButtonDown_Name", "righty+" },
            { "CButtonDown_Data", "3" },
            { "CButtonDown_ExtraData", "1" },
            { "CButtonLeft_InputType", "1" },
            { "CButtonLeft_Name", "rightx-" },
            { "CButtonLeft_Data", "2" },
            { "CButtonLeft_ExtraData", "0" },
            { "CButtonRight_InputType", "1" },
            { "CButtonRight_Name", "rightx+" },
            { "CButtonRight_Data", "2" },
            { "CButtonRight_ExtraData", "1" },
            { "LeftTrigger_InputType", "0" },
            { "LeftTrigger_Name", "leftshoulder" },
            { "LeftTrigger_Data", "9" },
            { "LeftTrigger_ExtraData", "0" },
            { "RightTrigger_InputType", "0" },
            { "RightTrigger_Name", "rightshoulder" },
            { "RightTrigger_Data", "10" },
            { "RightTrigger_ExtraData", "0" },
            { "ZTrigger_InputType", "1" },
            { "ZTrigger_Name", "lefttrigger+" },
            { "ZTrigger_Data", "4" },
            { "ZTrigger_ExtraData", "1" },
            { "AnalogStickUp_InputType", "1" },
            { "AnalogStickUp_Name", "lefty-" },
            { "AnalogStickUp_Data", "1" },
            { "AnalogStickUp_ExtraData", "0" },
            { "AnalogStickDown_InputType", "1" },
            { "AnalogStickDown_Name", "lefty+" },
            { "AnalogStickDown_Data", "1" },
            { "AnalogStickDown_ExtraData", "1" },
            { "AnalogStickLeft_InputType", "1" },
            { "AnalogStickLeft_Name", "leftx-" },
            { "AnalogStickLeft_Data", "0" },
            { "AnalogStickLeft_ExtraData", "0" },
            { "AnalogStickRight_InputType", "1" },
            { "AnalogStickRight_Name", "leftx+" },
            { "AnalogStickRight_Data", "0" },
            { "AnalogStickRight_ExtraData", "1" },
        };
        static readonly Dictionary<string, string> simple64_nso = new Dictionary<string, string>()
        {
            { "A", "\"" + "0,4" + "\"" },
            { "B", "\"" + "1,4" + "\"" },
            { "Z", "\"" + "4,5,1" + "\"" },
            { "Start", "\"" + "6,4" + "\"" },
            { "L", "\"" + "9,4" + "\"" },
            { "R", "\"" + "10,4" + "\"" },
            { "DPadL", "\"" + "13,4" + "\"" },
            { "DPadR", "\"" + "14,4" + "\"" },
            { "DPadU", "\"" + "11,4" + "\"" },
            { "DPadD", "\"" + "12,4" + "\"" },
            { "CLeft", "\"" + "2,4" + "\"" },
            { "CRight", "\"" + "4,4" + "\"" },
            { "CUp", "\"" + "3,4" + "\"" },
            { "CDown", "\"" + "5,5,-1" + "\"" },
            { "AxisLeft", "\"" + "0,5,-1" + "\"" },
            { "AxisRight", "\"" + "0,5,1" + "\"" },
            { "AxisUp", "\"" + "1,5,-1" + "\"" },
            { "AxisDown", "\"" + "1,5,1" + "\"" },
        };
        static readonly Dictionary<string, string> simple64_raphnet = new Dictionary<string, string>()
        {
            { "A", "\"" + "0,4" + "\"" },
            { "B", "\"" + "1,4" + "\"" },
            { "Z", "\"" + "2,4" + "\"" },
            { "Start", "\"" + "3,4" + "\"" },
            { "L", "\"" + "4,4" + "\"" },
            { "R", "\"" + "5,4" + "\"" },
            { "DPadL", "\"" + "12,4" + "\"" },
            { "DPadR", "\"" + "13,4" + "\"" },
            { "DPadU", "\"" + "10,4" + "\"" },
            { "DPadD", "\"" + "11,4" + "\"" },
            { "CLeft", "\"" + "8,4" + "\"" },
            { "CRight", "\"" + "9,4" + "\"" },
            { "CUp", "\"" + "6,4" + "\"" },
            { "CDown", "\"" + "7,4" + "\"" },
            { "AxisLeft", "\"" + "0,5,-1" + "\"" },
            { "AxisRight", "\"" + "0,5,1" + "\"" },
            { "AxisUp", "\"" + "1,5,-1" + "\"" },
            { "AxisDown", "\"" + "1,5,1" + "\"" },
        };
        static readonly Dictionary<string, string> simple64_mayflash = new Dictionary<string, string>()
        {
            { "A", "\"" + "1,4" + "\"" },
            { "B", "\"" + "2,4" + "\"" },
            { "Z", "\"" + "6,4" + "\"" },
            { "Start", "\"" + "9,4" + "\"" },
            { "L", "\"" + "4,4" + "\"" },
            { "R", "\"" + "5,4" + "\"" },
            { "DPadL", "\"" + "0,3,8" + "\"" },
            { "DPadR", "\"" + "0,3,2" + "\"" },
            { "DPadU", "\"" + "0,3,1" + "\"" },
            { "DPadD", "\"" + "0,3,4" + "\"" },
            { "CLeft", "\"" + "2,5,-1" + "\"" },
            { "CRight", "\"" + "2,5,1" + "\"" },
            { "CUp", "\"" + "3,5,-1" + "\"" },
            { "CDown", "\"" + "3,5,1" + "\"" },
            { "AxisLeft", "\"" + "0,5,-1" + "\"" },
            { "AxisRight", "\"" + "0,5,1" + "\"" },
            { "AxisUp", "\"" + "1,5,-1" + "\"" },
            { "AxisDown", "\"" + "1,5,1" + "\"" },
        };
        static readonly Dictionary<string, string> ares_nso = new Dictionary<string, string>()
        {
            { "Pad.Up", "3/11" },
            { "Pad.Down", "3/12" },
            { "Pad.Left", "3/13" },
            { "Pad.Right", "3/14" },
            { "Select", "" },
            { "Start", "3/6" },
            { "A..South", "3/0" },
            { "B..East", "" },
            { "X..West", "3/1" },
            { "Y..North", "" },
            { "L-Bumper", "3/9" },
            { "R-Bumper", "3/10" },
            { "L-Trigger", "" },
            { "R-Trigger", "0/4/Hi" },
            { "L-Stick..Click", "" },
            { "R-Stick..Click", "" },
            { "L-Up", "0/1/Lo" },
            { "L-Down", "0/1/Hi" },
            { "L-Left", "0/0/Lo" },
            { "L-Right", "0/0/Hi" },
            { "R-Up", "3/3" },
            { "R-Down", "0/5/Hi" },
            { "R-Left", "3/2" },
            { "R-Right", "3/4" },
        };
        static readonly Dictionary<string, string> ares_raphnet = new Dictionary<string, string>()
        {
            { "Pad.Up", "3/10" },
            { "Pad.Down", "3/11" },
            { "Pad.Left", "3/12" },
            { "Pad.Right", "3/13" },
            { "Select", "" },
            { "Start", "3/3" },
            { "A..South", "3/0" },
            { "B..East", "" },
            { "X..West", "3/1" },
            { "Y..North", "" },
            { "L-Bumper", "3/4" },
            { "R-Bumper", "3/5" },
            { "L-Trigger", "" },
            { "R-Trigger", "3/2" },
            { "L-Stick..Click", "" },
            { "R-Stick..Click", "" },
            { "L-Up", "0/1/Lo" },
            { "L-Down", "0/1/Hi" },
            { "L-Left", "0/0/Lo" },
            { "L-Right", "0/0/Hi" },
            { "R-Up", "3/6" },
            { "R-Down", "3/7" },
            { "R-Left", "3/8" },
            { "R-Right", "3/9" },
        };
        static readonly Dictionary<string, string> ares_mayflash = new Dictionary<string, string>()
        {
            { "Pad.Up", "1/1/Lo" },
            { "Pad.Down", "1/1/Hi" },
            { "Pad.Left", "1/0/Lo" },
            { "Pad.Right", "1/0/Hi" },
            { "Select", "" },
            { "Start", "3/9" },
            { "A..South", "3/1" },
            { "B..East", "" },
            { "X..West", "3/2" },
            { "Y..North", "" },
            { "L-Bumper", "3/4" },
            { "R-Bumper", "3/5" },
            { "L-Trigger", "" },
            { "R-Trigger", "3/6" },
            { "L-Stick..Click", "" },
            { "R-Stick..Click", "" },
            { "L-Up", "0/1/Lo" },
            { "L-Down", "0/1/Hi" },
            { "L-Left", "0/0/Lo" },
            { "L-Right", "0/0/Hi" },
            { "R-Up", "0/3/Lo" },
            { "R-Down", "0/3/Hi" },
            { "R-Left", "0/2/Lo" },
            { "R-Right", "0/2/Hi" },
        };
        static readonly Dictionary<string, string> bizhawk_nso = new Dictionary<string, string>()
        {
            { "A Up", "X AxisUp" },
            { "A Down", "X AxisDown" },
            { "A Left", "X AxisLeft" },
            { "A Right", "X AxisRight" },
            { "DPad U", "DpadUp" },
            { "DPad D", "DpadDown" },
            { "DPad L", "DpadLeft" },
            { "DPad R", "DpadRight" },
            { "Start", "Start" },
            { "Z", "LeftTrigger" },
            { "B", "B" },
            { "A", "A" },
            { "C Up", "Y" },
            { "C Down", "RightTrigger" },
            { "C Left", "X" },
            { "C Right", "Back" },
            { "L", "LeftShoulder" },
            { "R", "RightShoulder" },
        };
        static readonly Dictionary<string, string> bizhawk_raphnet = new Dictionary<string, string>()
        {
            { "A Up", "X AxisUp" },
            { "A Down", "X AxisDown" },
            { "A Left", "X AxisLeft" },
            { "A Right", "X AxisRight" },
            { "DPad U", "B11" },
            { "DPad D", "B12" },
            { "DPad L", "B13" },
            { "DPad R", "B14" },
            { "Start", "B4" },
            { "Z", "B3" },
            { "B", "B2" },
            { "A", "B1" },
            { "C Up", "B7" },
            { "C Down", "B8" },
            { "C Left", "B9" },
            { "C Right", "B10" },
            { "L", "B5" },
            { "R", "B6" },
        };
        static readonly Dictionary<string, string> bizhawk_mayflash = new Dictionary<string, string>()
        {
            { "A Up", "X AxisUp" },
            { "A Down", "X AxisDown" },
            { "A Left", "X AxisLeft" },
            { "A Right", "X AxisRight" },
            { "DPad U", "POV0U" },
            { "DPad D", "POV0D" },
            { "DPad L", "POV0L" },
            { "DPad R", "POV0R" },
            { "Start", "B10" },
            { "Z", "B7" },
            { "B", "B3" },
            { "A", "B2" },
            { "C Up", "W-" },
            { "C Down", "W+" },
            { "C Left", "Z-" },
            { "C Right", "Z+" },
            { "L", "B5" },
            { "R", "B6" },
        };
        static readonly Dictionary<string, string> libretro_nso = new Dictionary<string, string>()
        {
            { "analog_dpad_mode", "0" },
            { "b_btn", "1" },
            { "down_btn", "h0down" },
            { "l2_btn", "6" },
            { "l_btn", "4" },
            { "l_x_minus_axis", "-0" },
            { "l_x_plus_axis", "+0" },
            { "l_y_minus_axis", "-1" },
            { "l_y_plus_axis", "+1" },
            { "left_btn", "h0left" },
            { "r_btn", "5" },
            { "r_x_minus_btn", "3" },
            { "r_x_plus_btn", "8" },
            { "r_y_minus_btn", "2" },
            { "r_y_plus_btn", "7" },
            { "right_btn", "h0right" },
            { "select_btn", "13" },
            { "start_btn", "9" },
            { "up_btn", "h0up" },
            { "y_btn", "0" },
        };
        static readonly Dictionary<string, string> libretro_raphnet = new Dictionary<string, string>()
        {
            { "a_btn", "7" },
            { "b_btn", "0" },
            { "down_btn", "11" },
            { "l2_btn", "2" },
            { "l_btn", "4" },
            { "l_x_minus_axis", "-0" },
            { "l_x_plus_axis", "+0" },
            { "l_y_minus_axis", "-1" },
            { "l_y_plus_axis", "+1" },
            { "left_btn", "12" },
            { "r_btn", "5" },
            { "r_x_minus_btn", "8" },
            { "r_x_plus_btn", "9" },
            { "r_y_minus_btn", "6" },
            { "r_y_plus_btn", "7" },
            { "right_btn", "13" },
            { "select_btn", "4" },
            { "start_btn", "3" },
            { "up_btn", "10" },
            { "y_btn", "1" },
        };
        static readonly Dictionary<string, string> libretro_mayflash = new Dictionary<string, string>()
        {
            { "b_btn", "1" },
            { "a_axis", "+3" },
            { "down_btn", "h0down" },
            { "l2_btn", "6" },
            { "l_btn", "4" },
            { "l_x_minus_axis", "-0" },
            { "l_x_plus_axis", "+0" },
            { "l_y_minus_axis", "-1" },
            { "l_y_plus_axis", "+1" },
            { "left_btn", "h0left" },
            { "r_btn", "5" },
            { "r_x_minus_axis", "-2" },
            { "r_x_plus_axis", "+2" },
            { "r_y_minus_axis", "-3" },
            { "r_y_plus_axis", "+3" },
            { "right_btn", "h0right" },
            { "select_btn", "4" },
            { "start_btn", "9" },
            { "up_btn", "h0up" },
            { "y_btn", "2" },
        };

        #endregion

        #region Dictionaries hotkeymappings
        static readonly Dictionary<string, string> mupen64_nso_hk = new Dictionary<string, string>()
        {
            { "Hotkey_Exit_InputType", "0;0" },
            { "Hotkey_Exit_Name", "misc1;start" },
            { "Hotkey_Exit_Data", "15;6" },
            { "Hotkey_Exit_ExtraData", "0;0" },
            { "Hotkey_Resume_InputType", "1;0" },
            { "Hotkey_Resume_Name", "righttrigger+;misc1" },
            { "Hotkey_Resume_Data", "5;15" },
            { "Hotkey_Resume_ExtraData", "1;0" },
            { "Hotkey_Screenshot_InputType", "0;1" },
            { "Hotkey_Screenshot_Name", "misc1;lefttrigger+" },
            { "Hotkey_Screenshot_Data", "15;4" },
            { "Hotkey_Screenshot_ExtraData", "0;1" },
            { "Hotkey_SpeedFactor50_InputType", "0;0" },
            { "Hotkey_SpeedFactor50_Name", "misc1;dpleft" },
            { "Hotkey_SpeedFactor50_Data", "15;13" },
            { "Hotkey_SpeedFactor50_ExtraData", "0;0" },
            { "Hotkey_SpeedFactor100_InputType", "0;0" },
            { "Hotkey_SpeedFactor100_Name", "misc1;dpup" },
            { "Hotkey_SpeedFactor100_Data", "15;11" },
            { "Hotkey_SpeedFactor100_ExtraData", "0;0" },
            { "Hotkey_SpeedFactor250_InputType", "0;0" },
            { "Hotkey_SpeedFactor250_Name", "misc1;dpright" },
            { "Hotkey_SpeedFactor250_Data", "15;14" },
            { "Hotkey_SpeedFactor250_ExtraData", "0;0" },
            { "Hotkey_SaveState_InputType", "0;0" },
            { "Hotkey_SaveState_Name", "misc1;b" },
            { "Hotkey_SaveState_Data", "15;1" },
            { "Hotkey_SaveState_ExtraData", "0;0" },
            { "Hotkey_LoadState_InputType", "0;0" },
            { "Hotkey_LoadState_Name", "misc1;x" },
            { "Hotkey_LoadState_Data", "15;2" },
            { "Hotkey_LoadState_ExtraData", "0;0" },
        };
        static readonly Dictionary<string, string> mupen64_raphnet_hk = new Dictionary<string, string>()
        {
            { "Hotkey_Exit_InputType", "0;0" },
            { "Hotkey_Exit_Name", "leftshoulder;start" },
            { "Hotkey_Exit_Data", "9;6" },
            { "Hotkey_Exit_ExtraData", "0;0" },
            { "Hotkey_Resume_InputType", "0;1" },
            { "Hotkey_Resume_Name", "leftshoulder;righty+" },
            { "Hotkey_Resume_Data", "9;3" },
            { "Hotkey_Resume_ExtraData", "0;1" },
            { "Hotkey_Screenshot_InputType", "0;1" },
            { "Hotkey_Screenshot_Name", "leftshoulder;rightx+" },
            { "Hotkey_Screenshot_Data", "9;2" },
            { "Hotkey_Screenshot_ExtraData", "0;1" },
            { "Hotkey_SpeedFactor50_InputType", "0;0" },
            { "Hotkey_SpeedFactor50_Name", "leftshoulder;dpleft" },
            { "Hotkey_SpeedFactor50_Data", "9;13" },
            { "Hotkey_SpeedFactor50_ExtraData", "0;0" },
            { "Hotkey_SpeedFactor100_InputType", "0;0" },
            { "Hotkey_SpeedFactor100_Name", "leftshoulder;dpup" },
            { "Hotkey_SpeedFactor100_Data", "9;11" },
            { "Hotkey_SpeedFactor100_ExtraData", "0;0" },
            { "Hotkey_SpeedFactor250_InputType", "0;0" },
            { "Hotkey_SpeedFactor250_Name", "leftshoulder;dpright" },
            { "Hotkey_SpeedFactor250_Data", "9;14" },
            { "Hotkey_SpeedFactor250_ExtraData", "0;0" },
            { "Hotkey_SaveState_InputType", "0;0" },
            { "Hotkey_SaveState_Name", "leftshoulder;b" },
            { "Hotkey_SaveState_Data", "9;1" },
            { "Hotkey_SaveState_ExtraData", "0;0" },
            { "Hotkey_LoadState_InputType", "0;1" },
            { "Hotkey_LoadState_Name", "leftshoulder;rightx-" },
            { "Hotkey_LoadState_Data", "9;2" },
            { "Hotkey_LoadState_ExtraData", "0;0" },
        };
        static readonly Dictionary<string, string> mupen64_mayflash_hk = new Dictionary<string, string>()
        {
            { "Hotkey_Exit_InputType", "0;0" },
            { "Hotkey_Exit_Name", "leftshoulder;start" },
            { "Hotkey_Exit_Data", "9;6" },
            { "Hotkey_Exit_ExtraData", "0;0" },
            { "Hotkey_Resume_InputType", "0;1" },
            { "Hotkey_Resume_Name", "leftshoulder;righty+" },
            { "Hotkey_Resume_Data", "9;3" },
            { "Hotkey_Resume_ExtraData", "0;1" },
            { "Hotkey_Screenshot_InputType", "0;1" },
            { "Hotkey_Screenshot_Name", "leftshoulder;rightx+" },
            { "Hotkey_Screenshot_Data", "9;2" },
            { "Hotkey_Screenshot_ExtraData", "0;1" },
            { "Hotkey_SpeedFactor50_InputType", "0;0" },
            { "Hotkey_SpeedFactor50_Name", "leftshoulder;dpleft" },
            { "Hotkey_SpeedFactor50_Data", "9;13" },
            { "Hotkey_SpeedFactor50_ExtraData", "0;0" },
            { "Hotkey_SpeedFactor100_InputType", "0;0" },
            { "Hotkey_SpeedFactor100_Name", "leftshoulder;dpup" },
            { "Hotkey_SpeedFactor100_Data", "9;11" },
            { "Hotkey_SpeedFactor100_ExtraData", "0;0" },
            { "Hotkey_SpeedFactor250_InputType", "0;0" },
            { "Hotkey_SpeedFactor250_Name", "leftshoulder;dpright" },
            { "Hotkey_SpeedFactor250_Data", "9;14" },
            { "Hotkey_SpeedFactor250_ExtraData", "0;0" },
            { "Hotkey_SaveState_InputType", "0;0" },
            { "Hotkey_SaveState_Name", "leftshoulder;b" },
            { "Hotkey_SaveState_Data", "9;1" },
            { "Hotkey_SaveState_ExtraData", "0;0" },
            { "Hotkey_LoadState_InputType", "0;1" },
            { "Hotkey_LoadState_Name", "leftshoulder;rightx-" },
            { "Hotkey_LoadState_Data", "9;2" },
            { "Hotkey_LoadState_ExtraData", "0;0" },
        };
        static readonly Dictionary<string, string> libretro_nso_hk = new Dictionary<string, string>()
        {
            { "input_enable_hotkey_btn", "13" },
            { "input_joypad_driver", "sdl2" },
            { "input_exit_emulator_btn", "9" },
            { "input_pause_toggle_btn", "7" },
            { "input_menu_toggle_btn", "1" },
            { "input_load_state_btn", "3" },
            { "input_save_state_btn", "0" },
            { "input_ai_service_btn", "5" },
            { "input_state_slot_decrease_btn", "h0down" },
            { "input_state_slot_increase_btn", "h0up" },
            { "input_rewind_btn", "h0left" },
            { "input_hold_fast_forward_btn", "h0right" },
        };
        static readonly Dictionary<string, string> libretro_raphnet_hk = new Dictionary<string, string>()
        {
            { "input_enable_hotkey_btn", "4" },
            { "input_joypad_driver", "sdl2" },
            { "input_exit_emulator_btn", "3" },
            { "input_pause_toggle_btn", "7" },
            { "input_menu_toggle_btn", "0" },
            { "input_load_state_btn", "8" },
            { "input_save_state_btn", "1" },
            { "input_ai_service_btn", "5" },
            { "input_state_slot_decrease_btn", "11" },
            { "input_state_slot_increase_btn", "10" },
            { "input_rewind_btn", "12" },
            { "input_hold_fast_forward_btn", "13" },
        };
        static readonly Dictionary<string, string> libretro_mayflash_hk = new Dictionary<string, string>()
        {
            { "input_enable_hotkey_btn", "4" },
            { "input_joypad_driver", "sdl2" },
            { "input_exit_emulator_btn", "9" },
            { "input_pause_toggle_axis", "+3" },
            { "input_pause_toggle_btn", "nul" },
            { "input_menu_toggle_btn", "1" },
            { "input_load_state_axis", "-2" },
            { "input_load_state_btn", "nul" },
            { "input_save_state_btn", "2" },
            { "input_ai_service_btn", "5" },
            { "input_state_slot_decrease_btn", "h0down" },
            { "input_state_slot_increase_btn", "h0up" },
            { "input_rewind_btn", "h0left" },
            { "input_hold_fast_forward_btn", "h0right" },
        };
        #endregion

        #region Dictionaries controllerInfo
        static readonly Dictionary<string, string> bizhawk_nso_info = new Dictionary<string, string>()
        {
            { "XInvert", "false" },
            { "YInvert", "false" },
            { "dinput", "false" },
        };
        static readonly Dictionary<string, string> bizhawk_raphnet_info = new Dictionary<string, string>()
        {
            { "XInvert", "false" },
            { "YInvert", "true" },
            { "dinput", "true" },
        };
        static readonly Dictionary<string, string> bizhawk_mayflash_info = new Dictionary<string, string>()
        {
            { "XInvert", "false" },
            { "YInvert", "true" },
            { "dinput", "true" },
        };
        static readonly Dictionary<string, string> libretro_nso_info = new Dictionary<string, string>()
        {
            { "input_analog_sensitivity", "1.500000" },
        };
        #endregion
    }

    public class N64ControllerJsonModel
    {
        public string Emulator { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
        public Dictionary<string, string> Mapping { get; set; }
        public Dictionary<string, string> HotKeyMapping { get; set; }
        public Dictionary<string, string> ControllerInfo { get; set; }
    }
}