using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;
using EmulatorLauncher.Common.Joysticks;

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
                     Type = ExtractRawLighGunType(device.DevicePath)
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

        private int GetGunPriority()
        {
            switch (Type)
            {
                case RawLighGunType.Gun4Ir:
                    if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8042"))
                        return 10;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8044"))
                        return 12;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8046"))
                        return 14;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8043"))
                        return 16;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8045"))
                        return 18;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8047"))
                        return 20;
                    else
                        return 22 + Index;

                case RawLighGunType.Blamcon:
                    if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0100"))
                        return 40;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0101"))
                        return 42;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0102"))
                        return 44;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0103"))
                        return 46;
                    else if (DevicePath != null && DevicePath.Contains("VID_3673&PID_0104"))
                        return 48;
                    else
                        return 50 + Index;

                case RawLighGunType.SindenLightgun:
                    if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F01"))
                        return 60;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F38"))
                        return 62;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F02"))
                        return 64;
                    else if (DevicePath != null && DevicePath.Contains("VID_16C0&PID_0F39"))
                        return 66;
                    else
                        return 68 + Index;

                case RawLighGunType.AELightgun:
                    if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8037"))
                        return 80;
                    else if (DevicePath != null && DevicePath.Contains("VID_2341&PID_8038"))
                        return 82;
                    else
                        return 90 + Index;

                case RawLighGunType.RetroShooter:
                    if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5750"))
                        return 100;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5751"))
                        return 102;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5752"))
                        return 104;
                    else if (DevicePath != null && DevicePath.Contains("VID_0483&PID_5753"))
                        return 106;
                    else
                        return 108 + Index;

                case RawLighGunType.Xenas:
                    if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&71ff"))
                        return 120;
                    else if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&72ff"))
                        return 122;
                    else if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&73ff"))
                        return 124;
                    else if (DevicePath != null && DevicePath.Contains("VID_023f30_PID&74ff"))
                        return 126;
                    else
                        return 128 + Index;

                case RawLighGunType.Aimtrak:
                    if (DevicePath != null && DevicePath.Contains("VID_D209&PID_1601"))
                        return 140;
                    else if (DevicePath != null && DevicePath.Contains("VID_D209&PID_1602"))
                        return 142;
                    else if (DevicePath != null && DevicePath.Contains("VID_D209&PID_1603"))
                        return 144;
                    else
                        return 146 + Index;

                case RawLighGunType.Xgunner:
                    if (DevicePath != null && DevicePath.Contains("VID_1209&PID_0001"))
                        return 160;
                    else if (DevicePath != null && DevicePath.Contains("VID_1209&PID_0002"))
                        return 162;
                    else if (DevicePath != null && DevicePath.Contains("VID_1209&PID_0003"))
                        return 164;
                    else
                        return 166 + Index;

                case RawLighGunType.Wiimote4Guns:
                    if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultia"))
                        return 180;
                    else if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultib"))
                        return 181;
                    else if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultic"))
                        return 182;
                    else if (DevicePath != null && DevicePath.ToLowerInvariant().Contains("vmultid"))
                        return 183;
                    else
                        return 184 + Index;

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
