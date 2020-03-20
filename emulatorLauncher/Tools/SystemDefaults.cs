using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace emulatorLauncher
{
    class SystemDefaults
    {
        public static string GetDefaultEmulator(string system)
        {
            if (_instance == null)
                _instance = new SystemDefaults();

            return _instance.values.Where(i => i.system == system).Select(i => i.emulator).FirstOrDefault();
        }

        public static string GetDefaultCore(string system)
        {
            if (_instance == null)
                _instance = new SystemDefaults();

            return _instance.values.Where(i => i.system == system).Select(i => i.core).FirstOrDefault();
        }

        private static SystemDefaults _instance;
        
        class YmlSystem
        {
            public string system { get; set; }

            public string emulator { get; set; }
            public string core { get; set; }
        }

        private List<YmlSystem> values;

        private SystemDefaults()
        {
            values = new List<YmlSystem>();
            YmlSystem current = null;

            var lines = Encoding.UTF8.GetString(Properties.Resources.configgen_defaults).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
                    string value = tmp.Substring(idx+1).Trim();

                    if (indent == 0 & string.IsNullOrEmpty(value))
                    {
                        current = new YmlSystem() { system = name };
                        values.Add(current);
                    }
                    else if (current != null && indent == 1 && !string.IsNullOrEmpty(value))
                    {
                        if (name == "emulator")
                            current.emulator = value;
                        else if (name == "core")
                            current.core = value;
                    }
                }
                
            }
        }
    }
}
