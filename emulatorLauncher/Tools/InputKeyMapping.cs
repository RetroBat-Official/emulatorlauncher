using System;
using System.Collections.Generic;
using System.Linq;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{

    class InputKeyMapping : List<KeyValuePair<InputKey, string>>
    {
        public InputKeyMapping() { }

        public InputKeyMapping(InputKeyMapping source)
        {
            this.AddRange(source);
        }

        public void Add(InputKey key, string value)
        {
            this.Add(new KeyValuePair<InputKey, string>(key, value));
        }

        public bool ContainsKey(InputKey key)
        {
            return this.Any(i => i.Key == key);
        }

        public string this[InputKey key]
        {
            get
            {
                return this.Where(i => i.Key == key).Select(i => i.Value).FirstOrDefault();
            }
            set
            {
                var idx = this.FindIndex(i => i.Key == key);
                if (idx >= 0)
                    this[idx] = new KeyValuePair<InputKey, string>(key, value);
                else
                    this.Add(key, value);
            }
        }
    }
}