using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using System.IO;
using System.Xml;

namespace emulatorLauncher.Tools
{
    static class Misc
    {
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

        public static string FormatPath(string path, IRelativePath relativeTo)
        {
            if (!string.IsNullOrEmpty(path))
            {
                string home = GetHomePath(relativeTo);
                path = path.Replace("%HOME%", home);
                path = path.Replace("~", home);

                path = path.Replace("/", "\\");
                if (path.StartsWith("\\") && !path.StartsWith("\\\\"))
                    path = "\\" + path;

                if (relativeTo != null && path.StartsWith(".\\"))
                    path = Path.Combine(Path.GetDirectoryName(relativeTo.FilePath), path.Substring(2));
            }

            return path;
        }

        private static string GetHomePath(IRelativePath relativeTo)
        {
            if (relativeTo == null)
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            string path = Path.GetDirectoryName(relativeTo.FilePath);
            if (!path.StartsWith("\\\\"))
            {
                try
                {
                    string parent = Directory.GetParent(path).FullName;
                    if (File.Exists(Path.Combine(parent, "EmulationStation.exe")))
                        return parent;
                }
                catch { }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
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

    }
}
