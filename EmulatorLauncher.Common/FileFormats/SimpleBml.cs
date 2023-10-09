using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Dynamic;

namespace EmulatorLauncher.Common.FileFormats
{
    public class BmlFile : BmlContainer
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

        public void Save(string bmlFile)
        {
            File.WriteAllText(bmlFile, ToString());
        }

        private string _path;

        public static BmlFile Parse(string bml)
        {
            var root = new BmlFile() { Name = "root", Indent = -1 };
            if (string.IsNullOrEmpty(bml))
                return root;

            BmlContainer current = root;

            Stack<BmlContainer> stack = new Stack<BmlContainer>();
            stack.Push(root);

            var lines = bml.Replace("\r\n", "\n").Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.None);
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
                if (idx >= 0 || idx == -1)
                {
                    if (idx >= 0)
                    {
                        string name = tmp.Substring(0, idx).Trim();
                        string value = tmp.Substring(idx + 1).Trim();
                        var item = new BmlElement() { Name = name, Value = value };
                        current.Elements.Add(item);
                    }

                    if (idx == -1)
                    {
                        string name = tmp.Trim();
                        var folder = new BmlContainer() { Name = name, Indent = indent };
                        current.Elements.Add(folder);
                        stack.Push(folder);
                        current = folder;
                    }
                    /*else
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
                        }*/
                }
                else if (!string.IsNullOrEmpty(tmp))
                {
                    var item = new BmlElement() { Name = "", Value = tmp };
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

        public static BmlFile Load(string bmlFile)
        {
            var root = new BmlFile() { Name = "root", Indent = -1 };
            root._path = bmlFile;
            if (!File.Exists(bmlFile))
                return root;

            string bml = File.ReadAllText(bmlFile);

            BmlFile file = Parse(bml);
            file._path = bmlFile;
            return file;
        }
    }

    public interface IBmlElement
    {
        string Name { get; set; }
    }
    
    public class BmlElement : IBmlElement
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return Name + ": " + Value.ToString();
        }
    }

    public class BmlContainer : DynamicObject, IBmlElement, IEnumerable<IBmlElement>
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
                BmlElement elt = element as BmlElement;
                if (elt != null)
                    result = elt.Value;
                else
                    result = element;

                return true;
            }
            
            result = null;
            return true;
        }

        class BmlSetMemberBinder : SetMemberBinder
        {
            public BmlSetMemberBinder(string name, bool ignoreCase) : base(name, ignoreCase) { }
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
                BmlContainer container = GetOrCreateContainer(binder.Name);

                foreach (var item in value.GetType()
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Select(pi => new { Name = pi.Name, Value = pi.GetValue(value, null) }))
                {
                    if (item.Value != null)
                        container.TrySetMember(new BmlSetMemberBinder(item.Name, binder.IgnoreCase), item.Value);
                }
                    
            }

            return true;
        }
        #endregion

        public BmlContainer()
        {
            Elements = new List<IBmlElement>();
        }

        public string Name { get; set; }

        private void AddElement(IBmlElement element)
        {
            BmlElement last = (Elements.Count > 0 ? Elements[Elements.Count - 1] : null) as BmlElement;
            if (last != null && string.IsNullOrEmpty(last.Name) && last.Value == "...")
            {
                Elements.Insert(Elements.Count - 1, element);
                return;
            }

            Elements.Add(element);
        }

        public BmlContainer GetContainer(string key)
        {
            return Elements.OfType<BmlContainer>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
        }

        public BmlContainer GetOrCreateContainer(string key)
        {
            var element = GetContainer(key);
            if (element == null)
            {
                element = new BmlContainer() { Name = key };

                // Convert Element to Container
                var item = Elements.FirstOrDefault(e => !(e is BmlContainer) && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
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
                var element = Elements.OfType<BmlElement>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element != null)
                    return element.Value;

                return null;
            }
            set
            {
                var element = Elements.OfType<BmlElement>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element == null)
                {
                    element = new BmlElement() { Name = key };

                    // Convert Container to Element
                    var container = Elements.FirstOrDefault(e => e is BmlContainer && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
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

        public List<IBmlElement> Elements { get; private set; }

        public override string ToString()
        {
            return "[Folder] " + (Name ?? "");
        }

        public IEnumerator<IBmlElement> GetEnumerator()
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
                BmlContainer container = item as BmlContainer;
                if (container != null)
                {
                    if (container.Elements.Count > 0)
                    {
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(item.Name);
                        sb.AppendLine();

                        container.SerializeTo(sb, indent + 1);
                    }

                    continue;
                }

                BmlElement element = item as BmlElement;
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

    class SimpleBml<T> : IEnumerable<T> where T : new()
    {
        private List<T> _values;

        private static List<T> FillElements(object obj, BmlContainer bmlElements)
        {
            List<T> ret = null; 

            foreach (var bmlEntry in bmlElements.Elements)
            {
                BmlContainer container = bmlEntry as BmlContainer;
                if (container != null)
                {
                    if (typeof(T).Equals(obj))
                    {
                        T current = Activator.CreateInstance<T>();

                        var bmlNameProperty = typeof(T).GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(BmlNameAttribute)));
                        if (bmlNameProperty != null)
                            bmlNameProperty.SetValue(current, container.Name, null);
                       
                        FillElements(current, container);

                        if (ret == null)
                            ret = new List<T>();

                        ret.Add(current);
                    }
                    else
                    {
                        var propertyType = (obj is Type) ? (Type)obj : obj.GetType();
                        var objectProperty = propertyType.GetProperty(bmlEntry.Name);
                        if (objectProperty != null && !objectProperty.PropertyType.IsValueType && objectProperty.PropertyType != typeof(string))
                        {
                            object child = Activator.CreateInstance(objectProperty.PropertyType);

                            var bmlNameProperty = objectProperty.PropertyType.GetProperties().FirstOrDefault(prop => Attribute.IsDefined(prop, typeof(BmlNameAttribute)));
                            if (bmlNameProperty != null)
                                bmlNameProperty.SetValue(obj, container.Name, null);

                            FillElements(child, container);
                            objectProperty.SetValue(obj, child, null);
                        }
                    }
                    continue;
                }

                var type = (obj is Type) ? (Type)obj : obj.GetType();

                var property = type.GetProperty(bmlEntry.Name);
                if (property != null && bmlEntry is BmlElement)
                    property.SetValue(obj, ((BmlElement)bmlEntry).Value, null);
            }

            return ret;
        }

        public static SimpleBml<T> Parse(string bml)
        {
            var ret = new SimpleBml<T>();
            ret._values = FillElements(typeof(T), BmlFile.Parse(bml));
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

    class BmlNameAttribute : Attribute { }
}
