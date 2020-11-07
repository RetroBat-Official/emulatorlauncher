using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace emulatorLauncher.PadToKeyboard
{
    class JoystickListener : IDisposable
    {
        private PadToKey _mapping;
        private InputConfig[] _inputList;

        private AutoResetEvent _waitHandle;
        private Thread thread;

        public JoystickListener(InputConfig[] inputList, PadToKey mapping)
        {
            bool joy2Key = Process.GetProcessesByName("JoyToKey").Length > 0;
            if (joy2Key)
                return;

            _inputList = inputList;
            _mapping = mapping;

            _waitHandle = new AutoResetEvent(false);

            if (_mapping != null && _inputList != null && _inputList.Length > 0)
                Start();
        }

        public void Start()
        {
            if (thread != null)
                return;

            thread = new Thread(this.DoWork);
            thread.IsBackground = true;
            thread.Start();
        }

        public void Dispose()
        {
            if (_waitHandle != null)
            {
                _waitHandle.Set();
                thread = null;
            }
        }

        class InputKeyInfo
        {
            public InputKeyInfo()
            {
            }

            public InputKeyInfo(InputKey k, int v = 1, bool isAxis = false)
            {
                _keys.Add((int)k, new InputValue(v, false));
            }

            public void Add(InputKey k, int v = 1, bool isAxis = false)
            {
                _keys.Remove((int)k);
                _keys.Add((int)k, new InputValue(v, isAxis));
            }

            private bool _isAxis;

            public void Remove(InputKey key)
            {
                _keys.Remove((int)key);
            }

            class InputValue
            {
                public InputValue(int val, bool isAxis = false)
                {
                    Value = val;
                    IsAxis = isAxis;
                }

                public int Value { get; set; }
                public bool IsAxis { get; set; }

                public override string ToString()
                {
                    if (IsAxis)
                        return "axis:"+Value;

                    return Value.ToString();
                }
            };

            private Dictionary<int, InputValue> _keys = new Dictionary<int, InputValue>();

            public static InputKeyInfo operator | (InputKeyInfo a, InputKeyInfo b)
            {
                InputKeyInfo ret = new InputKeyInfo();
                ret._keys = new Dictionary<int, InputValue>(a._keys);

                foreach (var kb in b._keys)
                    ret._keys[kb.Key] = kb.Value;
                
                return ret;
            }

            public override bool Equals(object obj)
            {
                InputKeyInfo c1 = this;
                InputKeyInfo c2 = obj as InputKeyInfo;
                if (c2 == null)
                    return false;

                if (c1._keys.Count != c2._keys.Count)
                    return false;

                foreach (var key in c1._keys)
                {
                    InputValue value;
                    if (!c2._keys.TryGetValue(key.Key, out value))
                        return false;

                    if (value.Value != key.Value.Value || value.IsAxis != key.Value.IsAxis)
                        return false;
                }

                return true;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public static bool operator ==(InputKeyInfo c1, InputKeyInfo c2)
            {
                return !object.ReferenceEquals(c1, null) && !object.ReferenceEquals(c2, null) && c1.Equals(c2);
            }

            public static bool operator !=(InputKeyInfo c1, InputKeyInfo c2)
            {
                if (c1 == null && c2 != null)
                    return true;

                return !c1.Equals(c2);
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                foreach (var kb in _keys)
                {
                    if (sb.Length > 0)
                        sb.Append(" | ");

                    sb.Append(((InputKey)kb.Key).ToString() + ":" + kb.Value);
                }

                return sb.ToString();
            }
            
            public bool HasFlag(InputKey k)
            {
                return FromInputKey(k).Count > 0;
            }

            public int GetMouseInputValue(InputKey k)
            {
                InputValue newValue;
                if (!_keys.TryGetValue((int)k, out newValue))
                    return 0;

                if (k == InputKey.rightanalogright || k == InputKey.rightanalogdown || k == InputKey.leftanalogright || k == InputKey.leftanalogdown)
                    return Math.Abs(newValue.Value);

                return -Math.Abs(newValue.Value);
            }

            Dictionary<int, InputValue> FromInputKey(InputKey k)
            {
                Dictionary<int, InputValue> ret = new Dictionary<int, InputValue>();

                int kz = 0;

                foreach(var key in _keys)
                {
                    if (((int) k & key.Key) == key.Key)
                    {
                        ret[key.Key] = key.Value;
                        kz |= key.Key;
                    }
                }

                if (kz != (int)k)
                    return new Dictionary<int, InputValue>();

                return ret;
            }

            public bool HasNewInput(InputKey k, InputKeyInfo old, bool checkAxisChanged = false)
            {
                var newValues = FromInputKey(k);
                if (newValues.Count == 0)
                    return false;

                var oldValues = old.FromInputKey(k);
                if (oldValues.Count == 0)
                {
                    if (newValues.Any(v => v.Value.IsAxis))
                    {
                        foreach(var nv in newValues.Where(v => v.Value.IsAxis))
                            oldValues[nv.Key] = new InputValue(0, true);
                    }
                    else
                        return true;
                }
                
                foreach (var ov in oldValues)
                {
                    if (!newValues.ContainsKey(ov.Key))
                        return false;

                    var oldVal = oldValues[ov.Key];
                    var newVal = newValues[ov.Key];

                    if (oldVal.IsAxis)
                    {
                        if (checkAxisChanged)
                        {
                            if (oldVal.Value != newVal.Value)
                                return true;
                        }

                        const int DEADZONE = 20000;

                        bool oldOutOfDeadZone = Math.Abs(oldVal.Value) >= DEADZONE;
                        bool newOutOfDeadZone = Math.Abs(newVal.Value) >= DEADZONE;

                        if (newOutOfDeadZone && !oldOutOfDeadZone)
                            return true;
                    }
                    else if (oldVal.Value != newVal.Value)
                        return true;
                }

                return false;                
            }

            public InputKeyInfo Clone()
            {
                InputKeyInfo clone = new InputKeyInfo();
                clone._keys = new Dictionary<int, InputValue>(_keys);
                return clone;
            }
        }

        public static InputKey RevertedAxis(InputKey key)
        {
            if (key == InputKey.leftanalogleft)
                return InputKey.leftanalogright;

            else if (key == InputKey.leftanalogup)
                return InputKey.leftanalogdown;

            if (key == InputKey.rightanalogleft)
                return InputKey.rightanalogright;

            if (key == InputKey.rightanalogup)
                return InputKey.rightanalogdown;

            return key;
        }

        public void DoWork()
        {
            JoyInputs joysticks = new JoyInputs();

            SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
            SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_JoystickEventState(1);

            int numJoysticks = SDL.SDL_NumJoysticks();
            for (int i = 0; i < numJoysticks; i++)
                AddJoystick(joysticks, i);

            InputKeyInfo oldIState = new InputKeyInfo();
            InputKeyInfo istate = new InputKeyInfo();

            while (true)
            {
                if (_waitHandle.WaitOne(1))
                    break;

                try
                {
                    SDL.SDL_Event evt;
                    if (SDL.SDL_PollEvent(out evt) != 0)
                    {
                        if (evt.type == SDL.SDL_EventType.SDL_QUIT)
                            break;

                        switch (evt.type)
                        {
                            case SDL.SDL_EventType.SDL_JOYAXISMOTION:
                                {                                   
                                     const int DEADZONE = 500;
                                     int initialValue = 0;

                                     int normValue = 0;

                                     if (Math.Abs(evt.jaxis.axisValue - initialValue) > DEADZONE)
                                     {
                                         if (evt.jaxis.axisValue - initialValue > 0)
                                             normValue = 1;
                                         else
                                             normValue = -1;
                                     }

                                     var axis = joysticks.FindInputMapping(evt.jaxis.which, "axis", evt.jaxis.axis);
                                     if (axis != null)
                                     {
                                         var axisName = axis.Name;
                                         var revertedAxis = RevertedAxis(axisName);
                                         int value = evt.jaxis.axisValue;

                                         if (value != 0 && (Math.Abs(value) / value) == -axis.Value)
                                         {
                                             if (revertedAxis != axisName)
                                             {
                                                 axisName = revertedAxis;
                                                 revertedAxis = axis.Name;
                                                 value = -value;
                                             }
                                             else
                                             {
                                                 normValue = 0;
                                                 value = 0;
                                             }
                                         }

                                         if (normValue != 0)
                                         {
                                             istate.Remove(revertedAxis);
                                             istate.Add(axisName, value, true);
                                         }
                                         else
                                         {
                                             istate.Remove(revertedAxis);
                                             istate.Remove(axisName);
                                         }
                                     }                                  
                                }
                                break;

                            case SDL.SDL_EventType.SDL_JOYBUTTONDOWN:
                            case SDL.SDL_EventType.SDL_JOYBUTTONUP:
                                {
                                    var js = joysticks.FirstOrDefault(j => j.Id == evt.jbutton.which);
                                    if (js != null)
                                    {
                                        foreach (var conf in js.Config.Input.Where(i => i.Type == "button" && i.Id == evt.jbutton.button))
                                        {
                                            if (evt.jbutton.state == SDL.SDL_PRESSED)
                                                istate.Add(conf.Name);
                                            else
                                                istate.Remove(conf.Name);
                                        }
                                    }
                                }
                                break;

                            case SDL.SDL_EventType.SDL_JOYHATMOTION:
                                {
                                    var js = joysticks.FirstOrDefault(j => j.Id == evt.jhat.which);
                                    if (js != null)
                                    {
                                        var up = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_UP);
                                        if (up != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_UP) == SDL.SDL_HAT_UP)
                                                istate.Add(up.Name);
                                            else
                                                istate.Remove(up.Name);
                                        }

                                        var right = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_RIGHT);
                                        if (right != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_RIGHT) == SDL.SDL_HAT_RIGHT)
                                                istate.Add(right.Name);
                                            else
                                                istate.Remove(right.Name);
                                        }

                                        var down = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_DOWN);
                                        if (down != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_DOWN) == SDL.SDL_HAT_DOWN)
                                                istate.Add(down.Name);
                                            else
                                                istate.Remove(down.Name);
                                        }

                                        var left = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_LEFT);
                                        if (left != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_LEFT) == SDL.SDL_HAT_LEFT)
                                                istate.Add(left.Name);
                                            else
                                                istate.Remove(left.Name);
                                        }
                                    }
                                }
                                break;

                            case SDL.SDL_EventType.SDL_JOYDEVICEREMOVED:
                                {
                                    var js = joysticks.FirstOrDefault(j => j.Id == evt.jdevice.which);
                                    if (js != null)
                                    {
                                        SDL.SDL_JoystickClose(js.SdlJoystick);
                                        joysticks.Remove(js);
                                    }
                                }

                                break;

                            case SDL.SDL_EventType.SDL_JOYDEVICEADDED:
                                if (!joysticks.Any(j => j.Id == evt.jdevice.which))
                                    AddJoystick(joysticks, evt.jdevice.which);
                                break;
                        }

                        if (istate != oldIState)
                        {
                            Debug.WriteLine("State : " + istate.ToString() + " - OldState : " + oldIState.ToString());

                            //istate.HasNewInput(InputKey.leftanalogleft, oldIState, true);

                            ProcessJoystickState(istate, oldIState);

                            oldIState = istate.Clone();                            
                        }

                        Thread.Sleep(1);
                    }
                }
                catch { }
            }

            foreach(var joy in joysticks)
                SDL.SDL_JoystickClose(joy.SdlJoystick);
            
            SDL.SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_Quit();
        }

        private void ProcessJoystickState(InputKeyInfo keyState, InputKeyInfo prevState)
        {
            IntPtr hWndProcess;
            bool isDesktop;
            string process = GetActiveProcessFileName(out isDesktop, out hWndProcess);

            var mapping = _mapping[process];
            if (mapping != null)
            {
                foreach (var keyMap in mapping.Input)
                {
                    if (!keyMap.IsValid())
                        continue;

                    SendInput(keyState, prevState, hWndProcess, process, keyMap);
                }
            }

            var commonMapping = _mapping["*"];
            if (commonMapping != null)
            {
                foreach (var keyMap in commonMapping.Input)
                {
                    if (!keyMap.IsValid())
                        continue;

                    if (mapping != null && mapping[keyMap.Name] != null)
                        continue;

                    SendInput(keyState, prevState, hWndProcess, process, keyMap);
                }
            }
        }

        private void SendInput(InputKeyInfo newState, InputKeyInfo oldState, IntPtr hWndProcess, string process, PadToKeyInput input)
        {
            if (input.Type == PadToKeyType.Mouse && (input.Code == "CLICK" || input.Code == "RCLICK" || input.Code == "MCLICK"))
            {
                if (oldState.HasNewInput(input.Name, newState)) // Released
                {
                    if (input.Code == "CLICK")
                        SendKey.SendMouseInput(SendKey.MouseInput.Click, false);
                    else if (input.Code == "RCLICK")
                        SendKey.SendMouseInput(SendKey.MouseInput.RClick, false);
                    else if (input.Code == "MCLICK")
                        SendKey.SendMouseInput(SendKey.MouseInput.MClick, false);
                }
                else if (newState.HasNewInput(input.Name, oldState)) // Pressed
                {
                    if (input.Code == "CLICK")
                        SendKey.SendMouseInput(SendKey.MouseInput.Click, true);
                    else if (input.Code == "RCLICK")
                        SendKey.SendMouseInput(SendKey.MouseInput.RClick, true);
                    else if (input.Code == "MCLICK")
                        SendKey.SendMouseInput(SendKey.MouseInput.MClick, true);
                }

                return;
            }
            
            if (input.Type == PadToKeyType.Mouse && (input.Code == "X" || input.Code == "Y"))
            {
                if (!newState.HasFlag(input.Name) && !newState.HasFlag(RevertedAxis(input.Name)))
                {
                    Debug.WriteLine("STOP MOUSE MOVE " + input.Code);

                    // Stop 
                    if (input.Code == "X")
                        _mouseMove.X = 0;
                    else
                        _mouseMove.Y = 0;

                    if (_mouseMove.IsEmpty && _mouseTimer != null)
                    {
                        _mouseTimer.Dispose();
                        _mouseTimer = null;
                    }
                }
                else if (newState.HasNewInput(input.Name, oldState, true))
                {
                    // Moving
                    if (input.Code == "X")
                        _mouseMove.X = newState.GetMouseInputValue(input.Name);
                    else
                        _mouseMove.Y = newState.GetMouseInputValue(input.Name);

                    Debug.WriteLine("Mouse @ " + _mouseMove.ToString());

                    if (_mouseMove.IsEmpty)
                    {
                        if (_mouseTimer != null)
                            _mouseTimer.Dispose();

                        _mouseTimer = null;
                    }
                    else if (_mouseTimer == null)
                        _mouseTimer = new System.Threading.Timer(new TimerCallback(OnMouseTimerProc), this, 0, 1);
                }
                else if (newState.HasNewInput(RevertedAxis(input.Name), oldState, true))
                {
                    // Moving
                    if (input.Code == "X")
                        _mouseMove.X = newState.GetMouseInputValue(RevertedAxis(input.Name));
                    else
                        _mouseMove.Y = newState.GetMouseInputValue(RevertedAxis(input.Name));

                    Debug.WriteLine("Mouse @ " + _mouseMove.ToString());

                    if (_mouseMove.IsEmpty)
                    {
                        if (_mouseTimer != null)
                            _mouseTimer.Dispose();

                        _mouseTimer = null;
                    }
                    else if (_mouseTimer == null)
                        _mouseTimer = new System.Threading.Timer(new TimerCallback(OnMouseTimerProc), this, 0, 5);
                }

                return;
            }

            if (input.Key != null && (input.Key.StartsWith("(") || input.Key.StartsWith("{")))
            {
                if (newState.HasNewInput(input.Name, oldState))
                {
                    if (input.Keys != 0)
                    {
                        if (process == null)
                            SimpleLogger.Instance.Info("SendKey : " + input.Key + " to <unknown process>");
                        else
                            SimpleLogger.Instance.Info("SendKey : " + input.Key + " to " + process);
                    }

                    if (input.Key == "(%{CLOSE})" && hWndProcess != IntPtr.Zero)
                    {
                        SendMessage(hWndProcess, WM_CLOSE, 0, 0);
                    }
                    else if (input.Key == "(%{KILL})" && hWndProcess != IntPtr.Zero)
                    {
                        KillProcess(hWndProcess, process);
                    }
                    else if (input.Key == "(%{F4})" && process == "emulationstation")
                    {
                        SendKey.Send(Keys.Alt, true);
                        SendKey.Send(Keys.F4, true);
                        SendKey.Send(Keys.Alt, false);
                        SendKey.Send(Keys.F4, false);
                    }
                    else
                        SendKeys.SendWait(input.Key);
                }
            }
            else if (input.Keys != 0 || input.ScanCodes.Length != 0)
                SendKeyMap(input, oldState, newState, process);
        }

        private System.Threading.Timer _mouseTimer = null;
        private System.Drawing.Point _mouseMove = new System.Drawing.Point();

        private double EaseMouse(double x)
        {
            double v = x / 32768.0f;

            if (v < 0)
                return -(1 - Math.Sqrt(1 - Math.Pow(v, 2))) * 24.0;

            return (1 - Math.Sqrt(1 - Math.Pow(v, 2))) * 24.0;
            // return (v*v*v) * 24.0;
        }

        private void OnMouseTimerProc(object state)
        {
            JoystickListener t = (JoystickListener)state;

            int x = (int) EaseMouse((double) t._mouseMove.X);;
            int y = (int) EaseMouse((double) t._mouseMove.Y);;
            SendKey.MoveMouseBy(x, y);
        }

        private void SendKeyMap(PadToKeyInput input, InputKeyInfo oldState, InputKeyInfo newState, string processName)
        {
            if (oldState.HasNewInput(input.Name, newState))
            {
                if (processName == null)
                    SimpleLogger.Instance.Info("SendKey : Release '" + input.Keys + "' to <unknown process>");
                else
                    SimpleLogger.Instance.Info("SendKey : Release '" + input.Keys + "' to " + processName);

                if (input.ScanCodes.Length != 0)
                {
                    foreach(uint sc in input.ScanCodes)
                        SendKey.SendScanCode(sc, false);
                }
                else if (input.Keys != Keys.None)
                    SendKey.Send(input.Keys, false);
            }
            else if (newState.HasNewInput(input.Name, oldState))
            {
                if (processName == null)
                    SimpleLogger.Instance.Info("SendKey : Press '" + input.Keys + "' to <unknown process>");
                else
                    SimpleLogger.Instance.Info("SendKey : Press '" + input.Keys + "' to " + processName);

                if (input.ScanCodes.Length != 0)
                {
                    foreach (uint sc in input.ScanCodes)
                        SendKey.SendScanCode(sc, true);
                }
                else if (input.Keys != Keys.None)
                    SendKey.Send(input.Keys, true);
            }
        }
        
        private void AddJoystick(JoyInputs joysticks, int i)
        {
            IntPtr joy = SDL.SDL_JoystickOpen(i);
            var guid = SDL.SDL_JoystickGetGUID(joy);
            var guid2 = SDL.SDL_JoystickGetDeviceGUID(i);
            var name = SDL.SDL_JoystickName(joy);

            var conf = _inputList.FirstOrDefault(cfg => cfg.ProductGuid == guid);
            if (conf == null)
                conf = _inputList.FirstOrDefault(cfg => cfg.DeviceName == name);

            if (conf != null)
                joysticks.Add(new JoyInput(SDL.SDL_JoystickInstanceID(joy), joy, conf));
        }

        const int WM_CLOSE = 0x0010;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetDesktopWindow")]
        static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool IsChild(IntPtr hwndParent, IntPtr hwnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);


        private void KillProcess(IntPtr hWndProcess, string process)
        {
            uint pid;
            GetWindowThreadProcessId(hWndProcess, out pid);

            try
            {
                Process p = Process.GetProcessById((int)pid);
                p.Kill();
            }
            catch { }
        }

        string GetActiveProcessFileName(out bool isDesktop, out IntPtr hMainWnd)
        {
            isDesktop = false;
            hMainWnd = IntPtr.Zero;

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd != GetDesktopWindow())
                hMainWnd = hwnd;

            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);

            try
            {
                Process p = Process.GetProcessById((int)pid);

                if (p != null && p.MainWindowHandle != IntPtr.Zero && p.MainWindowHandle != GetDesktopWindow())
                    hMainWnd = p.MainWindowHandle;

                string fn = null;

                try
                {
                    fn = Path.GetFileNameWithoutExtension(p.MainModule.FileName).ToLower();
                }
                catch
                {
                    fn = p.ProcessName.ToLower();
                }

                if (fn == "explorer")
                {
                    int style = GetWindowLong(hwnd, -16);
                    if (style == -1778384896)
                        isDesktop = true;
                }

                return fn;
            }
            catch { }

            return null;
        }

        class JoyInput
        {
            public int Id { get; private set; }
            public IntPtr SdlJoystick { get; private set; }
            public InputConfig Config { get; private set; }

            public JoyInput(int id, IntPtr joystick, InputConfig conf)
            {
                Id = id;
                SdlJoystick = joystick;
                Config = conf;
            }
        }

        class JoyInputs : List<JoyInput>
        {

            public Input FindInputMapping(int which, string type, int id, int value = -1)
            {
                var js = this.FirstOrDefault(j => j.Id == which);
                if (js == null)
                    return null;

                if (type == "hat")
                    return js.Config.Input.FirstOrDefault(i => i.Type == type && i.Id == id && i.Value == value);
                
                return js.Config.Input.FirstOrDefault(i => i.Type == type && i.Id == id);
            }

        }
    }
}
