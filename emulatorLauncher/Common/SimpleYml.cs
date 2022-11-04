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
            Save(_path);
        }

        public void Save(string ymlFile)
        {
            File.WriteAllText(ymlFile, ToString());
        }

        private string _path;

        public static YmlFile Load(string ymlFile)
        {
            var root = new YmlFile() { Name = "root", Indent = -1 };
            root._path = ymlFile;
            if (!File.Exists(ymlFile))
                return root;

            YmlContainer current = root;
            
            Stack<YmlContainer> stack = new Stack<YmlContainer>();
            stack.Push(root);

            string yml = File.ReadAllText(ymlFile);
            
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

    class SimpleYml<T> : IEnumerable<T> where T : IYmlItem, new()
    {
        private List<T> _values;

        public SimpleYml(string yml)
        {
            _values = new List<T>();

            if (string.IsNullOrEmpty(yml))
                return;

            T current = default(T);

            var lines = yml.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                int indent = 0;
                foreach (var chr in line) if (chr == 32) indent++; else break;
                indent /= 2;

                string tmp = line.Trim();
                int idx = tmp.IndexOf(":");
                if (idx >= 0)
                {
                    string name = tmp.Substring(0, idx).Trim();
                    string value = tmp.Substring(idx + 1).Trim();

                    if (indent == 0 & string.IsNullOrEmpty(value))
                    {
                        current = new T() { system = name };
                        _values.Add(current);
                    }
                    else if (current != null && indent == 1 && !string.IsNullOrEmpty(value))
                    {
                        var property = typeof(T).GetProperty(name);
                        if (property != null)
                            property.SetValue(current, value, null);
                    }
                }
            }
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

    interface IYmlItem
    {
        string system { get; set; }
    }

}
