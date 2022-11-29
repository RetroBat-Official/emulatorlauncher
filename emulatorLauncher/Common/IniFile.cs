using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace emulatorLauncher
{
    [Flags]
    public enum IniOptions
    {
        UseSpaces = 1,
        KeepEmptyValues = 2,
        AllowDuplicateValues = 4,
        KeepEmptyLines = 8
    }

    public class IniFile : IDisposable
    {
        public static IniFile FromFile(string path, IniOptions options = (IniOptions) 0)
        {
            return new IniFile(path, options);
        }

        public IniFile(string path, IniOptions options = (IniOptions) 0)
        {
            _options = options;
            _path = path;
            _dirty = false;

            if (!File.Exists(_path))
                return;

            try
            {
                using (TextReader iniFile = new StreamReader(_path))
                {
                    Section currentSection = null;

                    var namesInSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string strLine = iniFile.ReadLine();
                    while (strLine != null)
                    {
                        strLine = strLine.Trim();

                        if (strLine != "" || _options.HasFlag(IniOptions.KeepEmptyLines))
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
                                string[] keyPair = strLine.Split(new char[] { '=' }, 2);

                                if (currentSection == null)
                                {
                                    namesInSection.Clear();
                                    currentSection = _sections.GetOrAddSection(null);
                                }

                                var key = new Key();
                                key.Name = keyPair[0].Trim();

                                if (!key.IsComment && !_options.HasFlag(IniOptions.AllowDuplicateValues) && namesInSection.Contains(key.Name))
                                {
                                    strLine = iniFile.ReadLine();
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

                                    key.Value = keyPair[1].Trim();
                                }
                           
                                currentSection.Add(key);
                            }
                        }

                        strLine = iniFile.ReadLine();
                    }

                    iniFile.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string[] EnumerateSections()
        {
            return _sections.Select(s => s.Name).Distinct().ToArray();
        }

        public string[] EnumerateKeys(string sectionName)
        {
            var section = _sections.Get(sectionName);
            if (section != null)
                section.Select(k => k.Name).ToArray();

            return new string[] { };
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
            if (!_options.HasFlag(IniOptions.AllowDuplicateValues))
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
                        else if (_options.HasFlag(IniOptions.KeepEmptyLines))
                            sb.AppendLine();

                        continue;
                    }

                    if (entry.IsComment)
                    {
                        sb.AppendLine(entry.Name);
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.Value) && !_options.HasFlag(IniOptions.KeepEmptyValues))
                        continue;

                    if (string.IsNullOrEmpty(entry.Name))
                        continue;

                    sb.Append(entry.Name);

                    if (_options.HasFlag(IniOptions.UseSpaces))
                        sb.Append(" ");

                    sb.Append("=");

                    if (_options.HasFlag(IniOptions.UseSpaces))
                        sb.Append(" ");

                    sb.Append(entry.Value);

                    if (!string.IsNullOrEmpty(entry.Comment))
                    {
                        sb.Append("\t\t\t");
                        sb.Append(entry.Comment);
                    }

                    sb.AppendLine();
                }

                if (!_options.HasFlag(IniOptions.KeepEmptyLines))
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

            }
        }

        public void Dispose()
        {
            Save();
        }

        private IniOptions _options;
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
        #endregion
    }
}