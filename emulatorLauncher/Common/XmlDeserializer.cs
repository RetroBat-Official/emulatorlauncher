using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace emulatorLauncher
{
    class XmlDeserializer
    {
        public static T DeserializeFile<T>(string xml) where T : class
        {
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Auto,
                ValidationType = ValidationType.None
            };

            using (var reader = new FileStream(xml, FileMode.Open))
            {
                using (var xmlReader = XmlReader.Create(reader, settings))
                {
                    var obj = DeserializeType(xmlReader, typeof(T));
                    return (T)obj;
                }
            }
        }

        public static T DeserializeString<T>(string xml) where T : class
        {
            xml = XmlExtensions.StripInvalidXMLCharacters(xml);

            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Auto,
                ValidationType = ValidationType.None
            };

            using (var reader = new StringReader(xml))
            {
                using (var xmlReader = XmlReader.Create(reader, settings))
                {
                    var obj = DeserializeType(xmlReader, typeof(T));
                    return (T)obj;
                }
            }
        }

        #region Déserialisation
        private static object DeserializeType(XmlReader reader, Type t)
        {            
            reader.SkipWhitespaces();

            if (t.IsValueType || t == typeof(string))
                return ReadValueType(reader, t);

            reader.MoveToContent();

            if (reader.HasAttributes)
            {
                for (int i = 0; i < reader.AttributeCount; i++)
                {
                    reader.MoveToNextAttribute();

                    if (reader.LocalName == "type")
                    {
                        string xmlType = reader.Value;

                        int ns = xmlType == null ? -1 : xmlType.IndexOf(":");
                        if (ns > 0)
                            xmlType = xmlType.Substring(ns + 1);

                        XmlIncludeAttribute[] xmlIncludes = (XmlIncludeAttribute[])t.GetCustomAttributes(typeof(XmlIncludeAttribute), true);
                        XmlIncludeAttribute xtype = xmlIncludes.FirstOrDefault(x => x.Type.Name.Equals(xmlType, StringComparison.InvariantCultureIgnoreCase));
                        if (xtype != null)
                            t = xtype.Type;
                    }
                }
            }

            reader.MoveToElement();
            
            TypeMapping xmlMapping = GetTypeMapping(t);

            object ret = null;
            if (xmlMapping != null)
            {
                try { ret = xmlMapping.Constructor.Invoke(null); }
                catch { }
            }

            if (reader.IsEmptyElement)
                return ret;
            else
            {
                reader.ReadStartElement();
                reader.SkipWhitespaces();
            }

            var lists = new Dictionary<PropertyInfo, IList>();

            if (xmlMapping != null && ret != null)
            {
                foreach (var propertyMapping in xmlMapping.Properties.Values)
                {
                    var property = propertyMapping.Property;
                    if (!property.PropertyType.IsValueType && property.PropertyType != typeof(string) &&
                        property.PropertyType.IsGenericType && typeof(IList).IsAssignableFrom(property.PropertyType))
                    {
                        IList lst = property.GetValue(ret, null) as IList;
                        if (lst == null && property.CanWrite)
                        {
                            lst = Activator.CreateInstance(property.PropertyType) as IList;
                            property.SetValue(ret, lst, null);
                        }

                        if (lst != null)
                            lists[property] = lst;
                    }
                }
            }

            while (reader.IsStartElement())
            {
                var localName = reader.LocalName;

                if (reader.IsEmptyElement)
                {
                    reader.Read();
                    reader.SkipWhitespaces();
                    continue;
                }

                PropertyMapping propertyMapping = null;
                if (ret != null && xmlMapping != null && xmlMapping.Properties.TryGetValue(localName, out propertyMapping))
                {
                    var property = propertyMapping.Property;
                    var propertyType = property.PropertyType;

                    if (propertyType.IsValueType || propertyType == typeof(string))
                    {
                        if (propertyType.IsGenericType && propertyType.Name.StartsWith("Nullable"))
                            propertyType = propertyType.GetGenericArguments().FirstOrDefault();

                        if (propertyType != null)
                        {
                            object val = DeserializeType(reader, propertyType);
                            if (val != null)
                                property.SetValue(ret, val, null);

                            reader.SkipWhitespaces();
                            continue;
                        }
                    }
                    else if (propertyType.IsGenericType && typeof(IList).IsAssignableFrom(propertyType))
                    {
                        IList lst;
                        if (!lists.TryGetValue(property, out lst))
                        {
                            lst = Activator.CreateInstance(propertyType) as IList;
                            lists[property] = lst;
                        }

                        Type tp = propertyType.GetGenericArguments().FirstOrDefault();

                        if (propertyMapping.XmlElement == null)
                        {
                            string xmlTypeName = propertyMapping.Property.Name;

                            if (propertyType.IsGenericType)
                            {
                                var genericType = propertyType.GetGenericArguments().FirstOrDefault();
                                if (genericType != null)
                                {
                                    XmlTypeAttribute[] types = (XmlTypeAttribute[])genericType.GetCustomAttributes(typeof(XmlTypeAttribute), false);
                                    if (types.Length > 0)
                                        xmlTypeName = types[0].TypeName;
                                    else
                                        xmlTypeName = GetXmlTypeName(genericType);                                   
                                }
                            }

                            if (reader.IsEmptyElement)
                                reader.Read();
                            else
                            {
                                reader.Read();
                                reader.SkipWhitespaces();

                                while (!reader.EOF && reader.NodeType == XmlNodeType.Element && reader.Name == xmlTypeName)
                                {
                                    object value = DeserializeType(reader, tp);
                                    if (value != null)
                                        lst.Add(value);

                                    if (!reader.EOF && !reader.IsEmptyElement)
                                    {
                                        if (reader.NodeType == XmlNodeType.Text)
                                            while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                                                if (!reader.Read())
                                                    break;

                                        if (reader.NodeType == XmlNodeType.EndElement)
                                            reader.ReadEndElement();
                                    }

                                    reader.SkipWhitespaces();
                                }

                                if (!reader.EOF && reader.NodeType == XmlNodeType.EndElement && reader.Name == propertyMapping.Property.Name)
                                    reader.ReadEndElement();
                            }

                            reader.SkipWhitespaces();
                            continue;
                        }
                        else
                        {
                            object value = null;

                            if (tp == typeof(string) || tp.IsValueType)
                            {                                
                                if (!reader.EOF && !reader.IsEmptyElement)
                                {
                                    value = ReadValueType(reader, tp);
                                    if (value != null)
                                        lst.Add(value);
                                }

                                reader.SkipWhitespaces();
                                continue;
                            }
                            else
                                value = DeserializeType(reader, tp);

                            if (value != null)
                                lst.Add(value);

                            if (!reader.EOF && !reader.IsEmptyElement)
                            {
                                if (reader.NodeType == XmlNodeType.Text)
                                    while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                                        if (!reader.Read())
                                            break;

                                reader.ReadEndElement();
                            }

                            reader.SkipWhitespaces();

                            if (!reader.EOF && reader.NodeType == XmlNodeType.EndElement)
                            {
                                if (typeof(IEnumerable).IsAssignableFrom(propertyType) && (propertyType.IsGenericType || propertyType.IsArray))
                                {
                                    string nameOfElement = string.IsNullOrEmpty(propertyMapping.XmlElement.ElementName) ? propertyMapping.Property.Name : propertyMapping.XmlElement.ElementName;
                                    if (reader.Name == nameOfElement)
                                    {
                                        reader.ReadEndElement();
                                        reader.SkipWhitespaces();
                                    }
                                }
                            }

                            continue;
                        }
                    }
                    else
                    {
                        Type xmlType = propertyType;

                        bool convert = false;

                        if (propertyMapping.XmlElement != null && propertyMapping.XmlElement.Type != null && propertyMapping.XmlElement.Type != xmlType)
                        {
                            convert = true;
                            xmlType = propertyMapping.XmlElement.Type;
                        }

                        object value = DeserializeType(reader, xmlType);
                        if (value != null)
                        {
                            if (convert)
                            {
                                var converter = xmlType.GetMethod("op_Implicit", new[] { xmlType });
                                if (converter != null)
                                    value = converter.Invoke(null, new[] { value });
                            }

                            property.SetValue(ret, value, null);
                        }
                    }
                }
                else
                {
                    XmlTypeAttribute[] types = (XmlTypeAttribute[])t.GetCustomAttributes(typeof(XmlTypeAttribute), false);
                    if (types.Length > 0 && types[0].TypeName == localName)
                        return DeserializeType(reader, t);

                    if (reader.IsEmptyElement)
                        reader.Read();
                    else
                        reader.Skip();

                    reader.SkipWhitespaces();
                    continue;
                }

                if (!reader.EOF)
                {
                    if (reader.IsEmptyElement)
                        reader.Read();
                    else
                    {
                        if (reader.NodeType == XmlNodeType.Text)
                            while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                                if (!reader.Read())
                                    break;

                        reader.ReadEndElement();
                    }
                }
                reader.SkipWhitespaces();
            }

            return ret;
        }

        private static object ReadValueType(XmlReader reader, Type t)
        {
            if (!t.IsValueType && t != typeof(string))
                return null;

            if (reader.EOF)
                return null;

            if (reader.IsEmptyElement)
            {
                reader.Read();
                reader.SkipWhitespaces();
                return null;
            }

            try
            {
                if (t.IsEnum)
                {
                    string value = reader.ReadElementContentAsString();
                    return GetXmlEnumValue(t, value);
                }

                if (t == typeof(Int32))
                {
                    string strValue = reader.ReadElementContentAsString();

                    int value = 0;
                    int.TryParse(strValue, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value);
                    return value;
                }

                if (t == typeof(Decimal))
                {
                    string strValue = reader.ReadElementContentAsString();

                    decimal value = 0;
                    decimal.TryParse(strValue, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out value);
                    return value;
                }

                try
                {
                    object valueTypeValue = reader.ReadElementContentAs(t, null);
                    reader.SkipWhitespaces();
                    return valueTypeValue;
                }
                catch
                {
                    return t == typeof(string) ? null : Activator.CreateInstance(t);
                }
            }
            catch
            {
                reader.Skip();
                reader.SkipWhitespaces();
                return null;
            }
        }

        private static object GetXmlEnumValue(System.Type value, string xmlEnum)
        {
            foreach (FieldInfo fi in value.GetFields())
            {
                var attributes = (XmlEnumAttribute[])fi.GetCustomAttributes(typeof(XmlEnumAttribute), false);
                if (attributes.Length > 0 && attributes[0].Name == xmlEnum)
                    return fi.GetValue(fi.Name);

                if (fi.Name == xmlEnum)
                    return fi.GetValue(fi.Name);
            }

            try { return Enum.Parse(value, xmlEnum); }
            catch { }

            return null;
        }

        private static string GetXmlTypeName(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return "boolean";

                case TypeCode.SByte:
                    return "byte";

                case TypeCode.Byte:
                    return "unsignedByte";

                case TypeCode.Int16:
                    return "short";

                case TypeCode.UInt16:
                    return "unsignedShort";

                case TypeCode.Int32:
                    return "int";

                case TypeCode.UInt32:
                    return "unsignedInt";

                case TypeCode.Int64:
                    return "long";

                case TypeCode.UInt64:
                    return "unsignedLong";

                case TypeCode.Single:
                    return "float";

                case TypeCode.Double:
                    return "double";

                case TypeCode.Decimal:
                    return "decimal";

                case TypeCode.DateTime:
                    return "dateTime";

                case TypeCode.String:
                    return "string";

                default:

                    if (type == typeof(byte[]))
                        return "base64Binary";

                    if (type == typeof(Guid))
                        return "guid";

                    return type.Name;
            }
        }
        #endregion

        #region Cache
        class PropertyMapping
        {
            public PropertyInfo Property { get; set; }
            public XmlElementAttribute XmlElement { get; set; }
        }

        class TypeMapping
        {
            public TypeMapping()
            {
                Properties = new Dictionary<string, PropertyMapping>();
            }

            public ConstructorInfo Constructor { get; set; }
            public Dictionary<string, PropertyMapping> Properties { get; private set; }
        }

        static object _lock = new object();

        static TypeMapping GetTypeMapping(Type t)
        {
            lock (_lock)
            {
                TypeMapping xmlMapping;
                if (!_xmlTypeMappings.TryGetValue(t, out xmlMapping))
                {
                    xmlMapping = new TypeMapping();
                    xmlMapping.Constructor = t.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new Type[] { }, null);

                    PropertyInfo[] properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    foreach (PropertyInfo property in properties)
                    {
                        if (Attribute.IsDefined(property, typeof(XmlIgnoreAttribute)))
                            continue;

                        if ((property.PropertyType.IsValueType || property.PropertyType == typeof(string)) && !property.CanWrite)
                            continue;

                        string name = property.Name;

                        PropertyMapping pm = new PropertyMapping();
                        pm.Property = property;

                        if (Attribute.IsDefined(property, typeof(XmlElementAttribute)))
                        {
                            XmlElementAttribute xmlElement = property.GetCustomAttributes(typeof(XmlElementAttribute), true).FirstOrDefault() as XmlElementAttribute;
                            if (xmlElement != null)
                            {
                                if (!string.IsNullOrEmpty(xmlElement.ElementName))
                                    name = xmlElement.ElementName;

                                pm.XmlElement = xmlElement;
                            }
                        }

                        xmlMapping.Properties[name] = pm;
                    }

                    _xmlTypeMappings[t] = xmlMapping;
                }

                return xmlMapping;
            }
        }
        
        private static Dictionary<Type, TypeMapping> _xmlTypeMappings = new Dictionary<Type, TypeMapping>();
        #endregion
    }
}
