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
    public enum IniOptionsJGenesis
    {
        UseSpaces = 1,
        KeepEmptyValues = 2,
        AllowDuplicateValues = 4,
        KeepEmptyLines = 8,
        UseDoubleEqual = 16,  // nosgba inifile uses double equals as separator !
    }

    public class IniFileJGenesis : IDisposable
    {
        public static IniFileJGenesis FromFile(string path, IniOptionsJGenesis options = (IniOptionsJGenesis) 0)
        {
            return new IniFileJGenesis(path, options);
        }

        public IniFileJGenesis(string path, IniOptionsJGenesis options = (IniOptionsJGenesis)0)
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
                    SectionJGenesis currentSection = null;
                    var namesInSection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string strLine = iniFile.ReadLine();
                    while (strLine != null)
                    {
                        strLine = strLine.Trim();

                        if (strLine != "" || _options.HasFlag(IniOptionsJGenesis.KeepEmptyLines))
                        {
                            // Handle nested sections [[...]]
                            if (strLine.StartsWith("[[") && strLine.EndsWith("]]"))
                            {
                                int end = strLine.IndexOf("]]");
                                if (end > 0)
                                {
                                    namesInSection.Clear();
                                    // Extract nested section name without the brackets
                                    string nestedSectionName = strLine.Substring(2, end - 2).Trim();
                                    currentSection = _sections.GetOrAddSection(nestedSectionName, true);
                                    currentSection.IsNested = true;  // Mark this section as nested
                                }
                            }
                            // Handle regular sections [...]
                            else if (strLine.StartsWith("[") && strLine.EndsWith("]"))
                            {
                                int end = strLine.IndexOf("]");
                                if (end > 0)
                                {
                                    namesInSection.Clear();
                                    // Extract regular section name without the brackets
                                    currentSection = _sections.GetOrAddSection(strLine.Substring(1, end - 1).Trim(), false);
                                }
                            }
                            else
                            {
                                // Handle key-value pairs
                                string[] keyPair = _options.HasFlag(IniOptionsJGenesis.UseDoubleEqual)
                                    ? strLine.Split(new string[] { "==" }, 2, StringSplitOptions.None)
                                    : strLine.Split(new char[] { '=' }, 2);

                                if (currentSection == null)
                                {
                                    namesInSection.Clear();
                                    currentSection = _sections.GetOrAddSection(null);  // Default section
                                }

                                var key = new Key();
                                key.Name = keyPair[0].Trim();

                                // Handle comment or duplicate values
                                if (!key.IsComment && !_options.HasFlag(IniOptionsJGenesis.AllowDuplicateValues) && namesInSection.Contains(key.Name))
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

                                    // Handle comments within key-value pairs
                                    var commentIdx = keyPair[1].IndexOf(";");
                                    if (commentIdx > 0)
                                    {
                                        key.Comment = keyPair[1].Substring(commentIdx);
                                        keyPair[1] = keyPair[1].Substring(0, commentIdx);
                                    }

                                    key.Value = keyPair[1].Trim();
                                }

                                currentSection.Add(key);  // Add the key to the section
                            }
                        }

                        strLine = iniFile.ReadLine();  // Move to the next line
                    }

                    iniFile.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public IniSectionJGenesis GetOrCreateSection(string key)
        {
            return new PrivateIniSectionJGenesis(key, this);
        }

        class PrivateIniSectionJGenesis : IniSectionJGenesis { public PrivateIniSectionJGenesis(string name, IniFileJGenesis ini) : base(name, ini) { } }

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
            if (string.IsNullOrEmpty(sectionName))
                sectionName = string.Empty;

            // Explicitly check if the sectionName is nested by ensuring it starts with "[[" and ends with "]]"
            if (sectionName.StartsWith("[[") && sectionName.EndsWith("]]"))
            {
                // This is a nested section, strip the [[ ]] before processing
                string nestedSectionName = sectionName.Substring(2, sectionName.Length - 4).Trim();

                // Get or create the nested section
                var section = _sections.GetOrAddSection(nestedSectionName, true);
                if (section == null)
                    throw new InvalidOperationException($"Unable to create nested section: {nestedSectionName}");

                // Add or update nested entry
                var key = section.Get(keyName);
                if (key != null && key.Value == value)
                    return;

                if (key == null)
                    key = section.Add(keyName);

                key.Value = value;
            }
            else
            {
                // This is a regular section (not nested), no brackets to strip
                var section = _sections.GetOrAddSection(sectionName);

                var key = section.Get(keyName);
                if (key != null && key.Value == value)
                    return;

                if (key == null)
                    key = section.Add(keyName);

                key.Value = value;
            }

            _dirty = true;
        }

        public void AppendValue(string sectionName, string keyName, string value)
        {
            if (!_options.HasFlag(IniOptionsJGenesis.AllowDuplicateValues))
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

        public void DeleteSection(string sectionName)
        {
            // Check if the section is nested
            if (sectionName.StartsWith("[[") && sectionName.EndsWith("]]"))
            {
                // Remove the nested section (strip the brackets)
                string nestedSectionName = sectionName.Substring(2, sectionName.Length - 4).Trim();
                _sections.Remove(nestedSectionName);
            }
            else
            {
                // Remove the regular section
                _sections.Remove(sectionName);
            }

            _dirty = true;  // Mark the file as dirty after modification
        }

        public bool IsDirty { get { return _dirty; } }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            // Loop through sections
            foreach (var section in _sections)
            {
                int i = 0;
                // Skip empty sections or ROOT section
                if (string.IsNullOrEmpty(section.Name) || section.Name == "ROOT" || !section.Any())
                    continue;

                if (section.IsNested)
                {
                    sb.AppendLine("[[" + section.Name + "]]");  // Use nested format
                }
                else
                {
                    sb.AppendLine("[" + section.Name + "]");  // Regular section format
                }

                // Loop through the entries in the section (key-value pairs)
                foreach (Key entry in section)
                {
                    bool isLast = i == section.ToList().Count - 1;

                    // Handle empty keys
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // If a comment exists, print it
                        if (!string.IsNullOrEmpty(entry.Comment))
                            sb.AppendLine(entry.Comment);
                        // If the option allows empty lines, print an empty line
                        else if (_options.HasFlag(IniOptionsJGenesis.KeepEmptyLines))
                            sb.AppendLine();
                        continue;
                    }

                    // Handle comments in the key-value pairs
                    if (entry.IsComment)
                    {
                        sb.AppendLine(entry.Name); // Print the comment
                        continue;
                    }

                    // Skip empty values if the option disallows them
                    if (string.IsNullOrEmpty(entry.Value) && !_options.HasFlag(IniOptionsJGenesis.KeepEmptyValues))
                        continue;

                    // Print the key-value pair
                    sb.Append(entry.Name);

                    // Add space for clarity, if the option enabled
                    if (_options.HasFlag(IniOptionsJGenesis.UseSpaces))
                        sb.Append(" ");

                    // Use '==' for double equal, otherwise use '='
                    if (_options.HasFlag(IniOptionsJGenesis.UseDoubleEqual))
                        sb.Append("==");
                    else
                        sb.Append("=");

                    // Add space if option enabled
                    if (_options.HasFlag(IniOptionsJGenesis.UseSpaces))
                        sb.Append(" ");

                    // Print the value
                    sb.Append(entry.Value);

                    // If there's a comment for this key, append it after the value
                    if (!string.IsNullOrEmpty(entry.Comment))
                    {
                        sb.Append("\t\t\t");  // Add tab space before the comment
                        sb.Append(entry.Comment); // Append the comment
                    }

                    sb.AppendLine(); // End the key-value line

                    if (isLast && !string.IsNullOrEmpty(entry.Name))
                        sb.AppendLine();

                    i++;
                }

                // Add an empty line after each section if the option allows
                if (!_options.HasFlag(IniOptionsJGenesis.KeepEmptyLines))
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
                SimpleLogger.Instance.Error("[IniFile] Save failed " + ex.Message, ex);
            }
        }

        public void Dispose()
        {
            Save();
        }

        private IniOptionsJGenesis _options;
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

        class SectionJGenesis : IEnumerable<Key>
        {
            public bool IsNested { get; set; }

            public SectionJGenesis(bool isNested = false)
            {
                _keys = new KeyList();
                IsNested = isNested;
            }

            public string Name { get; set; }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                // Only add double square brackets for sections that explicitly have [[...]]
                if (!string.IsNullOrEmpty(Name))
                {
                    // Only wrap with double square brackets if the section is marked as nested
                    if (Name.StartsWith("[[") && Name.EndsWith("]]"))
                    {
                        sb.AppendLine(Name); // Already in nested format
                    }
                    else
                    {
                        sb.AppendLine($"[{Name}]"); // Regular section format
                    }
                }

                // Add keys for this section
                foreach (var key in _keys)
                {
                    sb.AppendLine(key.ToString());
                }

                return sb.ToString();
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

        class SectionsJGenesis : IEnumerable<SectionJGenesis>
        {
            public SectionsJGenesis()
            {
                _sections = new List<SectionJGenesis>();
            }

            public SectionJGenesis Get(string sectionName)
            {
                if (sectionName == null)
                    sectionName = string.Empty;

                return _sections.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.InvariantCultureIgnoreCase));
            }

            public SectionJGenesis GetOrAddSection(string sectionName, bool isNested = false)
            {
                if (string.IsNullOrEmpty(sectionName))
                    sectionName = string.Empty;

                var section = Get(sectionName);
                if (section == null)
                {
                    section = new SectionJGenesis 
                    { 
                        Name = sectionName,
                        IsNested = isNested ? true : false,
                    };
                    _sections.Add(section);
                }

                return section;
            }

            public void Remove(string sectionName)
            {
                var section = _sections.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.InvariantCultureIgnoreCase));
                if (section != null)
                {
                    _sections.Remove(section);
                }
            }

            private List<SectionJGenesis> _sections;

            public IEnumerator<SectionJGenesis> GetEnumerator()
            {
                return _sections.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return _sections.GetEnumerator();
            }
        }

        private SectionsJGenesis _sections = new SectionsJGenesis();
        #endregion
    }


    public class IniSectionJGenesis
    {
        private IniFileJGenesis _ini;
        private string _sectionName;

        protected IniSectionJGenesis(string name, IniFileJGenesis ini)
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