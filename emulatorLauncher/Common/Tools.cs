using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net;
using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices.ComTypes;

namespace emulatorLauncher.Tools
{
    static class Misc
    {
        /*
        public static int GetLightGunCount()
        {
            var guns = RawLightgun.GetRawLightguns();

            int sindenLightGun = guns.Count(g => g.Type == RawLighGunType.SindenLightgun);
            int wiiMote = guns.Count(g => g.Type == RawLighGunType.MayFlashWiimote);
            int mice = guns.Count(g => g.Type == RawLighGunType.Mouse);

            return mice;
            
            string[] sindenDeviceIds = new string[] { "VID_16C0&PID_0F01", "VID_16C0&PID_0F02", "VID_16C0&PID_0F38", "VID_16C0&PID_0F39" };

            int mouses = 0;
            int sindenLightGun = 0;
            int wiimotes = 0;

            var searcher = new ManagementObjectSearcher("select * from Win32_PointingDevice");
            foreach (var obj in searcher.Get())
            {
                object pnpDeviceID = obj.GetPropertyValue("PNPDeviceID");
                if (pnpDeviceID == null)
                    continue;

                string deviceId = pnpDeviceID.ToString();
                if (sindenDeviceIds.Any(d => deviceId.Contains(d)))
                    continue;

                object pointingType = obj.GetPropertyValue("PointingType");
                if (pointingType is ushort && ((ushort)pointingType) == 2)
                    mouses++;
            }

            // Count connected Sinden Guns 
            foreach (ManagementObject obj1 in new ManagementObjectSearcher("Select * from WIN32_SerialPort").Get())
            {
                object pnpDeviceID = obj1.GetPropertyValue("PNPDeviceID");
                if (pnpDeviceID == null)
                    continue;

                string deviceId = pnpDeviceID.ToString();

                if (sindenDeviceIds.Any(d => deviceId.Contains(d)))
                    sindenLightGun++;
            }

            if (IsSindenLightGunConnected())
            {
                if (HasWiimoteGun(WiiModeGunMode.Mouse) && sindenLightGun > 0)
                    return Math.Max(mouses, sindenLightGun + 1);

                return Math.Max(mouses, sindenLightGun);
            }

            return mouses;
        }
        */
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

        public static bool IsSindenLightGunConnected()
        {
            // Find Sinden process
            var px = Process.GetProcessesByName("Lightgun").FirstOrDefault();
            if (px == null)
                return false;

            // When Sinden Lightgun app is running & Start is pressed, there's an ActiveMovie window in the process, with the class name "FilterGraphWindow"
            if (!User32.FindHwnds(px.Id, hWnd => User32.GetClassName(hWnd) == "FilterGraphWindow", false).Any())
                return false;

            // Check if any Sinden Gun is connected
            string[] sindenDeviceIds = new string[] { "VID_16C0&PID_0F01", "VID_16C0&PID_0F02", "VID_16C0&PID_0F38", "VID_16C0&PID_0F39" };

            foreach (ManagementObject obj1 in new ManagementObjectSearcher("Select * from WIN32_SerialPort").Get())
            {
                object pnpDeviceID = obj1.GetPropertyValue("PNPDeviceID");
                if (pnpDeviceID == null)
                    continue;

                string deviceId = pnpDeviceID.ToString();

                if (sindenDeviceIds.Any(d => deviceId.Contains(d)))
                    return true;
            }

            return false;
        }

        public static string GetProcessCommandline(this Process process)
        {
            if (process == null)
                return null;

            try
            {
                using (var cquery = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + process.Id))
                {
                    var commandLine = cquery.Get()
                        .OfType<System.Management.ManagementObject>()
                        .Select(p => (string)p["CommandLine"])
                        .FirstOrDefault();

                    return commandLine;
                }
            }
            catch
            {

            }

            return null;
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

        public static bool IsWindowsEightOrTen
        {
            get
            {
                if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 2)
                    return true;

                if (Environment.OSVersion.Version.Major >= 7)
                    return true;

                return false;
            }
        }

        public static bool IsAvailableNetworkActive()
        {
            // only recognizes changes related to Internet adapters
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                // however, this will include all adapters -- filter by opstatus and activity
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                return (from face in interfaces
                        where face.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                        where (face.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) && (face.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                        select face.GetIPv4Statistics()).Any(statistics => (statistics.BytesReceived > 0) && (statistics.BytesSent > 0));
            }

            return false;
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


        public static int IndexOf(this byte[] arrayToSearchThrough, byte[] patternToFind)
        {
            if (patternToFind.Length > arrayToSearchThrough.Length)
                return -1;

            for (int i = 0; i < arrayToSearchThrough.Length - patternToFind.Length + 1; ++i)
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

    enum WiiModeGunMode : int
    {
        Any = 0,
        Mouse = 1,
        Gamepad = 2
    }




}
