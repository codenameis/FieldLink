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
using static FieldLink.PlcDrivers.Panasonic.PanasonicAddressParser;
using static FieldLink.PlcDrivers.Panasonic.PanasonicCommandBuilder;
using static FieldLink.PlcDrivers.Panasonic.PanasonicResponseParser;

namespace FieldLink.PlcDrivers.Panasonic
{
    /// <summary>Panasonic 프로토콜의 값과 바이트 표현을 변환합니다.</summary>
    public static class PanasonicValueConverter
    {
        /// <summary>CalculateCrc 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "sb">sb에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static string CalculateCrc(StringBuilder sb)
        {
            byte tmp = 0;
            tmp = (byte)sb[0];
            for (int i = 1; i < sb.Length; i++)
            {
                tmp ^= (byte)sb[i];
            }

            return ProtocolBytes.ByteToHexString(new byte[] { tmp });
        }

        /// <summary>AppendCoil 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "sb">sb에 사용할 입력값입니다.</param>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult AppendCoil(StringBuilder sb, string address)
        {
            // 주소 분석
            OperationResult<string, int> analysis = AnalysisAddress(address);
            if (!analysis.IsSuccess)
                return OperationResult.CreateFailedResult<byte[]>(analysis);
            sb.Append(analysis.Content1);
            if (analysis.Content1 == "X" || analysis.Content1 == "Y" || analysis.Content1 == "R" || analysis.Content1 == "L")
            {
                sb.Append((analysis.Content2 / 16).ToString("D3"));
                sb.Append((analysis.Content2 % 16).ToString("X1"));
            }
            else if (analysis.Content1 == "T" || analysis.Content1 == "C")
            {
                sb.Append("0");
                sb.Append(analysis.Content2.ToString("D3"));
            }
            else
            {
                return new OperationResult<byte[]>(ProtocolMessages.NotSupportedDataType);
            }

            return OperationResult.CreateSuccessResult();
        }

        /// <summary>GetMcErrorDescription 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "code">code에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetMcErrorDescription(int code)
        {
            switch (code)
            {
                case 0x4031:
                    return ProtocolMessages.PanasonicMc4031;
                case 0xC051:
                    return ProtocolMessages.PanasonicMcC051;
                case 0xC056:
                    return ProtocolMessages.PanasonicMcC056;
                case 0xC059:
                    return ProtocolMessages.PanasonicMcC059;
                case 0xC05B:
                    return ProtocolMessages.PanasonicMcC05B;
                case 0xC05C:
                    return ProtocolMessages.PanasonicMcC05C;
                case 0xC05F:
                    return ProtocolMessages.PanasonicMcC05F;
                case 0xC060:
                    return ProtocolMessages.PanasonicMcC060;
                case 0xC061:
                    return ProtocolMessages.PanasonicMcC061;
                default:
                    return ProtocolMessages.MelsecPleaseReferToManualDocument;
            }
        }
    }
}
