using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FieldLink.Secs.Protocols;

namespace FieldLink.Secs.Types
{
    /// <summary>SECS-II 값입니다. 배열 값은 복사하지 않으며 직렬화 중에는 수정하지 마세요.</summary>
    public sealed class SecsValue
    {
        /// <summary>본문 없는 메시지 값을 생성합니다.</summary>
        public SecsValue() : this(SecsItemType.None, null) { }
        /// <summary>문자열 값을 생성합니다.</summary>
        public SecsValue(string value) : this(SecsItemType.ASCII, value) { }
        /// <summary>문자열 목록을 생성합니다.</summary>
        public SecsValue(string[] value) : this((IEnumerable<object>)value) { }
        /// <summary>이진 배열을 생성합니다. U1 배열은 형식을 명시하세요.</summary>
        public SecsValue(byte[] value) : this(SecsItemType.Binary, value) { }
        /// <summary>Bool 값을 생성합니다.</summary>
        public SecsValue(bool value) : this(SecsItemType.Bool, value) { }
        /// <summary>Bool 배열을 생성합니다.</summary>
        public SecsValue(bool[] value) : this(SecsItemType.Bool, value) { }
        /// <summary>SByte 값을 생성합니다.</summary>
        public SecsValue(sbyte value) : this(SecsItemType.SByte, value) { }
        /// <summary>SByte 배열을 생성합니다.</summary>
        public SecsValue(sbyte[] value) : this(SecsItemType.SByte, value) { }
        /// <summary>Byte 값을 생성합니다.</summary>
        public SecsValue(byte value) : this(SecsItemType.Byte, value) { }
        /// <summary>Int16 값을 생성합니다.</summary>
        public SecsValue(short value) : this(SecsItemType.Int16, value) { }
        /// <summary>Int16 배열을 생성합니다.</summary>
        public SecsValue(short[] value) : this(SecsItemType.Int16, value) { }
        /// <summary>UInt16 값을 생성합니다.</summary>
        public SecsValue(ushort value) : this(SecsItemType.UInt16, value) { }
        /// <summary>UInt16 배열을 생성합니다.</summary>
        public SecsValue(ushort[] value) : this(SecsItemType.UInt16, value) { }
        /// <summary>Int32 값을 생성합니다.</summary>
        public SecsValue(int value) : this(SecsItemType.Int32, value) { }
        /// <summary>Int32 배열을 생성합니다.</summary>
        public SecsValue(int[] value) : this(SecsItemType.Int32, value) { }
        /// <summary>UInt32 값을 생성합니다.</summary>
        public SecsValue(uint value) : this(SecsItemType.UInt32, value) { }
        /// <summary>UInt32 배열을 생성합니다.</summary>
        public SecsValue(uint[] value) : this(SecsItemType.UInt32, value) { }
        /// <summary>Int64 값을 생성합니다.</summary>
        public SecsValue(long value) : this(SecsItemType.Int64, value) { }
        /// <summary>Int64 배열을 생성합니다.</summary>
        public SecsValue(long[] value) : this(SecsItemType.Int64, value) { }
        /// <summary>UInt64 값을 생성합니다.</summary>
        public SecsValue(ulong value) : this(SecsItemType.UInt64, value) { }
        /// <summary>UInt64 배열을 생성합니다.</summary>
        public SecsValue(ulong[] value) : this(SecsItemType.UInt64, value) { }
        /// <summary>Single 값을 생성합니다.</summary>
        public SecsValue(float value) : this(SecsItemType.Single, value) { }
        /// <summary>Single 배열을 생성합니다.</summary>
        public SecsValue(float[] value) : this(SecsItemType.Single, value) { }
        /// <summary>Double 값을 생성합니다.</summary>
        public SecsValue(double value) : this(SecsItemType.Double, value) { }
        /// <summary>Double 배열을 생성합니다.</summary>
        public SecsValue(double[] value) : this(SecsItemType.Double, value) { }

