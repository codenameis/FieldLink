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

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>Omron의 Fins 프로토콜의 데이터 타입</summary>
    public class OmronFinsDataType
    {
        /// <summary>Fins의 데이터 타입을 인스턴스화합니다.</summary>
        /// <param name = "bitCode">비트 조작을 위한 명령어</param>
        /// <param name = "wordCode">워드 조작을 위한 명령어</param>
        public OmronFinsDataType(byte bitCode, byte wordCode)
        {
            BitCode = bitCode;
            WordCode = wordCode;
        }

        /// <summary>비트 조작을 위한 명령어</summary>
        public byte BitCode { get; private set; }
        /// <summary>워드 조작을 위한 명령어</summary>
        public byte WordCode { get; private set; }

        /// <summary>DM 영역</summary>
        public static readonly OmronFinsDataType DM = new OmronFinsDataType(0x02, 0x82);
        /// <summary>CIO 영역</summary>
        public static readonly OmronFinsDataType CIO = new OmronFinsDataType(0x30, 0xB0);
        /// <summary>작업 영역</summary>
        public static readonly OmronFinsDataType WR = new OmronFinsDataType(0x31, 0xB1);
        /// <summary>홀딩 비트 영역</summary>
        public static readonly OmronFinsDataType HR = new OmronFinsDataType(0x32, 0xB2);
        /// <summary>보조 비트 영역</summary>
        public static readonly OmronFinsDataType AR = new OmronFinsDataType(0x33, 0xB3);
        /// <summary>TIM 영역</summary>
        public static readonly OmronFinsDataType TIM = new OmronFinsDataType(0x09, 0x89);
    }
}
