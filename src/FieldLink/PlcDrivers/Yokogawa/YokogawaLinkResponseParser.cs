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

namespace FieldLink.PlcDrivers.Yokogawa
{
    /// <summary>YokogawaLink 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class YokogawaLinkResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorMsg(byte code)
        {
            switch (code)
            {
                case 0x01:
                    return ProtocolMessages.YokogawaLinkError01;
                case 0x02:
                    return ProtocolMessages.YokogawaLinkError02;
                case 0x03:
                    return ProtocolMessages.YokogawaLinkError03;
                case 0x04:
                    return ProtocolMessages.YokogawaLinkError04;
                case 0x05:
                    return ProtocolMessages.YokogawaLinkError05;
                case 0x06:
                    return ProtocolMessages.YokogawaLinkError06;
                case 0x07:
                    return ProtocolMessages.YokogawaLinkError07;
                case 0x08:
                    return ProtocolMessages.YokogawaLinkError08;
                case 0x41:
                    return ProtocolMessages.YokogawaLinkError41;
                case 0x42:
                    return ProtocolMessages.YokogawaLinkError42;
                case 0x43:
                    return ProtocolMessages.YokogawaLinkError43;
                case 0x44:
                    return ProtocolMessages.YokogawaLinkError44;
                case 0x51:
                    return ProtocolMessages.YokogawaLinkError51;
                case 0x52:
                    return ProtocolMessages.YokogawaLinkError52;
                case 0xF1:
                    return ProtocolMessages.YokogawaLinkErrorF1;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }
    }
}
