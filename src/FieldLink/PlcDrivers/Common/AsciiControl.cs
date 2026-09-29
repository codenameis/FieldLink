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

namespace FieldLink.PlcDrivers.Common
{
    /// <summary>제어형 ascii 자원 정보</summary>
    public class AsciiControl
    {
        /// <summary>널 문자</summary>
        public const byte NUL = 0x00;
        /// <summary>제목 시작</summary>
        public const byte SOH = 0x01;
        /// <summary>텍스트 시작</summary>
        public const byte STX = 0x02;
        /// <summary>텍스트 끝</summary>
        public const byte ETX = 0x03;
        /// <summary>전송 종료</summary>
        public const byte EOT = 0x04;
        /// <summary>요청</summary>
        public const byte ENQ = 0x05;
        /// <summary>통지</summary>
        public const byte ACK = 0x06;
        /// <summary>벨</summary>
        public const byte BEL = 0x07;
        /// <summary>백스페이스</summary>
        public const byte BS = 0x08;
        /// <summary>수평화 표시기</summary>
        public const byte HT = 0x09;
        /// <summary>선변자</summary>
        public const byte LF = 0x0a;
        /// <summary>수직 표기</summary>
        public const byte VT = 0x0b;
        /// <summary>페이지를 변경</summary>
        public const byte FF = 0x0c;
        /// <summary>캐리지 리턴</summary>
        public const byte CR = 0x0d;
        /// <summary>전환할 필요가 없습니다.</summary>
        public const byte SO = 0x0e;
        /// <summary>스위치를 활성화</summary>
        public const byte SI = 0x0f;
        /// <summary>데이터 링크 정의</summary>
        public const byte DLE = 0x10;
        /// <summary>장치 제어 1</summary>
        public const byte DC1 = 0x11;
        /// <summary>장치 제어 2</summary>
        public const byte DC2 = 0x12;
        /// <summary>장치 제어 3</summary>
        public const byte DC3 = 0x13;
        /// <summary>장치 제어 4</summary>
        public const byte DC4 = 0x14;
        /// <summary>수신 거부</summary>
        public const byte NAK = 0x15;
        /// <summary>동시 공백</summary>
        public const byte SYN = 0x16;
        /// <summary>전송 블록 종료</summary>
        public const byte ETB = 0x17;
        /// <summary>취소</summary>
        public const byte CAN = 0x18;
        /// <summary>중간중간</summary>
        public const byte EM = 0x19;
        /// <summary>대체</summary>
        public const byte SUB = 0x1a;
        /// <summary>오버플로</summary>
        public const byte ESC = 0x1b;
        /// <summary>파일 분리자</summary>
        public const byte FS = 0x1c;
        /// <summary>그룹 구분 문자</summary>
        public const byte GS = 0x1d;
        /// <summary>기록 분리자</summary>
        public const byte RS = 0x1e;
        /// <summary>단위 분리자</summary>
        public const byte US = 0x1f;
    }
}
