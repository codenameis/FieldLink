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
    public class MelsecMcDataType
    {
        /// <summary>Mitsubishi 데이터 타입 객체를 인스턴스화합니다. 타입 코드가 알고 있으면 값에 따라 확장할 수 있습니다.</summary>
        /// <param name = "code">데이터 타입 코드</param>
        /// <param name = "type">0 또는 1, 0은 문자, 1은 비트입니다.</param>
        /// <param name = "asciiCode">ASCII 형식의 타입 정보</param>
        /// <param name = "fromBase">10 또는 16</param>
        public MelsecMcDataType(ushort code, byte type, string asciiCode, int fromBase)
        {
            DataCode = code;
            AsciiCode = asciiCode;
            FromBase = fromBase;
            if (type < 2)
                DataType = type;
        }

        /// <summary>타입의 코드값</summary>
        public ushort DataCode { get; private set; } = 0x00;
        /// <summary>데이터의 종류, 0은 문자, 1은 비트</summary>
        public byte DataType { get; private set; } = 0x00;
        /// <summary>ASCII 형식으로 통신할 때 타입 설명</summary>
        public string AsciiCode { get; private set; }
        /// <summary>10자리 또는 16자리 주소로 표시합니다.</summary>
        public int FromBase { get; private set; }

        /// <summary>X 입력 릴레이</summary>
        public readonly static MelsecMcDataType X = new MelsecMcDataType(0x9C, 0x01, "X*", 16);
        /// <summary>Y 출력 릴레이</summary>
        public readonly static MelsecMcDataType Y = new MelsecMcDataType(0x9D, 0x01, "Y*", 16);
        /// <summary>M 내부 릴레이</summary>
        public readonly static MelsecMcDataType M = new MelsecMcDataType(0x90, 0x01, "M*", 10);
        /// <summary>SM 특수 릴레이</summary>
        public readonly static MelsecMcDataType SM = new MelsecMcDataType(0x91, 0x01, "SM", 10);
        /// <summary>S 스텝 리레일</summary>
        public readonly static MelsecMcDataType S = new MelsecMcDataType(0x98, 0x01, "S*", 10);
        /// <summary>L 잠금 릴레이</summary>
        public readonly static MelsecMcDataType L = new MelsecMcDataType(0x92, 0x01, "L*", 10);
        /// <summary>F 경보</summary>
        public readonly static MelsecMcDataType F = new MelsecMcDataType(0x93, 0x01, "F*", 10);
        /// <summary>V변 리리저</summary>
        public readonly static MelsecMcDataType V = new MelsecMcDataType(0x94, 0x01, "V*", 10);
        /// <summary>B 연결 리레일</summary>
        public readonly static MelsecMcDataType B = new MelsecMcDataType(0xA0, 0x01, "B*", 16);
        /// <summary>SB 특수 연결 릴레이</summary>
        public readonly static MelsecMcDataType SB = new MelsecMcDataType(0xA1, 0x01, "SB", 16);
        /// <summary>DX 직접 입력</summary>
        public readonly static MelsecMcDataType DX = new MelsecMcDataType(0xA2, 0x01, "DX", 16);
        /// <summary>DY 직접 출력</summary>
        public readonly static MelsecMcDataType DY = new MelsecMcDataType(0xA3, 0x01, "DY", 16);
        /// <summary>D 데이터 레지스터</summary>
        public readonly static MelsecMcDataType D = new MelsecMcDataType(0xA8, 0x00, "D*", 10);
        /// <summary>특수 링크 메모리</summary>
        public readonly static MelsecMcDataType SD = new MelsecMcDataType(0xA9, 0x00, "SD", 10);
        /// <summary>W 링크 레지스터</summary>
        public readonly static MelsecMcDataType W = new MelsecMcDataType(0xB4, 0x00, "W*", 16);
        /// <summary>SW 특수 연결 레지스터</summary>
        public readonly static MelsecMcDataType SW = new MelsecMcDataType(0xB5, 0x00, "SW", 16);
        /// <summary>R 파일 레지스터</summary>
        public readonly static MelsecMcDataType R = new MelsecMcDataType(0xAF, 0x00, "R*", 10);
        /// <summary>주소 변경 레지스터</summary>
        public readonly static MelsecMcDataType Z = new MelsecMcDataType(0xCC, 0x00, "Z*", 10);
        /// <summary>파일 저장소 ZR 구역</summary>
        public readonly static MelsecMcDataType ZR = new MelsecMcDataType(0xB0, 0x00, "ZR", 10);
        /// <summary>타이머의 현재 값</summary>
        public readonly static MelsecMcDataType TN = new MelsecMcDataType(0xC2, 0x00, "TN", 10);
        /// <summary>타이머의 터치</summary>
        public readonly static MelsecMcDataType TS = new MelsecMcDataType(0xC1, 0x01, "TS", 10);
        /// <summary>타이머의 코일</summary>
        public readonly static MelsecMcDataType TC = new MelsecMcDataType(0xC0, 0x01, "TC", 10);
        /// <summary>누적 타이머의 터치</summary>
        public readonly static MelsecMcDataType SS = new MelsecMcDataType(0xC7, 0x01, "SS", 10);
        /// <summary>축적 타이머의 코일</summary>
        public readonly static MelsecMcDataType SC = new MelsecMcDataType(0xC6, 0x01, "SC", 10);
        /// <summary>누적 타이머의 현재 값</summary>
        public readonly static MelsecMcDataType SN = new MelsecMcDataType(0xC8, 0x00, "SN", 10);
        /// <summary>카운터의 현재 값</summary>
        public readonly static MelsecMcDataType CN = new MelsecMcDataType(0xC5, 0x00, "CN", 10);
        /// <summary>카운터의 터치</summary>
        public readonly static MelsecMcDataType CS = new MelsecMcDataType(0xC4, 0x01, "CS", 10);
        /// <summary>카운터의 코일</summary>
        public readonly static MelsecMcDataType CC = new MelsecMcDataType(0xC3, 0x01, "CC", 10);
        /// <summary>X 입력 릴레이</summary>
        public readonly static MelsecMcDataType R_X = new MelsecMcDataType(0x009C, 0x01, "X***", 16);
        /// <summary>Y 입력 릴레이</summary>
        public readonly static MelsecMcDataType R_Y = new MelsecMcDataType(0x009D, 0x01, "Y***", 16);
        /// <summary>M 내부 릴레이</summary>
        public readonly static MelsecMcDataType R_M = new MelsecMcDataType(0x0090, 0x01, "M***", 10);
        /// <summary>특수 릴레이</summary>
        public readonly static MelsecMcDataType R_SM = new MelsecMcDataType(0x0091, 0x01, "SM**", 10);
        /// <summary>릴레이를 잠금</summary>
        public readonly static MelsecMcDataType R_L = new MelsecMcDataType(0x0092, 0x01, "L***", 10);
        /// <summary>경보 장치</summary>
        public readonly static MelsecMcDataType R_F = new MelsecMcDataType(0x0093, 0x01, "F***", 10);
        /// <summary>방향 전환 릴레이터</summary>
        public readonly static MelsecMcDataType R_V = new MelsecMcDataType(0x0094, 0x01, "V***", 10);
        /// <summary>S 스텝 리레일</summary>
        public readonly static MelsecMcDataType R_S = new MelsecMcDataType(0x0098, 0x01, "S***", 10);
        /// <summary>연결 릴레이</summary>
        public readonly static MelsecMcDataType R_B = new MelsecMcDataType(0x00A0, 0x01, "B***", 16);
        /// <summary>특수 링크 릴레이</summary>
        public readonly static MelsecMcDataType R_SB = new MelsecMcDataType(0x00A1, 0x01, "SB**", 16);
        /// <summary>입력 리어에 직접 액세스</summary>
        public readonly static MelsecMcDataType R_DX = new MelsecMcDataType(0x00A2, 0x01, "DX**", 16);
        /// <summary>직접 출력 릴에 접근</summary>
        public readonly static MelsecMcDataType R_DY = new MelsecMcDataType(0x00A3, 0x01, "DY**", 16);
        /// <summary>데이터 레지스터</summary>
        public readonly static MelsecMcDataType R_D = new MelsecMcDataType(0x00A8, 0x00, "D***", 10);
        /// <summary>특수 데이터 레지스터</summary>
        public readonly static MelsecMcDataType R_SD = new MelsecMcDataType(0x00A9, 0x00, "SD**", 10);
        /// <summary>링크 레지스터</summary>
        public readonly static MelsecMcDataType R_W = new MelsecMcDataType(0x00B4, 0x00, "W***", 16);
        /// <summary>특수 링크 레지스터</summary>
        public readonly static MelsecMcDataType R_SW = new MelsecMcDataType(0x00B5, 0x00, "SW**", 16);
        /// <summary>파일 저장소</summary>
        public readonly static MelsecMcDataType R_R = new MelsecMcDataType(0x00AF, 0x00, "R***", 10);
        /// <summary>주소 변경 레지스터</summary>
        public readonly static MelsecMcDataType R_Z = new MelsecMcDataType(0x00CC, 0x00, "Z***", 10);
        /// <summary>긴 누적 타이머 터치</summary>
        public readonly static MelsecMcDataType R_LSTS = new MelsecMcDataType(0x0059, 0x01, "LSTS", 10);
        /// <summary>긴 축적 타이머 코일</summary>
        public readonly static MelsecMcDataType R_LSTC = new MelsecMcDataType(0x0058, 0x01, "LSTC", 10);
        /// <summary>롱 누적 타이머 현재 값</summary>
        public readonly static MelsecMcDataType R_LSTN = new MelsecMcDataType(0x005A, 0x00, "LSTN", 10);
        /// <summary>누적 타이머 터치</summary>
        public readonly static MelsecMcDataType R_STS = new MelsecMcDataType(0x00C7, 0x01, "STS*", 10);
        /// <summary>축적 타이머 코일</summary>
        public readonly static MelsecMcDataType R_STC = new MelsecMcDataType(0x00C6, 0x01, "STC*", 10);
        /// <summary>누적 타이머 현재 값</summary>
        public readonly static MelsecMcDataType R_STN = new MelsecMcDataType(0x00C8, 0x00, "STN*", 10);
        /// <summary>긴 타이머 터치</summary>
        public readonly static MelsecMcDataType R_LTS = new MelsecMcDataType(0x0051, 0x01, "LTS*", 10);
        /// <summary>긴 타이머 코일</summary>
        public readonly static MelsecMcDataType R_LTC = new MelsecMcDataType(0x0050, 0x01, "LTC*", 10);
        /// <summary>롱타이머의 현재 값</summary>
        public readonly static MelsecMcDataType R_LTN = new MelsecMcDataType(0x0052, 0x00, "LTN*", 10);
        /// <summary>타이머 터치</summary>
        public readonly static MelsecMcDataType R_TS = new MelsecMcDataType(0x00C1, 0x01, "TS**", 10);
        /// <summary>타이머 코일</summary>
        public readonly static MelsecMcDataType R_TC = new MelsecMcDataType(0x00C0, 0x01, "TC**", 10);
        /// <summary>타이머의 현재 값</summary>
        public readonly static MelsecMcDataType R_TN = new MelsecMcDataType(0x00C2, 0x00, "TN**", 10);
        /// <summary>길거리 카운터 터치</summary>
        public readonly static MelsecMcDataType R_LCS = new MelsecMcDataType(0x0055, 0x01, "LCS*", 10);
        /// <summary>길이를 계산하는 코일</summary>
        public readonly static MelsecMcDataType R_LCC = new MelsecMcDataType(0x0054, 0x01, "LCC*", 10);
        /// <summary>길거리 카운터의 현재 값</summary>
        public readonly static MelsecMcDataType R_LCN = new MelsecMcDataType(0x0056, 0x00, "LCN*", 10);
        /// <summary>카운터 터치</summary>
        public readonly static MelsecMcDataType R_CS = new MelsecMcDataType(0x00C4, 0x01, "CS**", 10);
        /// <summary>카운터 코일</summary>
        public readonly static MelsecMcDataType R_CC = new MelsecMcDataType(0x00C3, 0x01, "CC**", 10);
        /// <summary>카운터 현재 값</summary>
        public readonly static MelsecMcDataType R_CN = new MelsecMcDataType(0x00C5, 0x00, "CN**", 10);
        /// <summary>X 입력 릴레이</summary>
        public readonly static MelsecMcDataType Keyence_X = new MelsecMcDataType(0x9C, 0x01, "X*", 16);
        /// <summary>Y 출력 릴레이</summary>
        public readonly static MelsecMcDataType Keyence_Y = new MelsecMcDataType(0x9D, 0x01, "Y*", 16);
        /// <summary>연결 릴레이</summary>
        public readonly static MelsecMcDataType Keyence_B = new MelsecMcDataType(0xA0, 0x01, "B*", 16);
        /// <summary>내부 보조 릴레이</summary>
        public readonly static MelsecMcDataType Keyence_M = new MelsecMcDataType(0x90, 0x01, "M*", 10);
        /// <summary>릴레이를 잠금</summary>
        public readonly static MelsecMcDataType Keyence_L = new MelsecMcDataType(0x92, 0x01, "L*", 10);
        /// <summary>제어 릴레이</summary>
        public readonly static MelsecMcDataType Keyence_SM = new MelsecMcDataType(0x91, 0x01, "SM", 10);
        /// <summary>메모리를 제어</summary>
        public readonly static MelsecMcDataType Keyence_SD = new MelsecMcDataType(0xA9, 0x00, "SD", 10);
        /// <summary>데이터 저장소</summary>
        public readonly static MelsecMcDataType Keyence_D = new MelsecMcDataType(0xA8, 0x00, "D*", 10);
        /// <summary>파일 저장소</summary>
        public readonly static MelsecMcDataType Keyence_R = new MelsecMcDataType(0xAF, 0x00, "R*", 10);
        /// <summary>파일 저장소</summary>
        public readonly static MelsecMcDataType Keyence_ZR = new MelsecMcDataType(0xB0, 0x00, "ZR", 16);
        /// <summary>링크 레지스터</summary>
        public readonly static MelsecMcDataType Keyence_W = new MelsecMcDataType(0xB4, 0x00, "W*", 16);
        /// <summary>타이머 (현재 값)</summary>
        public readonly static MelsecMcDataType Keyence_TN = new MelsecMcDataType(0xC2, 0x00, "TN", 10);
        /// <summary>타이머 ((연결점)</summary>
        public readonly static MelsecMcDataType Keyence_TS = new MelsecMcDataType(0xC1, 0x01, "TS", 10);
        /// <summary>타이머 ((코일)</summary>
        public readonly static MelsecMcDataType Keyence_TC = new MelsecMcDataType(0xC0, 0x01, "TC", 10);
        /// <summary>카운터 (현재 값)</summary>
        public readonly static MelsecMcDataType Keyence_CN = new MelsecMcDataType(0xC5, 0x00, "CN", 10);
        /// <summary>카운터 ((연결점)</summary>
        public readonly static MelsecMcDataType Keyence_CS = new MelsecMcDataType(0xC4, 0x01, "CS", 10);
        /// <summary>카운터 ((코일)</summary>
        public readonly static MelsecMcDataType Keyence_CC = new MelsecMcDataType(0xC3, 0x01, "CC", 10);
        /// <summary>입력 릴레이</summary>
        public readonly static MelsecMcDataType Panasonic_X = new MelsecMcDataType(0x9C, 0x01, "X*", 10);
        /// <summary>출력 릴레이</summary>
        public readonly static MelsecMcDataType Panasonic_Y = new MelsecMcDataType(0x9D, 0x01, "Y*", 10);
        /// <summary>연결 릴레이</summary>
        public readonly static MelsecMcDataType Panasonic_L = new MelsecMcDataType(0xA0, 0x01, "L*", 10);
        /// <summary>내부 릴레이</summary>
        public readonly static MelsecMcDataType Panasonic_R = new MelsecMcDataType(0x90, 0x01, "R*", 10);
        /// <summary>데이터 저장소</summary>
        public readonly static MelsecMcDataType Panasonic_DT = new MelsecMcDataType(0xA8, 0x00, "D*", 10);
        /// <summary>링크 메모리</summary>
        public readonly static MelsecMcDataType Panasonic_LD = new MelsecMcDataType(0xB4, 0x00, "W*", 10);
        /// <summary>타이머 (현재 값)</summary>
        public readonly static MelsecMcDataType Panasonic_TN = new MelsecMcDataType(0xC2, 0x00, "TN", 10);
        /// <summary>타이머 ((연결점)</summary>
        public readonly static MelsecMcDataType Panasonic_TS = new MelsecMcDataType(0xC1, 0x01, "TS", 10);
        /// <summary>카운터 (현재 값)</summary>
        public readonly static MelsecMcDataType Panasonic_CN = new MelsecMcDataType(0xC5, 0x00, "CN", 10);
        /// <summary>카운터 ((연결점)</summary>
        public readonly static MelsecMcDataType Panasonic_CS = new MelsecMcDataType(0xC4, 0x01, "CS", 10);
        /// <summary>특수 링크 릴레이</summary>
        public readonly static MelsecMcDataType Panasonic_SM = new MelsecMcDataType(0x91, 0x01, "SM", 10);
        /// <summary>특수 링크 메모리</summary>
        public readonly static MelsecMcDataType Panasonic_SD = new MelsecMcDataType(0xA9, 0x00, "SD", 10);
    }
}
