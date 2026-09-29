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
using static FieldLink.PlcDrivers.LSIS.LSCpuValueConverter;
using static FieldLink.PlcDrivers.LSIS.LSCpuAddressParser;
using static FieldLink.PlcDrivers.LSIS.LSCpuCommandBuilder;
using static FieldLink.PlcDrivers.LSIS.LSCpuDefinitions;

namespace FieldLink.PlcDrivers.LSIS
{
    /// <summary>LSCpu 응답의 상태 코드와 데이터를 해석합니다.</summary>
    public static class LSCpuResponseParser
    {
        /// <summary>장치 오류 또는 상태 코드에 대응하는 설명을 반환합니다.</summary>
        /// <param name = "err">err에 사용할 입력값입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static string GetErrorText(int err)
        {
            switch (err)
            {
                case 0x0003:
                    return ProtocolMessages.LsisCnet0003;
                case 0x0004:
                    return ProtocolMessages.LsisCnet0004;
                case 0x0007:
                    return ProtocolMessages.LsisCnet0007;
                case 0x0011:
                    return ProtocolMessages.LsisCnet0011;
                case 0x0090:
                    return ProtocolMessages.LsisCnet0090;
                case 0x0190:
                    return ProtocolMessages.LsisCnet0190;
                case 0x0290:
                    return ProtocolMessages.LsisCnet0290;
                case 0x1132:
                    return ProtocolMessages.LsisCnet1132;
                case 0x1232:
                    return ProtocolMessages.LsisCnet1232;
                case 0x1234:
                    return ProtocolMessages.LsisCnet1234;
                case 0x1332:
                    return ProtocolMessages.LsisCnet1332;
                case 0x1432:
                    return ProtocolMessages.LsisCnet1432;
                case 0x7132:
                    return ProtocolMessages.LsisCnet7132;
                default:
                    return ProtocolMessages.UnknownError;
            }
        }

        /// <summary>UnpackResponseContent 형식의 프로토콜 데이터를 처리합니다.</summary>
        /// <param name = "send">send에 사용할 입력값입니다.</param>
        /// <param name = "response">해석할 응답 데이터입니다.</param>
        /// <returns>명령 생성 또는 데이터 해석 결과입니다.</returns>
        public static OperationResult<byte[]> UnpackResponseContent(byte[] send, byte[] response)
        {
            try
            {
                if (response[0] == AsciiControl.ACK)
                {
                    if (response[1] == 110 || response[1] == 111 || response[1] == 119) // 쓰기
                        return OperationResult.CreateSuccessResult(response);
                    string cmd = Encoding.ASCII.GetString(response);
                    var buffReceiver = cmd.Substring(1, cmd.Length - 2);
                    buffReceiver = buffReceiver.Substring(1, buffReceiver.Length - 3);
                    return OperationResult.CreateSuccessResult(GetBytesFromHex(buffReceiver));
                }
                else if (response[0] == AsciiControl.NAK)
                {
                    // NAK
                    int err = Convert.ToInt32(Encoding.ASCII.GetString(response, 6, 4), 16);
                    return new OperationResult<byte[]>(err, GetErrorText(err));
                }
                else
                {
                    return new OperationResult<byte[]>(response[0], "Source: " + ProtocolBytes.GetAsciiStringRender(response));
                }
            }
            catch (Exception ex)
            {
                return new OperationResult<byte[]>(err: 1, "Wrong:" + ex.Message + Environment.NewLine + "Source: " + response.ToHexString());
            }
        }
    }
}
