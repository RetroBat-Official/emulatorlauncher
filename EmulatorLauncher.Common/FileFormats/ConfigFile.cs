using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Dynamic;

namespace EmulatorLauncher.Common.FileFormats
{
    public class ConfigFile : DynamicObject, IEnumerable<ConfigItem>
    {
        public ConfigFileOptions Options { get; set; }

        public static ConfigFile FromArguments(string[] args)
        {
            ConfigFile arguments = new ConfigFile();

            string current = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                    current = arg.Substring(1);
                else if (!string.IsNullOrEmpty(current))
                {
                    arguments[current] = arg;
                    current = null;
                }
                else
                    arguments["rom"] = arg;
            }

            return arguments;
        }

        public static ConfigFile LoadEmulationStationSettings(string file)
        {
            var ret = new ConfigFile();
            if (!File.Exists(file))
                return ret;

            try
            {
                XDocument doc = XDocument.Load(file);

                foreach (XElement element in doc.Root.Descendants())
                {
                    if (element.Attribute("name") != null && element.Attribute("value") != null)
                    {
                        string name = element.Attribute("name").Value;
                        string val = element.Attribute("value").Value;
                        ret[name] = val;
                    }
                }
            }
            catch { }

            return ret;
        }

        private readonly static string EmptyLine = "------------EmptyLine----------------";

        public static ConfigFile FromFile(string file, ConfigFileOptions options = null)
        {
            var ret = new ConfigFile
            {
                Options = options
            };

            if (!File.Exists(file))
                return ret;

            bool keepComments = options != null && options.KeepComments;

            foreach (var line in File.ReadAllLines(file))
            {
                bool skip = false;

                foreach (var chr in line)
                {
                    if (chr == '#' || chr == ';')
                    {
                        skip = true;
                        break;
                    }

                    if (chr != ' ' && chr != '\t')
                        break;
                }

                if (skip && !keepComments)
                    continue;

                bool doubleEquals = options != null && options.UseDoubleEqual;

                int idx = doubleEquals ? line.IndexOf("==", StringComparison.Ordinal) : line.IndexOf("=", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    string value = line.Substring(idx + (doubleEquals ? 2 : 1)).Trim();
                    if (value.StartsWith("\""))
                        value = value.Substring(1);

                    if (value.EndsWith("\""))
                        value = value.Substring(0, value.Length-1);

                    ret[line.Substring(0, idx).Trim()] = value;
                }
                else
                    ret[line] = EmptyLine;
            }

            return ret;
        }

        public void ImportOverrides(ConfigFile cfg)
        {
            if (cfg == null)
                return;

            foreach (var item in cfg._data.Values)
                this[item.Name] = item.Value;
        }

