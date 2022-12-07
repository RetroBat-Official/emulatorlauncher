using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher.PadToKeyboard
{

    class JoyInputState
    {
        public JoyInputState()
        {
        }

        public JoyInputState(InputKey k, int v = 1, bool isAxis = false)
        {
            _keys.Add((int)k, new InputValue(v, false));
        }

        public void Add(InputKey k, int v = 1, bool isAxis = false)
        {
            _keys.Remove((int)k);
            _keys.Add((int)k, new InputValue(v, isAxis));
        }

        public void Remove(InputKey key)
        {
            _keys.Remove((int)key);
        }

        class InputValue
        {
            public InputValue(int val, bool isAxis = false)
            {
                Value = val;
                IsAxis = isAxis;
            }

            public int Value { get; set; }
            public bool IsAxis { get; set; }

            public override string ToString()
            {
                if (IsAxis)
                    return "axis:" + Value;

                return Value.ToString();
            }
        };

        private Dictionary<int, InputValue> _keys = new Dictionary<int, InputValue>();

        public static JoyInputState operator |(JoyInputState a, JoyInputState b)
        {
            JoyInputState ret = new JoyInputState();
            ret._keys = new Dictionary<int, InputValue>(a._keys);

            foreach (var kb in b._keys)
                ret._keys[kb.Key] = kb.Value;

            return ret;
        }

        public override bool Equals(object obj)
        {
            JoyInputState c1 = this;
            JoyInputState c2 = obj as JoyInputState;
            if (c2 == null)
                return false;

            if (c1._keys.Count != c2._keys.Count)
                return false;

            foreach (var key in c1._keys)
            {
                InputValue value;
                if (!c2._keys.TryGetValue(key.Key, out value))
                    return false;

                if (value.Value != key.Value.Value || value.IsAxis != key.Value.IsAxis)
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(JoyInputState c1, JoyInputState c2)
        {
            return !object.ReferenceEquals(c1, null) && !object.ReferenceEquals(c2, null) && c1.Equals(c2);
        }

        public static bool operator !=(JoyInputState c1, JoyInputState c2)
        {
            if (c1 == null && c2 != null)
                return true;

            return !c1.Equals(c2);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var kb in _keys)
            {
                if (sb.Length > 0)
                    sb.Append(" | ");

                sb.Append(((InputKey)kb.Key).ToString() + ":" + kb.Value);
            }

            return sb.ToString();
        }

        public bool HasFlag(InputKey k)
        {
            return FromInputKey(k).Count > 0;
        }

        public int GetMouseInputValue(InputKey k)
        {
            InputValue newValue;
            if (!_keys.TryGetValue((int)k, out newValue))
                return 0;

            if (k == InputKey.joystick2right || k == InputKey.joystick2down || k == InputKey.joystick1right || k == InputKey.joystick2down)
                return Math.Abs(newValue.Value);

            return -Math.Abs(newValue.Value);
        }

        Dictionary<int, InputValue> FromInputKey(InputKey k)
        {
            Dictionary<int, InputValue> ret = new Dictionary<int, InputValue>();

            int kz = 0;

            foreach (var key in _keys)
            {
                if (((int)k & key.Key) == key.Key)
                {
                    ret[key.Key] = key.Value;
                    kz |= key.Key;
                }
            }

            if (kz != (int)k)
                return new Dictionary<int, InputValue>();

            return ret;
        }

        public bool HasNewInput(InputKey k, JoyInputState old, bool checkAxisChanged = false)
        {
            var newValues = FromInputKey(k);
            if (newValues.Count == 0)
                return false;

            var oldValues = old.FromInputKey(k);
            if (oldValues.Count == 0)
            {
                if (newValues.Any(v => v.Value.IsAxis))
                {
                    foreach (var nv in newValues.Where(v => v.Value.IsAxis))
                        oldValues[nv.Key] = new InputValue(0, true);
                }
                else
                    return true;
            }

            foreach (var ov in oldValues)
            {
                if (!newValues.ContainsKey(ov.Key))
                    return false;

                var oldVal = oldValues[ov.Key];
                var newVal = newValues[ov.Key];

                if (oldVal.IsAxis)
                {
                    if (checkAxisChanged)
                    {
                        if (oldVal.Value != newVal.Value)
                            return true;
                    }

                    const int DEADZONE = 20000;

                    bool oldOutOfDeadZone = Math.Abs(oldVal.Value) >= DEADZONE;
                    bool newOutOfDeadZone = Math.Abs(newVal.Value) >= DEADZONE;

                    if (newOutOfDeadZone && !oldOutOfDeadZone)
                        return true;
                }
                else if (oldVal.Value != newVal.Value)
                    return true;
            }

            return false;
        }

        public JoyInputState Clone()
        {
            JoyInputState clone = new JoyInputState();
            clone._keys = new Dictionary<int, InputValue>(_keys);
            return clone;
        }
    }

}
