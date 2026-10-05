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

                if (GetWiimote4GunsPlayer(devicePath) > 0)
                    return RawLighGunType.Wiimote4Guns;
            }

            return RawLighGunType.Mouse;
        }

        // Wiimote4Guns player number (1-4) from the device path, 0 if not a Wiimote4Guns device
        // Legacy versions : vmultia..vmultid / HIDMaestro versions : VID_001F..VID_004F with PID_BACC
        private static int GetWiimote4GunsPlayer(string devicePath)
        {
            if (string.IsNullOrEmpty(devicePath))
                return 0;

            string path = devicePath.ToLowerInvariant();
            for (int p = 1; p <= 4; p++)
            {
                if (path.Contains("vmulti" + (char)('a' + p - 1)) || path.Contains("vid_00" + p + "f&pid_bacc"))
                    return p;
            }

            return 0;
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
                    int playerNumber = GetWiimote4GunsPlayer(mouse.DevicePath);
                    mouse.Name = "Wiimote4Guns P" + (playerNumber > 0 ? playerNumber : 1);
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
                    int w4gPlayer = GetWiimote4GunsPlayer(DevicePath);
                    Priority = w4gPlayer > 0 ? 179 + w4gPlayer : 184 + Index;   // P1..P4 -> 180..183
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

            if (Type == RawLighGunType.Mouse && IsLikelyIntegrated())
                return 10000 + Index;

            return Priority;
        }

        private bool IsLikelyIntegrated()
        {
            if (string.IsNullOrEmpty(DevicePath))
                return false;

            string path = DevicePath.ToUpperInvariant();

            // Special cases to exclude that do not have VID
            if (path.ToLowerInvariant().Contains("vmulti"))
                return false;

            // Known integrated touchpad vendors
            if (path.Contains("ASUP") ||
                path.Contains("ELAN") ||
                path.Contains("SYN") ||
                path.Contains("MSFT"))
                return true;

            // No VID => almost certainly not USB
            if (!path.Contains("VID_"))
                return true;

            return false;
        }

        public override string ToString()
        {
            return Name + " [" + Type + "] [" + Index + "] [" + DevicePath + "]";
        }

        #region Gun / keyboard association
        // Associate each Wiimote gun (Mayflash / Wiimote4Guns) with its own keyboard device.
        // Must run once, in player order and before any gun index override,
        // as FindAssociatedKeyboard skips keyboards that are already associated.
        public static Dictionary<RawLightgun, RawInputDevice> AssociateGunKeyboards(RawLightgun[] orgGuns, List<RawInputDevice> keyboards, RawInputDevice keyboard)
        {
            var associations = new Dictionary<RawLightgun, RawInputDevice>();

            foreach (var gun in orgGuns)
            {
                if (gun == null || associations.ContainsKey(gun))
                    continue;

                if (gun.Type != RawLighGunType.MayFlashWiimote && gun.Type != RawLighGunType.Wiimote4Guns)
                    continue;

                associations.Add(gun, FindAssociatedKeyboard(gun.DevicePath, keyboards, keyboard, associations));
            }

            return associations;
        }

        public static RawInputDevice FindAssociatedKeyboard(string gunPath, List<RawInputDevice> keyboards, RawInputDevice keyboard, Dictionary<RawLightgun, RawInputDevice> associations)
        {
            if (string.IsNullOrEmpty(gunPath) || keyboards == null)
                return keyboard;

            if (associations == null)
                associations = new Dictionary<RawLightgun, RawInputDevice>();

            string lowerPath = gunPath.ToLowerInvariant();
            bool isVmulti = lowerPath.Contains("vmulti");
            bool isHidMaestro = gunPath.Contains("&PID_BACC");

            // Keyboards already associated to another gun must not be reused
            List<RawInputDevice> kbToIgnore = associations.Values.Where(v => v != null).ToList();

            // Handle Wiimote4Guns differently
            if (isVmulti || isHidMaestro)
            {
                string gunIdentifier = null;

                // HIDMaestro : mouse (Col01) and keyboard (Col02) collections share the same VID/PID (e.g. VID_001F&PID_BACC)
                if (isHidMaestro)
                    gunIdentifier = GetVIDPID(gunPath).ToLowerInvariant();
                else if (lowerPath.Contains("vmultia"))
                    gunIdentifier = "vmultia";
                else if (lowerPath.Contains("vmultib"))
                    gunIdentifier = "vmultib";
                else if (lowerPath.Contains("vmultic"))
                    gunIdentifier = "vmultic";
                else if (lowerPath.Contains("vmultid"))
                    gunIdentifier = "vmultid";

                if (string.IsNullOrEmpty(gunIdentifier))
                    return keyboard;

                foreach (var kb in keyboards)
                {
                    if (kbToIgnore.Contains(kb))
                        continue;

                    if (kb.DevicePath.ToLowerInvariant().Contains(gunIdentifier))
                        return kb;
                }
            }
            else
            {
                // Original logic for MayFlashWiimote and other guns
                if (associations.Any(g => g.Key.DevicePath == gunPath))
                    return associations.First(g => g.Key.DevicePath == gunPath).Value;

                string mouseVIDPID = GetWiimoteVIDPID(gunPath);
                string mouseChar = GetWiimoteAssociationChar(gunPath);
                string toSearch = mouseVIDPID + "_" + mouseChar;

                foreach (var kb in keyboards)
                {
                    if (kbToIgnore.Contains(kb))
                        continue;

                    string kbVIDPID = GetWiimoteVIDPID(kb.DevicePath);
                    string kbChar = GetWiimoteAssociationChar(kb.DevicePath);

                    if (kbVIDPID != null && kbChar != null)
                    {
                        string toFind = kbVIDPID + "_" + kbChar;
                        if (toSearch.ToLowerInvariant() == toFind.ToLowerInvariant())
                            return kb;
                    }
                }
            }

            return keyboard;
        }

        // Find the keyboard collection exposed by the same physical device as the gun (same VID/PID, same interface when composite)
        public static RawInputDevice FindKeyboardByVidPid(string gunPath, List<RawInputDevice> keyboards, RawInputDevice defaultKeyboard)
        {
            if (string.IsNullOrEmpty(gunPath) || keyboards == null || keyboards.Count == 0)
                return defaultKeyboard;

            int startIndex = gunPath.IndexOf("VID");
            if (startIndex < 0)
                return defaultKeyboard;

            int endIndex = gunPath.IndexOf('#', startIndex);
            if (endIndex < 0)
                return defaultKeyboard;

            // Composite device : keep the interface number (e.g. VID_xxxx&PID_yyyy&MI_02)
            int miIndex = gunPath.IndexOf("MI_", startIndex);
            if (miIndex >= 0 && miIndex < endIndex)
                endIndex = Math.Min(miIndex + 5, gunPath.Length);

            string searchPath = gunPath.Substring(startIndex, endIndex - startIndex);
            var kb = keyboards.FirstOrDefault(k => k.DevicePath.Contains(searchPath));
            if (kb != null)
                return kb;

            // Fallback : same VID/PID, any interface or collection
            string vidPid = GetVIDPID(gunPath);
            if (!string.IsNullOrEmpty(vidPid))
            {
                kb = keyboards.FirstOrDefault(k => k.DevicePath.Contains(vidPid));
                if (kb != null)
                    return kb;
            }

            return defaultKeyboard;
        }

        public static string GetVIDPID(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "";

            bool acpi = false;
            int vidIndex = path.IndexOf("VID");
            if (vidIndex < 0)
            {
                int acpiIndex = path.IndexOf("ACPI");
                if (acpiIndex < 0)
                    return "";

                vidIndex = acpiIndex + 5;
                acpi = true;
            }

            int pidIndex = path.IndexOf("PID");
            if (pidIndex < 0 && acpi)
                pidIndex = path.IndexOf("#", vidIndex + 5);
            if (pidIndex < 0)
                return "";

            int endindex = acpi ? path.IndexOf("#", pidIndex) : path.IndexOf("&", pidIndex);
            if (endindex < 0)
                return path.Substring(vidIndex, path.Length - vidIndex);
            else
                return path.Substring(vidIndex, endindex - vidIndex);
        }
        public static string GetWiimoteVIDPID(string devicePath)
        {
            try
            {
                string[] parts = devicePath.Split('#');
                if (parts.Length < 3)
                    return null;

                string[] vidPidParts = parts[1].Split('&');
                string vidPid = $"{vidPidParts[0]}&{vidPidParts[1]}"; // Only take VID and PID

                string partAfterSecondHash = parts[2];
                char characterAfterSecondHash = partAfterSecondHash[0];

                return vidPid;
            }
            catch
            {
                return null;
            }
        }

        public static string GetWiimoteAssociationChar(string devicePath)
        {
            try
            {
                string[] parts = devicePath.Split('#');
                if (parts.Length < 3)
                    return "";

                string partAfterSecondHash = parts[2];
                char characterAfterSecondHash = partAfterSecondHash[0]; // First character

                return characterAfterSecondHash.ToString();
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }

    // When adding new type, don't forget about MAME64 vidpid forcing
    public enum RawLighGunType
    {
        SindenLightgun,
        MayFlashWiimote, // Using mode 1 and 2
        Gun4Ir,
        AELightgun,
        RetroShooter,
        Blamcon,
        Aimtrak,
        Xenas,
        Wiimote4Guns, // using wiimote4guns plugin
        Xgunner,
        Mouse
    }
}
