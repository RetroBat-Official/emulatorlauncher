using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Windows.Forms;
using System.Management;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;

namespace emulatorLauncher.Tools
{
    [XmlRoot("inputList")]
    [XmlType("inputList")]
    public class EsInput
    {
        public static InputConfig[] Load(string xmlFile)
        {
            if (!File.Exists(xmlFile))
                return null;

            try
            {
                EsInput ret = xmlFile.FromXml<EsInput>();
                if (ret != null)
                    return ret.InputConfigs.ToArray();
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.Show(ex.Message);
#endif
                SimpleLogger.Instance.Error("[InputConfig] Error : " + ex.Message);
            }

            return null;
        }

        [XmlElement("inputConfig")]
        public List<InputConfig> InputConfigs { get; set; }
    }

    public class InputConfig
    {
        public override string ToString()
        {
            return DeviceName;
        }

        [XmlIgnore]
        public Guid ProductGuid
        {
            get
            {
                return DeviceGUID.FromSdlGuidString();
            }
        }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("deviceName")]
        public string DeviceName { get; set; }

        [XmlAttribute("deviceGUID")]
        public string DeviceGUID { get; set; }

        [XmlElement("input")]
        public List<Input> Input { get; set; }

        [XmlIgnore]
        public Input this[InputKey key]
        {
            get
            {
                return Input.FirstOrDefault(i => i.Name == key);
            }
        }
        
        public Input ToSdlCode(InputKey key)
        {
            Input input = this[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            var ctrl = SdlGameControllers.GetGameController(ProductGuid);
            if (ctrl == null)
                return input;

            int axisValue = 1;

            var mapping = ctrl.Mapping;
            var sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Input.Id == input.Id && m.Input.Value == input.Value);

            if (sdlret == null && input.Type == "axis")
            {
                var invret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Input.Id == input.Id && m.Input.Value == -input.Value);
                if (invret != null)
                {
                    sdlret = invret;
                    axisValue = -1;
                }
            }