        /// <summary>지원되는 값들로 중첩 목록을 생성합니다. null 목록은 빈 목록입니다.</summary>
        public SecsValue(IEnumerable<object> values) : this(SecsItemType.List,
            (values ?? Enumerable.Empty<object>()).Select(FromObject).ToArray()) { }
        /// <summary>형식과 값을 지정합니다. List는 SecsValue 열거형, None은 null을 받습니다.</summary>
        public SecsValue(SecsItemType type, object value)
        {
            if (!Enum.IsDefined(typeof(SecsItemType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (type == SecsItemType.List)
            {
                var children = value == null ? new SecsValue[0] : (value as IEnumerable<SecsValue>)?.ToArray();
                if (children == null || children.Any(v => v == null || v.ItemType == SecsItemType.None))
                    throw new ArgumentException("List children must be nonempty SECS items.", nameof(value));
                value = children;
            }
            else if (type == SecsItemType.None)
            {
                if (value != null)
                    throw new ArgumentException("None has no value.", nameof(value));
            }
            else
            {
                Type element = ElementType(type);
                if (value == null || (value.GetType() != element && value.GetType() != element.MakeArrayType()) ||
                    ((type == SecsItemType.Binary || type == SecsItemType.JIS8) && !(value is byte[])) ||
                    (type == SecsItemType.ASCII && !(value is string)))
                    throw new ArgumentException("Value does not match the SECS type.", nameof(value));
            }
            ItemType = type;
            Value = value;
        }
        /// <summary>XML로 저장한 값을 복원합니다. 숫자는 문화권과 무관하게 해석합니다.</summary>
        public SecsValue(XElement element)
        {
            var value = SecsXml.Decode(element, 0);
            ItemType = value.ItemType;
            Value = value.Value;
        }
        /// <summary>데이터 형식입니다.</summary>
        public SecsItemType ItemType { get; }
        /// <summary>형식에 대응하는 스칼라, 배열 또는 문자열입니다.</summary>
        public object Value { get; }
        /// <summary>원소 수입니다. 문자열은 문자 수이며 인코딩된 바이트 수와 다를 수 있습니다.</summary>
        public int Length => Value is Array a ? a.Length : Value is string s ? s.Length : Value == null ? 0 : 1;
        /// <summary>기본 시스템 인코딩으로 직렬화합니다.</summary>
        public byte[] ToSourceBytes() => ToSourceBytes(Encoding.Default);
        /// <summary>지정한 문자열 인코딩으로 직렬화합니다.</summary>
        public byte[] ToSourceBytes(Encoding encoding) => Secs2Codec.Encode(this, encoding);
        /// <summary>전체 본문을 해석합니다. 잘리거나 남는 데이터는 거부합니다.</summary>
        public static SecsValue ParseFromSource(byte[] source, Encoding encoding) => Secs2Codec.Decode(source, encoding);
        /// <summary>빈 List를 생성합니다.</summary>
        public static SecsValue EmptyListValue() => new SecsValue(SecsItemType.List, null);
        /// <summary>본문 없는 값을 생성합니다.</summary>
        public static SecsValue EmptySecsValue() => new SecsValue();
        /// <summary>중첩 목록을 생성합니다.</summary>
        public static SecsValue CreateListSecsValue(params object[] values) => new SecsValue(values);
        /// <summary>상태 변수 이름 목록으로 변환합니다.</summary>
        public VariableName[] ToVariableNames() => RequireList(this).Select(v => (VariableName)v).ToArray();
        /// <summary>XML 표현을 반환합니다.</summary>
        public XElement ToXElement() => SecsXml.Encode(this, 0);
        /// <inheritdoc />
        public override string ToString() => ToXElement().ToString();
        internal static SecsValue[] RequireList(SecsValue value)
        {
            if (value == null || value.ItemType != SecsItemType.List)
                throw new InvalidDataException("Expected a SECS list.");
            return (SecsValue[])value.Value;
        }
        private static SecsValue FromObject(object value)
        {
            if (value is SecsValue item)
                return item;
            if (value is string text)
                return new SecsValue(text);
            if (value is byte[] binary)
                return new SecsValue(binary);
            if (value is IEnumerable<object> list)
                return new SecsValue(list);
            foreach (SecsItemType type in Enum.GetValues(typeof(SecsItemType)))
            {
                if (type == SecsItemType.List || type == SecsItemType.None || type == SecsItemType.ASCII ||
                    type == SecsItemType.Binary || type == SecsItemType.JIS8)
                    continue;
                Type element = ElementType(type);
                if (value != null && (value.GetType() == element || value.GetType() == element.MakeArrayType()))
                    return new SecsValue(type, value);
            }
            throw new ArgumentException("Unsupported SECS list value.", nameof(value));
        }
        internal static Type ElementType(SecsItemType type)
        {
            switch (type)
            {
                case SecsItemType.Bool: return typeof(bool);
                case SecsItemType.SByte: return typeof(sbyte);
                case SecsItemType.Byte: return typeof(byte);
                case SecsItemType.Int16: return typeof(short);
                case SecsItemType.UInt16: return typeof(ushort);
                case SecsItemType.Int32: return typeof(int);
                case SecsItemType.UInt32: return typeof(uint);
                case SecsItemType.Int64: return typeof(long);
                case SecsItemType.UInt64: return typeof(ulong);
                case SecsItemType.Single: return typeof(float);
                case SecsItemType.Double: return typeof(double);
                case SecsItemType.Binary:
                case SecsItemType.JIS8: return typeof(byte);
                case SecsItemType.ASCII: return typeof(string);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
