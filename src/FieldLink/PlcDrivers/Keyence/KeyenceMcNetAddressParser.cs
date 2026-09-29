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
    /// <summary>KeyenceMcNet 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class KeyenceMcNetAddressParser
    {
        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static bool CheckKeyenceBoolAddress(string address)
        {
            if (address.StartsWith("MR", StringComparison.OrdinalIgnoreCase) || address.StartsWith("CR", StringComparison.OrdinalIgnoreCase) || address.StartsWith("LR", StringComparison.OrdinalIgnoreCase) || address.StartsWith("R", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
    }
}
