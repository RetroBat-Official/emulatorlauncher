using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace EmulatorLauncher.Common.Lightguns
{
    public class RawLightgun
    {
        #region Public Factory
        public static RawLightgun[] GetRawLightguns()
        {
            if (_cache == null)
                _cache = GetRawLightgunsInternal();

            return _cache;
        }

        public static int GetUsableLightGunCount()
        {
            var guns = RawLightgun.GetRawLightguns();

            int gunCount = guns.Count(g => g.Type != RawLighGunType.Mouse);
            if (gunCount > 0)
                return gunCount;

            int mice = guns.Count(g => g.Type == RawLighGunType.Mouse);
            return mice;
        }

        public static bool IsSindenLightGunConnected()
        {
            // Find Sinden software process (if software is not running , no need to check for the gun and no need to create the border)
            var px = Process.GetProcessesByName("Lightgun").FirstOrDefault();
            if (px == null)
                return false;

            /* When Sinden Lightgun app is running & Start is pressed, there's an ActiveMovie window in the process, with the class name "FilterGraphWindow" --- disabled for now but kept in case we need it later
            if (!User32.FindHwnds(px.Id, hWnd => User32.GetClassName(hWnd) == "FilterGraphWindow", false).Any())
                return false;*/

            // Check if any Sinden Gun is connected
            return RawLightgun.GetRawLightguns().Any(gun => gun.Type == RawLighGunType.SindenLightgun);
        }
        #endregion

        #region Private methods
        private RawLightgun() { }

        private static RawLighGunType ExtractRawLighGunType(string devicePath)
        {
            if (!string.IsNullOrEmpty(devicePath))
            {
                string[] sindenDeviceIds = new string[] { "VID_16C0&PID_0F01", "VID_16C0&PID_0F02", "VID_16C0&PID_0F38", "VID_16C0&PID_0F39" };
                if (sindenDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.SindenLightgun;

                string[] gun4irDeviceIds = new string[] { "VID_2341&PID_8042", "VID_2341&PID_8043", "VID_2341&PID_8044", "VID_2341&PID_8045" };
                if (gun4irDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Gun4Ir;

                string[] mayFlashWiimoteIds = new string[] { "VID_0079&PID_1802" };  // Mayflash Wiimote, using mode 1
                if (mayFlashWiimoteIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.MayFlashWiimote;

                string[] retroShooterIds = new string[] { "VID_0483&PID_5750", "VID_0483&PID_5751" };
                if (retroShooterIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.RetroShooter;
            }

            return RawLighGunType.Mouse;
        }

        private static RawLightgun[] _cache;

        private static RawLightgun[] GetRawLightgunsInternal()
        {
            var mouseNames = new List<RawLightgun>();

            int index = 0;
            foreach (var device in RawInputDevice.GetRawInputDevices().Where(t => t.Type == RawInputDeviceType.Mouse))
            {
                 mouseNames.Add(new RawLightgun() { Name = device.Name, DevicePath = device.DevicePath, Index = index, Type = ExtractRawLighGunType(device.DevicePath) });
                 index++;
            }

            // Sort by, gun4IR, thenby (sinden) lightgun, thenby wiimotes, then by physical index
            mouseNames.Sort((x, y) => x.GetGunPriority().CompareTo(y.GetGunPriority()));

            return mouseNames.ToArray();
        }

        #endregion

        public int Index { get; set; }
        public string Name { get; set; }
        public string DevicePath { get; set; }
        public RawLighGunType Type { get; private set; }

        private int GetGunPriority()
        {
            switch (Type)
            {
                case RawLighGunType.Gun4Ir:
                    if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8042"))
                        return 10;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8043"))
                        return 11;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8044"))
                        return 12;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8045"))
                        return 13;
                    else
                        return 14 + Index;

                case RawLighGunType.SindenLightgun:
                    if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F01"))
                        return 50;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F38"))
                        return 51;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F02"))
                        return 52;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F39"))
                        return 53;
                    else
                        return 55 + Index;

                case RawLighGunType.RetroShooter:
                    if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5750"))
                        return 100;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5751"))
                        return 101;
                    else
                        return 102 + Index;

                case RawLighGunType.MayFlashWiimote:
                    return 200 + Index;

                default:
                    if (Name != null && Name.IndexOf("lightgun", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        return 300 + Index;

                    if (Name != null && Name.IndexOf("wiimote", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        return 400 + Index;
                    
                    break;
            }

            return 1000 + Index;
        }

        public override string ToString()
        {
            return Name + " [" + Type + "] [" + Index + "] [" + DevicePath + "]";
        }
    }

    // When adding new type, don't forget about MAME64 vidpid forcing
    public enum RawLighGunType
    {
        SindenLightgun,
        MayFlashWiimote, // Using mode 1
        Gun4Ir,
        RetroShooter,
        Mouse
    }
}
