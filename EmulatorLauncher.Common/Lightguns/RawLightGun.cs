using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using EmulatorLauncher.Common.Joysticks;
using SharpDX.DirectInput;

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
            /*var px = Process.GetProcessesByName("Lightgun").FirstOrDefault();
            if (px == null)
                return false;

            When Sinden Lightgun app is running & Start is pressed, there's an ActiveMovie window in the process, with the class name "FilterGraphWindow" --- disabled for now but kept in case we need it later
            if (!User32.FindHwnds(px.Id, hWnd => User32.GetClassName(hWnd) == "FilterGraphWindow", false).Any())
                return false;*/

            // Check if any Sinden Gun is connected
            return RawLightgun.GetRawLightguns().Any(gun => gun.Type == RawLighGunType.SindenLightgun);
        }
        #endregion

        #region Private methods
        private RawLightgun() { }

        public static RawLighGunType ExtractRawLighGunType(string devicePath)
        {
            if (!string.IsNullOrEmpty(devicePath))
            {
                string[] sindenDeviceIds = new string[] { "VID_16C0&PID_0F01", "VID_16C0&PID_0F02", "VID_16C0&PID_0F38", "VID_16C0&PID_0F39" };
                if (sindenDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.SindenLightgun;

                string[] gun4irDeviceIds = new string[] { "VID_2341&PID_8042", "VID_2341&PID_8043", "VID_2341&PID_8044", "VID_2341&PID_8045", "VID_2341&PID_8046", "VID_2341&PID_8047" };
                if (gun4irDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Gun4Ir;

                string[] mayFlashWiimoteIds = new string[] { "VID_0079&PID_1802" };  // Mayflash Wiimote, using mode 1
                if (mayFlashWiimoteIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.MayFlashWiimote;

                string[] retroShooterIds = new string[] { "VID_0483&PID_5750", "VID_0483&PID_5751", "VID_0483&PID_5752", "VID_0483&PID_5753" };
                if (retroShooterIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.RetroShooter;

                string[] blamconDeviceIds = new string[] { "VID_3673&PID_0100", "VID_3673&PID_0101", "VID_3673&PID_0102", "VID_3673&PID_0103", "VID_3673&PID_0104" };
                if (blamconDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Blamcon;

                string[] aimtrackDeviceIds = new string[] { "VID_D209&PID_1601", "VID_D209&PID_1602", "VID_D209&PID_1603" };
                if (aimtrackDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Aimtrak;

                string[] aeLightgunDeviceIds = new string[] { "VID_2341&PID_8037", "VID_2341&PID_8038" };
                if (aeLightgunDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.AELightgun;

                string[] xenasDeviceIds = new string[] { "VID_023f30_PID&71ff", "VID_023f30_PID&72ff", "VID_023f30_PID&73ff", "VID_023f30_PID&74ff" };
                if (xenasDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Xenas;

                string[] xgunnerDeviceIds = new string[] { "VID_1209&PID_0001", "VID_1209&PID_0002", "VID_1209&PID_0003", "VID_1209&PID_0004" };
                if (xgunnerDeviceIds.Any(d => devicePath.Contains(d)))
                    return RawLighGunType.Xgunner;

                string[] wiimote4GunsDeviceIds = new string[] { "vmultia", "vmultib", "vmultic", "vmultid" };
                if (wiimote4GunsDeviceIds.Any(d => devicePath.ToLowerInvariant().Contains(d.ToLowerInvariant())))
                    return RawLighGunType.Wiimote4Guns;
            }

            return RawLighGunType.Mouse;
        }

        private static int GetGamePadIndex(RawInputDevice device)
        {
            var gamepads = RawInputDevice.GetRawInputControllers();

            for (int i = 0; i < gamepads.Length; i++)
            {
                var gp = gamepads[i];
                if (gp.VendorId == device.VendorId && gp.ProductId == device.ProductId)
                    return i;
            }
            return -1;
        }

        private static RawLightgun[] _cache;

        private static RawLightgun[] GetRawLightgunsInternal()
        {
            var mouseNames = new List<RawLightgun>();

            int index = 0;
            foreach (var device in RawInputDevice.GetRawInputDevices().Where(t => t.Type == RawInputDeviceType.Mouse))
            {
                mouseNames.Add(new RawLightgun()
                {
                    Name = device.Name,
                    Manufacturer = device.Manufacturer,
                    DevicePath = device.DevicePath,
                    Index = index,
                    VendorId = device.VendorId,
                    ProductId = device.ProductId,
                    Type = ExtractRawLighGunType(device.DevicePath),
                    GamepadIndex = GetGamePadIndex(device)
                });
                    
                index++;
            }

            foreach (var mouse in mouseNames)
            {
                if (mouse.Type == RawLighGunType.Wiimote4Guns)
                {
                    int playerNumber = 1;
                    if (mouse.DevicePath != null)
                    {
                        if (mouse.DevicePath.ToLowerInvariant().Contains("vmultia"))
                            playerNumber = 1;
                        else if (mouse.DevicePath.ToLowerInvariant().Contains("vmultib"))
                            playerNumber = 2;
                        else if (mouse.DevicePath.ToLowerInvariant().Contains("vmultic"))
                            playerNumber = 3;
                        else if (mouse.DevicePath.ToLowerInvariant().Contains("vmultid"))
                            playerNumber = 4;
                    }
                    mouse.Name = "Wiimote4Guns P" + playerNumber;
                    mouse.Manufacturer = "RetroBat";
                }
            }

            // Sort known lightguns first, then by physical index.
            mouseNames.Sort((x, y) => x.GetGunPriority().CompareTo(y.GetGunPriority()));

            // Temporary log
            foreach (var mouse in mouseNames)
                SimpleLogger.Instance.Info("[RawLightGun] -> " + mouse.Name + " (" + mouse.Type + ") -> Priority : " + mouse.GetGunPriority());

            return mouseNames.ToArray();
        }

        #endregion

        public int Index { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string DevicePath { get; set; }
        public USB_VENDOR VendorId { get; set; }
        public USB_PRODUCT ProductId { get; set; }
        public RawLighGunType Type { get; private set; }
        public int Priority { get; set; }
        public int GamepadIndex { get; set; }

        private int GetGunPriority()
        {
            Priority = 1000 + Index;
            switch (Type)
            {
                case RawLighGunType.Gun4Ir:
                    if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8042"))
                        Priority = 10;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8044"))
                        Priority = 12;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8046"))
                        Priority = 14;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8043"))
                        Priority = 16;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8045"))
                        Priority = 18;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8047"))
                        Priority = 20;
                    else
                        Priority = 22 + Index;
                    break;

                case RawLighGunType.Blamcon:
                    if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0100"))
                        Priority = 40;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0101"))
                        Priority = 42;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0102"))
                        Priority = 44;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0103"))
                        Priority = 46;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0104"))
                        Priority = 48;
                    else
                        Priority = 50 + Index;
                    break;

                case RawLighGunType.SindenLightgun:
                    if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F01"))
                        Priority = 60;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F38"))
                        Priority = 62;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F02"))
                        Priority = 64;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F39"))
                        Priority = 66;
                    else
                        Priority = 68 + Index;
                    break;

                case RawLighGunType.AELightgun:
                    if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8037"))
                        Priority = 80;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8038"))
                        Priority = 82;
                    else
                        Priority = 90 + Index;
                    break;

                case RawLighGunType.RetroShooter:
                    if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5750"))
                        Priority = 100;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5751"))
                        Priority = 102;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5752"))
                        Priority = 104;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5753"))
                        Priority = 106;
                    else
                        Priority = 108 + Index;
                    break;

                case RawLighGunType.Xenas:
                    if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&71ff"))
                        Priority = 120;
                    else if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&72ff"))
                        Priority = 122;
                    else if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&73ff"))
                        Priority = 124;
                    else if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&74ff"))
                        Priority = 126;
                    else
                        Priority = 128 + Index;
                    break;

                case RawLighGunType.Aimtrak:
                    if (DevicePath != null && DevicePath.Contains("VID_D209&PID_1601"))
                        Priority = 140;
                    else if (DevicePath != null && DevicePath.Contains("VID_D209&PID_1602"))
                        Priority = 142;
                    else if (DevicePath != null && DevicePath.Contains("VID_D209&PID_1603"))
                        Priority = 144;
                    else
                        Priority = 146 + Index;
                    break;

                case RawLighGunType.Xgunner:
                    if (DevicePath != null && DevicePath.Contains("VID_1209&PID_0001"))
                        Priority = 160;
                    else if (DevicePath != null && DevicePath.Contains("VID_1209&PID_0002"))
                        Priority = 162;
                    else if (DevicePath != null && DevicePath.Contains("VID_1209&PID_0003"))
                        Priority = 164;
                    else
                        Priority = 166 + Index;
                    break;

                case RawLighGunType.Wiimote4Guns:
                    if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultia"))
                        Priority = 180;
                    else if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultib"))
                        Priority = 181;
                    else if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultic"))
                        Priority = 182;
                    else if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultid"))
                        Priority = 183;
                    else
                        Priority = 184 + Index;
                    break;

                case RawLighGunType.MayFlashWiimote:
                    Priority = 200 + Index;
                    break;

                default:
                    if (Name != null && Name.IndexOf("lightgun", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        Priority = 300 + Index;

                    if (Name != null && Name.IndexOf("wiimote", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        Priority = 400 + Index;
                    
                    break;
            }

            return Priority;
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
        AELightgun,
        RetroShooter,
        Blamcon,
        Aimtrak,
        Xenas,
        Wiimote4Guns,
        Xgunner,
        Mouse
    }
}
