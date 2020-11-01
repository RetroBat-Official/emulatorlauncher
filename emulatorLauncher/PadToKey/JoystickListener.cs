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

            InputKey oldState = (InputKey)0;
            InputKey state = (InputKey)0;

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
                                    /*
                                     const int DEADZONE = 23000;
                                     int initialValue = 0;

                                     int normValue = 0;
                                     if (Math.Abs(evt.jaxis.axisValue - initialValue) > DEADZONE) // batocera
                                     {
                                         if (evt.jaxis.axisValue - initialValue > 0) // batocera
                                             normValue = 1;
                                         else
                                             normValue = -1;
                                     }

                                     var axis = joysticks.FindInputMapping(evt.jaxis.which, "axis", evt.jaxis.axis);
                                     if (axis != null)
                                     {
                                         if (normValue == 1)
                                             state |= axis.Name;
                                         else
                                             state &= ~axis.Name;
                                     }
                                  */
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
                                                state |= conf.Name;
                                            else
                                                state &= ~conf.Name;
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
                                                state |= up.Name;
                                            else
                                                state &= ~up.Name;
                                        }

                                        var right = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_RIGHT);
                                        if (right != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_RIGHT) == SDL.SDL_HAT_RIGHT)
                                                state |= right.Name;
                                            else
                                                state &= ~right.Name;
                                        }

                                        var down = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_DOWN);
                                        if (down != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_DOWN) == SDL.SDL_HAT_DOWN)
                                                state |= down.Name;
                                            else
                                                state &= ~down.Name;
                                        }

                                        var left = js.Config.Input.FirstOrDefault(i => i.Type == "hat" && i.Id == evt.jhat.hat && i.Value == SDL.SDL_HAT_LEFT);
                                        if (left != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_LEFT) == SDL.SDL_HAT_LEFT)
                                                state |= left.Name;
                                            else
                                                state &= ~left.Name;
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

                        if (state != oldState)
                        {
                            ProcessJoystickState(state, oldState);
                            oldState = state;
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

        private void ProcessJoystickState(InputKey keyState, InputKey prevState)
        {
            IntPtr hWndProcess;
            bool isDesktop;
            string process = GetActiveProcessFileName(out isDesktop, out hWndProcess);

            var mapping = _mapping[process];
            if (mapping != null)
            {
                foreach (var keyMap in mapping.Input)
                {
                    if (string.IsNullOrEmpty(keyMap.Key) && keyMap.ScanCodes.Length == 0)
                        continue;

                    if (keyMap.Key != null && (keyMap.Key.StartsWith("(") || keyMap.Key.StartsWith("{")))
                    {
                        if (!prevState.HasFlag(keyMap.Name) && keyState.HasFlag(keyMap.Name))
                        {
                            if (keyMap.Keys != 0)
                            {
                                if (process == null)
                                    SimpleLogger.Instance.Info("SendKey : " + keyMap.Key + " to <unknown process>");
                                else
                                    SimpleLogger.Instance.Info("SendKey : " + keyMap.Key + " to " + process);
                            }

                            if (keyMap.Key == "(%{CLOSE})" && hWndProcess != IntPtr.Zero)
                            {
                                SendMessage(hWndProcess, WM_CLOSE, 0, 0);
                            }
                            else if (keyMap.Key == "(%{F4})" && process == "emulationstation")
                            {
                                SendKey.Send(Keys.Alt, true);
                                SendKey.Send(Keys.F4, true);
                                SendKey.Send(Keys.Alt, false);
                                SendKey.Send(Keys.F4, false);
                            }
                            else
                                SendKeys.SendWait(keyMap.Key);
                        }
                    }
                    else if (keyMap.Keys != 0 || keyMap.ScanCodes.Length != 0)
                        SendKeyMap(keyMap, prevState, keyState, process);
                }
            }

            var commonMapping = _mapping["*"];
            if (commonMapping != null)
            {
                foreach (var keyMap in commonMapping.Input)
                {
                    if (string.IsNullOrEmpty(keyMap.Key) && string.IsNullOrEmpty(keyMap.Code))
                        continue;

                    if (mapping != null && mapping[keyMap.Name] != null)
                        continue;

                    if (keyMap.Key != null && (keyMap.Key.StartsWith("(") || keyMap.Key.StartsWith("{")))
                    {
                        if (!prevState.HasFlag(keyMap.Name) && keyState.HasFlag(keyMap.Name))
                        {

                            if (keyMap.Keys != 0)
                            {
                                if (process == null)
                                    SimpleLogger.Instance.Info("SendKey : " + keyMap.Key + " to <unknown process>");
                                else
                                    SimpleLogger.Instance.Info("SendKey : " + keyMap.Key + " to " + process);
                            }

                            if (keyMap.Key == "(%{CLOSE})" && hWndProcess != IntPtr.Zero)
                            {
                                SendMessage(hWndProcess, WM_CLOSE, 0, 0);
                            }
                            else if (keyMap.Key == "(%{F4})" && process == "emulationstation")
                            {
                                SendKey.Send(Keys.Alt, true);
                                SendKey.Send(Keys.F4, true);
                                SendKey.Send(Keys.Alt, false);
                                SendKey.Send(Keys.F4, false);
                            }
                            else
                                SendKeys.SendWait(keyMap.Key);
                        }

                    }
                    else if (keyMap.Keys != 0 || keyMap.ScanCodes.Length != 0)
                        SendKeyMap(keyMap, prevState, keyState, process);
                }
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

        private void SendKeyMap(PadToKeyInput input, InputKey prevState, InputKey keyState, string processName)
        {
            if (prevState.HasFlag(input.Name) && !keyState.HasFlag(input.Name))
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
            else if (!prevState.HasFlag(input.Name) && keyState.HasFlag(input.Name))
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
