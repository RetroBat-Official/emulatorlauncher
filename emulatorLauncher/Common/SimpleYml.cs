using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

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

            YmlContainer current = root;

            Stack<YmlContainer> stack = new Stack<YmlContainer>();
            stack.Push(root);

            var lines = yml.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                int indent = 0;
                foreach (var chr in line)
                    if (chr == 32)
                        indent++;
                    else
                        break;

                indent /= 2;

                string tmp = line.Trim();
                int idx = tmp.IndexOf(":");
                if (idx >= 0)
                {
                    string name = tmp.Substring(0, idx).Trim();
                    string value = tmp.Substring(idx + 1).Trim();

                    while (stack.Count > 0 && current.Indent >= indent)
                        current = stack.Pop();

                    if (!stack.Contains(current))
                        stack.Push(current);

                    if (string.IsNullOrEmpty(value))
                    {
                        var folder = new YmlContainer() { Name = name, Indent = indent };
                        current.Elements.Add(folder);
                        stack.Push(folder);
                        current = folder;
                    }
                    else
                    {
                        var item = new YmlElement() { Name = name, Value = value };
                        current.Elements.Add(item);
                    }
                }
                else
                {
                    var item = new YmlElement() { Name = "", Value = tmp };
                    current.Elements.Add(item);
                }
            }

            return root;
        }

        public static YmlFile Load(string ymlFile)
        {
            var root = new YmlFile() { Name = "root", Indent = -1 };
            root._path = ymlFile;
            if (!File.Exists(ymlFile))
                return root;

            string yml = File.ReadAllText(ymlFile);
            return Parse(yml);
        }
    }

    class YmlElement
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            if (Value != null && !(this is YmlContainer))
                return Name + ": " + Value.ToString();

            return Name;
        }
    }

    class YmlContainer : YmlElement, IEnumerable<YmlElement>
    {
        public YmlContainer()
        {
            Elements = new List<YmlElement>();
        }

        public YmlContainer GetOrCreateContainer(string key)
        {
            var element = Elements.OfType<YmlContainer>().FirstOrDefault(e => key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
            if (element == null)
            {
                // Convert Element to Container
                var item = Elements.FirstOrDefault(e => !(e is YmlContainer) && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (item != null)
                    Elements.Remove(item);

                element = new YmlContainer() { Name = key };
                Elements.Add(element);
            }

            return element;
        }

        public string this[string key]
        {
            get
            {
                var element = Elements.FirstOrDefault(e => !(e is YmlContainer) && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element != null)
                    return element.Value;

                return null;
            }
            set
            {
                var element = Elements.FirstOrDefault(e => !(e is YmlContainer) && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                if (element == null)
                {
                    // Convert Container to Element
                    var container = Elements.FirstOrDefault(e => e is YmlContainer && key.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
                    if (container != null)
                        Elements.Remove(container);

                    element = new YmlElement() { Name = key };
                    Elements.Add(element);
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

        public List<YmlElement> Elements { get; private set; }

        public override string ToString()
        {
            return "[Folder] " + base.ToString();
        }

        public IEnumerator<YmlElement> GetEnumerator()
        {
            return Elements.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return Elements.GetEnumerator();
        }

        protected void SerializeTo(StringBuilder sb, int indent = 0)
        {
            foreach (var element in Elements)
            {
                YmlContainer container = element as YmlContainer;
                if (container != null)
                {
                    if (container.Elements.Count > 0)
                    {
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(element.Name);
                        sb.AppendLine(":");

                        container.SerializeTo(sb, indent + 1);
                    }

                    continue;
                }

                if (element.Value == null)
                    continue;

                sb.Append(new string(' ', indent * 2));

                if (!string.IsNullOrEmpty(element.Name))
                {
                    sb.Append(element.Name);
                    sb.Append(": ");
                    sb.AppendLine(element.Value.ToString());
                }
                else
                    sb.AppendLine(element.Value.ToString());
            }
        }

        internal int Indent;
    }

    class SimpleYml<T> : IEnumerable<T> where T : new()
    {
        private List<T> _values;

        private List<T> FillElements(object obj, YmlContainer ymlElements)
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
                if (property != null)
                    property.SetValue(obj, ymlEntry.Value, null);
            }

            return ret;
        }

        public SimpleYml(string yml)
        {
            _values = FillElements(typeof(T), YmlFile.Parse(yml));            
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
