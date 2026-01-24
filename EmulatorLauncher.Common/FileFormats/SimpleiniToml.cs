using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace EmulatorLauncher.Common.FileFormats
{
    [Flags]
    public enum IniTomlOptions
    {
        UseSpaces = 1,
        KeepEmptyValues = 2,
        AllowDuplicateValues = 4,
        KeepEmptyLines = 8,
        UseDoubleEqual = 16,
        ManageKeysWithQuotes = 32
    }

    public class IniTomlFile : IDisposable
    {
        public static IniTomlFile FromFile(string path, IniTomlOptions options = (IniTomlOptions)0)
        {
            return new IniTomlFile(path, options);
        }

        // Stores TOML arrays per key
        private Dictionary<string, List<object>> _tomlArrays = new Dictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

        public List<object> GetArray(string key)
        {
            if (_tomlArrays.TryGetValue(key, out var arr))
                return arr;
            return new List<object>();
        }

        public void SetArray(string sectionName, string key, List<object> values)
        {
            _tomlArrays[key] = values;

            string arrayString = "[" + string.Join(", ", values.Select(v =>
            {
                if (v is Dictionary<string, string> dict)
                    return "{ " + string.Join(", ", dict.Select(kv => kv.Key + " = '" + kv.Value + "'")) + " }";
                else
                    return "'" + v.ToString() + "'";
            })) + "]";

            WriteValue(sectionName, key, arrayString);
        }

        public void SetOptions(IniTomlOptions options)
        {
            _options = options;
        }

        public IniTomlFile(string path, IniTomlOptions options = (IniTomlOptions)0)
        {
            _options = options;
            _path = path;
            _dirty = false;

            if (!File.Exists(_path))
                return;

            try
            {
                using (TextReader iniTomlFile = new StreamReader(_path))
                {
                    Section currentSection = null;

                    var namesInSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string strLine = iniTomlFile.ReadLine();
                    while (strLine != null)
                    {
                        strLine = strLine.Trim();

                        if (strLine != "" || _options.HasFlag(IniTomlOptions.KeepEmptyLines))
                        {
                            if (strLine.StartsWith("["))
                            {
                                int end = strLine.IndexOf("]");
                                if (end > 0)
                                {
                                    namesInSection.Clear();
                                    currentSection = _sections.GetOrAddSection(strLine.Substring(1, end - 1));
                                }
                            }
                            else
                            {
                                string[] keyPair = _options.HasFlag(IniTomlOptions.UseDoubleEqual) ? strLine.Split(new string[] { "==" }, 2, StringSplitOptions.None) : strLine.Split(new char[] { '=' }, 2);

                                if (currentSection == null)
                                {
                                    namesInSection.Clear();
                                    currentSection = _sections.GetOrAddSection(null);
                                }

                                var key = new Key();

                                string keyName = keyPair[0].Trim();

                                if (_options.HasFlag(IniTomlOptions.ManageKeysWithQuotes))
                                {
                                    // If the key is surrounded by quotes, remove them
                                    if (keyName.StartsWith("\"") && keyName.EndsWith("\""))
                                    {
                                        keyName = keyName.Substring(1, keyName.Length - 2);  // Remove quotes
                                    }
                                }

                                key.Name = keyName;

                                if (!key.IsComment && !_options.HasFlag(IniTomlOptions.AllowDuplicateValues) && namesInSection.Contains(key.Name))
                                {
                                    strLine = iniTomlFile.ReadLine();
                                    continue;
                                }

                                if (key.IsComment)
                                {
                                    key.Name = strLine;
                                    key.Value = null;
                                }
                                else if (keyPair.Length > 1)
                                {
                                    namesInSection.Add(key.Name);

                                    var commentIdx = keyPair[1].IndexOf(";");
                                    if (commentIdx > 0)
                                    {
                                        key.Comment = keyPair[1].Substring(commentIdx);
                                        keyPair[1] = keyPair[1].Substring(0, commentIdx);
                                    }

                                    var value = keyPair[1].Trim();
                                    
                                    if (value.StartsWith("[") && !value.EndsWith("]"))
                                    {
                                        var arrayLines = new List<string> { value };
                                        while ((strLine = iniTomlFile.ReadLine()) != null)
                                        {
                                            strLine = strLine.Trim();
                                            arrayLines.Add(strLine);
                                            if (strLine.EndsWith("]"))
                                                break;
                                        }
                                        value = string.Join(" ", arrayLines);
                                    }

                                    if (value.StartsWith("[") && value.EndsWith("]"))
                                    {
                                        _tomlArrays[keyName] = ParseTomlArray(value);
                                    }

                                }

                                currentSection.Add(key);
                            }
                        }

                        strLine = iniTomlFile.ReadLine();
                    }

                    iniTomlFile.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public IniTomlSection GetOrCreateSection(string key)
        {
            return new PrivateIniTomlSection(key, this);
        }

        class PrivateIniTomlSection : IniTomlSection { public PrivateIniTomlSection(string name, IniTomlFile ini) : base(name, ini) { } }

        public string[] EnumerateSections()
        {
            return _sections.Select(s => s.Name).Distinct().ToArray();
        }

        public string[] EnumerateKeys(string sectionName)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
                return section.Select(k => k.Name).ToArray();

            return new string[] { };
        }

        public KeyValuePair<string, string>[] EnumerateValues(string sectionName)
        {
            var ret = new List<KeyValuePair<string, string>>();

            var section = _sections.Get(sectionName);
            if (section != null)
            {
                foreach (var item in section)
                {
                    if (item.IsComment || string.IsNullOrEmpty(item.Name))
                        continue;

                    ret.Add(new KeyValuePair<string, string>(item.Name, item.Value));
                }
            }

            return ret.ToArray();
        }

        public void ClearSection(string sectionName)
        {
            var section = _sections.Get(sectionName);
            if (section != null && section.Any())
            {
                _dirty = true;
                section.Clear();
            }
        }

        public string GetValue(string sectionName, string key)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
                return section.GetValue(key);

            return null;
        }

        public void WriteValue(string sectionName, string keyName, string value)
        {
            var section = _sections.GetOrAddSection(sectionName);

            var key = section.Get(keyName);
            if (key != null && key.Value == value)
                return;

            if (key == null)
                key = section.Add(keyName);

            key.Value = value;

            _dirty = true;
        }

        public void AppendValue(string sectionName, string keyName, string value)
        {
            if (!_options.HasFlag(IniTomlOptions.AllowDuplicateValues))
            {
                WriteValue(sectionName, keyName, value);
                return;
            }

            var section = _sections.GetOrAddSection(sectionName);
            section.Add(keyName, value);

            _dirty = true;
        }

        public void Remove(string sectionName, string keyName)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
            {
                foreach (var key in section.Where(k => k.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase)).ToArray())
                {
                    _dirty = true;
                    section.Remove(key);
                }
            }
        }

        public bool IsDirty { get { return _dirty; } }

        public override string ToString()
        {
            ArrayList sections = new ArrayList();
            StringBuilder sb = new StringBuilder();

            foreach (var section in _sections)
            {
                if (!string.IsNullOrEmpty(section.Name) && section.Name != "ROOT" && section.Any())
                    sb.AppendLine("[" + section.Name + "]");

                foreach (Key entry in section)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        if (!string.IsNullOrEmpty(entry.Comment))
                            sb.AppendLine(entry.Comment);
                        else if (_options.HasFlag(IniTomlOptions.KeepEmptyLines))
                            sb.AppendLine();

                        continue;
                    }

                    if (entry.IsComment)
                    {
                        sb.AppendLine(entry.Name);
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Value) && !_options.HasFlag(IniTomlOptions.KeepEmptyValues))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    if (_options.HasFlag(IniTomlOptions.ManageKeysWithQuotes))
                    {
                        sb.Append("\"" + entry.Name + "\"");  // Add quotes around the key
                    }

                    else
                    {
                        sb.Append(entry.Name);
                    }

                    if (_options.HasFlag(IniTomlOptions.UseSpaces))
                        sb.Append(" ");

                    if (_options.HasFlag(IniTomlOptions.UseDoubleEqual))
                        sb.Append("==");
                    else
                        sb.Append("=");

                    if (_options.HasFlag(IniTomlOptions.UseSpaces))
                        sb.Append(" ");

                    sb.Append(entry.Value);

                    if (!string.IsNullOrEmpty(entry.Comment))
                    {
                        sb.Append("\t\t\t");
                        sb.Append(entry.Comment);
                    }

                    sb.AppendLine();
                }

                if (!_options.HasFlag(IniTomlOptions.KeepEmptyLines))
                    sb.AppendLine();
            }

            return sb.ToString();
        }

        public void Save()
        {
            if (!_dirty)
                return;

            try
            {
                string dir = Path.GetDirectoryName(_path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (TextWriter tw = new StreamWriter(_path))
                {
                    tw.Write(ToString());
                    tw.Close();
                }

                _dirty = false;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[iniTomlFile] Save failed " + ex.Message, ex);
            }
        }

        public void Dispose()
        {
            Save();
        }

        private IniTomlOptions _options;
        private bool _dirty;
        private string _path;

        #region Private classes
        class Key
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public string Comment { get; set; }

            public bool IsComment
            {
                get
                {
                    return Name == null || Name.StartsWith(";") || Name.StartsWith("#");
                }
            }

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Name))
                    return "";

                if (string.IsNullOrEmpty(Value))
                    return Name + "=";

                return Name + "=" + Value;
            }
        }

        class KeyList : List<Key>
        {

        }

        class Section : IEnumerable<Key>
        {
            public Section()
            {
                _keys = new KeyList();
            }

            public string Name { get; set; }


            public override string ToString()
            {
                if (string.IsNullOrEmpty(Name))
                    return "";

                return "[" + Name + "]";
            }

            public bool Exists(string keyName)
            {
                foreach (var key in _keys)
                    if (key.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))
                        return true;

                return false;
            }

            public Key Get(string keyName)
            {
                foreach (var key in _keys)
                    if (key.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))
                        return key;

                return null;
            }

            public string GetValue(string keyName)
            {
                foreach (var key in _keys)
                    if (key.Name.Equals(keyName, StringComparison.InvariantCultureIgnoreCase))
                        return key.Value;

                return null;
            }

            private KeyList _keys;

            public IEnumerator<Key> GetEnumerator()
            {
                return _keys.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _keys.GetEnumerator();
            }

            public Key Add(string keyName, string value = null)
            {
                var key = new Key() { Name = keyName, Value = value };
                _keys.Add(key);
                return key;
            }

            public Key Add(Key key)
            {
                _keys.Add(key);
                return key;
            }

            internal void Clear()
            {
                _keys.Clear();
            }

            internal void Remove(Key key)
            {
                _keys.Remove(key);
            }
        }

        class Sections : IEnumerable<Section>
        {
            public Sections()
            {
                _sections = new List<Section>();
            }

            public Section Get(string sectionName)
            {
                if (sectionName == null)
                    sectionName = string.Empty;

                return _sections.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.InvariantCultureIgnoreCase));
            }

            public Section GetOrAddSection(string sectionName)
            {
                if (sectionName == null)
                    sectionName = string.Empty;

                var section = Get(sectionName);
                if (section == null)
                {
                    section = new Section() { Name = sectionName };

                    if ((string.IsNullOrEmpty(sectionName) || sectionName == "ROOT") && _sections.Count > 0)
                        _sections.Insert(0, section);
                    else
                        _sections.Add(section);
                }

                return section;
            }

            private List<Section> _sections;

            public IEnumerator<Section> GetEnumerator()
            {
                return _sections.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _sections.GetEnumerator();
            }
        }

        private Sections _sections = new Sections();

        // Parses a TOML array, returns list of strings or list of tables (as Dictionary)
        private List<object> ParseTomlArray(string arrayString)
        {
            arrayString = arrayString.Trim().TrimStart('[').TrimEnd(']');
            var items = new List<object>();

            int braceLevel = 0;
            StringBuilder sb = new StringBuilder();

            foreach (char c in arrayString)
            {
                if (c == '{') braceLevel++;
                if (c == '}') braceLevel--;

                if (c == ',' && braceLevel == 0)
                {
                    var item = sb.ToString().Trim();
                    if (item.StartsWith("{") && item.EndsWith("}"))
                        items.Add(ParseTomlTable(item));
                    else
                        items.Add(item.Trim('\'', '"'));
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                var item = sb.ToString().Trim();
                if (item.StartsWith("{") && item.EndsWith("}"))
                    items.Add(ParseTomlTable(item));
                else
                    items.Add(item.Trim('\'', '"'));
            }

            return items;
        }

        private Dictionary<string, string> ParseTomlTable(string tableString)
        {
            tableString = tableString.Trim().TrimStart('{').TrimEnd('}');
            var dict = new Dictionary<string, string>();
            var parts = tableString.Split(',');

            foreach (var part in parts)
            {
                var kv = part.Split(new char[] { '=' }, 2);
                if (kv.Length == 2)
                    dict[kv[0].Trim()] = kv[1].Trim(' ', '\'', '"');
            }

            return dict;
        }

        #endregion
    }


    public class IniTomlSection
    {
        private IniTomlFile _ini;
        private string _sectionName;

        protected IniTomlSection(string name, IniTomlFile ini)
        {
            _ini = ini;
            _sectionName = name;
        }

        public string this[string key]
        {
            get
            {
                return _ini.GetValue(_sectionName, key);
            }
            set
            {
                _ini.WriteValue(_sectionName, key, value);
            }
        }

        public void Clear()
        {
            _ini.ClearSection(_sectionName);
        }

        public string[] Keys
        {
            get
            {
                return _ini.EnumerateKeys(_sectionName);
            }
        }
    }
}