            if (sdlret == null)
            {
                if (mapping.All(m => m.Axis == SDL_CONTROLLER_AXIS.INVALID))
                {
                    switch (key)
                    {
                        case InputKey.left:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_LEFT);
                            break;
                        case InputKey.right:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_RIGHT);
                            break;
                        case InputKey.up:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_UP);
                            break;
                        case InputKey.down:
                            sdlret = mapping.FirstOrDefault(m => m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_DOWN);
                            break;
                    }
                }

                if (sdlret == null)
                {
                    SimpleLogger.Instance.Warning("[InputConfig] ToSdlCode error can't find <input name=\"" + key.ToString() + "\" type=\"" + input.Type + "\" id=\"" + input.Id + "\" value=\"" + input.Value + "\" /> in SDL2 mapping :\r\n" + ctrl.SdlBinding);
                    return input;
                }
            }

            Input ret = new Input() { Name = input.Name };

            if (sdlret.Button != SDL_CONTROLLER_BUTTON.INVALID)
            {
                ret.Type = "button";
                ret.Id = (int)sdlret.Button;
                ret.Value = 1;
                return ret;
            }

            if (sdlret.Axis != SDL_CONTROLLER_AXIS.INVALID)
            {
                ret.Type = "axis";
                ret.Id = (int)sdlret.Axis;
                ret.Value = axisValue;
                return ret;
            }

            return ToXInputCodes(key);
        }

        /// <summary>
        /// Translate EmulationStation/SDL input to XInput compatible codes
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Input ToXInputCodes(InputKey key)
        {
            Input input = this[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            if (!XInputDevice.IsXInputDevice(this.DeviceGUID))
                return input;

            Input ret = new Input();
            ret.Name = input.Name;
            ret.Type = input.Type;
            ret.Id = input.Id;
            ret.Value = input.Value;

            // Inverstion de start et select
            if (input.Type == "button" && input.Id == 6)
                ret.Id = 7;
            else if (input.Type == "button" && input.Id == 7)
                ret.Id = 6;

            if (input.Type == "axis" && ret.Id == 1 || ret.Id == 3) // up/down axes are inverted
                ret.Value = -ret.Value;

            return ret;
        }

        /// <summary>
        /// Translate EmulationStation/SDL input to XInput Mapping
        /// </summary>
        /// <param name="key"></param>
        /// <param name="revertAxis"></param>
        /// <returns></returns>
        public XINPUTMAPPING GetXInputMapping(InputKey key, bool revertAxis = false)
        {
            Input input = this[key];
            if (input == null)
                return XINPUTMAPPING.UNKNOWN;

            if (input.Type == "key")
                return XINPUTMAPPING.UNKNOWN;

            if (!XInputDevice.IsXInputDevice(this.DeviceGUID))
                return XINPUTMAPPING.UNKNOWN;

            if (input.Type == "button")
                return (XINPUTMAPPING)input.Id;

            if (input.Type == "hat")
                return (XINPUTMAPPING)(input.Value + 10);

            if (input.Type == "axis")
            {
                switch (input.Id)
                {
                    case 2:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.RIGHTANALOG_RIGHT;

                        return XINPUTMAPPING.RIGHTANALOG_LEFT;

                    case 5:
                        return XINPUTMAPPING.RIGHTTRIGGER;

                    case 0:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.LEFTANALOG_RIGHT;

                        return XINPUTMAPPING.LEFTANALOG_LEFT;

                    case 1:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.LEFTANALOG_DOWN;

                        return XINPUTMAPPING.LEFTANALOG_UP;

                    case 4:
                        return XINPUTMAPPING.LEFTTRIGGER;

                    case 3:
                        if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0))
                            return XINPUTMAPPING.RIGHTANALOG_DOWN;

                        return XINPUTMAPPING.RIGHTANALOG_UP;
                }
            }

            return XINPUTMAPPING.UNKNOWN;
        }
    }

    public class Input
    {
        [XmlAttribute("name")]
        public InputKey Name { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("id")]
        public long Id { get; set; }

        [XmlAttribute("value")]
        public long Value { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            if ((int)Name != 0)
                sb.Append(" name:" + Name);

            if (Type != null)
                sb.Append(" type:" + Type);

            sb.Append(" id:" + Id);
            sb.Append(" value:" + Value);

            return sb.ToString().Trim();
        }

        public override bool Equals(object obj)
        {
            if (base.Equals(obj))
                return true;

            Input src = obj as Input;
            if (src == null)
                return false;

            return Name == src.Name && Id == src.Id && Value == src.Value && Type == src.Type;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [Flags]
    public enum InputKey
    {
        // batocera ES compatibility
        hotkey = 8,
        pageup = 512,
        pagedown = 131072,

        l2 = 1024,
        r2 = 262144,

        l3 = 2048,
        r3 = 524288,

        a = 1,
        b = 2,
        down = 4,
        hotkeyenable = 8,
        left = 16,

        leftanalogdown = 32,
        leftanalogleft = 64,
        leftanalogright = 128,
        leftanalogup = 256,


        rightanalogup = 8192,
        rightanalogdown = 16384,
        rightanalogleft = 32768,
        rightanalogright = 65536,

        leftthumb = 1024,
        rightthumb = 262144,

        leftshoulder = 512,
        lefttrigger = 2048,

        rightshoulder = 131072,
        righttrigger = 524288,

        right = 4096,
        select = 1048576,
        start = 2097152,
        up = 4194304,
        x = 8388608,
        y = 16777216,

        joystick1down = 32,
        joystick1left = 64,
        joystick1right = 128,
        joystick1up = 256,

        joystick2up = 8192,
        joystick2down = 16384,
        joystick2left = 32768,
        joystick2right = 65536
    }

    public enum XINPUTMAPPING
    {
        UNKNOWN = -1,

        A = 0,
        B = 1,
        Y = 2,
        X = 3,
        LEFTSHOULDER = 4,
        RIGHTSHOULDER = 5,

        BACK = 6,
        START = 7,

        LEFTSTICK = 8,
        RIGHTSTICK = 9,
        GUIDE = 10,

        DPAD_UP = 11,
        DPAD_RIGHT = 12,
        DPAD_DOWN = 14,
        DPAD_LEFT = 18,

        LEFTANALOG_UP = 21,
        LEFTANALOG_RIGHT = 22,
        LEFTANALOG_DOWN = 24,
        LEFTANALOG_LEFT = 28,

        RIGHTANALOG_UP = 31,
        RIGHTANALOG_RIGHT = 32,
        RIGHTANALOG_DOWN = 34,
        RIGHTANALOG_LEFT = 38,

        RIGHTTRIGGER = 51,
        LEFTTRIGGER = 52
    }

    [Flags]
    public enum GamepadButtonFlags : ushort
    {
        DPAD_UP = 1,
        DPAD_DOWN = 2,
        DPAD_LEFT = 4,
        DPAD_RIGHT = 8,
        START = 16,
        BACK = 32,
        LEFTTRIGGER = 64,
        RIGHTTRIGGER = 128,
        LEFTSHOULDER = 256,
        RIGHTSHOULDER = 512,
        A = 4096,
        B = 8192,
        X = 16384,
        Y = 32768
    }

    enum XINPUT_GAMEPAD
    {
        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        LEFTSHOULDER = 4,
        RIGHTSHOULDER = 5,

        BACK = 6,
        START = 7,

        LEFTSTICK = 8,
        RIGHTSTICK = 9,
        GUIDE = 10
    }

    enum XINPUT_HATS
    {
        DPAD_UP = 1,
        DPAD_RIGHT = 2,
        DPAD_DOWN = 4,
        DPAD_LEFT = 8
    }

    enum SDL_CONTROLLER_BUTTON
    {
        INVALID = -1,

        A = 0,
        B = 1,
        X = 2,
        Y = 3,
        BACK = 4,
        GUIDE = 5,
        START = 6,
        LEFTSTICK = 7,
        RIGHTSTICK = 8,
        LEFTSHOULDER = 9,
        RIGHTSHOULDER = 10,
        DPAD_UP = 11,
        DPAD_DOWN = 12,
        DPAD_LEFT = 13,
        DPAD_RIGHT = 14
    };

    enum SDL_CONTROLLER_AXIS
    {
        INVALID = -1,

        LEFTX = 0,
        LEFTY = 1,
        RIGHTX = 2,
        RIGHTY = 3,
        TRIGGERLEFT = 4,
        TRIGGERRIGHT = 5
    }

}
