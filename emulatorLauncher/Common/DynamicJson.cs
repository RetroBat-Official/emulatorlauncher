/*--------------------------------------------------------------------------
* DynamicJson
* ver 1.2.0.0 (May. 21th, 2010)
*
* created and maintained by neuecc <ils@neue.cc>
* licensed under Microsoft Public License(Ms-PL)
* 
* Modified by f.caruso
* 
*--------------------------------------------------------------------------*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Globalization;

namespace emulatorLauncher.Tools
{
    public class DynamicJson : DynamicObject
    {        
        /// <summary>
        /// Loads a json string
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static DynamicJson Parse(string json)
        {
            return Parse(json, Encoding.UTF8);
        }

        /// <summary>
        /// Loads a json string
        /// </summary>
        /// <param name="json"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static DynamicJson Parse(string json, Encoding encoding)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(encoding.GetBytes(json), XmlDictionaryReaderQuotas.Max))
            {
                return ToValue(XElement.Load(reader));
            }
        }
        /// <summary>
        /// Loads a json file
        /// </summary>
        /// <param name="jsonFile"></param>
        /// <returns></returns>
        public static DynamicJson Load(string jsonFile)
        {
            if (!File.Exists(jsonFile))
                return new DynamicJson() { _path = jsonFile };

            var bytes = File.ReadAllText(jsonFile);
            DynamicJson ret = Parse(bytes, Encoding.UTF8);
            ret._path = jsonFile;
            return ret;
        }

        private string _path;

        public void Save()
        {
            if (!string.IsNullOrEmpty(_path))
                Save(_path);
        }

        public void Save(string ymlFile)
        {
            File.WriteAllText(ymlFile, ToString());
        }

        public DynamicJson GetOrCreateContainer(string key, bool asArray = false)
        {
            DynamicJson result;

            var element = xml.Element(key);
            if (element == null)
            {
                var tempElement = new XElement(key, CreateTypeAttr(asArray ? JsonType.array : JsonType.@object));
                result = new DynamicJson(tempElement, asArray ? JsonType.array : JsonType.@object) { _temporaryParentObject = this };
                return result;
            }

            object ret;
            if (TryGet(element, out ret) && ret is DynamicJson)
                return (DynamicJson)ret;

            return null;
        }

        public DynamicJson GetObject(string key)
        {
            var element = xml.Element(key);
            if (element == null)
                return null;

            object ret;
            if (TryGet(element, out ret) && ret is DynamicJson)
                return ret as DynamicJson;

            return null;
        }

        public ArrayList GetArray(string key)
        {
            var element = xml.Element(key);            
            if (element == null)
                return new ArrayList();

            object ret;
            if (TryGet(element, out ret) && ret is DynamicJson)
            {
                DynamicJson dj = ret as DynamicJson;
                if (dj.IsArray)
                {
                    var arr = new ArrayList();
                    
                    foreach (var item in dj.xml.Elements().Select(x => ToValue(x)))
                        arr.Add(item);

                    return arr;
                }
            }

            return new ArrayList();
        }

        public void SetObject(string key, object obj)
        {
            if (obj is IEnumerable && !(obj is string))
                SetArray(key, (IEnumerable) obj);
            else
                TrySet(key, obj);
        }

        private void SetArray(string key, IEnumerable array)
        {
            DynamicJson ret = new DynamicJson() { _temporaryParentObject = this };
            ret.jsonType = JsonType.array;

            foreach (var item in array)
                ret.xml.Add(new XElement("item", CreateTypeAttr(GetJsonType(item)), CreateJsonNode(item)));

            TrySet(key, ret);
        }

        public string this[string key]
        {
            get
            {
                var element = xml.Element(key);
                if (element != null)
                {
                    object ret;
                    if (TryGet(element, out ret))
                    {
                        DynamicJson child = ret as DynamicJson;
                        if (child == null || child.IsArray)
                            return ret.ToString();
                    }
                }

                return null;
            }
            set
            {
                object newValue = value;

                var element = xml.Element(key);
                if (element != null)
                {
                    var type = (JsonType)Enum.Parse(typeof(JsonType), element.Attribute("type").Value);
                    switch (type)
                    {
                        case JsonType.boolean:
                            newValue = Convert.ToBoolean(value);
                            break;

                        case JsonType.number:
                            newValue = Convert.ToDouble(value);
                            break;

                        case JsonType.array:
                        case JsonType.@object:
                            newValue = Parse(value);
                            break;
                    }
                }
                else if (!string.IsNullOrEmpty(value))
                {
                    // Guess type from data
                    if (value.Length > 1 && value.StartsWith("\"") && value.EndsWith("\""))
                        newValue = value.Substring(1, value.Length - 2);
                    else if (value.ToLowerInvariant() == "true" || value.ToLowerInvariant() == "false")
                        newValue = Convert.ToBoolean(value);
                    else if (value.All(c => char.IsNumber(c) || c == '.' || c == '-'))
                        newValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    else if (value.Length > 1 && value.StartsWith("[") && value.EndsWith("]")) // Array
                        newValue = Parse(value);
                }

                DynamicJson dj = newValue as DynamicJson;
                if (dj != null && dj.IsArray)
                {
                    var final = new ArrayList();

                    dynamic enu = dj;
                    foreach (var item in enu)
                        final.Add(item);

                    newValue = final.ToArray();
                }

                TrySet(key, newValue);                
            }
        }

        /// <summary>create JsonSring from primitive or IEnumerable or Object({public property name:property value})</summary>
        public static string Serialize(object obj)
        {
            return CreateJsonString(new XStreamingElement("root", CreateTypeAttr(GetJsonType(obj)), CreateJsonNode(obj)));
        }

        private static dynamic ToValue(XElement element)
        {
            var type = (JsonType)Enum.Parse(typeof(JsonType), element.Attribute("type").Value);
            switch (type)
            {
                case JsonType.boolean:
                    return (bool)element;
                case JsonType.number:
                    return (double)element;
                case JsonType.@string:
                    return (string)element;
                case JsonType.@object:
                case JsonType.array:
                    return new DynamicJson(element, type);
                case JsonType.@null:
                default:
                    return null;
            }
        }

        private static JsonType GetJsonType(object obj)
        {
            if (obj == null) return JsonType.@null;

            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Boolean:
                    return JsonType.boolean;
                case TypeCode.String:
                case TypeCode.Char:
                case TypeCode.DateTime:
                    return JsonType.@string;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    return JsonType.number;
                case TypeCode.Object:
                    if (obj is DynamicJson)
                        return ((DynamicJson)obj).IsArray ? JsonType.array : JsonType.@object;
                    else 
                        return (obj is IEnumerable) ? JsonType.array : JsonType.@object;
                case TypeCode.DBNull:
                case TypeCode.Empty:
                default:
                    return JsonType.@null;
            }
        }

        private static XAttribute CreateTypeAttr(JsonType type)
        {
            return new XAttribute("type", type.ToString());
        }

        private static object CreateJsonNode(object obj)
        {
            var type = GetJsonType(obj);
            switch (type)
            {
                case JsonType.@string:
                case JsonType.number:
                    return obj;
                case JsonType.boolean:
                    return obj.ToString().ToLower();
                case JsonType.@object:
                    return CreateXObject(obj);
                case JsonType.array:
                    if (obj is DynamicJson)
                        return CreateXObject(obj);

                    return CreateXArray(obj as IEnumerable);
                case JsonType.@null:
                default:
                    return null;
            }
        }

        private static IEnumerable<XStreamingElement> CreateXArray<T>(T obj) where T : IEnumerable
        {
            return obj.Cast<object>()
                .Select(o => new XStreamingElement("item", CreateTypeAttr(GetJsonType(o)), CreateJsonNode(o)));
        }

        private static IEnumerable<XStreamingElement> CreateXObject(object obj)
        {
            DynamicJson dj = obj as DynamicJson;
            if (dj != null)
            {
                if (dj.IsArray)
                {
                    var ret = new List<XStreamingElement>();

                    foreach(var item in (dynamic)dj)
                        ret.Add(new XStreamingElement("item", CreateTypeAttr(GetJsonType(item)), CreateJsonNode(item)));

                    return ret.ToArray();
                }
                else
                {
                    return dj.GetDynamicMemberNames()
                        .Select(pi => new { Name = pi, Value = ToValue(dj.xml.Element(pi)) })
                        .Select(a => new XStreamingElement(a.Name, CreateTypeAttr(GetJsonType(a.Value)), CreateJsonNode(a.Value)));
                }
            }

            return obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(pi => new { Name = pi.Name, Value = pi.GetValue(obj, null) })
                .Select(a => new XStreamingElement(a.Name, CreateTypeAttr(GetJsonType(a.Value)), CreateJsonNode(a.Value)));
        }

        private static string CreateJsonString(XStreamingElement element)
        {
            using (var ms = new MemoryStream())
            {
                var methods = typeof(JsonReaderWriterFactory).GetMethods(BindingFlags.Static | BindingFlags.Public);
                var createJsonWriterEx = methods.FirstOrDefault(method => method.Name == "CreateJsonWriter" && method.GetParameters().Length == 4);

                using (var writer = createJsonWriterEx == null ?
                    JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, false) :
                    createJsonWriterEx.Invoke(null, new object[] { ms, Encoding.UTF8, false, true }) as System.Xml.XmlDictionaryWriter)
                {
                    element.WriteTo(writer);
                    writer.Flush();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }

        // dynamic structure represents JavaScript Object/Array

        XElement xml;
        JsonType jsonType;

        /// <summary>create blank JSObject</summary>
        public DynamicJson()
        {
            xml = new XElement("root", CreateTypeAttr(JsonType.@object));
            jsonType = JsonType.@object;
        }

        private DynamicJson(XElement element, JsonType type)
        {
            Debug.Assert(type == JsonType.array || type == JsonType.@object);

            xml = element;
            jsonType = type;
        }

        public bool IsObject { get { return jsonType == JsonType.@object; } }

        public bool IsArray { get { return jsonType == JsonType.array; } }

        /// <summary>has property or not</summary>
        public bool IsDefined(string name)
        {
            return IsObject && (xml.Element(name) != null);
        }

        /// <summary>has property or not</summary>
        public bool IsDefined(int index)
        {
            return IsArray && (xml.Elements().ElementAtOrDefault(index) != null);
        }

        /// <summary>delete property</summary>
        public bool Remove(string name)
        {
            var elem = xml.Element(name);
            if (elem != null)
            {
                elem.Remove();
                return true;
            }
            else return false;
        }

        /// <summary>delete property</summary>
        public bool Remove(int index)
        {
            var elem = xml.Elements().ElementAtOrDefault(index);
            if (elem != null)
            {
                elem.Remove();
                return true;
            }
            else return false;
        }

        /// <summary>mapping to Array or Class by Public PropertyName</summary>
        public T Deserialize<T>()
        {
            return (T)Deserialize(typeof(T));
        }

        private object Deserialize(Type type)
        {
            return (IsArray) ? DeserializeArray(type) : DeserializeObject(type);
        }

        private dynamic DeserializeValue(XElement element, Type elementType)
        {
            var value = ToValue(element);
            if (value is DynamicJson)
            {
                value = ((DynamicJson)value).Deserialize(elementType);
            }
            return Convert.ChangeType(value, elementType);
        }

        private object DeserializeObject(Type targetType)
        {
            var result = Activator.CreateInstance(targetType);
            var dict = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(pi => pi.Name, pi => pi);
            foreach (var item in xml.Elements())
            {
                PropertyInfo propertyInfo;
                if (!dict.TryGetValue(item.Name.LocalName, out propertyInfo)) continue;
                var value = DeserializeValue(item, propertyInfo.PropertyType);
                propertyInfo.SetValue(result, value, null);
            }
            return result;
        }

        private object DeserializeArray(Type targetType)
        {
            if (targetType.IsArray) // Foo[]
            {
                var elemType = targetType.GetElementType();
                dynamic array = Array.CreateInstance(elemType, xml.Elements().Count());
                var index = 0;
                foreach (var item in xml.Elements())
                {
                    array[index++] = DeserializeValue(item, elemType);
                }
                return array;
            }
            else // List<Foo>
            {
                var elemType = targetType.GetGenericArguments()[0];
                dynamic list = Activator.CreateInstance(targetType);
                foreach (var item in xml.Elements())
                {
                    list.Add(DeserializeValue(item, elemType));
                }
                return list;
            }
        }

        // Delete
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            result = (IsArray)
                ? Remove((int)args[0])
                : Remove((string)args[0]);
            return true;
        }

        // IsDefined, if has args then TryGetMember
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (args.Length > 0)
            {
                result = null;
                return false;
            }

            result = IsDefined(binder.Name);
            return true;
        }

        // Deserialize or foreach(IEnumerable)
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type == typeof(IEnumerable) || binder.Type == typeof(object[]))
            {
                var ie = (IsArray)
                    ? xml.Elements().Select(x => ToValue(x))
                    : xml.Elements().Select(x => (dynamic)new KeyValuePair<string, object>(x.Name.LocalName, ToValue(x)));
                result = (binder.Type == typeof(object[])) ? ie.ToArray() : ie;
            }
            else
            {
                result = Deserialize(binder.Type);
            }
            return true;
        }

        private bool TryGet(XElement element, out object result)
        {
            if (element == null)
            {
                result = null;
                return false;
            }

            result = ToValue(element);
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            return (IsArray)
                ? TryGet(xml.Elements().ElementAtOrDefault((int)indexes[0]), out result)
                : TryGet(xml.Element((string)indexes[0]), out result);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (IsArray)
            {
                if (binder.Name == "Length")
                {
                    result = xml.Elements().Count();
                    return true;
                }

                return TryGet(xml.Elements().ElementAtOrDefault(int.Parse(binder.Name)), out result);
            }

            var element = xml.Element(binder.Name);
            if (element == null)
            {
                var tempElement = new XElement(binder.Name, CreateTypeAttr(JsonType.@object));
                result = new DynamicJson(tempElement, JsonType.@object) { _temporaryParentObject = this };
                return true;
            }

            return TryGet(element, out result);
        }

        private DynamicJson _temporaryParentObject;

        private void AssignTemporaryParentObject()
        {
            if (_temporaryParentObject != null)
            {
                _temporaryParentObject.xml.Add(xml);

                _temporaryParentObject.AssignTemporaryParentObject();
                _temporaryParentObject = null;
            }
        }

        private bool TrySet(string name, object value)
        {
            AssignTemporaryParentObject();

            var type = GetJsonType(value);
            var element = xml.Element(name);
            if (element == null)
            {
                xml.Add(new XElement(name, CreateTypeAttr(type), CreateJsonNode(value)));
            }
            else
            {
                element.Attribute("type").Value = type.ToString();
                element.ReplaceNodes(CreateJsonNode(value));
            }

            return true;
        }

        private bool TrySet(int index, object value)
        {
            AssignTemporaryParentObject();

            var type = GetJsonType(value);
            var e = xml.Elements().ElementAtOrDefault(index);
            if (e == null)
            {
                xml.Add(new XElement("item", CreateTypeAttr(type), CreateJsonNode(value)));
            }
            else
            {
                e.Attribute("type").Value = type.ToString();
                e.ReplaceNodes(CreateJsonNode(value));
            }

            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            return (IsArray)
                ? TrySet((int)indexes[0], value)
                : TrySet((string)indexes[0], value);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return (IsArray)
                ? TrySet(int.Parse(binder.Name), value)
                : TrySet(binder.Name, value);
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return (IsArray)
                ? xml.Elements().Select((x, i) => i.ToString())
                : xml.Elements().Select(x => x.Name.LocalName);
        }

        /// <summary>Serialize to JsonString</summary>
        public override string ToString()
        {
            // <foo type="null"></foo> is can't serialize. replace to <foo type="null" />
            foreach (var elem in xml.Descendants().Where(x => x.Attribute("type").Value == "null"))
            {
                elem.RemoveNodes();
            }
            return CreateJsonString(new XStreamingElement("root", CreateTypeAttr(jsonType), xml.Elements()));
        }
        
        enum JsonType
        {
            @string, number, boolean, @object, array, @null
        }
    }


}