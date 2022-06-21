using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;

namespace emulatorLauncher
{
    class ConfigFile : IEnumerable<ConfigItem>
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

        private static string EmptyLine = "------------EmptyLine----------------";

        public static ConfigFile FromFile(string file, ConfigFileOptions options = null)
        {
            var ret = new ConfigFile();
            ret.Options = options;
            if (!File.Exists(file))
                return ret;

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

                if (skip)
                    continue;

                int idx = line.IndexOf("=", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    string value = line.Substring(idx + 1).Trim();
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

                if (key == "bios" || key == "saves" || key == "thumbnails" || key == "shaders" || key == "decorations" || key == "screenshots" || key == "roms")
                {
                    if (Directory.Exists(Path.GetFullPath(Path.Combine(LocalPath, "..", key))))
                        return Path.Combine(Path.GetFullPath(Path.Combine(LocalPath, "..", key)));
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
                return item.Value;

            return defaultValue;
        }

        public bool IsDirty { get; private set; }

        public void Save(string fileName, bool retroarchformat)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in _data.Values)
            {
                sb.Append(item.Name);

                if (item.Value == EmptyLine)
                {
                    sb.AppendLine();
                    continue;
                }

                if (retroarchformat)
                {
                    sb.Append(" = ");
                    sb.Append("\"");
                }
                else
                    sb.Append("=");

                sb.Append(item.Value);

                if (retroarchformat)
                    sb.Append("\"");

                sb.AppendLine();
            }

            File.WriteAllText(fileName, sb.ToString());
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
                return item.Value != null && item.Value.ToLower() == "true" || item.Value == "1";

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
    }

    class ConfigItem
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
        public bool CaseSensitive { get; set; }
    }
}
