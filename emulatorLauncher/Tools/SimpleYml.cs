using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher.Tools
{
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
