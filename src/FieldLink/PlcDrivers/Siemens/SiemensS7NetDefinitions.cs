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
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetCommandBuilder;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetAddressParser;
using static FieldLink.PlcDrivers.Siemens.SiemensS7NetResponseParser;

namespace FieldLink.PlcDrivers.Siemens
{
    /// <summary>SiemensS7Net 프로토콜의 명령 및 데이터 타입 상수입니다.</summary>
    public static class SiemensS7NetDefinitions
    {
        /// <summary>pduStart 프로토콜 값입니다.</summary>
        public const byte pduStart = 0x28; // CPU 시작
        /// <summary>pduStop 프로토콜 값입니다.</summary>
        public const byte pduStop = 0x29; // CPU 정지
        /// <summary>pduAlreadyStarted 프로토콜 값입니다.</summary>
        public const byte pduAlreadyStarted = 0x02; // 이미 실행 모드에서 CPU
        /// <summary>pduAlreadyStopped 프로토콜 값입니다.</summary>
        public const byte pduAlreadyStopped = 0x07; // CPU는 이미 정지 모드입니다.
    }
}
