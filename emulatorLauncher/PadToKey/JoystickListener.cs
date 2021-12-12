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
        public bool ProcessKilled { get; private set; }

        private PadToKey _mapping;
        private Controller[] _inputList;

        private AutoResetEvent _waitHandle;
        private Thread thread;

        public JoystickListener(Controller[] inputList, PadToKey mapping)
        {
            bool joy2Key = Process.GetProcessesByName("JoyToKey").Length > 0;
            if (joy2Key)
                return;

            _inputList = inputList;
            _mapping = mapping;

            if (_mapping != null && _inputList != null && _inputList.Length > 0)
            {
                _waitHandle = new AutoResetEvent(false);
                Start();
            }
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

        public static InputKey RevertedAxis(InputKey key)
        {
            if (key == InputKey.left)
                return InputKey.right;

            if (key == InputKey.right)
                return InputKey.up;

            if (key == InputKey.up)
                return InputKey.down;

            if (key == InputKey.down)
                return InputKey.up;

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
            Joysticks joysticks = new Joysticks(_inputList);

            SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
            SDL.SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL.SDL_JoystickEventState(1);

            int numJoysticks = SDL.SDL_NumJoysticks();
            for (int i = 0; i < numJoysticks; i++)
                joysticks.AddJoystick(i);

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

                                     var joy = joysticks[evt.jaxis.which];
                                     if (joy != null)
                                     {
                                         var axis = joy.GetInput("axis", evt.jaxis.axis);
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
                                                 joy.State.Remove(revertedAxis);
                                                 joy.State.Add(axisName, value, true);
                                             }
                                             else
                                             {
                                                 joy.State.Remove(revertedAxis);
                                                 joy.State.Remove(axisName);
                                             }
                                         }
                                     }
                                }
                                break;

                            case SDL.SDL_EventType.SDL_JOYBUTTONDOWN:
                            case SDL.SDL_EventType.SDL_JOYBUTTONUP:
                                {
                                    var joy = joysticks[evt.jbutton.which];
                                    if (joy != null)
                                    {                                        
                                        foreach (var conf in joy.GetButtons(evt.jbutton.button))
                                        {
                                            if (evt.jbutton.state == SDL.SDL_PRESSED)
                                                joy.State.Add(conf.Name);
                                            else
                                                joy.State.Remove(conf.Name);
                                        }
                                    }
                                }
                                break;

                            case SDL.SDL_EventType.SDL_JOYHATMOTION:
                                {
                                    var joy = joysticks[evt.jhat.which];
                                    if (joy != null)
                                    {
                                        var up = joy.GetInput("hat", evt.jhat.hat, (int) SDL.SDL_HAT_UP);
                                        if (up != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_UP) == SDL.SDL_HAT_UP)
                                                joy.State.Add(up.Name);
                                            else
                                                joy.State.Remove(up.Name);
                                        }

                                        var right = joy.GetInput("hat", evt.jhat.hat, (int)SDL.SDL_HAT_RIGHT);
                                        if (right != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_RIGHT) == SDL.SDL_HAT_RIGHT)
                                                joy.State.Add(right.Name);
                                            else
                                                joy.State.Remove(right.Name);
                                        }

                                        var down = joy.GetInput("hat", evt.jhat.hat, (int)SDL.SDL_HAT_DOWN);
                                        if (down != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_DOWN) == SDL.SDL_HAT_DOWN)
                                                joy.State.Add(down.Name);
                                            else
                                                joy.State.Remove(down.Name);
                                        }

                                        var left = joy.GetInput("hat", evt.jhat.hat, (int)SDL.SDL_HAT_LEFT);
                                        if (left != null)
                                        {
                                            if ((evt.jhat.hatValue & SDL.SDL_HAT_LEFT) == SDL.SDL_HAT_LEFT)
                                                joy.State.Add(left.Name);
                                            else
                                                joy.State.Remove(left.Name);
                                        }
                                    }
                                }
                                break;

                            case SDL.SDL_EventType.SDL_JOYDEVICEREMOVED:
                                joysticks.RemoveJoystick(evt.jdevice.which);
                                break;

                            case SDL.SDL_EventType.SDL_JOYDEVICEADDED:
                                joysticks.AddJoystick(evt.jdevice.which);
                                break;
                        }

                        foreach (var joy in joysticks)
                        {
                            if (joy.State == joy.OldState)
                                continue;

                            ProcessJoystickState(joy.Controller.PlayerIndex - 1, joy.State, joy.OldState);
                            
                            joy.OldState = joy.State.Clone();                            
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

        private void ProcessJoystickState(int playerIndex, JoyInputState keyState, JoyInputState prevState)
        {
            //Debug.WriteLine("ProcessJoystickState : " + keyState.ToString() + " - OldState : " + prevState.ToString());

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

                    if (keyMap.ControllerIndex >= 0 && keyMap.ControllerIndex != playerIndex)
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

                    if (keyMap.ControllerIndex >= 0 && keyMap.ControllerIndex != playerIndex)
                        continue;

                    if (mapping != null && mapping[keyMap.Name] != null)
                        continue;                 

                    SendInput(keyState, prevState, hWndProcess, process, keyMap);
                }
            }
        }

        private void SendInput(JoyInputState newState, JoyInputState oldState, IntPtr hWndProcess, string process, PadToKeyInput input)
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
                   // Debug.WriteLine("STOP MOUSE MOVE " + input.Code);

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

                   // Debug.WriteLine("Mouse @ " + _mouseMove.ToString());

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

                  //  Debug.WriteLine("Mouse @ " + _mouseMove.ToString());

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
                        const int WM_CLOSE = 0x0010;
                        User32.SendMessage(hWndProcess, WM_CLOSE, 0, 0);
                        ProcessKilled = true;
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
                        ProcessKilled = true;
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
        }

        private void OnMouseTimerProc(object state)
        {
            JoystickListener t = (JoystickListener)state;

            int x = (int) EaseMouse((double) t._mouseMove.X);;
            int y = (int) EaseMouse((double) t._mouseMove.Y);;
            SendKey.MoveMouseBy(x, y);
        }

        private void SendKeyMap(PadToKeyInput input, JoyInputState oldState, JoyInputState newState, string processName)
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


        private void KillProcess(IntPtr hWndProcess, string process)
        {
            uint pid;
            User32.GetWindowThreadProcessId(hWndProcess, out pid);

            try
            {
                Process p = Process.GetProcessById((int)pid);
                p.Kill();
                ProcessKilled = true;
            }
            catch { }
        }

        string GetActiveProcessFileName(out bool isDesktop, out IntPtr hMainWnd)
        {
            isDesktop = false;
            hMainWnd = IntPtr.Zero;

            IntPtr hwnd = User32.GetForegroundWindow();
            if (hwnd != User32.GetDesktopWindow())
                hMainWnd = hwnd;

            uint pid;
            User32.GetWindowThreadProcessId(hwnd, out pid);

            try
            {
                Process p = Process.GetProcessById((int)pid);

                if (p != null && p.MainWindowHandle != IntPtr.Zero && p.MainWindowHandle != User32.GetDesktopWindow())
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
                    int style = User32.GetWindowLong(hwnd, GWL.STYLE);
                    if (style == -1778384896)
                        isDesktop = true;
                }

                return fn;
            }
            catch { }

            return null;
        }
    }
}
