using FieldLink.PlcDrivers.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyCommandBuilder;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyResponseParser;
using static FieldLink.PlcDrivers.AllenBradley.AllenBradleyAddressParser;

namespace FieldLink.PlcDrivers.AllenBradley
{
    /// <summary>AllenBradley 프로토콜의 명령 및 데이터 타입 상수입니다.</summary>
    public static class AllenBradleyDefinitions
    {
        /// <summary>CIP_Execute_PCCC 프로토콜 값입니다.</summary>
        public const byte CIP_Execute_PCCC = 0x4B;
        /// <summary>CIP_READ_DATA 프로토콜 값입니다.</summary>
        public const byte CIP_READ_DATA = 0x4C;
        /// <summary>CIP_WRITE_DATA 프로토콜 값입니다.</summary>
        public const int CIP_WRITE_DATA = 0x4D;
        /// <summary>CIP_READ_WRITE_DATA 프로토콜 값입니다.</summary>
        public const int CIP_READ_WRITE_DATA = 0x4E;
        /// <summary>CIP_READ_FRAGMENT 프로토콜 값입니다.</summary>
        public const int CIP_READ_FRAGMENT = 0x52;
        /// <summary>CIP_WRITE_FRAGMENT 프로토콜 값입니다.</summary>
        public const int CIP_WRITE_FRAGMENT = 0x53;
        /// <summary>CIP_READ_LIST 프로토콜 값입니다.</summary>
        public const byte CIP_READ_LIST = 0x55;
        /// <summary>CIP_MULTIREAD_DATA 프로토콜 값입니다.</summary>
        public const int CIP_MULTIREAD_DATA = 0x1000;
        /// <summary>CIP_Type_DATE 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_DATE = 0x08;
        /// <summary>CIP_Type_TIME 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_TIME = 0x09;
        /// <summary>CIP_Type_TimeAndDate 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_TimeAndDate = 0x0A;
        /// <summary>CIP_Type_TimeOfDate 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_TimeOfDate = 0x0B;
        /// <summary>CIP_Type_Bool 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_Bool = 0xC1;
        /// <summary>CIP_Type_Byte 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_Byte = 0xC2;
        /// <summary>CIP_Type_Word 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_Word = 0xC3;
        /// <summary>CIP_Type_DWord 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_DWord = 0xC4;
        /// <summary>CIP_Type_LInt 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_LInt = 0xC5;
        /// <summary>CIP_Type_USInt 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_USInt = 0xC6;
        /// <summary>CIP_Type_UInt 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_UInt = 0xC7;
        /// <summary>CIP_Type_UDint 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_UDint = 0xC8;
        /// <summary>CIP_Type_ULint 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_ULint = 0xC9;
        /// <summary>CIP_Type_Real 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_Real = 0xCA;
        /// <summary>CIP_Type_Double 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_Double = 0xCB;
        /// <summary>CIP_Type_Struct 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_Struct = 0xCC;
        /// <summary>CIP_Type_String 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_String = 0xD0;
        /// <summary>CIP_Type_D1 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_D1 = 0xD1;
        /// <summary>CIP_Type_D2 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_D2 = 0xD2;
        /// <summary>CIP_Type_D3 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_D3 = 0xD3;
        /// <summary>CIP_Type_D4 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_D4 = 0xD4;
        /// <summary>CIP_Type_BitArray 프로토콜 값입니다.</summary>
        public const ushort CIP_Type_BitArray = 0xD3;
        /// <summary>OriginatorVendorID 프로토콜 값입니다.</summary>
        public const ushort OriginatorVendorID = 0x1009;
        /// <summary>호출자가 생성하여 전달하는 4바이트 발신자 시리얼 번호입니다.</summary>
        public const uint OriginatorSerialNumber = 0xC1A5460B;
    }
}
