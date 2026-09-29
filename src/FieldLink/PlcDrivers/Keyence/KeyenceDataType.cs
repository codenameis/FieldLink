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

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>Keyence PLC의 데이터 타입, 여기 몇 가지 일반적으로 사용되는 타입이 포함되어 있습니다.</summary>
    public class KeyenceDataType
    {
        /// <summary>만약 호출자가 타입 코드를 알고 있다면, 값에 따라 확장할 수 있습니다.</summary>
        /// <param name = "code">데이터 타입 코드</param>
        /// <param name = "type">0 또는 1</param>
        /// <param name = "asciiCode">ASCII 형식의 타입 정보</param>
        /// <param name = "fromBase">10 또는 16</param>
        public KeyenceDataType(byte code, byte type, string asciiCode, int fromBase)
        {
            DataCode = code;
            AsciiCode = asciiCode;
            FromBase = fromBase;
            if (type < 2)
                DataType = type;
        }

        /// <summary>타입의 코드값</summary>
        public byte DataCode { get; private set; } = 0x00;
        /// <summary>데이터의 종류, 0은 문자, 1은 비트</summary>
        public byte DataType { get; private set; } = 0x00;
        /// <summary>ASCII 형식으로 통신할 때 타입 설명</summary>
        public string AsciiCode { get; private set; }
        /// <summary>10자리 또는 16자리 주소로 표시합니다.</summary>
        public int FromBase { get; private set; }

        /// <summary>X 입력 릴레이</summary>
        public readonly static KeyenceDataType X = new KeyenceDataType(0x9C, 0x01, "X*", 16);
        /// <summary>Y 출력 릴레이</summary>
        public readonly static KeyenceDataType Y = new KeyenceDataType(0x9D, 0x01, "Y*", 16);
        /// <summary>연결 릴레이</summary>
        public readonly static KeyenceDataType B = new KeyenceDataType(0xA0, 0x01, "B*", 16);
        /// <summary>내부 보조 릴레이</summary>
        public readonly static KeyenceDataType M = new KeyenceDataType(0x90, 0x01, "M*", 10);
        /// <summary>릴레이를 잠금</summary>
        public readonly static KeyenceDataType L = new KeyenceDataType(0x92, 0x01, "L*", 10);
        /// <summary>제어 릴레이</summary>
        public readonly static KeyenceDataType SM = new KeyenceDataType(0x91, 0x01, "SM", 10);
        /// <summary>메모리를 제어</summary>
        public readonly static KeyenceDataType SD = new KeyenceDataType(0xA9, 0x00, "SD", 10);
        /// <summary>데이터 저장소</summary>
        public readonly static KeyenceDataType D = new KeyenceDataType(0xA8, 0x00, "D*", 10);
        /// <summary>파일 저장소</summary>
        public readonly static KeyenceDataType R = new KeyenceDataType(0xAF, 0x00, "R*", 10);
        /// <summary>파일 저장소</summary>
        public readonly static KeyenceDataType ZR = new KeyenceDataType(0xB0, 0x00, "ZR", 16);
        /// <summary>링크 레지스터</summary>
        public readonly static KeyenceDataType W = new KeyenceDataType(0xB4, 0x00, "W*", 16);
        /// <summary>타이머 (현재 값)</summary>
        public readonly static KeyenceDataType TN = new KeyenceDataType(0xC2, 0x00, "TN", 10);
        /// <summary>타이머 ((연결점)</summary>
        public readonly static KeyenceDataType TS = new KeyenceDataType(0xC1, 0x01, "TS", 10);
        /// <summary>카운터 (현재 값)</summary>
        public readonly static KeyenceDataType CN = new KeyenceDataType(0xC5, 0x00, "CN", 10);
        /// <summary>카운터 ((연결점)</summary>
        public readonly static KeyenceDataType CS = new KeyenceDataType(0xC4, 0x01, "CS", 10);
    }
}
