using System.Collections.Generic;

namespace ValveKeyValue.Deserialization
{
    class KVPartialState
    {
        public KVPartialState()
        {
        }

        private List<KVObject> _items = new List<KVObject>();

        public string Key { get; set; }

        public KVValue Value { get; set; }

        public IList<KVObject> Items { get { return _items; } }

        public bool Discard { get; set; }
    }
}
