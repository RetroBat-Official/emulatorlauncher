using System;
using System.Diagnostics;

namespace ValveKeyValue
{
    [DebuggerDisplay("{value}")]
    class KVObjectValue<TObject> : KVValue
        where TObject : IConvertible
    {
        public KVObjectValue(TObject value, KVValueType valueType)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            this.value = value;
            _valueType = valueType;
        }

        readonly TObject value;

        public override KVValueType ValueType { get { return _valueType; } }
        private KVValueType _valueType;

        public override TypeCode GetTypeCode()
        {
            switch (ValueType)
            {
                case KVValueType.Collection: 
                    return TypeCode.Object;
                case KVValueType.FloatingPoint: 
                    return TypeCode.Single;
                case KVValueType.Int32:
                case KVValueType.Pointer:
                    return TypeCode.Int32;
                case KVValueType.String: 
                    return TypeCode.String;
                case KVValueType.UInt64:
                    return TypeCode.UInt64;
                default:
                    throw new NotImplementedException("No known TypeCode for '" + ValueType + "'.");
            };
        }

        public override bool ToBoolean(IFormatProvider provider) { return ToInt32(provider) == 1; }

        public override byte ToByte(IFormatProvider provider) { return (byte)Convert.ChangeType(value, typeof(byte), provider); }

        public override char ToChar(IFormatProvider provider) { return (char)Convert.ChangeType(value, typeof(char), provider); }

        public override DateTime ToDateTime(IFormatProvider provider)
        {
            throw new InvalidCastException("Casting to DateTime is not supported.");
        }

        public override decimal ToDecimal(IFormatProvider provider) { return (decimal)Convert.ChangeType(value, typeof(decimal), provider); }

        public override double ToDouble(IFormatProvider provider) { return (double)Convert.ChangeType(value, typeof(double), provider); }

        public override short ToInt16(IFormatProvider provider) { return (short)Convert.ChangeType(value, typeof(short), provider); }

        public override int ToInt32(IFormatProvider provider) { return (int)Convert.ChangeType(value, typeof(int), provider); }

        public override long ToInt64(IFormatProvider provider) { return (long)Convert.ChangeType(value, typeof(long), provider); }

        public override sbyte ToSByte(IFormatProvider provider) { return (sbyte)Convert.ChangeType(value, typeof(sbyte), provider); }

        public override float ToSingle(IFormatProvider provider) { return (float)Convert.ChangeType(value, typeof(float), provider); }

        public override string ToString(IFormatProvider provider) { return (string)Convert.ChangeType(value, typeof(string), provider); }

        public override object ToType(Type conversionType, IFormatProvider provider) { return Convert.ChangeType(value, conversionType, provider); }

        public override ushort ToUInt16(IFormatProvider provider) { return (ushort)Convert.ChangeType(value, typeof(ushort), provider); }

        public override uint ToUInt32(IFormatProvider provider) { return (uint)Convert.ChangeType(value, typeof(uint), provider); }

        public override ulong ToUInt64(IFormatProvider provider) { return (ulong)Convert.ChangeType(value, typeof(ulong), provider); }

        public override string ToString() { return ToString(null); }
    }
}
