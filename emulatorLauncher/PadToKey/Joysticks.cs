using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher.PadToKeyboard
{
    class Joystick
    {
        public int Id { get; private set; }
        public IntPtr SdlJoystick { get; private set; }
        public Controller Controller { get; private set; }
        public JoyInputState OldState { get; set; }
        public JoyInputState State { get; private set; }

        public Joystick(int id, IntPtr joystick, Controller conf)
        {
            Id = id;
            SdlJoystick = joystick;
            Controller = conf;

            State = new JoyInputState();
            OldState = new JoyInputState();
        }

        public void Close()
        {
            if (SdlJoystick != IntPtr.Zero)
                SDL.SDL_JoystickClose(SdlJoystick);

            SdlJoystick = IntPtr.Zero;
        }

        public IEnumerable<Input> GetButtons(int id)
        {
            return Controller.Config.Input.Where(i => i.Type == "button" && i.Id == id);
        }

        public Input GetInput(string type, int id, int value = -1)
        {
            if (type == "hat")
                return Controller.Config.Input.FirstOrDefault(i => i.Type == type && i.Id == id && i.Value == value);

            return Controller.Config.Input.FirstOrDefault(i => i.Type == type && i.Id == id);
        }
    }

    class Joysticks : IEnumerable<Joystick>
    {
        List<Joystick> _joysticks = new List<Joystick>();
        Controller[] _controllers;

        public Joysticks(Controller[] controllers)
        {
            _controllers = controllers;
        }

        public void AddJoystick(int i)
        {
            var instanceId = SDL.SDL_JoystickGetDeviceInstanceID(i);
            if (_joysticks.Any(j => j.Id == instanceId))
                return;

            IntPtr joy = SDL.SDL_JoystickOpen(i);
            var guid = SDL.SDL_JoystickGetGUID(joy);
            var guid2 = SDL.SDL_JoystickGetDeviceGUID(i);
            var name = SDL.SDL_JoystickName(joy);

            Controller conf = null;

            string hidpath = InputDevices.GetInputDeviceParent(SDL.SDL_JoystickPathForIndex(i));
            if (!string.IsNullOrEmpty(hidpath))
                conf = _controllers.FirstOrDefault(cfg => cfg.DevicePath == hidpath);

            if (conf == null)
                conf = _controllers.FirstOrDefault(cfg => cfg.DeviceIndex == i);

            if (conf == null)
                conf = _controllers.FirstOrDefault(cfg => cfg.Config.ProductGuid == guid);

            if (conf == null)
                conf = _controllers.FirstOrDefault(cfg => cfg.Config.DeviceName == name);

            if (conf != null)
            {
                SimpleLogger.Instance.Info("[PadToKey] Add joystick " + conf.ToString());
                _joysticks.Add(new Joystick(instanceId, joy, conf));
            }
            else
                SimpleLogger.Instance.Error("[PadToKey] Unknown joystick " + name);
        }

        public void RemoveJoystick(int which)
        {
            var js = _joysticks.FirstOrDefault(j => j.Id == which);
            if (js != null)
            {
                SDL.SDL_JoystickClose(js.SdlJoystick);
                _joysticks.Remove(js);
            }
        }

        public Joystick this[int which]
        {
            get
            {
                return _joysticks.FirstOrDefault(j => j.Id == which);
            }
        }
        
        #region IEnumerable
        IEnumerator<Joystick> IEnumerable<Joystick>.GetEnumerator()
        {
            return _joysticks.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _joysticks.GetEnumerator();
        }
        #endregion
    }
}
