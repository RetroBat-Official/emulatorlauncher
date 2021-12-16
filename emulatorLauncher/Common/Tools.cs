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

namespace emulatorLauncher.Tools
{
    static class Misc
    {
        public static void RemoveWhere<T>(this IList<T> items, Predicate<T> func)
        {
            for (int i = items.Count - 1; i >= 0; i--)
                if (func(items[i]))
                    items.RemoveAt(i);
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

        public static string ExtractString(this string html, string start, string end)
        {
            int idx1 = html.IndexOf(start);
            if (idx1 < 0)
                return "";

            int idx2 = html.IndexOf(end, idx1 + start.Length);
            if (idx2 > idx1)
                return html.Substring(idx1 + start.Length, idx2 - idx1 - start.Length);

            return "";
        }

        public static int ToInteger(this string value)
        {
            int ret = 0;
            int.TryParse(value, out ret);
            return ret;
        }

        public static T FromXml<T>(string xmlPathName) where T : class
        {
            if (string.IsNullOrEmpty(xmlPathName))
                return default(T);

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (FileStream sr = new FileStream(xmlPathName, FileMode.Open, FileAccess.Read))
                return serializer.Deserialize(sr) as T;
        }

        public static T FromXmlString<T>(string xml) where T : class
        {
            if (string.IsNullOrEmpty(xml))
                return default(T);

            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Auto,
                ValidationType = ValidationType.None
            };

            XmlSerializer serializer = new XmlSerializer(typeof(T));

            using (var reader = new StringReader(xml))
            using (var xmlReader = XmlReader.Create(reader, settings))
            {
                var obj = serializer.Deserialize(xmlReader);
                return (T)obj;
            }
        }

        public static string ToXml<T>(this T obj, bool omitXmlDeclaration = false)
        {
            return obj.ToXml(new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                OmitXmlDeclaration = omitXmlDeclaration
            });
        }

        public static string ToXml<T>(this T obj, XmlWriterSettings xmlWriterSettings)
        {
            if (Equals(obj, default(T)))
                return String.Empty;

            using (var memoryStream = new MemoryStream())
            {
                var xmlSerializer = new XmlSerializer(obj.GetType());

                var xmlnsEmpty = new XmlSerializerNamespaces();
                xmlnsEmpty.Add(String.Empty, String.Empty);

                using (var xmlTextWriter = XmlWriter.Create(memoryStream, xmlWriterSettings))
                {
                    xmlSerializer.Serialize(xmlTextWriter, obj, xmlnsEmpty);
                    memoryStream.Seek(0, SeekOrigin.Begin); //Rewind the Stream.
                }

                var xml = xmlWriterSettings.Encoding.GetString(memoryStream.ToArray());
                return xml;
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

        [DllImport("shell32.dll")]
        public static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out]StringBuilder lpszPath, int nFolder, bool fCreate);

        public static string GetSystemDirectory()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                StringBuilder path = new StringBuilder(260);
                SHGetSpecialFolderPath(IntPtr.Zero, path, 0x0029, false); // CSIDL_SYSTEMX86
                return path.ToString();
            }

            return Environment.SystemDirectory;
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

        public static string RunWithOutput(string fileName, string arguments = null)
        {
            var ps = new ProcessStartInfo() { FileName = fileName };
            if (arguments != null)
                ps.Arguments = arguments;

            return RunWithOutput(ps);
        }

        public static string RunWithOutput(ProcessStartInfo ps)
        {
            List<string> lines = new List<string>();

            ps.UseShellExecute = false;
            ps.RedirectStandardOutput = true;
            ps.RedirectStandardError = true;
            ps.CreateNoWindow = true;

            var proc = new Process();
            proc.StartInfo = ps;
            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return (err ?? "") + (output ?? "");
        }

        public static string FormatVersionString(string version)
        {
            var numbers = version.Split(new char[] { '.', '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            while (numbers.Count < 4)
                numbers.Add("0");

            return string.Join(".", numbers.Take(4).ToArray());
        }


    }
}
