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
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeResponseParser;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlCommandBuilder;
using static FieldLink.PlcDrivers.Omron.OmronHostLinkCModeControlResponseParser;

namespace FieldLink.PlcDrivers.Omron
{
    /// <summary>OmronHostLinkCMode 주소를 프로토콜의 영역 코드와 위치로 변환합니다.</summary>
    public static class OmronHostLinkCModeAddressParser
    {
        /// <summary>GetEMAddress 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "start">start에 사용할 입력값입니다.</param>
        /// <param name = "isRead">isRead에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        internal static OperationResult<string, int> GetEMAddress(string address, int start, bool isRead)
        {
            // E 구역은 좀 더 복잡해서 특별한 계산이 필요합니다.
            string[] splits = address.SplitDot();
            int block = Convert.ToInt32(splits[0].Substring(start), 16);
            return OperationResult.CreateSuccessResult((isRead ? "RE" : "WE") + Encoding.ASCII.GetString(ProtocolBytes.BuildAsciiBytesFrom((byte)block)), (int)ushort.Parse(splits[1]));
        }

        /// <summary>입력 데이터의 프로토콜 필드를 해석합니다.</summary>
        /// <param name = "address">읽거나 기록할 프로토콜 주소입니다.</param>
        /// <param name = "isBit">비트 단위 요청이면 true입니다.</param>
        /// <param name = "isRead">isRead에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<string, int> AnalysisAddress(string address, bool isBit, bool isRead)
        {
            try
            {
                if (address.StartsWith("DM", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RD" : "WD", (int)ushort.Parse(address.Substring(2)));
                if (address.StartsWith("LR", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RL" : "WL", (int)ushort.Parse(address.Substring(2)));
                if (address.StartsWith("HR", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RH" : "WH", (int)ushort.Parse(address.Substring(2)));
                if (address.StartsWith("AR", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RJ" : "WJ", (int)ushort.Parse(address.Substring(2)));
                if (address.StartsWith("CIO", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RR" : "WR", (int)ushort.Parse(address.Substring(3)));
                if (address.StartsWith("TIM", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RC" : "WC", (int)ushort.Parse(address.Substring(3)));
                if (address.StartsWith("CNT", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.CreateSuccessResult(isRead ? "RC" : "WC", ushort.Parse(address.Substring(3)) + 2048);
                if (address.StartsWith("EM", StringComparison.OrdinalIgnoreCase))
                    return GetEMAddress(address, 2, isRead);
                switch (address[0])
                {
                    case 'D':
                    case 'd':
                        return OperationResult.CreateSuccessResult(isRead ? "RD" : "WD", (int)ushort.Parse(address.Substring(1)));
                    case 'C':
                    case 'c':
                        return OperationResult.CreateSuccessResult(isRead ? "RR" : "WR", (int)ushort.Parse(address.Substring(1)));
                    case 'H':
                    case 'h':
                        return OperationResult.CreateSuccessResult(isRead ? "RH" : "WH", (int)ushort.Parse(address.Substring(1)));
                    case 'A':
                    case 'a':
                        return OperationResult.CreateSuccessResult(isRead ? "RJ" : "WJ", (int)ushort.Parse(address.Substring(1)));
                    case 'E':
                    case 'e':
                        return GetEMAddress(address, 1, isRead);
                    default:
                        throw new Exception(ProtocolMessages.NotSupportedDataType);
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<string, int>(ex.Message);
            }
        }
    }
}
