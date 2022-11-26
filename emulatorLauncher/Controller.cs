using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    class Controller
    {
        public Controller()
        {
            DeviceIndex = -1;
        }

        public int PlayerIndex { get; set; }
        public int DeviceIndex { get; set; }
        public string Guid { get; set; }
        public string DevicePath { get; set; }
        public string Name { get; set; }
        public int NbButtons { get; set; }
        public int NbHats { get; set; }
        public int NbAxes { get; set; }

        public string GetSdlGuid(SdlVersion version = SdlVersion.SDL2_0_X)
        {
            if (version == SdlVersion.Current)
                return Guid;

            return Guid
                .FromSdlGuidString()
                .ConvertSdlGuid(version)
                .ToSdlGuidString();
        }

        public InputConfig Config { get; set; }

        #region SdlController
        private SdlGameControllers _sdlController;
        private bool _sdlControllerKnown = false;

        public SdlGameControllers SdlController
        {
            get
            {
                if (!_sdlControllerKnown)
                {
                    _sdlControllerKnown = true;

                    if (Name != "Keyboard")
                    {
                        if (!string.IsNullOrEmpty(DevicePath))
                            _sdlController = SdlGameControllers.GetGameControllerByPath(DevicePath);

                        if (_sdlController == null)
                            _sdlController = SdlGameControllers.GetGameController(Guid.FromSdlGuidString());
                    }
                }

                return _sdlController;
            }
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
        private bool _xInputDeviceKnown;

        public bool IsXInputDevice
        {
            get
            {
                if (_xInputDevice != null)
                    return true;

                return Name != "Keyboard" && Config != null && XInputDevice.IsXInputDevice(this.Config.DeviceGUID);
            }
        }

        public XInputDevice XInput
        {
            get
            {
                if (_xInputDeviceKnown == false)
                {
                    _xInputDeviceKnown = true;

                    if (Name == "Keyboard" || !IsXInputDevice)
                        return null;

                    var xinputindex = Program.Controllers
                        .OrderBy(c => c.DeviceIndex)
                        .Where(c => c.IsXInputDevice)
                        .ToList()
                        .IndexOf(this);

                    _xInputDevice = new XInputDevice(xinputindex);
                }

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
    }


}
