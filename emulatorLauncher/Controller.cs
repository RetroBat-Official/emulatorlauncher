using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common.Joysticks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace EmulatorLauncher
{
    class Controller
    {
        public Controller()
        {
            DeviceIndex = -1;
            xIndexReversed = -1;
        }

        public int PlayerIndex { get; set; }
        public int DeviceIndex { get; set; }
        public SdlJoystickGuid Guid { get; set; }
        public string DevicePath { get; set; }
        public string Name { get; set; }
        public int NbButtons { get; set; }
        public int NbHats { get; set; }
        public int NbAxes { get; set; }
        public int xIndexReversed { get; set; }

        public bool IsKeyboard { get { return "Keyboard".Equals(Name, StringComparison.InvariantCultureIgnoreCase); } }

        #region Arcade stick detection
        /// <summary>
        /// Best-effort arcade stick detection, used by the "arcadesticks_last" option.
        /// 1. XInput subtype ARCADE_STICK declared by the firmware.
        /// 2. USB id and name rules from system\tools\arcade_sticks.txt
        ///    (exclusion rules only cancel name rules, field by field).
        /// ARCADE_PAD (6-button fight pads) is deliberately not treated as a stick.
        /// </summary>
        public bool IsArcadeStick
        {
            get
            {
                if (IsKeyboard || Guid == null)
                    return false;

                // XInput driver guid: last byte is the XInput subtype
                if (Guid.WrappedTechID == SdlWrappedTechId.XInput && Guid.SubControllerType == (int)XINPUT_DEVSUBTYPE.ARCADE_STICK)
                    return true;

                // XInput device seen through RawInput/HID: read the subtype from the device itself.
                // Only trusted when VID/PID match, as the XInput slot may come from a fallback assignment.
                if (IsXInputDevice)
                {
                    var xinput = XInput;
                    if (xinput != null && xinput.Connected && xinput.SubType == XINPUT_DEVSUBTYPE.ARCADE_STICK &&
                        xinput.VendorId == VendorID && xinput.ProductId == ProductID)
                        return true;
                }

                return ArcadeStickRules.Matches((int)VendorID, (int)ProductID, Name, BusReportedName, HidManufacturer, HidProduct);
            }
        }

        /// <summary>
        /// HID device path usable with Windows device APIs (SDL XInput driver paths are not HID paths).
        /// </summary>
        private string GetHidDevicePath()
        {
            string path = DevicePath;
            if ((string.IsNullOrEmpty(path) || !path.StartsWith(@"\\?\")) && IsXInputDevice && XInput != null)
                path = XInput.Path;

            return (!string.IsNullOrEmpty(path) && path.StartsWith(@"\\?\")) ? path : null;
        }

        private string _busReportedName;

        /// <summary>
        /// Product name reported by the device hardware (USB iProduct), as shown in Device Manager
        /// "Bus reported device description". Empty when unavailable. Cached.
        /// </summary>
        public string BusReportedName
        {
            get
            {
                if (_busReportedName != null)
                    return _busReportedName;

                _busReportedName = string.Empty;
                if (IsKeyboard)
                    return _busReportedName;

                try
                {
                    string path = GetHidDevicePath();
                    if (path != null)
                        _busReportedName = DeviceHelper.GetBusReportedDeviceName(path) ?? string.Empty;
                }
                catch { }

                return _busReportedName;
            }
        }

        private bool _hidStringsKnown;
        private string _hidManufacturer = string.Empty;
        private string _hidProduct = string.Empty;

        private void EnsureHidStrings()
        {
            if (_hidStringsKnown)
                return;

            _hidStringsKnown = true;
            if (IsKeyboard)
                return;

            try
            {
                string path = GetHidDevicePath();
                if (path != null)
                    DeviceHelper.GetHidStrings(path, out _hidManufacturer, out _hidProduct);
            }
            catch { }
        }

        /// <summary>USB manufacturer string (iManufacturer) read from the HID device. Cached.</summary>
        public string HidManufacturer { get { EnsureHidStrings(); return _hidManufacturer ?? string.Empty; } }

        /// <summary>USB product string (iProduct) read from the HID device. Cached.</summary>
        public string HidProduct { get { EnsureHidStrings(); return _hidProduct ?? string.Empty; } }

        /// <summary>
        /// Rules loaded once from system\tools\arcade_sticks.txt (see the file header for the syntax).
        /// </summary>
        private static class ArcadeStickRules
        {
            private static List<string> _names;
            private static List<string> _exclusions;
            private static List<KeyValuePair<int, int>> _usbIds; // Value = -1 means any product id

            private static void Load()
            {
                if (_names != null)
                    return;

                _names = new List<string>();
                _exclusions = new List<string>();
                _usbIds = new List<KeyValuePair<int, int>>();

                string file = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "arcade_sticks.txt");
                if (!File.Exists(file))
                {
                    SimpleLogger.Instance.Info("[Controller] Arcade stick list not found: " + file);
                    return;
                }

                try
                {
                    foreach (var raw in File.ReadAllLines(file))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#"))
                            continue;

                        if (line.StartsWith("!"))
                        {
                            string text = line.Substring(1).Trim().ToLowerInvariant();
                            if (text.Length > 0)
                                _exclusions.Add(text);
                            continue;
                        }

                        if (line.StartsWith("usb:", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var parts = line.Substring(4).Split(':');

                            int vid;
                            if (!int.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out vid))
                                continue;

                            int pid = -1;
                            if (parts.Length > 1 && parts[1].Trim() != "*" &&
                                !int.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out pid))
                                continue;

                            _usbIds.Add(new KeyValuePair<int, int>(vid, pid));
                            continue;
                        }

                        _names.Add(line.ToLowerInvariant());
                    }

                    SimpleLogger.Instance.Info("[Controller] Arcade stick list loaded: " + _names.Count + " names, " + _usbIds.Count + " usb ids, " + _exclusions.Count + " exclusions.");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error("[Controller] Failed to read arcade stick list: " + ex.Message, ex);
                }
            }

            public static bool Matches(int vid, int pid, params string[] names)
            {
                Load();

                // USB ids are not affected by exclusions
                if (vid != 0 && _usbIds.Any(u => u.Key == vid && (u.Value == -1 || u.Value == pid)))
                    return true;

                // Name rules, evaluated field by field: an exclusion only cancels the field it is found
                foreach (var n in names)
                {
                    if (string.IsNullOrEmpty(n))
                        continue;

                    string name = n.ToLowerInvariant();
                    if (_exclusions.Any(e => name.Contains(e)))
                        continue;

                    if (_names.Any(r => name.Contains(r)))
                        return true;
                }

                return false;
            }
        }
        #endregion

        /// <summary>
        /// True when another player is bound to the very same device path. This happens with arcade
        /// boards exposing one joystick per HID collection (Xin-Mo / Xinmotek dual encoders,
        /// DragonRise and "Twin USB" 2-players boards, ...): every lookup made with that path
        /// returns the same device, so it cannot be used to resolve a joystick index.
        /// The SDL mapping stays usable, only the index resolution has to fall back on DeviceIndex.
        /// </summary>
        public bool HasAmbiguousDevicePath
        {
            get
            {
                if (string.IsNullOrEmpty(DevicePath) || Program.Controllers == null)
                    return false;

                return Program.Controllers.Any(c => c != this && !c.IsKeyboard
                    && string.Equals(c.DevicePath, this.DevicePath, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public SdlToDirectInput dinputCtrl = null;

        public SdlJoystickGuid GetSdlGuid(SdlVersion version = SdlVersion.SDL2_0_X, bool noRemoveDriver = false)
        {
            if (version == SdlVersion.Unknown)
                return Guid;

            return Guid.ConvertSdlGuid(Name??"", version, noRemoveDriver);            
        }

        private HashSet<string> _compatibleSdlGuids;

        /// <summary>
        ///  Get list of all possible guids for a controller, given Sdl Version
        /// </summary>
        public HashSet<string> CompatibleSdlGuids
        {
            get
            {
                if (_compatibleSdlGuids == null)
                {
                    _compatibleSdlGuids = new HashSet<string>(typeof(SdlVersion).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)
                        .Select(f => f.GetValue(null)).OfType<SdlVersion>()
                        .Select(f => GetSdlGuid(f).ToLowerInvariant())
                        .Distinct());
                }

                return _compatibleSdlGuids;
            }
        }

        public USB_VENDOR VendorID { get { return Guid.VendorId; } }
        public USB_PRODUCT ProductID { get { return Guid.ProductId; } }
        public SdlWrappedTechId SdlWrappedTechID { get { return Guid.WrappedTechID; } }

        public InputConfig Config { get; set; }
        
        #region SdlController
        private SdlGameController _sdlController;
        private bool _sdlControllerKnown = false;

        public SdlGameController SdlController
        {
            get
            {
                if (!_sdlControllerKnown)
                {
                    _sdlControllerKnown = true;

                    if (Name != "Keyboard")
                    {
                        if (!string.IsNullOrEmpty(DevicePath))
                            _sdlController = SdlGameController.GetGameControllerByPath(DevicePath);

                        // GUID fallback: two identical pads share the same SDL GUID, so the lookup
                        // would hand the very same SdlGameController to every one of them - and
                        // therefore the same joypad index. Better to return null and let callers
                        // fall back on DeviceIndex explicitly.
                        if (_sdlController == null)
                        {
                            bool guidIsAmbiguous = Program.Controllers != null
                                && Program.Controllers.Count(c => c != this && !c.IsKeyboard && c.Guid.ToGuid() == this.Guid.ToGuid()) > 0;

                            if (guidIsAmbiguous)
                                SimpleLogger.Instance.Warning("[Controller] Player " + PlayerIndex + " : path lookup failed and GUID is shared by several pads - no SDL match.");
                            else
                                _sdlController = SdlGameController.GetGameController(Guid.ToGuid());
                        }
                    }
                }

                return _sdlController;
            }
        }

        public void ResetSdlController()
        {
            _sdlControllerKnown = false;
            _sdlController = null;
        }
        #endregion

        #region WinmmJoystick
        private WinmmJoystick _winmmJoystick;
        private bool _winmmJoystickKnown = false;

        public WinmmJoystick WinmmJoystick
        {
            get
            {
                if (!_winmmJoystickKnown)
                {
                    _winmmJoystickKnown = true;

                    if (this.Config != null && Name != "Keyboard")
                    {
                        var di = DirectInput;
                        if (di != null)
                        {
                            _winmmJoystick = WinmmJoystick.Controllers.FirstOrDefault(m => m.VendorId == di.VendorId && m.ProductId == di.ProductId && m.DirectInputIndex == di.DeviceIndex);

                            if (_winmmJoystick == null)
                                _winmmJoystick = WinmmJoystick.Controllers.FirstOrDefault(m => m.VendorId == di.VendorId && m.ProductId == di.ProductId);
                        }
                    }
                }

                return _winmmJoystick;
            }
        }
        #endregion

        #region XInput
        private XInputDevice _xInputDevice;

        public bool IsXInputDevice
        {
            get
            {
                if (_xInputDevice != null)
                    return true;

                return Name != "Keyboard" && Config != null && XInputDevice.IsXInputDevice(this.Config.DeviceGUID);
            }
        }

        public bool UsesHardwareButtonOrder
        {
            get
            {
                switch (SdlWrappedTechID)
                {
                    case SdlWrappedTechId.RawInput:
                    case SdlWrappedTechId.XInput:
                        return true;
                    default:
                        return IsXInputDevice;
                }
            }
        }

        private static bool _xInputDevicesKnown;

        private static void EnsureXInputControllers()
        {
            if (_xInputDevicesKnown)
                return;

            _xInputDevicesKnown = true;

            var devices = XInputDevice.GetDevices();
            var xInputControllers = Program.Controllers.Where(c => c.Name != "Keyboard" && c.Config != null && XInputDevice.IsXInputDevice(c.Config.DeviceGUID)).ToArray();
            
            foreach (var c in xInputControllers)
            {
                var inst = devices.FirstOrDefault(dev => dev.Path == c.DevicePath || dev.ParentPath == c.DevicePath);
                if (inst != null)
                    c._xInputDevice = inst;
            }

            foreach (var c in xInputControllers.Where(dev => dev._xInputDevice == null).OrderBy(c => c.SdlController != null ? c.SdlController.Index : c.PlayerIndex))
            {
                int idx = 0;
                while (xInputControllers.Any(dev => dev._xInputDevice != null && dev._xInputDevice.DeviceIndex == idx))
                    idx++;

                c._xInputDevice = new XInputDevice(idx);
            }
        }

        public XInputDevice XInput
        {
            get
            {                    
                EnsureXInputControllers();
                return _xInputDevice;
            }
        }
        #endregion

        #region DirectInputInfo
        private DirectInputInfo _dInputDevice;
        private bool _dInputDeviceKnown;

        public DirectInputInfo DirectInput
        {
            get
            {
                if (!_dInputDeviceKnown)
                {
                    _dInputDeviceKnown = true;

                    if (Config != null && Name != "Keyboard")
                    {                        
                        if (!string.IsNullOrEmpty(this.DevicePath))
                        {
                            _dInputDevice = DirectInputInfo.Controllers.FirstOrDefault(c => this.DevicePath == c.ParentDevice);

                            if (_dInputDevice == null)
                                _dInputDevice = DirectInputInfo.Controllers.FirstOrDefault(c => this.DevicePath == c.DevicePath);
                        }

                        if (_dInputDevice == null)
                            _dInputDevice = DirectInputInfo.Controllers.FirstOrDefault(c => c.TestDirectInputDevice(Config.DeviceGUID));
                    }
                }

                return _dInputDevice;
            }
        }
        #endregion

        public string ToShortString()
        {
            return Name + ", Device:" + DeviceIndex.ToString() + ", Player:" + PlayerIndex.ToString();
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(DevicePath))
                return Name + " - Device:" + DeviceIndex.ToString() + ", Player:" + PlayerIndex.ToString() + ", Path:" + DevicePath;

            return Name + " - Device:" + DeviceIndex.ToString() + ", Player:" + PlayerIndex.ToString() + ", Guid:" + (Guid.ToString() ?? "null");
        }

        /// <summary>
        /// Translate EmulationStation Input to SDL Input
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Input GetSdlMapping(InputKey key)
        {
            if (Config == null)
                return null;

            Input input = Config[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            var ctrl = this.SdlController;
            if (ctrl == null)
                return input;

            int axisValue = 1;

            SdlControllerMapping sdlret = null;

            var mapping = ctrl.Mapping;
            if (mapping != null)
            {
                sdlret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Input.Id == input.Id && m.Input.Value == input.Value);

                if (sdlret == null && input.Type == "axis")
                {
                    var invret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Input.Id == input.Id && m.Input.Value == -input.Value);
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
                                sdlret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_LEFT);
                                break;
                            case InputKey.right:
                                sdlret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_RIGHT);
                                break;
                            case InputKey.up:
                                sdlret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_UP);
                                break;
                            case InputKey.down:
                                sdlret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.DPAD_DOWN);
                                break;
                        }
                    }
                    if (input.Name.ToString() == "select" || input.Name.ToString() == "hotkey")
                    {
                        sdlret = mapping.FirstOrDefault(m => m.Input != null && m.Input.Type == input.Type && m.Button == SDL_CONTROLLER_BUTTON.BACK);
                    }
                }
            }

            if (sdlret == null)
            {
                SimpleLogger.Instance.Warning("[InputConfig] ToSdlCode error can't find <input name=\"" + key.ToString() + "\" type=\"" + input.Type + "\" id=\"" + input.Id + "\" value=\"" + input.Value + "\" /> in SDL2 mapping :\r\n" + ctrl.SdlBinding);
                return input;
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

            return GetXInputInput(key);
        }

        /// <summary>
        /// Translate EmulationStation Input to XInput Input
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Input GetXInputInput(InputKey key)
        {
            if (Config == null)
                return null;

            Input input = Config[key];
            if (input == null)
                return null;

            if (input.Type == "key")
                return input;

            if (!this.IsXInputDevice)
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
        /// Translate EmulationStation to XInput Mapping
        /// </summary>
        /// <param name="key"></param>
        /// <param name="revertAxis"></param>
        /// <returns></returns>
        public XINPUTMAPPING GetXInputMapping(InputKey key, bool revertAxis = false)
        {
            if (Config == null)
                return XINPUTMAPPING.UNKNOWN;

            Input input = Config[key];
            if (input == null)
                return XINPUTMAPPING.UNKNOWN;

            if (input.Type == "key")
                return XINPUTMAPPING.UNKNOWN;

            if (!IsXInputDevice)
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

        /// <summary>
        /// Translate EmulationStation input to XInput button flags
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public XInputButtonFlags GetXInputButtonFlags(InputKey key)
        {
            XInputButtonFlags result;
            if (Enum.TryParse<XInputButtonFlags>(GetXInputMapping(key).ToString(), out result))
                return result;

            return XInputButtonFlags.NONE;
        }

        public Input GetDirectInputMapping(InputKey key, bool buttonTrigger = false)
        {
            if (Config == null)
                return null;

            Input input = Config[key];
            if (input == null)
                return null;

            #region dinputToSdl
            string gamecontrollerdb = Path.Combine(Program.AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string dinputGuid = Guid.ToString().Substring(0, 24) + "00000000";
            bool isXinput = this.IsXInputDevice;

            if (File.Exists(gamecontrollerdb))
            {
                try { dinputCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerdb, dinputGuid); }
                catch { }

                if (dinputCtrl != null && dinputCtrl.ButtonMappings != null)
                {
                    Input sdlToDinput = SdlToDirectInput.GetDinputInput(dinputCtrl, key, input, isXinput, buttonTrigger);
                    return sdlToDinput;
                }
            }
            #endregion

            if (SdlWrappedTechID == SdlWrappedTechId.HID)
            {
                var dinput = HidToDirectInput.Instance.FromInput(Guid, input);
                if (dinput != null)
                    return dinput;
            }

            return input;
        }

        public static void SetXinputReversedIndex(List<Controller> controllers)
        {
            try
            {
                var xinputControllers = controllers.Where(x => x.XInput != null).ToList();
                var duplicateGroups = xinputControllers
                .GroupBy(c => c.Guid.ToGuid())
                .Where(g => g.Count() > 1).ToList();

                foreach (var group in duplicateGroups)
                {
                    var pair = group.Take(2).ToList();

                    if (pair.Count == 2)
                    {
                        int c1Index = pair[0].XInput.DeviceIndex;
                        int c2Index = pair[1].XInput.DeviceIndex;

                        pair[0].xIndexReversed = c2Index;
                        pair[1].xIndexReversed = c1Index;
                    }
                }
            }
            catch { }
        }

        public static string GetSystemYmlMappingFile(string emulator, string core, string system, string[] mappingPaths)
        {
            string ret = null;

            foreach (var path in mappingPaths)
            {
                string result = path
                    .Replace("{systempath}", "system")
                    .Replace("{userpath}", "user");

                ret = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), result);

                if (File.Exists(ret))
                    return ret;
            }
            return null;
        }

        public static bool CheckSDL3dll()
        {
            string sdl3Sourcepath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "system", "tools", "SDL3.dll");
            string sdl3Targetpath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "emulationstation", "SDL3.dll");
            if (File.Exists(sdl3Sourcepath) && !File.Exists(sdl3Targetpath))
            {
                try { File.Copy(sdl3Sourcepath, sdl3Targetpath); } catch { }
            }

            if (!File.Exists(sdl3Targetpath))
            {
                SimpleLogger.Instance.Warning("[WARNING] SDL3.dll not found.");
                return false;
            }
            
            return true;
        }

        #region Sdl3Controller
        private Sdl3GameController _sdl3Controller;
        private static bool _sdl3ControllersKnown;

        public Sdl3GameController Sdl3Controller
        {
            get
            {
                EnsureSdl3Controllers();
                return _sdl3Controller;
            }
        }

        private static void EnsureSdl3Controllers()
        {
            if (_sdl3ControllersKnown)
                return;

            if (!CheckSDL3dll())
                return;

            var candidates = Sdl3GameController.GetControllers().ToList();
            if (candidates.Count == 0)
                return;

            _sdl3ControllersKnown = true;

            var pads = Program.Controllers.Where(c => !c.IsKeyboard && c.Config != null).ToList();

            // Precompute path variants (raw, shortened, parent, shortened parent) for SDL3 candidates
            var candidatePaths = candidates.ToDictionary(c => c, c => GetPathVariants(c.Path));

            // Pass 1 : path (authoritative, unique per device)
            foreach (var ctrl in pads)
            {
                var match = FindSdl3ByPath(ctrl, candidates, candidatePaths);
                if (match != null)
                {
                    ctrl._sdl3Controller = match;
                    candidates.Remove(match);
                }
            }

            // Pass 2 : bus + VID/PID + rank pairing for duplicates
            foreach (var group in pads.Where(c => c._sdl3Controller == null)
                .GroupBy(c => new { Bus = GetSdlBusType(c.Guid.ToString()), Vid = (ushort)c.VendorID, Pid = (ushort)c.ProductID }))
            {
                var unresolved = group.OrderBy(c => c.SdlController != null ? c.SdlController.Index : c.DeviceIndex).ToList();
                var sdl3Same = candidates
                    .Where(c => c.VendorId == group.Key.Vid && c.ProductId == group.Key.Pid)
                    .Where(c => { var bus = GetSdlBusType(c.GuidString); return bus == "0000" || bus == group.Key.Bus; })
                    .OrderBy(c => c.EnumerationIndex)
                    .ToList();

                for (int i = 0; i < unresolved.Count && i < sdl3Same.Count; i++)
                {
                    unresolved[i]._sdl3Controller = sdl3Same[i];
                    candidates.Remove(sdl3Same[i]);
                }
            }

            // Pass 3 : name (last resort)
            foreach (var ctrl in pads.Where(c => c._sdl3Controller == null))
            {
                var match = candidates.FirstOrDefault(c => string.Equals(c.Name, ctrl.Name, StringComparison.InvariantCultureIgnoreCase));
                if (match != null)
                {
                    ctrl._sdl3Controller = match;
                    candidates.Remove(match);
                }
            }

            // Pass 4 : elimination - exactly one pad and one candidate left
            var lastPads = pads.Where(c => c._sdl3Controller == null).ToList();
            if (lastPads.Count == 1 && candidates.Count == 1)
            {
                lastPads[0]._sdl3Controller = candidates[0];
                SimpleLogger.Instance.Info("[Controller] SDL3 match by elimination : " + lastPads[0].ToShortString() + " => " + candidates[0].Path);
                candidates.Clear();
            }

            foreach (var ctrl in pads)
            {
                if (ctrl._sdl3Controller != null)
                    SimpleLogger.Instance.Info("[Controller] SDL3 match : " + ctrl.ToShortString() + " => " + ctrl._sdl3Controller.Path);
                else
                    SimpleLogger.Instance.Warning("[Controller] No SDL3 match : " + ctrl.ToShortString());
            }
        }

        private static string GetSdlBusType(string sdlGuid)
        {
            if (string.IsNullOrEmpty(sdlGuid) || sdlGuid.Length < 4)
                return "0000";

            return sdlGuid.Substring(0, 4).ToLowerInvariant();
        }

        private static HashSet<string> GetPathVariants(string path)
        {
            var ret = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            if (string.IsNullOrEmpty(path))
                return ret;

            ret.Add(path);

            // "xinput#0", "Unknown", ... : not device interface paths, nothing to normalize
            if (path.IndexOf('\\') < 0)
                return ret;

            try
            {
                var shorten = InputDevices.ShortenDevicePath(path);
                if (!string.IsNullOrEmpty(shorten))
                    ret.Add(shorten);

                var parent = InputDevices.GetInputDeviceParent(path);
                if (!string.IsNullOrEmpty(parent))
                {
                    ret.Add(parent);

                    var shortenParent = InputDevices.ShortenDevicePath(parent);
                    if (!string.IsNullOrEmpty(shortenParent))
                        ret.Add(shortenParent);
                }
            }
            catch { }

            return ret;
        }

        private static Sdl3GameController FindSdl3ByPath(Controller ctrl, List<Sdl3GameController> candidates, Dictionary<Sdl3GameController, HashSet<string>> candidatePaths)
        {
            var ctrlPaths = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (ctrl.IsXInputDevice && ctrl.XInput != null)
                ctrlPaths.Add("xinput#" + ctrl.XInput.DeviceIndex);

            foreach (var v in GetPathVariants(ctrl.DevicePath))
                ctrlPaths.Add(v);

            if (ctrl.DirectInput != null)
            {
                foreach (var v in GetPathVariants(ctrl.DirectInput.DevicePath))
                    ctrlPaths.Add(v);
            }

            if (ctrlPaths.Count == 0)
                return null;

            // Exact raw-path match first...
            foreach (var c in candidates)
            {
                if (ctrlPaths.Contains(c.Path))
                    return c;
            }

            // ...then normalized-variant overlap
            foreach (var c in candidates)
            {
                if (candidatePaths[c].Overlaps(ctrlPaths))
                    return c;
            }

            return null;
        }
        #endregion
    }

    static class InputExtensions
    {
        private static Dictionary<InputKey, InputKey> revertedAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.joystick1right, InputKey.joystick1left },
            { InputKey.joystick1down, InputKey.joystick1up },
            { InputKey.joystick2right, InputKey.joystick2left },
            { InputKey.joystick2down, InputKey.joystick2up },
        };

        private static Dictionary<InputKey, InputKey> revertedDpadAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.right, InputKey.left },
            { InputKey.down, InputKey.up },
        };

        public static InputKey GetRevertedAxis(this InputKey key, out bool reverted)
        {
            reverted = false;

            InputKey revertedKey;
            if (revertedAxis.TryGetValue(key, out revertedKey))
            {
                key = revertedKey;
                reverted = true;
            }

            return key;
        }

        public static InputKey GetRevertedDpadAxis(this InputKey key, out bool reverted)
        {
            reverted = false;

            InputKey revertedKey;
            if (revertedDpadAxis.TryGetValue(key, out revertedKey))
            {
                key = revertedKey;
                reverted = true;
            }

            return key;
        }

        public static int GetSdlControllerIndex(this Controller ctrl)
        {
            var sdlDev = SdlGameController.GetGameControllerByPath(ctrl.DevicePath);
            return sdlDev != null ? sdlDev.Index : ctrl.DeviceIndex;
        }
    }
}
