using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace EmulatorLauncher
{
    partial class ArcadeDuckGenerator : Generator
    {
        /// <summary>
        /// Lightgun and trackball ports use a Raw Input mouse device ("NativeRawInputDevice" input mode) :
        /// - aiming / trackball motion is read from [ArcadeControllerPortN] PhysicalDevice = "RawInput:" + Raw Input device name,
        /// - buttons are bound as "RawMouse-N/ButtonX", N being the slot ArcadeDuck gives to the device (win32_raw_input_source.cpp).
        /// Pad bound axes are not usable for aiming in ArcadeDuck (half axis values only), so a mouse is always used when no gun is available.
        /// </summary>
        private class ArcadeDuckPointerDevices
        {
            private readonly List<RawLightgun> _guns;
            private readonly List<RawLightgun> _mice;
            private readonly List<string> _rawMiceSlots;
            private readonly bool _invert;
            private int _lightgunPorts;
            private int _trackballPorts;
            private bool _rawInputUsed;
            private bool _sindenUsed;
            private readonly int _dolphinBarWiimotes;
            private int _dolphinBarIndex;

            public ArcadeDuckPointerDevices(int lightgunPortCount)
            {
                var all = RawLightgun.GetRawLightguns();

                _mice = all.Where(g => g.Type == RawLighGunType.Mouse).ToList();
                _guns = new List<RawLightgun>();

                if (Program.SystemConfig.getOptBoolean("use_guns"))
                {
                    _guns = all.Where(g => g.Type != RawLighGunType.Mouse).ToList();

                    if (Program.SystemConfig.getOptBoolean("one_gun"))
                        _guns = _guns.Take(1).ToList();
                    else if (Program.SystemConfig.getOptBoolean("arcadeduck_gun_switch") && _guns.Count > 1)
                    {
                        var first = _guns[0];
                        _guns[0] = _guns[1];
                        _guns[1] = first;
                    }
                }

                // DolphinBar Wiimotes actually used by lightgun ports
                _dolphinBarWiimotes = _guns.Take(lightgunPortCount).Count(g => g.Type == RawLighGunType.MayFlashWiimote);

                _invert = Program.SystemConfig.getOptBoolean("gun_invert");
                _rawMiceSlots = EnumerateRawMice();
            }

            public void ConfigurePort(IniFile ini, string section, ArcadeDuckDatabase.Port port, int portIndex)
            {
                RawLightgun device = null;
                bool isGun = false;

                if (port.Type == "lightgun")
                {
                    if (_lightgunPorts < _guns.Count)
                    {
                        device = _guns[_lightgunPorts];
                        isGun = true;
                    }
                    else if (_lightgunPorts == 0 && _mice.Count > 0)
                        device = _mice[0];

                    _lightgunPorts++;
                }
                else if (port.Type == "trackball")
                {
                    if (_trackballPorts < _mice.Count)
                        device = _mice[_trackballPorts];

                    _trackballPorts++;
                }

                if (device == null)
                {
                    SimpleLogger.Instance.Info("[ArcadeDuck] Port " + (portIndex + 1) + " (" + port.Type + ") : no mouse or gun available.");
                    return;
                }

                int slot = GetRawMouseSlot(device.DevicePath);
                if (slot < 0)
                {
                    SimpleLogger.Instance.Warning("[ArcadeDuck] Port " + (portIndex + 1) + " : Raw Input device not found : " + device.DevicePath);
                    return;
                }

                SimpleLogger.Instance.Info("[ArcadeDuck] Port " + (portIndex + 1) + " (" + port.Type + ") : " + device.Name + " (" + device.Type + ") => RawMouse-" + slot);

                _rawInputUsed = true;
                if (device.Type == RawLighGunType.SindenLightgun)
                    _sindenUsed = true;

                string rawMouse = "RawMouse-" + slot + "/";

                ini.WriteValue(section, "InputMode", "NativeRawInputDevice");
                ini.WriteValue(section, "PhysicalDevice", "RawInput:" + device.DevicePath);

                if (port.Type == "lightgun")
                {
                    ini.AppendValue(section, "Trigger", rawMouse + (_invert ? "Button1" : "Button0"));
                    ini.AppendValue(section, "Reload", rawMouse + (_invert ? "Button0" : "Button1"));

                    string crosshair = Program.SystemConfig["arcadeduck_crosshair"];
                    bool showCrosshair = crosshair == "1" || (string.IsNullOrEmpty(crosshair) && !isGun);
                    ini.WriteValue(section, "CrosshairEnabled", showCrosshair ? "true" : "false");

                    if (isGun && device.Type == RawLighGunType.MayFlashWiimote)
                        ConfigureDolphinBarKeys(ini, section, port.Layout);
                }
                else
                {
                    ini.AppendValue(section, "Button1", rawMouse + "Button0");
                    ini.AppendValue(section, "Button2", rawMouse + "Button1");
                }
            }

            /// <summary>
            /// DolphinBar (mode 1 or 2) sends keyboard keys for the Wiimote buttons. ArcadeDuck reads all keyboards as one (Qt host keyboard),
            /// so each Wiimote uses different D-pad keys :
            /// - first Wiimote : Right = Start, Left = Coin
            /// - second Wiimote : Up = Start, Down = Coin
            /// Judge Dredd secondary weapon : D-pad Down with one Wiimote, else button 1 (mode 1) or + (mode 2) for player 1, button 2 (mode 1 only) for player 2.
            /// Keyboard keys 1-4 / 5-8 stay bound for start / coin.
            /// </summary>
            private void ConfigureDolphinBarKeys(IniFile ini, string section, string layout)
            {
                bool first = _dolphinBarIndex == 0;
                _dolphinBarIndex++;

                // Only 2 sets of D-pad keys are available
                if (_dolphinBarIndex > 2)
                    return;

                ini.AppendValue(section, "Start", "Keyboard/" + (first ? "Right" : "Up"));
                ini.AppendValue(section, "Coin", "Keyboard/" + (first ? "Left" : "Down"));

                if (layout != "jdredd")
                    return;

                bool normalMode = Program.SystemConfig["WiimoteMode"] == "normal";
                string key = null;

                if (_dolphinBarWiimotes == 1)
                    key = "Down";
                else if (first)
                    key = normalMode ? "PageUp" : "Return";
                else if (normalMode)
                    key = "PageDown";

                if (key != null)
                    ini.AppendValue(section, "Button2", "Keyboard/" + key);
            }

            public void Finish(IniFile ini)
            {
                if (_rawInputUsed)
                    ini.WriteValue("InputSources", "RawInput", "true");

                // Sinden border is only read from the first port section
                ini.WriteValue("ArcadeControllerPort1", "SindenBorder", _sindenUsed ? "true" : "false");

                if (_sindenUsed)
                    Guns.StartSindenSoftware();
            }

            private int GetRawMouseSlot(string devicePath)
            {
                if (string.IsNullOrEmpty(devicePath))
                    return -1;

                return _rawMiceSlots.FindIndex(p => p.Equals(devicePath, StringComparison.InvariantCultureIgnoreCase));
            }

            #region Raw Input enumeration
            [StructLayout(LayoutKind.Sequential)]
            private struct RAWINPUTDEVICELIST
            {
                public IntPtr hDevice;
                public uint dwType;
            }

            private const uint RIM_TYPEMOUSE = 0;
            private const uint RIDI_DEVICENAME = 0x20000007;

            [DllImport("user32.dll", SetLastError = true)]
            private static extern uint GetRawInputDeviceList([Out] RAWINPUTDEVICELIST[] pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

            [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode)]
            private static extern uint GetRawInputDeviceInfoW(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

            /// <summary>
            /// Same enumeration as ArcadeDuck (Win32RawInputSource::EnumerateRawInputMice) : every Raw Input mouse,
            /// sorted by device name. On a fresh start, the slot of a mouse is its position in this list.
            /// </summary>
            private static List<string> EnumerateRawMice()
            {
                var ret = new List<string>();

                try
                {
                    uint count = 0;
                    uint size = (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICELIST));

                    if (GetRawInputDeviceList(null, ref count, size) != 0 || count == 0)
                        return ret;

                    var devices = new RAWINPUTDEVICELIST[count];
                    uint read = GetRawInputDeviceList(devices, ref count, size);
                    if (read == uint.MaxValue)
                        return ret;

                    foreach (var device in devices.Take((int)read))
                    {
                        if (device.dwType != RIM_TYPEMOUSE)
                            continue;

                        uint nameSize = 0;
                        if (GetRawInputDeviceInfoW(device.hDevice, RIDI_DEVICENAME, IntPtr.Zero, ref nameSize) == uint.MaxValue || nameSize == 0)
                            continue;

                        IntPtr buffer = Marshal.AllocHGlobal((int)nameSize * 2);
                        try
                        {
                            uint written = GetRawInputDeviceInfoW(device.hDevice, RIDI_DEVICENAME, buffer, ref nameSize);
                            if (written == uint.MaxValue)
                                continue;

                            string name = Marshal.PtrToStringUni(buffer, (int)written).TrimEnd('\0');
                            if (!string.IsNullOrEmpty(name))
                                ret.Add(name);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }

                    // ArcadeDuck sorts on "RawInput:" + name with std::string operator< (ordinal)
                    ret.Sort(StringComparer.Ordinal);
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Warning("[ArcadeDuck] Unable to list Raw Input mice : " + ex.Message);
                }

                for (int i = 0; i < ret.Count; i++)
                    SimpleLogger.Instance.Info("[ArcadeDuck] RawMouse-" + i + " : " + ret[i]);

                return ret;
            }
            #endregion
        }
    }
}