        public string GetFullPath(string key)
        {
            string data = this[key];
            if (string.IsNullOrEmpty(data))
            {
                if (key == "home" && Directory.Exists(Path.Combine(LocalPath, ".emulationstation")))                        
                    return Path.Combine(LocalPath, ".emulationstation");

                if (key == "bios" || key == "saves" || key == "thumbnails" || key == "shaders" || key == "decorations" || key == "tattoos" || key == "screenshots" || key == "roms" || key == "records" || key == "cheats")
                {
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", key)));
                }
                else if (key == "retrobat")
                {
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..")));
                }
                else
                {
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, key)));

                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", key)));

                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", key)));

                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", key)));
                }
                
                return string.Empty;
            }

            if (data.Contains(":")) // drive letter -> Full path
                return data;

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "emulators", data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", "emulators", data)));

            if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", data))))
                return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", "system", data)));

            return data;
        }

        private OrderedDictionary<string, ConfigItem> _data = new OrderedDictionary<string, ConfigItem>();

        public ConfigFile() { }

        private string FormatKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return key;

            if (Options != null && Options.CaseSensitive)
                return key.Trim();

            return key.ToLowerInvariant().Trim();
        }

        public string this[string key]
        {
            get
            {
                if (!string.IsNullOrEmpty(key))
                {
                    ConfigItem item;
                    if (_data.TryGetValue(FormatKey(key), out item) && item != null)
                        if (item.Value != EmptyLine)
                            return item.Value;
                }

                return string.Empty;
            }
            set
            {
                var indexKey = key;

                if (string.IsNullOrEmpty(indexKey) && value == EmptyLine)
                {
                    if (this.Options == null || !Options.KeepEmptyLines)
                        return;

                    indexKey = EmptyLine + Guid.NewGuid();
                }
                else
                    indexKey = FormatKey(key);

                ConfigItem item;
                if (!_data.TryGetValue(indexKey, out item) || item == null)
                {
                    if ((this.Options == null || !Options.KeepEmptyValues) && string.IsNullOrEmpty(value))
                        return;

                    item = new ConfigItem() { Name = key, Value = value };
                    _data.Add(indexKey, item);
                    return;
                }

                if ((this.Options == null || !Options.KeepEmptyValues) && string.IsNullOrEmpty(value))
                    _data.Remove(indexKey);
                else if (item.Value != value)
                {
                    item.Value = value;
                    IsDirty = true;
                }
            }
        }

        public string GetValueOrDefault(string key, string defaultValue)
        {
            ConfigItem item;
            if (_data.TryGetValue(FormatKey(key), out item) && item != null)
                if (!string.IsNullOrEmpty(item.Value))
                    return item.Value;

            return defaultValue;
        }

        public string GetValueOrDefaultSlider(string key, string defaultValue)
        {
            ConfigItem item;
            if (_data.TryGetValue(FormatKey(key), out item) && item != null)
                if (!string.IsNullOrEmpty(item.Value))
                {
                    string ret = (item.Value).ToIntegerString();
                    return ret;
                }

            return defaultValue;
        }

        public virtual bool IsDirty { get; protected set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            string equalSign = this.Options != null && this.Options.UseDoubleEqual ? "==" : "=";
            bool useSpaces = this.Options != null && this.Options.UseSpaces;
            bool useQuotes = this.Options != null && this.Options.UseQuotes;

            foreach (var item in _data.Values)
            {
                sb.Append(item.Name);

                if (item.Value == EmptyLine)
                {
                    sb.AppendLine();
                    continue;
                }

                if (useSpaces)
                {
                    sb.Append(" ");
                    sb.Append(equalSign);
                    sb.Append(" ");
                }
                else
                    sb.Append(equalSign);

                if (useQuotes)
                    sb.Append("\"");
                    
                sb.Append(item.Value);

                if (useQuotes)
                    sb.Append("\"");

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void Save(string fileName, bool retroarchformat = false)
        {
            if (retroarchformat)
            {
                if (this.Options == null)
                    this.Options = new ConfigFileOptions();

                this.Options.UseDoubleEqual = false;
                this.Options.UseQuotes = true;
                this.Options.UseSpaces = true;

            }

            string text = ToString();
            File.WriteAllText(fileName, text);
        }

        public ConfigFile LoadAll(string key)
        {
            ConfigFile ret = new ConfigFile();

            if (string.IsNullOrEmpty(key))
                return ret;

            key = FormatKey(key) + ".";

            foreach (var item in _data.Values)
                if (item.Name.StartsWith(key, StringComparison.InvariantCultureIgnoreCase))
                    ret[item.Name.Substring(key.Length)] = item.Value;

            return ret;
        }

        public bool isOptSet(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return _data.ContainsKey(FormatKey(key));
        }

        public bool getOptBoolean(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            ConfigItem item;
            if (_data.TryGetValue(FormatKey(key), out item) && item != null)
                return item.Value != null && (item.Value.ToLower() == "true" || item.Value == "1" || item.Value.ToLower() == "enabled" || item.Value.ToLower() == "on" || item.Value.ToLower() == "yes");

            return false;
        }

        public int getInteger(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 0;

            ConfigItem item;
            if (_data.TryGetValue(FormatKey(key), out item) && item != null)
            {
                int ret;
                if (int.TryParse(item.Value, out ret))
                    return ret;
            }
            
            return 0;
        }

        public IEnumerator<ConfigItem> GetEnumerator()
        {
            return _data.Values.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _data.Values.GetEnumerator();
        }

        public void DisableAll(string key)
        {
            if (string.IsNullOrEmpty(key))
                return;

            List<string> toRemove = new List<string>();

            key = FormatKey(key);

            foreach (var item in _data)
                if (item.Value.Name != EmptyLine && item.Key.Contains(key))
                    toRemove.Add(item.Key);

            foreach (var item in toRemove)
                _data.Remove(item);            
        }

        public static string LocalPath
        {
            get
            {
                if (_localPath == null)
                    _localPath = Path.GetDirectoryName(typeof(ConfigFile).Assembly.Location);

                return _localPath;
            }
        }

        public void AppendLine(string line)
        {
            _data.Add(EmptyLine + Guid.NewGuid().ToString(), new ConfigItem()
            {
                Name = line,
                Value = EmptyLine
            });
        }

        private static string _localPath;

        #region DynamicObject
        public override IEnumerable<string> GetDynamicMemberNames()
        {
            PartialConfigFile me = this as PartialConfigFile;
            if (me != null)
            {
                return _data
                    .Where(d => !string.IsNullOrEmpty(d.Value.Name) && d.Value != null && d.Value.Name.StartsWith(me.Root))
                    .Select(d => d.Value.Name);
            }             

            return _data.Where(d => !string.IsNullOrEmpty(d.Value.Name) && d.Value != null).Select(d => d.Value.Name);
        }

        class PartialConfigFile : ConfigFile
        {           
            public PartialConfigFile(ConfigFile parent, string root)
            {
                _parent = parent;
                Root = root;
            }

            private ConfigFile _parent;
            public string Root { get; set; }

            public override bool IsDirty
            {
                get { return _parent.IsDirty; }
                protected set { _parent.IsDirty = value; }
            }
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string propertyPath = binder.Name;

            PartialConfigFile me = this as PartialConfigFile;
            if (me != null)
                propertyPath = me.Root + propertyPath;

            ConfigItem item;
            if (_data.TryGetValue(FormatKey(propertyPath), out item))
            {
                result = item.Value;
                return true;
            }

            var root = propertyPath + ".";

            if (_data.Values.Any(v => v.Name != null && v.Name.StartsWith(root)))
            {
                result = new PartialConfigFile(this, root) { _data = this._data };
                return true;
            }

            result = null;
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            string propertyPath = binder.Name;

            PartialConfigFile me = this as PartialConfigFile;
            if (me != null)
                propertyPath = me.Root + propertyPath;

            if (value == null)
                this.DisableAll(propertyPath);
            else
                this[propertyPath] = value.ToString();

            return true;
        }
        #endregion

    }

    public class ConfigItem
    {
        public override string ToString()
        {
            return Name + " = " + Value;
        }

        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class ConfigFileOptions
    {
        public ConfigFileOptions()
        {
            KeepEmptyValues = true;
        }

        public bool KeepEmptyLines { get; set; }
        public bool KeepEmptyValues { get; set; }
        public bool KeepComments { get; set; }
        public bool CaseSensitive { get; set; }
        public bool UseDoubleEqual { get; set; }
        public bool UseSpaces { get; set; }
        public bool UseQuotes { get; set; }

    }
}
