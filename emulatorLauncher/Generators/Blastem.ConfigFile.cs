using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace EmulatorLauncher.Common.FileFormats
{
    /// <summary>
    /// BlastEm configuration file (blastem.cfg, default.cfg, controller_types.cfg).
    ///
    /// Format, as implemented by BlastEm itself in config.c (parse_config_int) :
    ///  - one entry per line, leading and trailing whitespace is stripped,
    ///  - an empty line, or a line whose first character is '#', is skipped,
    ///  - a line whose last character is '{' opens a section named by the text before the brace,
    ///  - a line whose first character is '}' closes the current section,
    ///  - any other line is "key&lt;blank&gt;value" : the key ends at the first space or tab and the
    ///    value is the remainder of the line. BlastEm rejects a key with an empty value, so an
    ///    empty value removes the entry here instead of being written out.
    /// </summary>
    public class BlastemConfigNode
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>(StringComparer.Ordinal);

        public IEnumerable<string> Keys { get { return _order.ToArray(); } }

        /// <summary>
        /// Scalar value of a key. Returns null when the key is absent or holds a section.
        /// Assigning null or an empty string removes the key : BlastEm logs an error and drops
        /// any key that has no value, so such a line must never be written.
        /// </summary>
        public string this[string key]
        {
            get
            {
                object value;
                if (_items.TryGetValue(key, out value))
                    return value as string;

                return null;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Remove(key);
                    return;
                }

                if (!_items.ContainsKey(key))
                    _order.Add(key);

                _items[key] = value;
            }
        }

        /// <summary>Existing sub section, or null.</summary>
        public BlastemConfigNode GetSection(string name)
        {
            object value;
            if (_items.TryGetValue(name, out value))
                return value as BlastemConfigNode;

            return null;
        }

        /// <summary>Existing sub section, created empty when missing.</summary>
        public BlastemConfigNode GetOrCreateSection(string name)
        {
            var ret = GetSection(name);
            if (ret != null)
                return ret;

            Remove(name);

            ret = new BlastemConfigNode();
            _order.Add(name);
            _items[name] = ret;
            return ret;
        }

        /// <summary>Replace a sub section by an empty one, dropping everything it held.</summary>
        public BlastemConfigNode ResetSection(string name)
        {
            Remove(name);
            return GetOrCreateSection(name);
        }

        public bool Contains(string key)
        {
            return _items.ContainsKey(key);
        }

        public void Remove(string key)
        {
            if (_items.Remove(key))
                _order.Remove(key);
        }

        internal void Serialize(StringBuilder sb, int indent)
        {
            string pad = new string('\t', indent);

            foreach (var key in _order)
            {
                var section = _items[key] as BlastemConfigNode;
                if (section != null)
                {
                    sb.Append(pad).Append(key).Append(" {").Append("\r\n");
                    section.Serialize(sb, indent + 1);
                    sb.Append(pad).Append("}").Append("\r\n");
                }
                else
                    sb.Append(pad).Append(key).Append(' ').Append((string)_items[key]).Append("\r\n");
            }
        }
    }

    public class BlastemConfigFile : BlastemConfigNode
    {
        private string _path;

        /// <summary>
        /// Load a BlastEm configuration file. A missing file yields an empty document so that the
        /// caller can build one from scratch.
        /// </summary>
        public static BlastemConfigFile FromFile(string path)
        {
            var ret = new BlastemConfigFile { _path = path };

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return ret;

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return ret; }

            var stack = new Stack<BlastemConfigNode>();
            BlastemConfigNode current = ret;

            foreach (var raw in lines)
            {
                string line = raw.Trim();

                if (line.Length == 0 || line[0] == '#')
                    continue;

                if (line[0] == '}')
                {
                    if (stack.Count > 0)
                        current = stack.Pop();
                    continue;
                }

                if (line[line.Length - 1] == '{')
                {
                    string name = line.Substring(0, line.Length - 1).Trim();
                    if (name.Length == 0)
                        continue;

                    var child = current.GetOrCreateSection(name);
                    stack.Push(current);
                    current = child;
                    continue;
                }

                int cut = line.IndexOfAny(new[] { ' ', '\t' });
                if (cut <= 0)
                    continue;   // key with no value : BlastEm drops it too

                current[line.Substring(0, cut)] = line.Substring(cut + 1).Trim();
            }

            return ret;
        }

        public void Save(string path = null)
        {
            if (path != null)
                _path = path;

            if (string.IsNullOrEmpty(_path))
                return;

            var sb = new StringBuilder();
            Serialize(sb, 0);

            string dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                try { Directory.CreateDirectory(dir); } catch { }

            File.WriteAllText(_path, sb.ToString());
        }
    }
}