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
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoResponseParser;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlCommandBuilder;
using static FieldLink.PlcDrivers.Keyence.KeyenceNanoControlResponseParser;

namespace FieldLink.PlcDrivers.Keyence
{
    /// <summary>KeyenceNano 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class KeyenceNanoAddressParser
    {
        /// <summary>GetWordAddressMultiple 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "type">type에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static int GetWordAddressMultiple(string type)
        {
            if (type == "CTH" || type == "CTC" || type == "C" || type == "T" || type == "TS" || type == "TC" || type == "CS" || type == "CC" || type == "AT")
                return 2;
            else if (type == "DM" || type == "CM" || type == "TM" || type == "EM" || type == "FM" || type == "Z" || type == "W" || type == "ZF" || type == "VM")
                return 1;
            return 1;
        }
    }
}
