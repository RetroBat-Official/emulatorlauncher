using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Dynamic;

namespace emulatorLauncher.Tools
{
    class YmlFile : YmlContainer
    {
        public override string ToString()
        {        
            var sb = new StringBuilder();
            SerializeTo(sb);
            var final = sb.ToString();
            return final;        
        }

        public void Save()
        {
            if (!string.IsNullOrEmpty(_path))
                Save(_path);
        }

        public void Save(string ymlFile)
        {
            File.WriteAllText(ymlFile, ToString());
        }

        private string _path;

        public static YmlFile Parse(string yml)
        {
            var root = new YmlFile() { Name = "root", Indent = -1 };
            if (string.IsNullOrEmpty(yml))
                return root;

            YmlContainer current = root;

            Stack<YmlContainer> stack = new Stack<YmlContainer>();
            stack.Push(root);

            var lines = yml.Replace("\r\n", "\n").Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrEmpty(line))
                    continue;

                int indent = GetIndent(line);                
                string tmp = line.Trim();

                if (!string.IsNullOrEmpty(tmp))
                {
                    while (stack.Count > 0 && current.Indent >= indent)
                        current = stack.Pop();

                    if (!stack.Contains(current))
                        stack.Push(current);
                }

                int idx = tmp.IndexOf(":");
                if (idx >= 0)
                {
                    string name = tmp.Substring(0, idx).Trim();
                    string value = tmp.Substring(idx + 1).Trim();

                    if (string.IsNullOrEmpty(value))
                    {
                        var folder = new YmlContainer() { Name = name, Indent = indent };
                        current.Elements.Add(folder);
                        stack.Push(folder);
                        current = folder;
                    }
                    else
                    {
                        if (value == "|" || value == ">")
                        {
                            StringBuilder sbValue = new StringBuilder();
                            
                            i++;
                            while (i < lines.Length)
                            {
                                var childLine = lines[i].Replace("\r", "");

                                if (childLine.Trim().Length == 0)
                                {                     
                                    if (value == "|")
                                        sbValue.AppendLine();
                                    else
                                        sbValue.Append(" ");

                                    i++;
                                    continue;
                                }

                                int childIndent = GetIndent(childLine);
                                if (childIndent <= indent)
                                    break;

                                if (sbValue.Length > 0)
                                {
                                    if (value == "|")
                                        sbValue.AppendLine();
                                    else
                                        sbValue.Append(" ");
                                }

                                sbValue.Append(childLine.Substring((indent + 1) * 2));
                                i++;
                            }

                            if (sbValue.Length > 0)
                                sbValue.AppendLine();

                            i--;
                            value = sbValue.ToString();
                        }

                        var item = new YmlElement() { Name = name, Value = value };
                        current.Elements.Add(item);
                    }
                }
                else if (!string.IsNullOrEmpty(tmp))
                {
                    var item = new YmlElement() { Name = "", Value = tmp };
                    current.Elements.Add(item);
                }
            }

