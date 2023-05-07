using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Xml;
using System.ComponentModel;
using System.Reflection;

namespace emulatorLauncher
{
    public static class XmlReflectionSerializer
    {
        public static string ToXml(object obj, bool omitXmlDeclaration = false)
        {
            return ToXml(obj, new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = true,
                    OmitXmlDeclaration = omitXmlDeclaration
                });
        }

        public static string ToXml(object obj, XmlWriterSettings xmlWriterSettings)
        {
            using (var memoryStream = new MemoryStream())
            {
                ToXmlStream(obj, memoryStream, xmlWriterSettings);
                
                memoryStream.Seek(0, SeekOrigin.Begin); //Rewind the Stream.
                var xml = Encoding.UTF8.GetString(memoryStream.ToArray());
                return xml;
            }
        }

        public static void ToXmlStream(object obj, Stream stream, XmlWriterSettings xmlWriterSettings)
        {
            if (obj == null)
                return;

            using (var writer = XmlWriter.Create(stream, xmlWriterSettings))
            {
                var typeInfo = TypeCache.GetTypeInfo(obj.GetType());
                var xmlRoot = typeInfo.Attributes.OfType<XmlRootAttribute>().FirstOrDefault();
                if (xmlRoot != null && !string.IsNullOrEmpty(xmlRoot.ElementName))
                    writer.WriteStartElement(xmlRoot.ElementName, xmlRoot.Namespace);
                else
                {
                    string ns;
                    string name = GetTypeName(obj, out ns);
                    writer.WriteStartElement(name, ns);
                }

                ReflectionSerialize(writer, obj);
                writer.WriteEndElement();
            }
        }

        private static void ReflectionSerialize(XmlWriter writer, object obj)
        {
            if (obj == null)
                return;

            if (obj is IXmlSerializable)
            {
                ((IXmlSerializable)obj).WriteXml(writer);
                return;
            }

            var t = TypeCache.GetTypeInfo(obj.GetType());

            if (t.Type == typeof(string) || t.Type.IsValueType)
            {
                string data = ToXmlValue(obj, t.Type);
                if (data != null)
                    writer.WriteValue(data);

                return;
            }
            else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(t.Type))
            {
                foreach (var item in (System.Collections.IEnumerable)obj)
                {
                    if (item == null)
                        continue;

                    string ns = null;
                    var typeName = GetTypeName(item, out ns);

                    var xmlType = TypeCache.GetTypeInfo(item.GetType()).Attributes.OfType<XmlTypeAttribute>().FirstOrDefault();
                    if (xmlType != null && !string.IsNullOrEmpty(xmlType.TypeName))
                    {
                        typeName = xmlType.TypeName;
                        ns = xmlType.Namespace;
                    }

                    writer.WriteStartElement(typeName);
                    ReflectionSerialize(writer, item);
                    writer.WriteEndElement();
                }

                return;
            }

            XmlIncludeAttribute xatt = t.Attributes.OfType<XmlIncludeAttribute>().FirstOrDefault(x => x.Type == obj.GetType());
            if (xatt != null)
                writer.WriteAttributeString("type", "http://" + "www.w3.org/2001/XMLSchema-instance", xatt.Type.Name); // "xsi"

            var properties = t.Properties;

            foreach (var prop in properties.Where(p => p.HasAttribute(typeof(XmlAttributeAttribute))))
                ReflectionSerializeProperty(writer, prop, obj);

