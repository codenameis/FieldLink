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
    /// <summary>KeyenceSR2000 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class KeyenceSR2000ResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "error">error에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescription(string error)
        {
            switch (error)
            {
                case "00":
                    return ProtocolMessages.KeyenceSR2000Error00;
                case "01":
                    return ProtocolMessages.KeyenceSR2000Error01;
                case "02":
                    return ProtocolMessages.KeyenceSR2000Error02;
                case "03":
                    return ProtocolMessages.KeyenceSR2000Error03;
                case "04":
                    return ProtocolMessages.KeyenceSR2000Error04;
                case "05":
                    return ProtocolMessages.KeyenceSR2000Error05;
                case "10":
                    return ProtocolMessages.KeyenceSR2000Error10;
                case "11":
                    return ProtocolMessages.KeyenceSR2000Error11;
                case "12":
                    return ProtocolMessages.KeyenceSR2000Error12;
                case "13":
                    return ProtocolMessages.KeyenceSR2000Error13;
                case "14":
                    return ProtocolMessages.KeyenceSR2000Error14;
                case "20":
                    return ProtocolMessages.KeyenceSR2000Error20;
                case "21":
                    return ProtocolMessages.KeyenceSR2000Error21;
                case "22":
                    return ProtocolMessages.KeyenceSR2000Error22;
                case "23":
                    return ProtocolMessages.KeyenceSR2000Error23;
                case "99":
                    return ProtocolMessages.KeyenceSR2000Error99;
                default:
                    return ProtocolMessages.UnknownError + " :" + error;
            }
        }
    }
}
