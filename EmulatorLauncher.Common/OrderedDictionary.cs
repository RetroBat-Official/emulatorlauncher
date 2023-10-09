using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Specialized;

namespace EmulatorLauncher.Common
{
    public class OrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary
    {
        private readonly OrderedDictionary _privateDictionary;

        public OrderedDictionary()
        {
            _privateDictionary = new OrderedDictionary();
        }

        public OrderedDictionary(IDictionary<TKey, TValue> dictionary)
        {
            if (dictionary == null) return;

            _privateDictionary = new OrderedDictionary();

            foreach (var pair in dictionary)
            {
                _privateDictionary.Add(pair.Key, pair.Value);
            }
        }

        public bool IsReadOnly { get { return false; } }
        public int Count { get { return _privateDictionary.Count; } }
        int ICollection.Count { get { return _privateDictionary.Count; } }
        object ICollection.SyncRoot { get { return ((ICollection)_privateDictionary).SyncRoot; } }
        bool ICollection.IsSynchronized { get { return ((ICollection)_privateDictionary).IsSynchronized; } }

        bool IDictionary.IsFixedSize { get { return ((IDictionary)_privateDictionary).IsFixedSize; } }
        bool IDictionary.IsReadOnly { get { return _privateDictionary.IsReadOnly; } }
        ICollection IDictionary.Keys { get { return _privateDictionary.Keys; } }
        ICollection IDictionary.Values { get { return _privateDictionary.Values; } }

        void IDictionary.Add(object key, object value)
        {
            _privateDictionary.Add(key, value);
        }

        void IDictionary.Clear()
        {
            _privateDictionary.Clear();
        }

        bool IDictionary.Contains(object key)
        {
            return _privateDictionary.Contains(key);
        }

        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return _privateDictionary.GetEnumerator();
        }

        void IDictionary.Remove(object key)
        {
            _privateDictionary.Remove(key);
        }

        object IDictionary.this[object key]
        {
            get { return _privateDictionary[key]; }
            set { _privateDictionary[key] = value; }
        }

        void ICollection.CopyTo(Array array, int index)
        {
            _privateDictionary.CopyTo(array, index);
        }

        public TValue this[TKey key]
        {
            get
            {
                if (key == null) 
                    throw new ArgumentNullException("key");

                if (_privateDictionary.Contains(key))
                    return (TValue)_privateDictionary[key];

                return default(TValue);
            }
            set
            {
                if (key == null) 
                    throw new ArgumentNullException("key");

                _privateDictionary[key] = value;
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                var keys = new List<TKey>(_privateDictionary.Count);
                keys.AddRange(_privateDictionary.Keys.Cast<TKey>());
                return keys.AsReadOnly();
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                var values = new List<TValue>(_privateDictionary.Count);
                values.AddRange(_privateDictionary.Values.Cast<TValue>());
                return values.AsReadOnly();
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Add(TKey key, TValue value)
        {
            if (key == null) 
                throw new ArgumentNullException("key");

            _privateDictionary.Add(key, value);
        }

        public void Clear()
        {
            _privateDictionary.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (item.Key == null || !_privateDictionary.Contains(item.Key))
                return false;

            return _privateDictionary[item.Key].Equals(item.Value);
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null) 
                throw new ArgumentNullException("key");

            return _privateDictionary.Contains(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException("arrayIndex");
            if (array.Rank > 1 || arrayIndex >= array.Length
                               || array.Length - arrayIndex < _privateDictionary.Count)
                throw new ArgumentException("Bad Copy ToArray", "array");

            var index = arrayIndex;
            foreach (DictionaryEntry entry in _privateDictionary)
            {
                array[index] =
                    new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
                index++;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _privateDictionary)
            {
                yield return
                    new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (false == Contains(item)) 
                return false;

            _privateDictionary.Remove(item.Key);
            return true;
        }

        public bool Remove(TKey key)
        {
            if (key == null) 
                throw new ArgumentNullException("key");

            if (false == _privateDictionary.Contains(key)) 
                return false;

            _privateDictionary.Remove(key);
            return true;
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            var keyExists = _privateDictionary.Contains(key);
            value = keyExists ? (TValue)_privateDictionary[key] : default(TValue);

            return keyExists;
        }
    }
}
