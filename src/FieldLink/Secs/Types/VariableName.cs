using System;
using System.IO;

namespace FieldLink.Secs.Types
{
    /// <summary>상태 변수 ID, 이름과 단위입니다.</summary>
    public sealed class VariableName
    {
        /// <summary>변수 ID입니다.</summary>
        public long ID { get; set; }
        /// <summary>변수 이름입니다.</summary>
        public string Name { get; set; }
        /// <summary>단위입니다.</summary>
        public string Units { get; set; }
        /// <summary>S1F12의 변수 항목을 변환합니다.</summary>
        public static implicit operator VariableName(SecsValue value)
        {
            var list = SecsValue.RequireList(value);
            if (list.Length != 3 || list[1].ItemType != SecsItemType.ASCII || list[2].ItemType != SecsItemType.ASCII ||
                list[0].ItemType < SecsItemType.SByte || list[0].ItemType > SecsItemType.UInt64 || list[0].Value is Array)
                throw new InvalidDataException("Expected numeric ID, variable name and units.");
            return new VariableName { ID = Convert.ToInt64(list[0].Value), Name = (string)list[1].Value, Units = (string)list[2].Value };
        }
        /// <summary>변수를 SECS List로 변환합니다.</summary>
        public static implicit operator SecsValue(VariableName value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return new SecsValue(new object[] { value.ID, value.Name, value.Units });
        }
        /// <inheritdoc />
        public override string ToString() => Name;
    }
}
