using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;

namespace EmulatorLauncher.Common
{
    public static class Misc
    {
        public static void AddWindows11RoundCorners(this System.Windows.Forms.Form form)
        {
            if (form != null && form.IsHandleCreated && IsWindowsVersionAtLeast(WindowsVersion.Windows11))
            {
                int preference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
                DWM.DwmSetWindowAttribute(form.Handle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
        }

        /// <summary>
        /// Detects if WiimoteGun is running in gamepad mode
        /// </summary>
        /// <returns></returns>
        public static bool HasWiimoteGun(WiiModeGunMode mode = WiiModeGunMode.Any)
        {
            IntPtr hWndWiimoteGun = User32.FindWindow("WiimoteGun", null);
            if (hWndWiimoteGun != IntPtr.Zero)
            {
                if (mode == WiiModeGunMode.Any)
                    return true;

                int wndMode = (int)User32.GetProp(hWndWiimoteGun, "mode");
                return wndMode == (int)mode;
            }

            return false;
        }


        public static bool IsDeveloperModeEnabled
        {
            get
            {
                object val = RegistryKeyEx.GetRegistryValue(RegistryKeyEx.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense", RegistryKeyEx.Is64BitOperatingSystem ? RegistryViewEx.Registry64 : RegistryViewEx.Registry32);
                if (val != null && val is int)
                    return (int) val == 1;
                
                return false;                
            }
        }

        public static bool IsWindowsEightOrTen { get { return IsWindowsVersionAtLeast(WindowsVersion.Windows8); } }
        public static bool IsWindowsVersionAtLeast(WindowsVersion version) { return (int)WindowsVersion >= (int)version; }

        private static WindowsVersion? _windowsVersion;

        public static WindowsVersion WindowsVersion
        {
            get
            {
                if (!_windowsVersion.HasValue)
                {
                    int majorVersion = Environment.OSVersion.Version.Major;
                    int minorVersion = Environment.OSVersion.Version.Minor;

                    if (majorVersion == 10 && minorVersion == 0)
                        _windowsVersion = WindowsVersion.Windows10;
                    else if (majorVersion == 10)
                        _windowsVersion = WindowsVersion.Windows11;
                    else if (majorVersion == 6)
                    {
                        if (minorVersion == 0)
                            _windowsVersion = WindowsVersion.WindowsVista;
                        else if (minorVersion == 1)
                            _windowsVersion = WindowsVersion.Windows7;
                        else
                        {
                            var ver = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.SystemDirectory, "kernel32.dll"));

                            if (ver.ProductMajorPart >= 11)
                                _windowsVersion = WindowsVersion.Windows11;
                            else if (ver.ProductMajorPart >= 10 && ver.ProductBuildPart >= 22000)
                                _windowsVersion = WindowsVersion.Windows11;
                            else if (ver.ProductMajorPart == 10)
                                _windowsVersion = WindowsVersion.Windows10;
                            else if (minorVersion == 3)
                                _windowsVersion = WindowsVersion.Windows81;
                            else
                                _windowsVersion = WindowsVersion.Windows8;
                        }
                    }
                    else if (majorVersion == 3)
                        _windowsVersion = WindowsVersion.Windows95;
                    else if (majorVersion == 4)
                        _windowsVersion = WindowsVersion.Windows98;
                    else if (majorVersion == 5)
                    {
                        switch (minorVersion)
                        {
                            case 0:
                                return WindowsVersion.Windows2000;
                            case 1:
                                return WindowsVersion.WindowsXP;
                            case 2:
                                return WindowsVersion.WindowsXP;
                        }
                    }
                    else
                        _windowsVersion = WindowsVersion.WindowsXP;
                }

                return _windowsVersion.Value;
            }
        }

        private static bool? _isAvailableNetwork;
        public static bool IsAvailableNetworkActive()
        {
            if (_isAvailableNetwork.HasValue)
                return _isAvailableNetwork.Value;

            // only recognizes changes related to Internet adapters
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                // however, this will include all adapters -- filter by opstatus and activity
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                _isAvailableNetwork = (from face in interfaces
                                       where face.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                                       where (face.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) && (face.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                                       select face.GetIPv4Statistics()).Any(statistics => (statistics.BytesReceived > 0) && (statistics.BytesSent > 0));
            }
            else
                _isAvailableNetwork = false;

            return _isAvailableNetwork.Value;
        }

        public static bool NetworkHasPublicIP()
        {
            try
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    return false;

                var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

                foreach (var netInterface in networkInterfaces)
                {
                    // Filter out loopback and non-operational interfaces
                    if (netInterface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        netInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        continue;
                    }

                    // Get the IP properties of the network interface
                    var ipProps = netInterface.GetIPProperties();

                    foreach (var ipInfo in ipProps.UnicastAddresses)
                    {
                        if (ipInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            if (ipInfo.Address.ToString() == "::1")
                                continue;

                            bool isPublic = IsPublicIP(ipInfo.Address);
                            if (isPublic)
                                return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool IsPublicIP(IPAddress address) { return !IsPrivateIP(address); }

        public static bool IsPrivateIP(IPAddress address)
        {
            if (address.ToString() == "::1") 
                return true;

            byte[] ip = address.GetAddressBytes();
            switch (ip[0])
            {
                case 10:
                case 127:
                    return true;
                case 172:
                    return ip[1] >= 16 && ip[1] < 32;
                case 192:
                    return ip[1] == 168;
                default:
                    return false;
            }
        }

        public static Image Base64ToImage(string data)
        {
            Image image = null;
            if (data.Length > 0)
            {
                byte[] buffer = Convert.FromBase64String(data);
                if ((buffer != null) && (buffer.Length > 0))
                {
                    image = (Image)new ImageConverter().ConvertFrom(buffer);
                }
            }
            return image;
        }

        public static Rectangle GetPictureRect(Size imageSize, Rectangle rcPhoto, bool outerZooming = false, bool sourceRect = false)
        {
            int iScreenX = rcPhoto.X + rcPhoto.Width / 2;
            int iScreenY = rcPhoto.Y + rcPhoto.Height / 2;
            int cxDIB = imageSize.Width;
            int cyDIB = imageSize.Height;
            int iMaxX = rcPhoto.Width;
            int iMaxY = rcPhoto.Height;

            double xCoef = (double)iMaxX / (double)cxDIB;
            double yCoef = (double)iMaxY / (double)cyDIB;

            cyDIB = (int)((double)cyDIB * Math.Max(xCoef, yCoef));
            cxDIB = (int)((double)cxDIB * Math.Max(xCoef, yCoef));

            if (cxDIB > iMaxX)
            {
                cyDIB = (int)((double)cyDIB * (double)iMaxX / (double)cxDIB);
                cxDIB = iMaxX;
            }

            if (cyDIB > iMaxY)
            {
                cxDIB = (int)((double)cxDIB * (double)iMaxY / (double)cyDIB);
                cyDIB = iMaxY;
            }

            if (outerZooming)
            {

                if (sourceRect)
                {
                    double imageCoef = (double)imageSize.Width / (double)imageSize.Height;
                    double rectCoef = (double)rcPhoto.Width / (double)rcPhoto.Height;

                    if (imageCoef < rectCoef)
                    {
                        cyDIB = (int)(imageSize.Height * imageCoef / rectCoef);
                        return new Rectangle(0, imageSize.Height / 2 - cyDIB / 2, imageSize.Width, cyDIB);
                    }
                    else
                    {
                        cxDIB = (int)(imageSize.Width * rectCoef / imageCoef);
                        return new Rectangle(imageSize.Width / 2 - cxDIB / 2, 0, cxDIB, imageSize.Height);
                    }
                }

                if (imageSize.Width * yCoef < rcPhoto.Width)
                {
                    cxDIB = rcPhoto.Width;
                    cyDIB = (int)(imageSize.Height * xCoef);
                }
                else
                {
                    cyDIB = rcPhoto.Height;
                    cxDIB = (int)(imageSize.Width * yCoef);
                }
            }

            return new Rectangle(iScreenX - cxDIB / 2, iScreenY - cyDIB / 2, cxDIB, cyDIB);
        }


        public static int IndexOf(this byte[] arrayToSearchThrough, byte[] patternToFind, int startIndex = 0)
        {
            if (patternToFind.Length > arrayToSearchThrough.Length)
                return -1;

            for (int i = startIndex; i < arrayToSearchThrough.Length - patternToFind.Length + 1; ++i)
            {
                bool found = true;
                for (int j = 0; j < patternToFind.Length; j++)
                {
                    if (arrayToSearchThrough[i + j] != patternToFind[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    return i;
            }

            return -1;
        }
    }

    public enum WiiModeGunMode : int
    {
        Any = 0,
        Mouse = 1,
        Gamepad = 2
    }


    public enum WindowsVersion : int
    {
        Unknown = 0,
        Windows95 = 1,
        Windows98 = 2,
        WindowsMe = 3,
        Windows2000 = 4,
        WindowsXP = 5,
        WindowsVista = 6,
        Windows7 = 7,
        Windows8 = 8,
        Windows81 = 9,
        Windows10 = 10,
        Windows11 = 11
    }
}
