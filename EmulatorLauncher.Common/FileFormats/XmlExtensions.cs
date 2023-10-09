using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Xml;

namespace EmulatorLauncher.Common.FileFormats
{
    public static class XmlExtensions
    {
        public static System.Xml.Linq.XElement GetOrCreateElement(this System.Xml.Linq.XContainer container, string name)
        {
            var element = container.Element(name);
            if (element == null)
            {
                element = new System.Xml.Linq.XElement(name);
                container.Add(element);
            }
            return element;
        }

        public static T FromXml<T>(this string xmlPathName) where T : class
        {
            if (string.IsNullOrEmpty(xmlPathName))
                return default(T);

            using (FileStream sr = new FileStream(xmlPathName, FileMode.Open, FileAccess.Read))
            {
                if (typeof(IXmlSerializable).IsAssignableFrom(typeof(T)))
                {
                    using (var reader = XmlReader.Create(sr))
                    {
                        T t = Activator.CreateInstance<T>();
                        ((IXmlSerializable)t).ReadXml(reader);
                        return t;
                    }
                }

                XmlSerializer serializer = new XmlSerializer(typeof(T));
                return serializer.Deserialize(sr) as T;
            }
        }

        public static T FromXmlString<T>(this string xml) where T : class
        {
            if (string.IsNullOrEmpty(xml))
                return default(T);

            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreWhitespace = true,
                ConformanceLevel = ConformanceLevel.Auto,
                ValidationType = ValidationType.None
            };

           

            // Fix attributes strings containing & caracters
            foreach (var toFix in xml.ExtractStrings("\"", "\"", true).Distinct().Where(s => s.Contains("& ")))
                xml = xml.Replace(toFix, toFix.Replace("& ", "&amp; "));

            using (var reader = new StringReader(xml.TrimStart('\uFEFF')))
            using (var xmlReader = XmlReader.Create(reader, settings))
            {
                if (typeof(IXmlSerializable).IsAssignableFrom(typeof(T)))
                {
                    T t = Activator.CreateInstance<T>();
                    ((IXmlSerializable)t).ReadXml(xmlReader);
                    return t;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(T));
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
                IXmlSerializable xs = obj as IXmlSerializable;
                if (xs != null)
                {
                    using (XmlWriter xw = XmlWriter.Create(memoryStream, xmlWriterSettings))
                    {
                        xs.WriteXml(xw);
                        xw.Close();
                    }
                }
                else
                {
                    var xmlSerializer = new XmlSerializer(obj.GetType());

                    var xmlnsEmpty = new XmlSerializerNamespaces();
                    xmlnsEmpty.Add(String.Empty, String.Empty);

                    using (var xmlTextWriter = XmlWriter.Create(memoryStream, xmlWriterSettings))
                    {
                        xmlSerializer.Serialize(xmlTextWriter, obj, xmlnsEmpty);
                        memoryStream.Seek(0, SeekOrigin.Begin); //Rewind the Stream.
                    }
                }

                var xml = xmlWriterSettings.Encoding.GetString(memoryStream.ToArray());
                return xml;
            }
        }


        public static string StripInvalidXMLCharacters(string textIn)
        {
            if (textIn == null || textIn == string.Empty)
                return string.Empty;
            
            if (textIn.All(c => XmlConvert.IsXmlChar(c)))
                return textIn;

            var textOut = new StringBuilder();

            for (int i = 0; i < textIn.Length; i++)
            {
                char c = textIn[i];
                if (XmlConvert.IsXmlChar(c))
                    textOut.Append(c);
            }

            return textOut.ToString();
        }

        public static void SkipWhitespaces(this XmlReader reader)
        {
            if (reader == null)
                return;

            while (!reader.EOF && reader.NodeType == XmlNodeType.Whitespace)
                if (!reader.Read())
                    break;
        }
    }
}
