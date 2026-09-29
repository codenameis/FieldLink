using System;
using System.Text;

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>정수·실수의 프로토콜 바이트 순서입니다. 바이트 교환은 각 16비트 워드에 적용합니다.</summary>
    public enum ByteOrder
    {
        /// <summary>32비트 ABCD, 64비트 ABCDEFGH 순서입니다.</summary>
        BigEndian = 0,
        /// <summary>32비트 BADC, 64비트 BADCFEHG 순서입니다.</summary>
        BigEndianWithByteSwap = 1,
        /// <summary>32비트 CDAB, 64비트 GHEFCDAB 순서입니다.</summary>
        LittleEndianWithByteSwap = 2,
        /// <summary>32비트 DCBA, 64비트 HGFEDCBA 순서입니다.</summary>
        LittleEndian = 3
    }
}