            foreach (var prop in properties.Where(p => !p.HasAttribute(typeof(XmlAttributeAttribute))))
                ReflectionSerializeProperty(writer, prop, obj);
        }

        private static void ReflectionSerializeProperty(XmlWriter writer, PropertyCache propertyCache, object obj)
        {
            if (propertyCache.HasAttribute(typeof(XmlIgnoreAttribute)))
                return;

            PropertyInfo prop = propertyCache.Property;

            object value = prop.GetValue(obj, null);
            if (value == null)
                return;

            var attributes = propertyCache.Attributes;

            var defaultAttribute = attributes.OfType<DefaultValueAttribute>().FirstOrDefault();
            if (defaultAttribute != null && value.Equals(defaultAttribute.Value))
                return;

            if (propertyCache.ShouldSerializeMethod != null)
            {
                bool ret = (bool) propertyCache.ShouldSerializeMethod.Invoke(obj, null);
                if (!ret)
                    return;
            }

            Type propType = propertyCache.PropertyType;

            bool hasXmlElementName = false;
            bool isAttribute = false;

            string name = prop.Name;

            XmlElementAttribute[] xmlElements = attributes.OfType<XmlElementAttribute>().ToArray();
            if (xmlElements.Length > 0 && !string.IsNullOrEmpty(xmlElements[0].ElementName))
            {
                hasXmlElementName = true;
                name = xmlElements[0].ElementName;
            }

            var xmlAttribute = attributes.OfType<XmlAttributeAttribute>().FirstOrDefault();
            if (xmlAttribute != null && !string.IsNullOrEmpty(xmlAttribute.AttributeName))
            {
                isAttribute = true;
                name = xmlAttribute.AttributeName;
            }

            bool isXmlText = attributes.OfType<XmlTextAttribute>().Any();

            if (propType == typeof(string) || propType.IsValueType)
            {
                string data = ToXmlValue(value, propType);

                if (isAttribute)
                    writer.WriteAttributeString(name, data);
                else if (isXmlText)
                    writer.WriteValue(value.ToString());
                else
                    writer.WriteElementString(name, data);
            }
            else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propType))
            {
                if (hasXmlElementName)
                {
                    foreach (var item in (System.Collections.IEnumerable)value)
                    {
                        if (item == null)
                            continue;

                        if (xmlElements.Length > 1)
                        {
                            var xe = xmlElements.FirstOrDefault(a => item.GetType() == a.Type);
                            if (xe != null)
                                name = xe.ElementName;
                        }

                        writer.WriteStartElement(name);
                        ReflectionSerialize(writer, item);
                        writer.WriteEndElement();
                    }
                }
                else
                {
                    var xmlArray = attributes.OfType<XmlArrayAttribute>().FirstOrDefault();
                    if (xmlArray != null && !string.IsNullOrEmpty(xmlArray.ElementName))
                        name = xmlArray.ElementName;

                    string arrayItemName = null;

                    var xmlArrayItem = attributes.OfType<XmlArrayItemAttribute>().FirstOrDefault();
                    if (xmlArrayItem != null && !string.IsNullOrEmpty(xmlArrayItem.ElementName))
                        arrayItemName = xmlArrayItem.ElementName;

                    writer.WriteStartElement(name);

                    foreach (var item in (System.Collections.IEnumerable)value)
                    {
                        if (item == null)
                            continue;

                        if (!string.IsNullOrEmpty(arrayItemName))
                            writer.WriteStartElement(arrayItemName);
                        else
                        {
                            string ns = null;
                            var typeName = GetTypeName(item, out ns);

                            var xmlType = TypeCache.GetTypeInfo(item.GetType()).Attributes.OfType<XmlTypeAttribute>().FirstOrDefault();
                            if (xmlType != null && !string.IsNullOrEmpty(xmlType.TypeName))
                            {
                                typeName = xmlType.TypeName;
                                ns = xmlType.Namespace;
                            }

                            writer.WriteStartElement(typeName);
                        }

                        ReflectionSerialize(writer, item);
                        writer.WriteEndElement();
                    }

                    writer.WriteEndElement();
                }
            }
            else
            {
                writer.WriteStartElement(name);
                ReflectionSerialize(writer, value);
                writer.WriteEndElement();
            }
        }

        private static string ToXmlValue(object value, Type propType)
        {
            string data = value.ToString();

            if (propType == typeof(string))
                return data;

            if (propType == typeof(double))
                return XmlConvert.ToString((double)value);
            
            if (propType == typeof(float))
                return XmlConvert.ToString((float)value);
            
            if (propType == typeof(decimal))
                return XmlConvert.ToString((decimal)value);

            if (propType == typeof(DateTime))
                return XmlConvert.ToString(((DateTime)value), XmlDateTimeSerializationMode.RoundtripKind);
            
            if (propType == typeof(Guid))
                return XmlConvert.ToString((Guid)value);
            
            if (propType == typeof(TimeSpan))
                return XmlConvert.ToString((TimeSpan)value);
            
            if (propType == typeof(DateTimeOffset))
                return XmlConvert.ToString((DateTimeOffset)value);

            return data;
        }

        private static string GetTypeName(object o, out string ns)
        {
            ns = null;
            string clsName = null;

            Type type = o.GetType();

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return "boolean";

                case TypeCode.Char:
                    clsName = "char";
                    ns = "http://microsoft.com/wsdl/types/";
                    break;

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

                    if (type.IsArray && type.HasElementType)
                        return "ArrayOf"+type.GetElementType().Name;
                    else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type.IsGenericType)
                        return "ArrayOf" + type.GetGenericArguments().First().Name;
                    else if (type == typeof(byte[]))
                    {
                        clsName = "base64Binary";
                    }
                    else if (type == typeof(Guid))
                    {
                        clsName = "guid";
                        ns = "http://microsoft.com/wsdl/types/";
                    }
                    else
                        clsName = type.Name;

                    break;
            }

            return clsName;
        }

        #region Reflection cache
        class TypeCache
        {
            public static TypeCache GetTypeInfo(Type t)
            {
                lock (_lock)
                {
                    TypeCache ti;
                    if (_cache.TryGetValue(t, out ti))
                        return ti;

                    ti = new TypeCache();
                    ti.Type = t;
                    ti.Attributes = t.GetCustomAttributes(false);
                    ti.Properties = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                        .OrderBy(p => p.DeclaringType == t)
                        .Select(p => new PropertyCache(p))
                        .ToArray();

                    _cache[t] = ti;

                    return ti;
                }               
            }

            static object _lock = new object();

            static Dictionary<Type, TypeCache> _cache = new Dictionary<Type, TypeCache>();

            public Type Type { get; set; }
            public object[] Attributes { get; set; }
            public PropertyCache[] Properties { get; set; }
        }

        class PropertyCache
        {
            public PropertyCache(PropertyInfo prop)
            {
                Property = prop;
                Attributes = prop.GetCustomAttributes(false);

                PropertyType = prop.PropertyType;
                if (PropertyType.IsGenericType && PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    PropertyType = Nullable.GetUnderlyingType(PropertyType);

                ShouldSerializeMethod = prop.DeclaringType.GetMethod("ShouldSerialize" + prop.Name);               
            }

            public bool HasAttribute(Type t)
            {
                return Attributes.Any(a => a.GetType() == t);
            }

            public PropertyInfo Property { get; set; }
            public object[] Attributes { get; set; }
            public Type PropertyType { get; set; }
            public MethodInfo ShouldSerializeMethod { get; set; }
        }
        #endregion
    }
}
