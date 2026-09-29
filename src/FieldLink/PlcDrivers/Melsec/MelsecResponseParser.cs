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
using static FieldLink.PlcDrivers.Melsec.MelsecAddressParser;
using static FieldLink.PlcDrivers.Melsec.MelsecCommandBuilder;
using static FieldLink.PlcDrivers.Melsec.MelsecValueConverter;

namespace FieldLink.PlcDrivers.Melsec
{
    /// <summary>Melsec 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class MelsecResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorDescription(int code)
        {
            switch (code)
            {
                case 0x0002:
                    return ProtocolMessages.MelsecError02;
                case 0x0051:
                    return ProtocolMessages.MelsecError51;
                case 0x0052:
                    return ProtocolMessages.MelsecError52;
                case 0x0054:
                    return ProtocolMessages.MelsecError54;
                case 0x0055:
                    return ProtocolMessages.MelsecError55;
                case 0x0056:
                    return ProtocolMessages.MelsecError56;
                case 0x0058:
                    return ProtocolMessages.MelsecError58;
                case 0x0059:
                    return ProtocolMessages.MelsecError59;
                case 0xC04D:
                    return ProtocolMessages.MelsecErrorC04D;
                case 0xC050:
                    return ProtocolMessages.MelsecErrorC050;
                case 0xC051:
                case 0xC052:
                case 0xC053:
                case 0xC054:
                    return ProtocolMessages.MelsecErrorC051_54;
                case 0xC055:
                    return ProtocolMessages.MelsecErrorC055;
                case 0xC056:
                    return ProtocolMessages.MelsecErrorC056;
                case 0xC057:
                    return ProtocolMessages.MelsecErrorC057;
                case 0xC058:
                    return ProtocolMessages.MelsecErrorC058;
                case 0xC059:
                    return ProtocolMessages.MelsecErrorC059;
                case 0xC05A:
                case 0xC05B:
                    return ProtocolMessages.MelsecErrorC05A_B;
                case 0xC05C:
                    return ProtocolMessages.MelsecErrorC05C;
                case 0xC05D:
                    return ProtocolMessages.MelsecErrorC05D;
                case 0xC05E:
                    return ProtocolMessages.MelsecErrorC05E;
                case 0xC05F:
                    return ProtocolMessages.MelsecErrorC05F;
                case 0xC060:
                    return ProtocolMessages.MelsecErrorC060;
                case 0xC061:
                    return ProtocolMessages.MelsecErrorC061;
                case 0xC062:
                    return ProtocolMessages.MelsecErrorC062;
                case 0xC070:
                    return ProtocolMessages.MelsecErrorC070;
                case 0xC072:
                    return ProtocolMessages.MelsecErrorC072;
                case 0xC074:
                    return ProtocolMessages.MelsecErrorC074;
                default:
                    return ProtocolMessages.MelsecPleaseReferToManualDocument;
            }
        }

        /// <summary>입력 데이터가 프로토콜의 검증 조건을 만족하는지 확인합니다.</summary>
        /// <param name = "data">변환하거나 기록할 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static bool CheckCRC(byte[] data)
        {
            byte[] crc = FxCalculateCRC(data);
            if (crc[0] != data[data.Length - 2])
                return false;
            if (crc[1] != data[data.Length - 1])
                return false;
            return true;
        }
    }
}
