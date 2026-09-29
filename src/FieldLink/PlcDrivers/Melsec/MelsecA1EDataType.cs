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

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Mitsubishi PLC의 데이터 타입, 여기 몇 가지 일반적으로 사용되는 타입이 포함되어 있습니다.</summary>
    public class MelsecA1EDataType
    {
        /// <summary>만약 호출자가 타입 코드를 알고 있다면, 값에 따라 확장할 수 있습니다.</summary>
        /// <param name = "code">데이터 타입 코드</param>
        /// <param name = "type">0 또는 1</param>
        /// <param name = "asciiCode">ASCII 형식의 타입 정보</param>
        /// <param name = "fromBase">10 또는 16</param>
        public MelsecA1EDataType(ushort code, byte type, string asciiCode, int fromBase)
        {
            DataCode = code;
            AsciiCode = asciiCode;
            FromBase = fromBase;
            if (type < 2)
                DataType = type;
        }

        /// <summary>타입의 코드값 ((소프트웨어 컴포넌트 코드, 소프트웨어 컴포넌트 유형을 구분하기 위해, 예를 들어: D, R)</summary>
        public ushort DataCode { get; private set; } = 0x00;
        /// <summary>데이터의 종류, 0은 문자, 1은 비트</summary>
        public byte DataType { get; private set; } = 0x00;
        /// <summary>ASCII 형식으로 통신할 때 타입 설명</summary>
        public string AsciiCode { get; private set; }
        /// <summary>10자리 또는 16자리 주소로 표시합니다.</summary>
        public int FromBase { get; private set; }

        /// <summary>X 입력 레지스터</summary>
        public readonly static MelsecA1EDataType X = new MelsecA1EDataType(0x5820, 0x01, "X*", 16);
        /// <summary>Y 출력 레지스터</summary>
        public readonly static MelsecA1EDataType Y = new MelsecA1EDataType(0x5920, 0x01, "Y*", 16);
        /// <summary>M 중간 레지스터</summary>
        public readonly static MelsecA1EDataType M = new MelsecA1EDataType(0x4D20, 0x01, "M*", 10);
        /// <summary>S 상태 레지스터</summary>
        public readonly static MelsecA1EDataType S = new MelsecA1EDataType(0x5320, 0x01, "S*", 10);
        /// <summary>F 경보</summary>
        public readonly static MelsecA1EDataType F = new MelsecA1EDataType(0x4620, 0x01, "F*", 10);
        /// <summary>B 연결 릴레이</summary>
        public readonly static MelsecA1EDataType B = new MelsecA1EDataType(0x4220, 0x01, "B*", 16);
        /// <summary>TS 타이머 터치</summary>
        public readonly static MelsecA1EDataType TS = new MelsecA1EDataType(0x5453, 0x01, "TS", 10);
        /// <summary>TC 타이머 코일</summary>
        public readonly static MelsecA1EDataType TC = new MelsecA1EDataType(0x5443, 0x01, "TC", 10);
        /// <summary>TN 타이머의 현재 값</summary>
        public readonly static MelsecA1EDataType TN = new MelsecA1EDataType(0x544E, 0x00, "TN", 10);
        /// <summary>CS 카운터 터치</summary>
        public readonly static MelsecA1EDataType CS = new MelsecA1EDataType(0x4353, 0x01, "CS", 10);
        /// <summary>CC 카운터 코일</summary>
        public readonly static MelsecA1EDataType CC = new MelsecA1EDataType(0x4343, 0x01, "CC", 10);
        /// <summary>CN 카운터 현재 값</summary>
        public readonly static MelsecA1EDataType CN = new MelsecA1EDataType(0x434E, 0x00, "CN", 10);
        /// <summary>D 데이터 레지스터</summary>
        public readonly static MelsecA1EDataType D = new MelsecA1EDataType(0x4420, 0x00, "D*", 10);
        /// <summary>W 링크 레지스터</summary>
        public readonly static MelsecA1EDataType W = new MelsecA1EDataType(0x5720, 0x00, "W*", 16);
        /// <summary>R 파일 레지스터</summary>
        public readonly static MelsecA1EDataType R = new MelsecA1EDataType(0x5220, 0x00, "R*", 10);
    }
}