            return root;
        }

        private static int GetIndent(string line)
        {
            int indent = 0;
            foreach (var chr in line)
                if (chr == 32)
                    indent++;
                else
                    break;

            indent /= 2;
            return indent;
        }

        public static YmlFile Load(string ymlFile)
        {
            var root = new YmlFile() { Name = "root", Indent = -1 };
            root._path = ymlFile;
            if (!File.Exists(ymlFile))
                return root;

            string yml = File.ReadAllText(ymlFile);

            YmlFile file = Parse(yml);
            file._path = ymlFile;
            return file;
        }
    }

    interface IYmlElement
    {
        string Name { get; set; }
    }
    
    class YmlElement : IYmlElement
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Name + ": " + Value.ToString();
        }
    }

    class YmlContainer : DynamicObject, IYmlElement, IEnumerable<IYmlElement>
    {        
        #region DynamicObject
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return Elements.Where(d => !string.IsNullOrEmpty(d.Name)).Select(d => d.Name);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var element = Elements.FirstOrDefault(e => binder.Name.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
            if (element == null)
                element = Elements.FirstOrDefault(e => binder.Name.Replace("_", " ").Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));

            if (element != null)
            {
                YmlElement elt = element as YmlElement;
                if (elt != null)
                    result = elt.Value;
                else
                    result = element;

                return true;
            }
            
            result = null;
            return true;
        }

        class YmlSetMemberBinder : SetMemberBinder
        {
            public YmlSetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase) { }
            public override DynamicMetaObject FallbackSetMember(DynamicMetaObject target, DynamicMetaObject value, DynamicMetaObject errorSuggestion) { return null; }
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            if (value == null)
            {
                this.Remove(binder.Name.Replace("_", " "));
                this.Remove(binder.Name);
            }
            else if (value is string || value.GetType().IsValueType)
            {
                string newValue = value.ToString();

                if (value is bool)
                    newValue = ((bool)value).ToString().ToLowerInvariant();
                else if (value is decimal || value is float || value is double)
                    newValue = ((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture);

                var element = Elements.FirstOrDefault(e => binder.Name.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element == null)
                {
                    element = Elements.FirstOrDefault(e => binder.Name.Replace("_", " ").Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (element != null)
                    {
                        this[binder.Name.Replace("_", " ")] = newValue;
                    }
                }

                this[binder.Name] = newValue;
            }
            else
            {
                YmlContainer container = GetOrCreateContainer(binder.Name);

                foreach (var item in value.GetType()
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Select(pi => new { Name = pi.Name, Value = pi.GetValue(value, null) }))
                {
                    if (item.Value != null)
                        container.TrySetMember(new YmlSetMemberBinder(item.Name, binder.IgnoreCase), item.Value);
                }
                    
            }

            return true;
        }
        #endregion

        public YmlContainer()
        {
            Elements = new List<IYmlElement>();
        }

        public string Name { get; set; }

        private void AddElement(IYmlElement element)
        {
            YmlElement last = (Elements.Count > 0 ? Elements[Elements.Count - 1] : null) as YmlElement;
            if (last != null && string.IsNullOrEmpty(last.Name) && last.Value == "...")
            {
                Elements.Insert(Elements.Count - 1, element);
                return;
            }

            Elements.Add(element);
        }

        public YmlContainer GetContainer(string key)
        {
            return Elements.OfType<YmlContainer>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
        }

        public YmlContainer GetOrCreateContainer(string key)
        {
            var element = GetContainer(key);
            if (element == null)
            {
                element = new YmlContainer() { Name = key };

                // Convert Element to Container
                var item = Elements.FirstOrDefault(e => !(e is YmlContainer) && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (item != null)
                {
                    int pos = Elements.IndexOf(item);
                    Elements.Remove(item);
                    Elements.Insert(pos, element);
                }
                else
                    AddElement(element);
            }

            return element;
        }

        public string this[string key]
        {
            get
            {
                var element = Elements.OfType<YmlElement>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element != null)
                    return element.Value;

                return null;
            }
            set
            {
                var element = Elements.OfType<YmlElement>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element == null)
                {
                    element = new YmlElement() { Name = key };

                    // Convert Container to Element
                    var container = Elements.FirstOrDefault(e => e is YmlContainer && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (container != null)
                    {
                        int pos = Elements.IndexOf(container);
                        Elements.Remove(container);
                        Elements.Insert(pos, element);
                    }
                    else                    
                        AddElement(element);
                }

                element.Value = value;
            }
        }

        public void Remove(string key)
        {
            var element = Elements.FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
            if (element != null)
                Elements.Remove(element);
        }

        public List<IYmlElement> Elements { get; private set; }

        public override string ToString()
        {
            return "[Folder] " + (Name ?? "");
        }

        public IEnumerator<IYmlElement> GetEnumerator()
        {
            return Elements.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Elements.GetEnumerator();
        }

        protected void SerializeTo(StringBuilder sb, int indent = 0)
        {
            foreach (var item in Elements)
            {
                YmlContainer container = item as YmlContainer;
                if (container != null)
                {
                    if (container.Elements.Count > 0)
                    {
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(item.Name);
                        sb.AppendLine(":");

                        container.SerializeTo(sb, indent + 1);
                    }

                    continue;
                }

                YmlElement element = item as YmlElement;
                if (element == null)
                    continue;

                if (element.Value == null)
                    continue;

                sb.Append(new string(' ', indent * 2));

                if (!string.IsNullOrEmpty(element.Name))
                {
                    sb.Append(element.Name);
                    sb.Append(": ");
                    
                    if (element.Value.Contains("\r\n"))
                    {
                        sb.AppendLine("|");

                        var offset = new string(' ', (indent+1) * 2);
                        var lines = element.Value.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                        for (int i = 0; i < lines.Length - 1; i++)
                        {
                            sb.Append(offset);
                            sb.AppendLine(lines[i]);
                        }
                    }
                    else
                        sb.AppendLine(element.Value);
                }
                else
                    sb.AppendLine(element.Value);
            }
        }

        internal int Indent;
    }

    class SimpleYml<T> : IEnumerable<T> where T : new()
    {
        private List<T> _values;

        private static List<T> FillElements(object obj, YmlContainer ymlElements)
        {
            List<T> ret = null; 

            foreach (var ymlEntry in ymlElements.Elements)
            {
                YmlContainer container = ymlEntry as YmlContainer;
                if (container != null)
                {
                    if (typeof(T).Equals(obj))
                    {
                        T current = Activator.CreateInstance<T>();

                        var ymlNameProperty = typeof(T).GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(YmlNameAttribute)));
                        if (ymlNameProperty != null)
                            ymlNameProperty.SetValue(current, container.Name, null);
                       
                        FillElements(current, container);

                        if (ret == null)
                            ret = new List<T>();

                        ret.Add(current);
                    }
                    else
                    {
                        var propertyType = (obj is Type) ? (Type)obj : obj.GetType();
                        var objectProperty = propertyType.GetProperty(ymlEntry.Name);
                        if (objectProperty != null && !objectProperty.PropertyType.IsValueType && objectProperty.PropertyType != typeof(string))
                        {
                            object child = Activator.CreateInstance(objectProperty.PropertyType);

                            var ymlNameProperty = objectProperty.PropertyType.GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(YmlNameAttribute)));
                            if (ymlNameProperty != null)
                                ymlNameProperty.SetValue(obj, container.Name, null);

                            FillElements(child, container);
                            objectProperty.SetValue(obj, child, null);
                        }
                    }
                    continue;
                }

                var type = (obj is Type) ? (Type)obj : obj.GetType();

                var property = type.GetProperty(ymlEntry.Name);
                if (property != null && ymlEntry is YmlElement)
                    property.SetValue(obj, ((YmlElement)ymlEntry).Value, null);
            }

            return ret;
        }

        public static SimpleYml<T> Parse(string yml)
        {
            var ret = new SimpleYml<T>();
            ret._values = FillElements(typeof(T), YmlFile.Parse(yml));
            return ret;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _values.GetEnumerator();
        }
    }

    class YmlNameAttribute : Attribute { }
}
